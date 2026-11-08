using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MedivhsTriumph
    {
        internal const int DbfId = 123570;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(ControlsLegendary);
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => 1, EffectOperator.SET, new SelfCondition(ControlsLegendary)),
                PowerTask = new DamageTask(4, EntityType.ALLMINIONS, true)
            };
        }

        private static bool ControlsLegendary(IPlayable source)
        {
            var controller = source.Controller;
            return controller.BoardZone.Slots.Any(m => m.Id != source.Id && Entity.IsLegendary(m)) ||
                controller.Hero.Id != source.Id && Entity.IsLegendary(controller.Hero) ||
                controller.Hero.Weapon?.Id != source.Id && Entity.IsLegendary(controller.Hero.Weapon) ||
                controller.SecretZone.Any(m => m.Id != source.Id && Entity.IsLegendary(m)) ||
                controller.SecretZone.Quest?.Id != source.Id && Entity.IsLegendary(controller.SecretZone.Quest) ||
                controller.SecretZone.ModernQuests.Any(q => q.Id != source.Id && Entity.IsLegendary(q));
        }
    }
}
