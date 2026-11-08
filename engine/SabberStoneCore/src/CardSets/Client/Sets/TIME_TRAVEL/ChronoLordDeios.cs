using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChronoLordDeios
    {
        [StandardCard("TIME_064", 120985)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new Aura(AuraType.CONTROLLER,
                new Effect(GameTag.EXTRA_BATTLECRIES_BASE, EffectOperator.ADD, 1),
                new Effect(GameTag.EXTRA_DEATHRATTLES_BASE, EffectOperator.ADD, 1)) };
        }
    }
}
