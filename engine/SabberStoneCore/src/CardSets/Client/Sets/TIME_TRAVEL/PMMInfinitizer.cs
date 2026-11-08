using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Battlecry: set a friendly minion to 8/8 (TIME_043e); it can't attack heroes this turn (TIME_043e2).</summary>
    internal static class PMMInfinitizer
    {
        internal const int DbfId = 120818;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new AddEnchantmentTask("TIME_043e", EntityType.TARGET),
                new AddEnchantmentTask("TIME_043e2", EntityType.TARGET)) };
        }

        internal static void ConfigureStats(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(8), Effects.SetMaxHealth(8)) };
        }

        // Pinned TIME_043e2 carries TAG_ONE_TURN_EFFECT (338): removed at the end of the turn it was applied.
        internal static void ConfigurePacified(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.CANNOT_ATTACK_HEROES, EffectOperator.SET, 1)) { IsOneTurnEffect = true } };
        }
    }
}
