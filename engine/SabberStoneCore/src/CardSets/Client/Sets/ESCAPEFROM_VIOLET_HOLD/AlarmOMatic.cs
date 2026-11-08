using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AlarmOMatic
    {
        [StandardCard("JAIL_502", 125986)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Minion bot) || bot.Zone != c.BoardZone || bot.IsSilenced || bot.ToBeDestroyed) return;
                    var candidates = c.Opponent.HandZone.OfType<Minion>().ToArray();
                    if (candidates.Length == 0) return;
                    var incoming = candidates[g.Random.Next(candidates.Length)]; g.OnRandomHappened(true);
                    SwapRules.ExchangeBoardAndHand(bot, incoming);
                })
            } };
        }
    }
}
