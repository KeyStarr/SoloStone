using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CuriousGrimlynx
    {
        internal const int DbfId = 129814;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var source = (Minion)s;
                var beast = (Minion)Entity.FromCard(c, Cards.FromId("BE_850"));
                beast.AttackDamage = source.Health;
                beast.BaseHealth = source.AttackDamage;
                int position = source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                Generic.SummonBlock(g, beast, position, s);
            }) };
        }

    }
}
