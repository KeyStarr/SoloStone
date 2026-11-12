using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GodfreyTheBetrayer
    {
        internal const string StartOfGamePrefab = "JAILFX_Godfrey_StartOfGame_FX:d90529ac3e839004d924ed987fa692eb";
        [StandardCard("JAIL_509", 126125)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
            StartOfGameRules.Declare(card, source => {
                // The Start of Game effect is Godfrey's first authored sub-spell; his CardDef has no trigger slot to play.
                if (source.Game.History)
                {
                    source.Game.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellStart { Source = source.Id, SpellPrefabGuid = StartOfGamePrefab });
                    source.Game.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistorySubSpellEnd());
                }
                QuestRules.AddPlayerEffect(source.Controller, source, "JAIL_509e");
                source.Controller.Hero[(SabberStoneCore.Enums.GameTag)4717] = 1;
            });
        }
    }
}
