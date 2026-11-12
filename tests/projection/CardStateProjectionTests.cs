#if ENGINE_SOURCE
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Config;
using SabberStoneCore.Kettle;
internal static class CardStateProjectionTests
{
    internal static void Run()
    {
        void Check(bool value, string label) { if (!value) throw new Exception("CardStateProjection: " + label); }
        Check(NativeUIProjection.DeckId("test-standard-cardstate-warrior") == 900000158L
            && NativeUIProjection.DeckId("test-standard-cardstate-warlock") == 900000156L
            && NativeUIProjection.TestLoanerGroups[8].Count == 8, "group 9 stable fixture seats");
        Check(LocalCardCatalog.CardStateGeneratedIds.Count == 41 && LocalCardCatalog.CardStateGeneratedIds.All(id => NativeUIProjection.SupportsPreview(id)), "export helper closure auditable");
        var g = new Game(new GameConfig { History = true, Logging = false, Shuffle = false, SkipMulligan = true }); g.StartGame();
        var p = g.Player1; CostRules.AddNextTemporaryRule(p, 2);
        var card = Entity.FromCard(p, Cards.FromId("CS2_182")); TemporaryRules.Make(card); Generic.AddHandPhase(p, card);
        Check(g.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == card.Id && (int)t.Tag == 3630 && t.Value == 1)
            && g.PowerHistory.Last.OfType<PowerHistoryTagChange>().Any(t => t.EntityId == card.Id && t.Tag == GameTag.COST && t.Value == 2), "marker and discounted admitted COST history");
        var m = (Minion)Entity.FromCard(p, Cards.FromId("TIME_046")); Generic.SummonBlock(g, m, -1, p.Hero);
        var helper = m.AppliedEnchantments.Single();
        Check(g.PowerHistory.Last.OfType<PowerHistoryFullEntity>().Any(e => e.Entity.Id == helper.Id && e.Entity.Name == "TIME_046e"
            && e.Entity.Tags[GameTag.ATTACHED] == m.Id), "identified helper FULL with parent attachment");
        foreach (bool beforeMark in new[] { false, true })
        {
            var held = Generic.DrawCard(p, Cards.FromId("CS2_182"));
            if (!beforeMark) TemporaryRules.Make(held);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CFM_305e"), p.Hero, held);
            if (beforeMark) TemporaryRules.Make(held);
            Check(held[HandProgressRules.PrivateAttachmentTarget] == held.Id
                && held.AppliedEnchantments.All(e => e[HandProgressRules.PrivateAttachmentTarget] == held.Id),
                "Round3AttachmentEnrollment Unpinned_TemporaryProjection enrolls target and every earlier/later attachment");
        }
        Console.WriteLine("PASS Round3AttachmentEnrollment: ordinary Temporary handbuffs before/after marking");
        Console.WriteLine("PASS CardStateProjection: fixture seats, Temporary marker/COST, Dormant identified helper");
    }
}
#endif
