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
    internal static class Scorchreaver
    {
        internal const int DbfId = 115749;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.SPELL, spellSchool: 7);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/FIR_952/v1",
                new DiscoverThenContinueTask(Filter, recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen != null) {
                        Generic.RemoveFromZone(c, chosen);
                        Generic.AddHandPhase(c, chosen);
                    }
                    foreach (var spell in c.HandZone.Where(x => x.Card.Type == CardType.SPELL && x.Card[GameTag.SPELL_SCHOOL] == 7))
                        CostRules.AddModifier((Playable)spell, EffectOperator.SUB, 1);
                })) };
        }
    }
}
