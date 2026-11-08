using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Chromie
    {
        internal const int DbfId = 121672;
        private static int[] Plays(IPlayable source) => source.Game.HistoryFacts.Query.Plays()
            .Where(p => p.ControllerId == source.Controller.Id && p.DefinitionKnown).Select(p => p.DbfId).ToArray();
        private static int Count(IPlayable source)
        {
            var played = Plays(source).ToHashSet();
            return source.Controller.DeckZone.Count(c => played.Contains(c.Card.AssetId));
        }
        [StandardCard("TIME_103",DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Count);
            card.BoardHistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Count);
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                foreach (int dbf in Plays((IPlayable)s))
                {
                    var matches = c.DeckZone.Where(x => x.Card.AssetId == dbf).ToArray();
                    if (matches.Length == 0) continue;
                    g.OnRandomHappened(true);
                    Generic.Draw(c,matches[g.Random.Next(matches.Length)]);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
    }
}
