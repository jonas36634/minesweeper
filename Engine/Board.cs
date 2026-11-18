namespace Kernport;

public enum Phase { Ready, Playing, Won, Lost }
public sealed record Layout(int Width, int Height, int Mines)
{
    public static readonly Layout Small = new(9, 9, 10), Medium = new(16, 16, 40), Expert = new(30, 16, 99);
    public int Count => Width * Height;
    public bool Valid => Width is >= 5 and <= 40 && Height is >= 5 and <= 30 && Mines >= 1 && Mines <= Count - 9;
}

public sealed class Board
{
    public Layout Size { get; }
    public int[][] Around { get; }
    public bool[] Open { get; }
    public bool[] Flags { get; }
    private readonly bool[] mines;
    private readonly int[] clues;
    public Phase Phase { get; private set; }
    public int Trigger { get; private set; } = -1;
    public int Opened { get; private set; }
    public int FlagCount { get; private set; }
    public int First { get; private set; } = -1;
    public uint Seed { get; private set; }
    public int Clue(int i) => Open[i] ? clues[i] : -1;
    public bool ExposedMine(int i) => (Phase is Phase.Lost or Phase.Won) && mines[i];

    public Board(Layout size)
    {
        if (!size.Valid) throw new ArgumentException("Ungültige Feldgröße");
        Size = size;
        Open = new bool[size.Count]; Flags = new bool[size.Count];
        mines = new bool[size.Count]; clues = new int[size.Count];
        Around = Enumerable.Range(0, size.Count).Select(i => Neighbors(i).ToArray()).ToArray();
    }

    private IEnumerable<int> Neighbors(int cell)
    {
        int cx = cell % Size.Width, cy = cell / Size.Width;
        for (int y = Math.Max(0, cy - 1); y <= Math.Min(Size.Height - 1, cy + 1); y++)
            for (int x = Math.Max(0, cx - 1); x <= Math.Min(Size.Width - 1, cx + 1); x++)
            {
                int n = y * Size.Width + x;
                if (n != cell) yield return n;
            }
    }

    // Version 1: unsigned LCG and partial Fisher-Yates, candidates in row-major order.
    public void Start(int i, uint seed)
    {
        if (Phase != Phase.Ready || Flags[i]) return;
        First = i; Seed = seed;
        var candidates = Enumerable.Range(0, Size.Count).Where(n => n != i && !Around[i].Contains(n)).ToArray();
        for (int j = 0; j < Size.Mines; j++)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            int k = j + (int)(((ulong)seed * (uint)(candidates.Length - j)) >> 32);
            (candidates[j], candidates[k]) = (candidates[k], candidates[j]);
            mines[candidates[j]] = true;
            foreach (int n in Around[candidates[j]]) clues[n]++;
        }
        Phase = Phase.Playing;
        Reveal(i);
    }
    public bool Flag(int i)
    {
        if (Phase is Phase.Won or Phase.Lost || Open[i]) return false;
        Flags[i] = !Flags[i]; FlagCount += Flags[i] ? 1 : -1;
        return true;
    }
    public bool Reveal(int i)
    {
        if (Phase != Phase.Playing || Flags[i]) return false;
        var pending = new Stack<int>();
        if (Open[i])
        {
            if (clues[i] == 0 || Around[i].Count(n => Flags[n]) != clues[i]) return false;
            foreach (int n in Around[i]) pending.Push(n);
        }
        else pending.Push(i);
        int before = Opened;
        while (pending.TryPop(out int n))
        {
            if (Flags[n] || Open[n]) continue;
            if (mines[n]) { Trigger = n; Phase = Phase.Lost; return true; }
            Open[n] = true; Opened++;
            if (clues[n] == 0)
                foreach (int adjacent in Around[n]) if (!Open[adjacent]) pending.Push(adjacent);
        }
        if (Opened == Size.Count - Size.Mines) Phase = Phase.Won;
        return Opened != before;
    }
}
