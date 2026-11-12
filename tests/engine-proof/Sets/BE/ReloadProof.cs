using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class ReloadProof
{
    internal static void Run() => StandardSetsProof.Case("BE_555", Unpinned_ExcessBudgetAndCapacity);
    private static void Unpinned_ExcessBudgetAndCapacity(Game original, Controller ignored, string id)
    {
        foreach (bool zero in new[] { false, true })
        {
            var g = New(original.Seed); var p = g.Player1;
            var source = Play(g, p, id); var first = Offers(g).First();
            Assert(Offers(g).All(o => o.Card.Type == SabberStoneCore.Enums.CardType.SPELL && o.Card.Id != id), "declared class+neutral spell Discover excludes source");
            first.Cost = zero ? 0 : 3;
            Saved(g, next => {
                Answer(next, first.Id);
                Assert(next.IdEntityDic[source.Id][DiscoverRealRules.Budget] == (zero ? 8 : 5)
                    && next.IdEntityDic[first.Id].Cost == 0 && next.Player1.Choice != null, "carry unused budget, zero-cost choice consumes none");
            });
            var second = Offers(g).First(); second.Cost = zero ? 8 : 6;
            Saved(g, next => {
                Answer(next, second.Id);
                Assert(next.Player1.Choice == null && next.IdEntityDic[source.Id][DiscoverRealRules.Budget] == 0
                    && next.IdEntityDic[second.Id].Cost == (zero ? 0 : 1), "exact exhaustion stops, 3 then 6 spends 3 then 5");
            });
        }
        foreach (int capacity in new[] { 10, 12 })
        {
            var full = New(original.Seed); var owner = full.Player1;
            owner[SabberStoneCore.Enums.GameTag.MAXHANDSIZE] = capacity;
            for (int i = 0; i < 8; i++) Add(owner, "CS2_182");
            Play(full, owner, id);
            for (int i = 0; i < capacity - 8; i++)
            {
                Assert(Offers(full).Length > 0, "remaining budget continues while expanded hand has space");
                var offer = Offers(full).First(); offer.Cost = 0; Answer(full, offer.Id);
            }
            Assert(owner.HandZone.Count == capacity && owner.HandZone.IsFull && owner.Choice == null,
                "zero-cost sequence stops at current expanded hand capacity");
            Saved(full, next => Assert(next.Player1.HandZone.Count == capacity && next.Player1.HandZone.IsFull, "expanded full hand clone/snapshot"));
        }
        var reaction = New(original.Seed); var player = reaction.Player1;
        Play(reaction, player, id);
        var listener = Summon(player, "CS2_182");
        var fill = new Trigger(TriggerType.ZONE) { TriggerSource = TriggerSource.FRIENDLY,
            SingleTask = new CustomTask((game, controller, source, target, stack) => {
                if (source[DiscoverRealRules.Budget] != 0) return;
                source[DiscoverRealRules.Budget] = 1;
                Assert(controller.Choice == null, "consumed choice cleared before hand-entry reactions");
                while (!controller.HandZone.IsFull) Generic.DrawCard(controller, Cards.FromId("CS2_182"));
            }) };
        fill.Activate(listener);
        var firstReaction = Offers(reaction).First(); firstReaction.Cost = 0;
        Answer(reaction, firstReaction.Id);
        Assert(player.HandZone.IsFull && player.Choice == null, "intervening hand-fill reaction stops repeat despite unused budget");
        listener.ActivatedTrigger?.Remove();
    }
}
