using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class JadeGuardians
    {
        internal const int DbfId = 130021;
        private static int Discount(HistoryQuery history,int controller) => history.Payments().Count(f=>
            f.ControllerId==controller && f.ActionKind==HistoryPaymentKind.Play && f.Mana==2 && f.Health==0);
        private static int Display(IPlayable source) => Discount(source.Game.HistoryFacts.Query,source.Controller.Id);
        [StandardCard("JAIL_474", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Display);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var ctx = new EffectContext(g,c.Id,s.Id);
                int discount = Discount(ctx.History,c.Id);
                for (int i=0;i<2;i++)
                {
                    var chosen = ctx.PickRandomCard(B14K6JailPools.EightCostMinion(),recipeDbfId:DbfId);
                    if (chosen == null) continue;
                    var minion = (Playable)Entity.FromCard(c,chosen,new EntityData { [GameTag.DISPLAYED_CREATOR]=s.Id });
                    CostRules.AddModifier(minion,EffectOperator.SUB,discount);
                    Generic.AddHandPhase(c,minion);
                }
            }) };
        }
    }
}
