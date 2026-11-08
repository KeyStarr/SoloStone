using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BladeOfTheBlackEmpire
    {
        internal const int DbfId = 130402;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    bool shadow = ctx.History.Casts(ctx.History.ThisTurn).Any(f => f.ControllerId == c.Id
                        && f.SpellSchool == 6 && f.Outcome == HistoryCastOutcome.Completed);
                    var hero = shadow ? c.Opponent.Hero : c.Hero;
                    // Same fatigue path as an empty-deck draw, without drawing or changing the deck.
                    if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.FATIGUE, hero.Id, "", -1, 0));
                    hero.Fatigue++;
                    new EffectContext(g, hero.Controller.Id, hero.Id).Damage(hero.Id, hero.Fatigue);
                    if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                })
            } };
        }
    }
}
