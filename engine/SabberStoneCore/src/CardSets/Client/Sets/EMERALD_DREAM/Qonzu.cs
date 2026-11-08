using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Qonzu
    {
        internal const int DbfId = 114287;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/EDR_517/v1",
                new DiscoverThenContinueTask(EmeraldDreamPools.QonzuSpell(), recipeDbfId: DbfId),
                EffectContext.Task(ctx => ctx.LastChosenCardId == null ? TaskState.STOP : TaskState.COMPLETE),
                new CustomTask((g, c, s, t, stack) => stack.Number = stack.Playables.LastOrDefault()?.Id ?? 0),
                new DiscoverThenContinueTask("EDR_517A", "EDR_517B"),
                new CustomTask((g, c, s, t, stack) => {
                    if (stack.Number == 0) return;
                    var chosen = g.IdEntityDic[stack.Number];
                    var option = stack.Playables.LastOrDefault();
                    if (option?.Card.Id == "EDR_517B")
                    {
                        if (c.Opponent.DeckZone.IsFull) return;
                        chosen.Zone.Remove(chosen);
                        chosen.Controller = c.Opponent;
                        chosen[SabberStoneCore.Enums.GameTag.CONTROLLER] = c.Opponent.PlayerId;
                        c.Opponent.DeckZone.PutOnTop(chosen);
                    }
                    else if (option?.Card.Id == "EDR_517A")
                    {
                        chosen.Zone.Remove(chosen);
                        Generic.AddHandPhase(c, chosen);
                    }
                })) };
        }
    }
}
