#if ENGINE_SOURCE
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static class HandCardStateProjectionTests
{
    internal static void Run()
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception("card-state projection: " + label); }
        var g = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        g.StartGame(); var p = g.Player1;
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        Generic.DrawCard(p, Cards.FromId("CS2_182")); var parent = Generic.DrawCard(p, Cards.FromId("CATA_489"));
        var halves = p.HandZone.Where(c => c.Card.Shatter != null).ToArray();
        Check(halves.Length == 2 && halves.All(h => h.Id != parent.Id), "new half actors");
        foreach (var h in halves)
        {
            int full = g.PowerHistory.Full.FindIndex(e => e is PowerHistoryFullEntity f && f.Entity.Id == h.Id);
            int reference = g.PowerHistory.Full.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == h.Id);
            Check(full >= 0 && reference > full, "FULL precedes half references");
        }
        var heldCard = Generic.DrawCard(p, Cards.FromId("JAIL_801"));
        Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_087e"), p.Hero, heldCard);
        int attachmentId = heldCard.AppliedEnchantments.Single().Id;
        var snapshot = PowerHistoryBuilder.ShowEntity(heldCard);
        heldCard[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        Check(snapshot.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "counter snapshot immutable");
        var spell = (Spell)Entity.FromCard(p, Cards.FromId("GAME_005"));
        HandProgressRules.CompleteCast(p, HandProgressRules.CaptureCast(spell), HistoryCastKind.Player, HistoryCastOutcome.Completed);
        var upgrade = g.PowerHistory.Last.OfType<PowerHistoryChangeEntity>().Single(e => e.Entity.Id == heldCard.Id);
        Check(upgrade.CardId == "JAIL_801t" && upgrade.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && upgrade.Entity.Tags[GameTag.SPELL_SCHOOL] == 0,
            "same-id upgrade clears old-only tags");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Check(next.IdEntityDic[attachmentId][HandProgressRules.PrivateAttachmentTarget] == heldCard.Id,
                "retained attachment privacy provenance survives upgrade/clone/load");
            var target = next.IdEntityDic[heldCard.Id];
            Check(target[HandProgressRules.PrivateAttachmentTarget] == target.Id, "upgraded target remains enrolled after clone/load");
            Generic.AddEnchantmentBlock(next, Cards.FromId("CFM_305e"), next.Player1.Hero, target);
            Check(target.AppliedEnchantments.All(e => e[HandProgressRules.PrivateAttachmentTarget] == target.Id),
                "later handbuff inherits privacy after upgrade/clone/load");
            var copy = Generic.Copy(next.Player1, next.Player1.Hero, target, Zone.HAND);
            Generic.AddEnchantmentBlock(next, Cards.FromId("CS2_087e"), next.Player1.Hero, copy);
            Check(copy.AppliedEnchantments.All(e => e[HandProgressRules.PrivateAttachmentTarget] == copy.Id),
                "copied and newly granted attachments follow copied upgraded target");
            var minion = (Minion)target;
            next.Player1.HandZone.Remove(minion); Generic.SummonBlock(next, minion, -1, null);
            next.DeathProcessingAndAuraUpdate(); Generic.ReturnToHandBlock(next.Player1, minion);
            Generic.AddEnchantmentBlock(next, Cards.FromId("CFM_305e"), next.Player1.Hero, minion);
            Check(minion.AppliedEnchantments.Last()[HandProgressRules.PrivateAttachmentTarget] == minion.Id,
                "bounced upgraded target keeps privacy enrollment for later handbuffs");
        }
        Check(g.PowerHistory.Full.OfType<PowerHistoryFullEntity>().All(f => f.Entity.Tags.Keys.All(t => !ShatterRules.IsInternalTag(t) && !HandProgressRules.IsInternalTag(t))), "private tags excluded from snapshots");
        Check(NativeUIProjection.DeckId("test-standard-cardstate-mage") == 900000152L && NativeUIProjection.DeckId("test-standard-cardstate-shaman") == 900000155L,
            "fixture native IDs");
        Console.WriteLine("PASS CardStateProjectionTests: half FULL ordering, immutable counters, same-id upgrade zeros, private tags, group-9 IDs");
    }
}
#endif
