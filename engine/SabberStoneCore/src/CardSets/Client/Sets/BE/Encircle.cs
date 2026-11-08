using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Encircle
    {
        internal const int DbfId = 130520;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                // Printed order: own Shredclaws first (appended right, creation order), then the opponent's minion.
                for (int i = 0; i < 5 && !c.BoardZone.IsFull; i++) ctx.Summon("BE_873t");
                // A full enemy board has no slot: no pick and no RNG (as Undefeated Champion).
                if (c.Opponent.BoardZone.IsFull) return;
                var chosen = ctx.PickRandomCard(BEPools.OneCostMinion(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var generated = Entity.FromCard(c.Opponent, chosen);
                generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                new SummonOpTask().Process(g, c, s, null, new TaskStack { Playables = new[] { generated } });
            }) };
        }
    }
}
