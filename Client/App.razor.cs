using System.Diagnostics;
using System.Text.Json;
using Kernport;
using Microsoft.JSInterop;

namespace Client;

public partial class App
{
    private Session game = new();
    private Board board = new(Layout.Small);
    private Mode draftMode;
    private int level, width = 12, height = 12, mines = 20, focusCell;
    private bool flagTool, busy, dark, loaded, storageError, focusBoard;
    private long resumedAtSeconds;
    private Advice? advice;
    private string notice = "", dialog = "";
    private DotNetObjectReference<App>? reference;
    private Timer? clock;
    private readonly Stopwatch active = new();
    private CancellationTokenSource? generation;
    private List<Result> history = [];
    public record Result(Guid Id, string Group, bool Won, long Seconds);
    private Layout Selected => draftMode == Mode.Daily ? Layout.Medium : level switch { 0 => Layout.Small, 1 => Layout.Medium, 2 => Layout.Expert, _ => new(width, height, mines) };
    private string ModeName => game.Mode switch { Mode.Classic => "Klassisch", Mode.Logic => "Logik", _ => "Tagesaufgabe" };
    private long Seconds => resumedAtSeconds + (long)active.Elapsed.TotalSeconds;
    private static string TimeText(long s) => $"{s / 60:D2}:{s % 60:D2}";
    private string StateLabel => busy ? "Prüfung läuft" : board.Phase switch { Phase.Won => "Gewonnen", Phase.Lost => "Mine getroffen", Phase.Playing => game.Mode == Mode.Logic ? "Logisch geprüft" : ModeName, _ => "Startbereit" };
    private string Message => (storageError ? "Lokales Speichern ist nicht verfügbar. " : "") + (notice != "" ? notice : board.Phase switch
    {
        Phase.Won => "Geschafft! Alle sicheren Felder sind offen.",
        Phase.Lost => "Mine getroffen. Das Endbrett bleibt sichtbar.",
        Phase.Ready => game.Mode == Mode.Daily ? $"Tagesaufgabe {game.Day} (UTC). Öffne das markierte Startfeld." : "Wähle ein Feld zum Starten. Der erste Zug ist sicher.",
        _ => game.Mode == Mode.Daily ? $"Tagesaufgabe vom {game.Day} (UTC)." : $"{board.Opened} von {board.Size.Count - board.Size.Mines} sicheren Feldern offen."
    });
    private bool DailyTarget(int i) => game.Mode == Mode.Daily && board.Phase == Phase.Ready && i == Session.DailyStart;
    private string Coord(int i) => $"Zeile {i / board.Size.Width + 1}, Spalte {i % board.Size.Width + 1}";
    private string Label(int i) => Coord(i) + ", " + (board.Trigger == i ? "ausgelöste Mine" : board.ExposedMine(i) ? "Mine" : board.Flags[i] ? "Flagge" : board.Open[i] ? (board.Clue(i) == 0 ? "leer" : $"Zahl {board.Clue(i)}") : DailyTarget(i) ? "Startfeld" : "verdeckt");

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (first)
        {
            reference = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("kp.wire", reference);
            try
            {
                dark = (await Read("theme")) == "dark"; await JS.InvokeVoidAsync("kp.theme", dark);
                var stats = await Read("stats1");
                if (stats != null) { try { history = JsonSerializer.Deserialize(stats, StorageJson.Default.ListResult)?.Where(r => r != null && r.Seconds >= 0 && r.Group != null).DistinctBy(r => r.Id).ToList() ?? []; } catch (JsonException) { } }
                var raw = await Read("game1");
                if (raw != null)
                {
                    var saved = JsonSerializer.Deserialize(raw, StorageJson.Default.Session) ?? throw new FormatException();
                    board = saved.Restore(); game = saved; resumedAtSeconds = game.Elapsed; SyncDraft();
                    if (board.Phase == Phase.Playing) active.Start();
                    if (board.Phase is Phase.Won or Phase.Lost) await Record();
                }
            }
            catch (JSException) { storageError = true; }
            catch (Exception) { notice = "Der alte Spielstand konnte nicht geladen werden. Ein neues Feld ist bereit."; game = new(); board = new(Layout.Small); }
            loaded = true;
            clock = new Timer(_ => InvokeAsync(() => { if (board.Phase == Phase.Playing) StateHasChanged(); }), null, 1000, 1000);
            StateHasChanged();
        }
        if (dialog != "") await JS.InvokeVoidAsync("kp.modal");
        if (focusBoard) { focusBoard = false; await JS.InvokeVoidAsync("kp.focus", "c" + focusCell); }
    }
    private ValueTask<string?> Read(string key) => JS.InvokeAsync<string?>("kp.get", key);
    private async Task Write(string key, string value)
    { try { await JS.InvokeVoidAsync("kp.set", key, value); } catch (JSException) { storageError = true; } }
    private async Task Save()
    {
        if (board.Phase == Phase.Playing) active.Start(); else active.Stop();
        game.Elapsed = Seconds;
        await Write("game1", JsonSerializer.Serialize(game, StorageJson.Default.Session));
    }
    [JSInvokable] public Task Checkpoint() => loaded && board.Phase == Phase.Playing ? Save() : Task.CompletedTask;
    private void SyncDraft()
    {
        draftMode = game.Mode; level = game.Size == Layout.Small ? 0 : game.Size == Layout.Medium ? 1 : game.Size == Layout.Expert ? 2 : 3;
        if (level == 3) { width = game.Size.Width; height = game.Size.Height; mines = game.Size.Mines; }
    }
    private async Task ToggleTheme() { dark = !dark; await JS.InvokeVoidAsync("kp.theme", dark); await Write("theme", dark ? "dark" : "light"); }
    private void Dialog(string title) => dialog = title;
    private void Close() => dialog = "";
    private void Dimension(string k, string? raw) { int.TryParse(raw, out int v); if (k == "Breite") width = v; else if (k == "Höhe") height = v; else mines = v; }
    private Task RequestDaily() { draftMode = Mode.Daily; return RequestStart(); }
    private async Task RequestStart()
    {
        if (!loaded || busy || !Selected.Valid) return;
        if (board.Phase == Phase.Playing || (board.Phase == Phase.Ready && board.FlagCount > 0)) dialog = "Neue Partie starten?";
        else await StartSelected();
    }
    private Task Again() { SyncDraft(); return StartSelected(); }
    private async Task StartSelected()
    {
        Close(); game = new() { Size = Selected, Mode = draftMode }; board = new(game.Size); active.Reset(); resumedAtSeconds = 0; focusBoard = true;
        if (game.Mode == Mode.Daily) { game.Seed = Session.DailySeed(game.Day); level = 1; }
        notice = ""; advice = null; flagTool = false; focusCell = game.Mode == Mode.Daily ? Session.DailyStart : 0;
        await Save();
    }
    private void Cancel() => generation?.Cancel();
    private async Task<uint?> FindSeed(int i)
    {
        using var token = new CancellationTokenSource(); generation = token; busy = true;
        var watch = Stopwatch.StartNew();
        try
        {
            while (watch.ElapsedMilliseconds < 3500 && !token.IsCancellationRequested)
            {
                notice = "Suche ein vollständig logisch lösbares Feld …"; StateHasChanged(); await Task.Delay(16);
                if (token.IsCancellationRequested) break;
                var seed = (uint)Random.Shared.NextInt64(1L << 32);
                var trial = new Board(board.Size); trial.Start(i, seed);
                if (Solver.Certify(trial, () => watch.ElapsedMilliseconds >= 3500)) { notice = ""; return seed; }
            }
            notice = token.IsCancellationRequested ? "Suche abgebrochen. Wähle ein Startfeld." : "Kein geprüftes Feld innerhalb des Zeitlimits. Klicke für einen neuen Versuch.";
            return null;
        }
        finally { busy = false; generation = null; }
    }
    private async Task Play(int i, bool flag)
    {
        if (!loaded || busy || board.Phase is Phase.Won or Phase.Lost) return;
        advice = null; notice = ""; focusCell = i;
        if (flag) { if (!board.Flag(i)) return; }
        else if (board.Phase == Phase.Ready)
        {
            if (board.Flags[i]) return;
            if (game.Mode == Mode.Daily && i != Session.DailyStart) { notice = "Öffne zuerst das markierte Startfeld in Zeile 9, Spalte 9."; return; }
            uint? seed = game.Mode switch { Mode.Logic => await FindSeed(i), Mode.Daily => game.Seed, _ => (uint)Random.Shared.NextInt64(1L << 32) };
            if (seed == null) return;
            game.Seed = seed.Value; board.Start(i, game.Seed);
        }
        else if (!board.Reveal(i)) return;
        game.Moves.Add(new(i, flag)); await Save();
        if (board.Phase is Phase.Won or Phase.Lost) await Record();
    }
    private async Task Record()
    {
        if (history.Any(r => r.Id == game.Id)) return;
        history.Add(new(game.Id, $"{ModeName} · {game.Size.Width}×{game.Size.Height}/{game.Size.Mines} · {(game.Hints > 0 ? "mit Hinweis" : "ohne Hinweis")}", board.Phase == Phase.Won, Seconds));
        await Write("stats1", JsonSerializer.Serialize(history, StorageJson.Default.ListResult));
    }
    private async Task Hint()
    {
        var h = Solver.Hint(board);
        if (h == null) { notice = "Mit den sichtbaren Zahlen ist derzeit kein sicherer Zug ableitbar."; return; }
        int i = h.Targets[0]; advice = h with { Targets = [i] }; game.Hints++;
        string basis = h.Sources.Length == 0 ? "" : string.Join(" und ", h.Sources.Select(s => $"die {board.Clue(s)} in {Coord(s)}"));
        notice = $"{Coord(i)}: {(h.Mine ? "Mine" : "sicher")}. {h.Reason}" + (basis == "" ? "" : $" Grundlage: {basis}.") + (board.Flags[i] && !h.Mine ? " Entferne dort zuerst die Flagge." : "");
        await JS.InvokeVoidAsync("kp.focus", "c" + i);
        await Save();
    }
    [JSInvokable]
    public async Task Key(int i, string key)
    {
        int w = board.Size.Width;
        switch (key)
        {
            case "f": case "F": await Play(i, true); break;
            case "Enter": case " ": await Play(i, false); break;
            default:
                focusCell = key switch { "ArrowLeft" => i % w > 0 ? i - 1 : i, "ArrowRight" => i % w < w - 1 ? i + 1 : i, "ArrowUp" => i >= w ? i - w : i, "ArrowDown" => i + w < board.Size.Count ? i + w : i, "Home" => i - i % w, "End" => i - i % w + w - 1, _ => i };
                await JS.InvokeVoidAsync("kp.focus", "c" + focusCell); break;
        }
        StateHasChanged();
    }
    public void Dispose() { clock?.Dispose(); reference?.Dispose(); generation?.Cancel(); }
}
