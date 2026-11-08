using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Soletos
    {
        [StandardCard("TLC_817t3", 118551)]
        internal static void Life(Card card) => Configure(card, true, false);
        [StandardCard("TLC_817t4", 118552)]
        internal static void Death(Card card) => Configure(card, false, true);
        [StandardCard("TLC_817t5", 119879)]
        internal static void Cycle(Card card) => Configure(card, true, true);
        private static void Configure(Card card, bool life, bool death)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = life ? new CopyTask(EntityType.SOURCE, Zone.PLAY) : null,
                DeathrattleTask = death ? StateTaskList.Chain(new RandomTask(1, EntityType.ENEMIES),
                    new DamageTask(5, EntityType.STACK)) : null
            };
        }
    }
}
