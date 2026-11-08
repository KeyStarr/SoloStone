using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Hellraiser
    {
        internal const int DbfId = 126664;
        private static bool Empty(IPlayable card) => card.Controller.DeckZone.IsEmpty;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            PoweredUp.Register(card, Empty);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/JAIL_734/v1",
                new CustomTask((g, c, s, t, stack) => {
                    if (c.DeckZone.IsEmpty)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_734e"), (IPlayable)s, (IPlayable)s);
                }), new DiscoverThenContinueTask(DiscoverEntitySource.Deck, DiscoverEntityAction.Draw)) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(4), Effects.Health_N(4)) };
        }
    }
}
