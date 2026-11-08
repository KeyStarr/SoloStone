using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Vaultkeeper
    {
        [StandardCard("BE_151", 129093)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.PositionAnchor] = 1;
            var buff = new CustomTask((g, c, s, t, stack) => {
                var neighbours = s.Zone == c.BoardZone ? ((Minion)s).GetAdjacentMinions() : DeathContextRules.DeathNeighbours(s);
                foreach (var neighbour in neighbours)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("BE_151e"), (IPlayable)s, neighbour);
            });
            card.Power = new Power { PowerTask = buff, DeathrattleTask = buff };
        }
        [StandardCard("BE_151e", 130743)]
        internal static void Buff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
