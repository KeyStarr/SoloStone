using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CharredChameleon
    {
        internal const int DbfId = 115632;
        private static bool UsedPower(HistoryQuery history, int controllerId) => history.Payments(history.ThisTurn)
            .Any(f => f.ControllerId == controllerId && f.ActionKind == HistoryPaymentKind.HeroPower);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(UsedPower);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 },
                { PlayReq.REQ_FRIENDLY_TARGET, 0 }
            });
            card.TargetingAvailabilityPredicate = (controller, _) => UsedPower(controller.Game.HistoryFacts.Query, controller.Id);
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                if (ctx.TargetId is int target && UsedPower(ctx.History, ctx.ControllerId)) ctx.GiveBuff(target, "FIR_908e3");
                return TaskState.COMPLETE;
            }) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(2), Effects.Rush) };
        }

        private static bool UsedPower(IPlayable source) => UsedPower(source.Game.HistoryFacts.Query, source.Controller.Id);
    }
}
