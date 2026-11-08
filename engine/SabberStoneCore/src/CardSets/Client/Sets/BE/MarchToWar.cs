using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MarchToWar
    {
        internal const int DbfId = 130326;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (c.BoardZone.IsFull) return;
                int number = CostRules.ManaNumbers((IPlayable)s);
                var soldier = (Minion)Entity.FromCard(c, Cards.FromId("BE_142t"));
                soldier.AttackDamage = number; soldier.BaseHealth = number;
                Generic.SummonBlock(g, soldier, -1, s);
            }) };
        }
    }
}
