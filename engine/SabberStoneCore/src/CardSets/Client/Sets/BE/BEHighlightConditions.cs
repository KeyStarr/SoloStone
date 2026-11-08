using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BEHighlightConditions
    {
        internal static bool PlayedTwoSpellSchools(IPlayable source)
        {
            int mask = source[GameTag.TAG_SCRIPT_DATA_NUM_2];
            return (mask & (mask - 1)) != 0;
        }

        // DeathProcessing updates this counter; MainReady resets it for both controllers each turn.
        internal static bool FriendlyMinionDiedThisTurn(IPlayable source) =>
            source.Controller.NumFriendlyMinionsThatDiedThisTurn > 0;
    }
}
