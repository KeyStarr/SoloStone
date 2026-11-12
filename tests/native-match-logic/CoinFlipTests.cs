using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SabberStoneCore.Enums;

internal static partial class Program
{
    private static void CoinFlipControl()
    {
        string root = Path.Combine(Path.GetTempPath(), "coin-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "debug-scenario");
        try
        {
            foreach (string seat in new[] { "first", "second" })
            {
                path = Path.Combine(root, "debug-scenario-" + seat);
                File.WriteAllText(path, "runId=" + RunA + "\nhumanSeat=" + seat);
                var staged = DebugScenario.Read(path, null, null, RunA, true);
                Check(staged?.FirstPlayer == (seat == "first" ? 1 : 2), "enabled current-launch override honoured");
                Check(DebugScenario.Parse(staged.Canonical()).FirstPlayer == staged.FirstPlayer, "seat scenario canonical replay");
                staged.Consume();
                Check(DebugScenario.Read(path, null, null, RunA, true) == null, "override consumed once");
            }
            File.WriteAllText(path, "runId=" + RunA + "\nhumanSeat=second");
            Check(DebugScenario.Read(path, null, null, RunA, false) == null, "disabled test control refuses forced seat");
            File.WriteAllText(path, "runId=" + RunB + "\nhumanSeat=second");
            Check(DebugScenario.Read(path, null, null, RunA, true) == null, "stale launch refuses forced seat");
            RejectsScenario("humanSeat=random", "invalid seat");
        }
        finally { Directory.Delete(root, true); }
    }
}
