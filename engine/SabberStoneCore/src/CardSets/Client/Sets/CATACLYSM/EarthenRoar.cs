using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EarthenRoar
    {
        internal const int DbfId = 122883;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CataclysmHighlightConditions.HoldingAnotherDragon);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CATA_554/v1",
                EffectContext.Task(ctx => ctx.TargetId.HasValue ? ctx.GiveBuff(ctx.TargetId.Value, "CATA_554e") : TaskState.COMPLETE),
                EffectContext.Task(ctx => {
                    bool dragon = false;
                    ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => { dragon = CataclysmHighlightConditions.HoldingAnotherDragon((IPlayable)s); }));
                    return dragon ? TaskState.COMPLETE : TaskState.STOP;
                }),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board, SelectorSide.Enemy,
                    cardType: CardType.MINION, excludePlayTarget: true)),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE : ctx.GiveBuff(ctx.LastChosenEntityId, "CATA_554e"))) };
        }
        internal static void ConfigureHealth(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetMaxHealth(1)) };
        }
    }
}
