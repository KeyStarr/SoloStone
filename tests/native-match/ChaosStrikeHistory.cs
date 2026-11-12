using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void ChaosStrikeHistory()
    {
        var failures = new List<string>();
        int cases = 0, mutations = 0;
        foreach (bool bot in new[] { false, true })
        foreach (string variant in new[] { "CS2_182", "CS2_029", "EDR_842", "FIR_901", "TIME_614", "JAIL_441", "FIR_900", "EDR_815" })
        foreach (string mode in new[] { "normal", "empty-deck", "full-hand" })
        {
            var (match, capture) = AnimationMatch(bot, "showcase-demon-hunter-outcast");
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray())
                owner.SetasideZone.Add(old.Zone.Remove(old));
            var source = EngineEntity.FromCard(owner, Cards.FromId("CORE_BT_035"), zone: owner.HandZone);
            if (mode == "full-hand")
                for (int i = 0; i < 9; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            if (mode != "empty-deck") EngineEntity.FromCard(owner, Cards.FromId(variant), zone: owner.DeckZone);
            FlushFieldHistory(match);
            int hand = owner.HandZone.Count, deck = owner.DeckZone.Count, attack = owner.Hero.AttackDamage;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            Check(owner.Hero.AttackDamage == attack + 2, "Chaos +2 Attack unchanged");
            Check(owner.HandZone.Count == hand - 1 + (deck > 0 ? 1 : 0) && owner.DeckZone.Count == 0, "Chaos draw count unchanged");
            if (mode == "empty-deck") Check(owner.Hero.Fatigue == 1, "Chaos empty-deck fatigue unchanged");
            string name = $"Chaos-{variant}-{mode}-{bot}";
            AssertDrawImpactTargets(capture, source.Id, false, name, failures);
            var packets = capture.HistorySnapshots().ToArray();
            var history = packets.SelectMany(p => p.List).ToList();
            if (mode != "empty-deck")
            {
                int block = history.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == (int)BlockType.POWER);
                var target = block >= 0 && block + 1 < history.Count ? history[block + 1] : null;
                if (target?.HasMetaData == true && (int)target.MetaData.Type == 0)
                {
                    Check(target.MetaData.Data == 0 && target.MetaData.Info.SequenceEqual(new[] { owner.Hero.Id }),
                        "Chaos generic repair retains only typed hero target");
                    Check(NativeAdmission.ValidatePackets(packets.Select(p => (IReadOnlyList<PegasusGame.PowerHistoryData>)p.List.Where(e => !ReferenceEquals(e, target)).ToArray()))
                        .Any(f => f.Rule == "R1" && f.Source == source.Id), "Chaos TARGET removal must restore untyped inference");
                    mutations++;
                }
            }
            failures.AddRange(NativeAdmission.ValidatePackets(packets.Select(p => (IReadOnlyList<PegasusGame.PowerHistoryData>)p.List)).Select(f => name + ": " + f));
            capture.SerializeAll();
            cases++;
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Check(mutations == 32, "Chaos has 32 TARGET removal controls");
        Console.WriteLine($"PASS Chaos admission {cases} cases, both seats, eight draw identities, normal/empty/full-hand; {mutations} TARGET-removal mutations rejected");
    }
}
