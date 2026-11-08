using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LatorviusGazeOfTheCity
    {
        internal static readonly string[] Rewards = { "UNG_116t", "UNG_028t", "UNG_067t1", "UNG_920t1", "UNG_934t1",
            "UNG_940t8", "UNG_942t", "UNG_829t1", "UNG_954t1" };
        [StandardCard("TLC_602t", 117717)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var selected = Rewards.ChooseNElements(2, g.Random);
                foreach (string id in selected)
                {
                    var reward = Entity.FromCard(c, Cards.FromId(id), new EntityData {
                        [GameTag.ZONE] = (int)Zone.HAND, [GameTag.DISPLAYED_CREATOR] = s.Id
                    });
                    Generic.AddHandPhase(c, reward);
                }
                foreach (string id in Rewards.Except(selected))
                {
                    if (c.DeckZone.IsFull) break;
                    var reward = Entity.FromCard(c, Cards.FromId(id), new EntityData {
                        [GameTag.ZONE] = (int)Zone.DECK, [GameTag.DISPLAYED_CREATOR] = s.Id
                    });
                    Generic.ShuffleIntoDeck(c, s, reward);
                }
            }) };
        }
    }
}
