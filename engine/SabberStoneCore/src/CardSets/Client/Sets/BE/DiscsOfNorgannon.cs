using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DiscsOfNorgannon
    {
        [StandardCard("BE_250", 129112)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                DrawReplacementRules.MoveSpellsToTop(c);
                if (c.AppliedEnchantments?.Any(e => e.Card.Id == "BE_250e") != true)
                    Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("BE_250e"));
            }) };
        }
        [StandardCard("BE_250e", 129741)]
        internal static void ConfigureEffect(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = StateTaskList.Suspendable("client/BE_250e/v1",
                new DiscoverThenContinueTask("BE_250t", "BE_250t2"),
                new CustomTask((g, c, s, t, stack) => {
                    if (!c.DeckZone.IsEmpty)
                        Generic.Draw(c, stack.Playables[0].Card.Id == "BE_250t2" ? c.DeckZone[0] : c.DeckZone.TopCard);
                })) };
        }
        [StandardCard("BE_250t2", 131173)]
        internal static void ConfigureBottomOption(Card card) => ConfigureOption(card);
        [StandardCard("BE_250t", 131172)]
        internal static void ConfigureOption(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
