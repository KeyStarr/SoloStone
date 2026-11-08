using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChamberOfAspects
    {
        [StandardCard("CATA_477", 123117)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                TargetCondition = (c, t) => c.HandZone.Any(p => p is Minion),
                Task = StateTaskList.Suspendable("b16.chamber.v1",
                    new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand, cardType: CardType.MINION)),
                    EffectContext.Task(ctx => {
                        if (ctx.LastChosenEntityId != 0) ctx.GiveBuff(ctx.LastChosenEntityId, "CATA_477e");
                        return ctx.RunLegacy(new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE));
                    }))
            } };
        }
        [StandardCard("CATA_477e", 123118)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
