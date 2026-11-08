using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IcebeltMagi
    {
        internal const int DbfId = 129889;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int nonminions = 0;
                for (int i = 0; i < 2; i++)
                {
                    // Generic.Draw also returns burned cards, which are not successful draws.
                    bool hasHandSpace = !c.HandZone.IsFull;
                    var drawn = Generic.Draw(c);
                    if (hasHandSpace && drawn != null && !(drawn is Minion)) nonminions++;
                }
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                for (int i = 0; i < nonminions; i++) ctx.Summon("CS2_033", position++);
            }) };
        }

    }
}
