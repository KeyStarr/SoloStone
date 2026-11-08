using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Reforestation
    {
        internal const int DbfId = 114848;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HandProgress = new HandProgressDefinition("EDR_843t1", 3, HandProgressMode.OwnerTurnEnds);
        }

        internal static void ConfigureSpell(Card card) => ConfigureDraw(card, true, false);
        internal static void ConfigureMinion(Card card) => ConfigureDraw(card, false, true);
        internal static void ConfigureBoth(Card card) => ConfigureDraw(card, true, true);

        private static void ConfigureDraw(Card card, bool spell, bool minion)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (spell) ctx.DrawWhere(c => c.Card.Type == CardType.SPELL);
                if (minion) ctx.DrawWhere(c => c.Card.Type == CardType.MINION);
                return TaskState.COMPLETE;
            }) };
        }
    }
}
