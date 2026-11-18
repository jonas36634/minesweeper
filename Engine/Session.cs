using System.Globalization;
namespace Kernport;

public enum Mode { Classic, Logic, Daily }
public record Move(int Cell, bool Flag);
public sealed class Session
{
    public int Version { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public Layout Size { get; set; } = Layout.Small;
    public Mode Mode { get; set; }
    public string Day { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public uint Seed { get; set; }
    public int Hints { get; set; }
    public long Elapsed { get; set; }
    public List<Move> Moves { get; set; } = [];
    public const int DailyStart = 8 * 16 + 8;
    public static uint DailySeed(string day) => ("kernport/v1/" + day).Aggregate(2166136261u, (h, c) => unchecked((h ^ c) * 16777619u));
    public Board Restore()
    {
        if (Version != 1 || Id == Guid.Empty || !Enum.IsDefined(Mode) || Size?.Valid != true || Moves == null || Moves.Count > 100000 || Hints < 0 || Elapsed < 0 || Elapsed > 315360000 ||
           !DateOnly.TryParseExact(Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
           (Mode == Mode.Daily && (Size != Layout.Medium || Seed != DailySeed(Day)))) throw new FormatException();
        var b = new Board(Size);
        foreach (var m in Moves)
        {
            if ((uint)m.Cell >= Size.Count || b.Phase is Phase.Won or Phase.Lost) throw new FormatException();
            if (m.Flag) { if (!b.Flag(m.Cell)) throw new FormatException(); }
            else if (b.Phase == Phase.Ready)
            {
                if (b.Flags[m.Cell] || (Mode == Mode.Daily && m.Cell != DailyStart)) throw new FormatException();
                b.Start(m.Cell, Seed);
                if (Mode == Mode.Logic) { var trial = new Board(Size); trial.Start(m.Cell, Seed); if (!Solver.Certify(trial)) throw new FormatException(); }
            }
            else if (!b.Reveal(m.Cell)) throw new FormatException();
        }
        return b;
    }
}
