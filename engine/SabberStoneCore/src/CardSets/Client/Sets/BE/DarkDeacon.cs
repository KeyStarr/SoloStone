using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DarkDeacon
    {
        internal const int DbfId = 132348;
        private static int Remaining(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            return Math.Max(0,2 - history.Casts(history.Residence(source.Id,Zone.HAND)).Count(f =>
                f.ControllerId == source.Controller.Id && (f.SpellSchool == 6 || f.SpellSchool == 0 && CrystalsmithCultist.IsLegacyShadowSpell(f.DbfId)) && f.CasterKind == HistoryCastKind.Player && f.Outcome == HistoryCastOutcome.Completed));
        }
        private static bool Ready(IPlayable source) => Remaining(source) == 0;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.PoweredUpWhen(Ready);
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Remaining);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_012/v1",
                new ConditionTask(EntityType.SOURCE,new SelfCondition(Ready)),
                new FlagTask(false,EffectContext.Task(_ => TaskState.STOP)),
                CthunRules.ExternalAbility("client/BE_012/ability")) };
        }
    }
}
