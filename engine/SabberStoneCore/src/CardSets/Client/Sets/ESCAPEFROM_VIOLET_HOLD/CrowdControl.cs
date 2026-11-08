using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrowdControl
    {
        internal const int DbfId = 126100;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(LargeDeck);
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => -2, EffectOperator.ADD, new SelfCondition(LargeDeck)),
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    for (int i = 0; i < 2; i++)
                    {
                        new DamageTask(2, EntityType.ALLMINIONS, spellDmg: true).Process(g, c, s, t, stack);
                        g.DeathProcessingAndAuraUpdate();
                    }
                })
            };
        }
        private static bool LargeDeck(IPlayable source) =>
            source.Controller.DeckZone.Count >= 25;

    }
}
