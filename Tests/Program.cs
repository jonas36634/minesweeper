using Kernport;
using System.Diagnostics;

int cases = 0, solved = 0, hints = 0, chords = 0, wrongChords = 0;
void Check(bool x, string message) { if (!x) throw new Exception(message); cases++; }
bool[] Oracle(Layout size, uint seed, int start)
{
    List<int> pool = [];
    for (int i = 0; i < size.Count; i++)
        if (Math.Abs(i % size.Width - start % size.Width) > 1 || Math.Abs(i / size.Width - start / size.Width) > 1) pool.Add(i);
    var map = new bool[size.Count];
    for (int j = 0; j < size.Mines; j++)
    {
        seed = unchecked(1664525u * seed + 1013904223u);
        int k = j + (int)((ulong)seed * (uint)(pool.Count - j) / 4294967296UL);
        map[pool[k]] = true; pool[k] = pool[j];
    }
    return map;
}
foreach (var (size, valid) in new[] { (new Layout(4, 5, 1), false), (new(40, 30, 1191), true), (new(40, 30, 1192), false), (new(5, 5, 16), true), (new(5, 5, 17), false), (new(41, 20, 1), false), (new(5, 31, 1), false), (new(9, 9, 0), false) })
    Check(size.Valid == valid, "custom limits");
foreach (var size in new[] { Layout.Small, Layout.Medium, Layout.Expert, new Layout(5, 5, 16) })
{
    var times = new List<double>(); int successes = 0;
    int samples = size == Layout.Expert ? 80 : 120;
    for (uint seed = 0; seed < samples; seed++)
    {
        int first = (int)(seed % 4) switch { 0 => 0, 1 => size.Width - 1, 2 => size.Count / 2, _ => size.Count / 2 + size.Width / 2 };
        var b = new Board(size); var map = Oracle(size, seed, first);
        b.Start(first, seed);
        Check(b.Open[first] && b.Clue(first) == 0 && b.Around[first].All(i => b.Open[i]), $"flood {seed}");
        var watch = Stopwatch.StartNew(); bool certified = Solver.Certify(b); times.Add(watch.Elapsed.TotalMilliseconds);
        var manual = new Board(size); manual.Start(first, seed);
        // Walk through player-facing hints and compare every conclusion to the independently generated field.
        while (manual.Phase == Phase.Playing && Solver.Hint(manual) is { } hint)
        {
            Check(hint.Targets.All(i => map[i] == hint.Mine), $"false hint {size} seed {seed}"); hints++;
            foreach (int i in hint.Targets)
                if (hint.Mine) { if (!manual.Flags[i]) manual.Flag(i); }
                else if (!manual.Open[i]) manual.Reveal(i);
        }
        Check(!certified || manual.Phase == Phase.Won, $"uncertified path {seed}");
        if (certified) { solved++; successes++; }
        var play = new Board(size); play.Start(first, seed);
        int mine = Array.FindIndex(map, x => x);
        if (play.Phase == Phase.Playing) { play.Flag(mine); Check(!play.Reveal(mine), "flag protected"); play.Flag(mine); play.Reveal(mine); Check(play.Phase == Phase.Lost && play.Trigger == mine, "loss"); }
        Check(Enumerable.Range(0, size.Count).Count(play.ExposedMine) == size.Mines, "mine total");
        for (int i = 0; i < size.Count; i++)
        {
            Check(play.ExposedMine(i) == map[i], $"generation seed {seed}");
            if (play.Open[i]) Check(play.Clue(i) == play.Around[i].Count(j => map[j]), "clue");
            if (!map[i] && !manual.Open[i]) manual.Reveal(i);
        }
        Check(manual.Phase == Phase.Won, "victory");
        var chord = new Board(size); chord.Start(first, seed);
        int at = Enumerable.Range(0, size.Count).FirstOrDefault(i => chord.Clue(i) > 0 && chord.Around[i].Any(j => !map[j] && !chord.Open[j]), -1);
        if (at >= 0 && chord.Phase == Phase.Playing)
        {
            var ns = chord.Around[at]; var bad = new Board(size); bad.Start(first, seed);
            foreach (int m in ns.Where(j => map[j])) { chord.Flag(m); bad.Flag(m); }
            int wrong = ns.First(j => !map[j] && !chord.Open[j]); bad.Flag(ns.First(j => map[j])); bad.Flag(wrong);
            Check(chord.Reveal(at) && ns.Where(j => !map[j]).All(j => chord.Open[j]), "chord"); chords++;
            bad.Reveal(at); Check(bad.Phase == Phase.Lost, "wrong flags chord"); wrongChords++;
        }
    }
    times.Sort(); Console.WriteLine($"{size.Width}x{size.Height}: {successes}/{samples} zertifiziert; Prüfung pro Kandidat Median {times[times.Count / 2]:F2} ms, p95 {times[(int)(times.Count * .95)]:F2} ms.");
}
// Pre-start flags can interrupt a flood. A visible zero proves an adjacent flag wrong.
var dense = new Board(new Layout(5, 5, 16));
dense.Flag(6); dense.Start(12, 42);
var repair = Solver.Hint(dense);
Check(repair is { Mine: false } && repair.Targets.Contains(6), "false flag next to zero");
dense.Flag(6); dense.Reveal(6); Check(dense.Phase == Phase.Won, "repaired start");
var daily = new Session { Mode = Mode.Daily, Size = Layout.Medium, Day = "2026-11-18", Moves = [new(Session.DailyStart, false)] };
daily.Seed = Session.DailySeed(daily.Day);
Check(daily.Restore().Open.SequenceEqual(daily.Restore().Open), "daily replay");
daily.Moves.Add(new(-1, true));
try { daily.Restore(); throw new Exception("accepted corrupt move"); } catch (FormatException) { cases++; }
Console.WriteLine($"{cases} Prüfungen bestanden, {solved} geprüfte Felder, {hints} Hinweisschritte, {chords} korrekte und {wrongChords} fehlerhafte Akkorde.");
