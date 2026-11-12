using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static partial class RewindProof
{
    private static void CardStateRollback()
    {
        foreach (string id in new[] { "TIME_046", "CATA_489", "JAIL_801" })
        {
            var installed = SabberStoneCore.Model.Cards.FromId(id);
            var detached = new HistoryDefinition(installed).Materialize();
            Check(detached.Id == id && (detached.Dormant != null) == (installed.Dormant != null)
                && (detached.Shatter != null) == (installed.Shatter != null)
                && (detached.HandProgress != null) == (installed.HandProgress != null),
                "card-state metadata survives detached definition codec");
        }
        var game = Fixture();
        var sleeper = Summon(game.Player1, "TIME_046");
        var progress = Add(game.Player1, "JAIL_801");
        var temporary = Add(game.Player1, "CS2_182");
        TemporaryRules.Make(temporary);
        var combined = ShatterRules.GiveCombined(game.Player1, "CATA_489", game.Player1.Hero);
        LeylineRules.Upgrade(game.Player1, game.Player1.Hero, cost: 1, extraTriggers: 1, effect: 1);
        var leyline = Add(game.Player1, "MEND_500");
        game.DeathProcessingAndAuraUpdate();
        Play(game, game.Player1, "TIME_001");
        Check(progress[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "candidate cast advances held progress once");
        foreach (var next in new[] { game.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(game.SaveSnapshot()), game })
        {
            // Disturb candidate state so a restore that loses any card-state family is observable.
            DormantRules.Accelerate((Minion)next.IdEntityDic[sleeper.Id], 1);
            Generic.DiscardBlock(next.Player1, next.IdEntityDic[temporary.Id]);
            Generic.DiscardBlock(next.Player1, next.IdEntityDic[combined.Id]);
            Answer(next, true);
            var restoredSleeper = (Minion)next.IdEntityDic[sleeper.Id];
            var restoredTemporary = next.IdEntityDic[temporary.Id];
            var restoredCombined = next.IdEntityDic[combined.Id];
            Check(restoredSleeper.IsDormant && DormantRules.TurnsRemaining(restoredSleeper) == 3
                && restoredSleeper.AppliedEnchantments.Single().Card.Id == "TIME_046e",
                "Rewind restores Dormant timer and helper");
            Check(restoredTemporary.Zone == next.Player1.HandZone && TemporaryRules.IsTemporary(restoredTemporary)
                && restoredTemporary.AppliedEnchantments.Single().Card.Id == "GBL_999e",
                "Rewind restores Temporary residence and helper");
            Check(restoredCombined.Zone == next.Player1.HandZone && restoredCombined[ShatterRules.AlreadyCombined] == 1,
                "Rewind restores permanent Shatter state");
            Check(next.IdEntityDic[progress.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                "Rewind replay advances hand progress exactly once after clone/load");
            Check(LeylineRules.CostReduction(next.Player1) == 1 && LeylineRules.Times(next.IdEntityDic[leyline.Id]) == 2
                && LeylineRules.Magnitude(next.IdEntityDic[leyline.Id]) == leyline.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] + 1,
                "Leyline controller state coexists with Shatter tags through Rewind");
            Balanced(next);
        }
        Console.WriteLine("PASS Rewind card-state rollback: Dormant, Temporary, Shatter, hand progress; clone/load");
    }
}
