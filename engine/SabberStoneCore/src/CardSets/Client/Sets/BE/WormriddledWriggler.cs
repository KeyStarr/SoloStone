using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WormriddledWriggler
    {
        internal const int DbfId = 130346;
        [StandardCard("BE_566", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_566/v1",
                new SelectEntityThenContinueTask(),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                        if (t.Zone != c.HandZone || c.DeckZone.IsFull) return;
                        c.HandZone.Remove(t);
                        // Backward movement loses hand buffs before the new discount; legacy shuffle keeps attachments.
                        while (t.AppliedEnchantments?.Count > 0) t.AppliedEnchantments[0].Remove();
                        t.Reset();
                        CostRules.AddModifier((Playable)t, EffectOperator.SUB, 1);
                        Generic.ShuffleIntoDeck(c, s, t);
                    }), ctx.LastChosenEntityId))) };
        }
    }
}
