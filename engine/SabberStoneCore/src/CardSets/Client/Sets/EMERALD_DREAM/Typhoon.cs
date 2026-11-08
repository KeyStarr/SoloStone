using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Typhoon
    {
        internal const int DbfId = 114357;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var minions = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).ToArray();
                foreach (var minion in minions) {
                    bool opponent = g.Random.Next(2) == 1;
                    g.OnRandomHappened(true);
                    var destination = opponent ? c.Opponent : c;
                    minion.Zone.Remove(minion);
                    if (minion.AppliedEnchantments != null)
                        foreach (var enchantment in minion.AppliedEnchantments.ToArray()) enchantment.Remove();
                    minion.Reset();
                    // Reset leaves the targeting cache set by Silence; restore printed Elusive.
                    minion.CantBeTargetedBySpells = minion.Card.CantBeTargetedBySpells;
                    if (minion.Controller != destination) {
                        minion.Controller = destination;
                        minion[GameTag.CONTROLLER] = destination.PlayerId;
                    }
                    if (!Generic.ShuffleIntoDeck(destination, s, minion)) destination.GraveyardZone.Add(minion);
                }
            }) };
        }
    }
}
