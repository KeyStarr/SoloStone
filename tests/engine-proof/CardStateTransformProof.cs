using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CardStateTransformProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        foreach (string through in new[] { "CS2_029", "CS2_182" })
        foreach (string id in new[] { "JAIL_801", "CATA_489t", "CATA_489t2", "CS2_120" })
        {
            var game = HeroDivineShieldProof.NewGame(history); var owner = game.Player1;
            var card = Add(owner, id);
            TemporaryRules.Make(card);
            int helperId = card.AppliedEnchantments.Single().Id, position = card.ZonePosition;
            if (id == "JAIL_801")
            {
                card[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
                card = Generic.ChangeEntityBlock(owner, card, Cards.FromId(id), false);
                Check(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "TransformRoundTrip same-definition rebind keeps partial progress");
            }
            card = Generic.ChangeEntityBlock(owner, card, Cards.FromId(through), true);
            foreach (var next in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var p = next.Player1;
                var restored = Generic.ChangeEntityBlock(p, next.IdEntityDic[card.Id], Cards.FromId(id), true);
                string label = $"TransformRoundTrip {id}/{through}: ";
                Check(restored.Id == card.Id && restored.ZonePosition == position && restored.Zone == p.HandZone,
                    label + "same hand identity/slot");
                Check(TemporaryRules.IsTemporary(restored) && restored.AppliedEnchantments.Single().Id == helperId &&
                    restored.AppliedEnchantments.Single().Target == restored, label + "Temporary helper retained and rebound once");
                foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, (GameTag)401,
                    GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
                    Check(restored[tag] == restored.Card[tag], label + tag + " uses returned definition");
                if (id == "JAIL_801")
                {
                    Check(restored[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, label + "fresh definition counter 3");
                    for (int remaining = 2; remaining >= 0; remaining--)
                    {
                        Play(next, p, "GAME_005");
                        Check(next.IdEntityDic[card.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == remaining,
                            label + "completed cast counter " + remaining);
                    }
                    Check(next.IdEntityDic[card.Id].Card.Id == "JAIL_801t", label + "third cast really upgrades");
                }
                else if (id.StartsWith("CATA_489"))
                {
                    Check(restored[(GameTag)4240] == 1 && restored[(GameTag)401] == 1, label + "Shattered markers restored");
                    Add(p, id == "CATA_489t" ? "CATA_489t2" : "CATA_489t");
                    Check(p.HandZone.Any(c => c.Card.Id == "CATA_489" && c[ShatterRules.AlreadyCombined] == 1),
                        label + "returned half really recombines");
                }
                TemporaryRulesProof.End(next);
                Check(p.DiscardedEntities.Count == 1 && !p.HandZone.Any(TemporaryRules.IsTemporary),
                    label + "retained Temporary really expires once");
            }
        }
        foreach (bool history in new[] { false, true })
        foreach (bool awake in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            var minion = HeroDivineShieldProof.Summon(game, "TIME_046");
            int helperId = DormantRules.SleepGeneration(minion);
            if (awake) DormantRules.Awaken(minion); else DormantRules.Accelerate(minion, 1);
            minion = (Minion)Generic.ChangeEntityBlock(p, minion, Cards.FromId("CS2_182"), true);
            Check(!minion.IsDormant && !DormantRules.HasState(minion) && minion[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 &&
                game.IdEntityDic[helperId][GameTag.ATTACHED] == 0, "TransformRoundTrip Dormant exit clears state/helper");
            foreach (var next in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var restored = (Minion)Generic.ChangeEntityBlock(next.Player1, next.IdEntityDic[minion.Id], Cards.FromId("TIME_046"), true);
                Check(restored.IsDormant && DormantRules.TurnsRemaining(restored) == 3 &&
                    restored[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && DormantRules.SleepGeneration(restored) != helperId &&
                    restored.AppliedEnchantments.Count(e => e.Card.Id == "TIME_046e") == 1,
                    "TransformRoundTrip Dormant return starts fresh three-turn sleep/helper");
                DormantRules.Accelerate(restored, 3);
                Check(!restored.IsDormant && restored[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0,
                    "TransformRoundTrip Dormant return really awakens");
            }
        }
        Console.WriteLine("PASS CardStateTransformRoundTrip: hand progress/Temporary/both Shatter halves; board Dormant asleep/awake; same/cross-type; history on/off; clone/restore; " +
            "Unpinned_HandFresh Unpinned_HandCastScope Unpinned_HandUpgradeTiming Unpinned_TemporaryResidence Unpinned_TemporaryExpiry Unpinned_TemporaryEndOrder Unpinned_ShatterPermanent Unpinned_ShatterJoin Unpinned_DormantLifecycle");
    }
}
