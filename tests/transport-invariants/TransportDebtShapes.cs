using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace TransportInvariants;

internal static partial class HistoryShapeProof
{
    // Three history shapes the strict corpus flagged that turned out legitimate, accepted only under typed conditions:
    //  - Onyxia: the hero takes 5 damage, 2 absorbed by Armor and the other 3 replaced by +3 Health, while the DAMAGE metadata
    //    keeps the full 5;
    //  - Genn: a held card transforms and its earlier +2/+2 layer is restored (not a fresh buff);
    //  - Rewind: FATIGUE going back to 0 inside a GAME_RESET block is restoration, not a fatigue draw.
    // Each has a negative control (wrong amount, no reset block, ordinary buff) so the exemptions cannot hide real corruption.
    internal static void DebtShapes()
    {
        ExemptionConditionStreams();
        var f = new Fixture(CardClass.WARRIOR); var p = f.Player;
        f.Summon(p, "CATA_155");
        var victim = f.Summon(p.Opponent, "CAP_806");
        victim[GameTag.ATK] = 5;
        p.Hero[GameTag.ATK] = 1; p.Hero.Armor = 2;
        f.Ready("onyxia-health-replacement");
        int before = p.Hero.BaseHealth;
        f.Act(HeroAttackTask.Any(p, victim));
        f.Session.History.Finish();
        var bucket = HistoryShapeReport.Bucket(f.Session.History.Findings, "DAMAGE_META_AMOUNT", "HERO_01");
        Check(bucket.Count == 1 && p.Hero.BaseHealth == before + 3 && p.Hero.Armor == 0,
            "Onyxia replaces three Health loss after two Armor loss; bucket=" + bucket.Count + "; health=" + before + "/" + p.Hero.BaseHealth + "; armor=" + p.Hero.Armor + "; findings=" + string.Join(";", f.Session.History.Findings));
        Check(HistoryShapeReport.Classify(new[] { bucket }, HistoryShapeReport.LoadBaseline(), false).Fresh.Length == 0,
            "Onyxia full five-damage metadata is reviewed separately from two-Armor recorder delta");
        foreach (bool correctAmount in new[] { false, true })
        {
            var shape = Seed();
            Feed(shape, TC(12, GameTag.ARMOR, 2), Start(1, 12, 15));
            var hit = Hit(source: 15, amount: 2); hit.Victim = 12; hit.ReplacedHealth = 3;
            shape.RecordMutation(12, GameTag.ARMOR, 0, hit);
            Feed(shape, Meta(HistoryMeta.Type.DAMAGE, correctAmount ? 5 : 6, 12), TC(12, GameTag.ARMOR, 0), End());
            shape.Finish();
            var damage = HistoryShapeReport.Bucket(shape.Findings, "DAMAGE_META_AMOUNT", "HERO_01");
            Check(damage.Count == 1 && (HistoryShapeReport.Classify(new[] { damage }, HistoryShapeReport.LoadBaseline(), false).Fresh.Length == 0) == correctAmount,
                "replacement accepts only Armor loss plus replaced Health=" + correctAmount);
        }
        foreach (bool reset in new[] { false, true })
        {
            var shape = Seed();
            Feed(shape, Full(10, "TIME_004"), TC(12, GameTag.FATIGUE, 1));
            shape.Findings.Clear();
            Feed(shape, Start(reset ? 11 : 3, 10, 0), new PowerHistoryData {
                ResetGame = new() { SourceIsRewind = true }
            }, TC(12, GameTag.FATIGUE, 0), End());
            shape.Finish();
            var fatigue = HistoryShapeReport.Bucket(shape.Findings, "FATIGUE_BLOCK", "TIME_004");
            Check(fatigue.Count == 1 && (HistoryShapeReport.Classify(new[] { fatigue }, HistoryShapeReport.LoadBaseline(), false).Fresh.Length == 0) == reset,
                "FATIGUE restoration accepted only inside actual Rewind reset block=" + reset);
        }
        Console.WriteLine("PASS transport debt shapes: retained hand layers, Onyxia replacement, Rewind fatigue; ordinary corruption rejected");
    }
}
