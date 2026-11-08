using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ulfar
    {
        internal const int DbfId = 127409;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Random(type: CardType.MINION);
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                foreach (var minion in c.BoardZone.GetAll().Where(m => m.Id != s.Id).ToArray())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("CORE_CATA_006e"), (IPlayable)s, minion);
            }) };
        }
        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                if (c.BoardZone.IsFull) return;
                stack ??= new TaskStack(); stack.Number = CostRules.DeathCost((IPlayable)s);
                var context = new EffectContext(g,c.Id,s.Id,stack:stack);
                var pick = context.PickRandomCard(Filter, PoolCostBinding.StackNumber, DbfId);
                if (pick != null) context.Summon(pick.Id, EffectContext.DeathrattlePosition(c,s));
            }) };
        }
    }
}
