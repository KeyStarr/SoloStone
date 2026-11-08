using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GodfreyTheBetrayer
    {
        [StandardCard("JAIL_509", 126125)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
            StartOfGameRules.Declare(card, source => {
                QuestRules.AddPlayerEffect(source.Controller, source, "JAIL_509e");
                source.Controller.Hero[(SabberStoneCore.Enums.GameTag)4717] = 1;
            });
        }
    }
}
