namespace Kernport;

public record Advice(int[] Targets, bool Mine, int[] Sources, string Reason);
public static class Solver
{
    private sealed record Rule(int[] Cells, int Left, int Source);
    // Only public clues and independently proved mines enter these constraints.
    private static IEnumerable<Advice> Infer(Board b, bool[] proved)
    {
        var rules = new List<Rule>();
        for (int i = 0; i < b.Size.Count; i++)
        {
            if (!b.Open[i]) continue;
            var cells = b.Around[i].Where(n => !b.Open[n] && !proved[n]).ToArray();
            if (cells.Length == 0) continue;
            int left = b.Clue(i) - b.Around[i].Count(n => proved[n]);
            if (left == 0 || left == cells.Length)
                yield return new(cells, left != 0, [i], left == 0
                    ? "Alle Minen an dieser Zahl sind bereits bestimmt. Die übrigen Nachbarfelder sind sicher."
                    : $"An dieser Zahl fehlen {left} Minen bei genau {cells.Length} ungeklärten Nachbarfeldern. Jedes davon enthält eine Mine.");
            rules.Add(new(cells, left, i));
        }
        var unknown = Enumerable.Range(0, b.Size.Count).Where(i => !b.Open[i] && !proved[i]).ToArray();
        int rest = b.Size.Mines - proved.Count(v => v);
        if (unknown.Length > 0 && (rest == 0 || rest == unknown.Length))
            yield return new(unknown, rest != 0, [], rest == 0 ? "Alle Minen des Felds sind bestimmt. Die übrigen Felder sind sicher." : "Es bleiben genau so viele Minen wie ungeklärte Felder. Jedes enthält eine Mine.");
        // Only overlapping neighborhoods can form a nonempty subset.
        foreach (var a in rules)
            foreach (var c in rules)
            {
                if (a.Cells.Length >= c.Cells.Length || Math.Abs(a.Source / b.Size.Width - c.Source / b.Size.Width) > 2 ||
                    Math.Abs(a.Source % b.Size.Width - c.Source % b.Size.Width) > 2 || !a.Cells.All(n => Array.IndexOf(c.Cells, n) >= 0)) continue;
                int diff = c.Left - a.Left;
                if (diff != 0 && diff != c.Cells.Length - a.Cells.Length) continue;
                yield return new(c.Cells.Except(a.Cells).ToArray(), diff != 0, [a.Source, c.Source],
                    diff == 0 ? "Beide Zahlen benötigen gleich viele weitere Minen. Die zusätzlichen Felder der größeren Gruppe sind sicher."
                    : "Nach Abzug der kleineren Gruppe braucht die größere für jedes zusätzliche Feld eine Mine.");
            }
    }
    public static Advice? Hint(Board b)
    {
        if (b.Phase != Phase.Playing) return null;
        var proved = new bool[b.Size.Count];
        while (true)
        {
            bool changed = false;
            var steps = Infer(b, proved).ToArray();
            var safe = steps.FirstOrDefault(step => !step.Mine);
            if (safe != null) return safe;
            foreach (var step in steps)
            {
                var unmarked = step.Targets.Where(i => !b.Flags[i]).ToArray();
                if (unmarked.Length > 0) return step with { Targets = unmarked };
                foreach (int i in step.Targets) if (!proved[i]) { proved[i] = true; changed = true; }
            }
            if (!changed) return null;
        }
    }

    public static bool Certify(Board trial, Func<bool>? expired = null)
    {
        var proved = new bool[trial.Size.Count];
        while (trial.Phase == Phase.Playing)
        {
            if (expired?.Invoke() == true) return false;
            bool progress = false;
            foreach (var step in Infer(trial, proved).ToArray())
                foreach (int i in step.Targets)
                {
                    if (step.Mine) { progress |= !proved[i]; proved[i] = true; }
                    else if (!trial.Open[i]) progress |= trial.Reveal(i);
                }
            if (!progress) break;
        }
        return trial.Phase == Phase.Won;
    }
}
