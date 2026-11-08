using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BarbedThorn
    {
        internal const int DbfId = 114517;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
        internal static void ConfigurePoisonOption(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("EDR_525e1", EntityType.SOURCE) };
        }
        internal static void ConfigureDeathrattleOption(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("EDR_525e", EntityType.SOURCE) };
        }
        internal static void ConfigurePoison(Card card)
        {
            card.Implemented = true;
            card.Tags[GameTag.TAG_ONE_TURN_EFFECT] = 1;
            card.Power = new Power { Enchant = new Enchant(GameTag.POISONOUS, EffectOperator.SET, 1) { IsOneTurnEffect = true } };
        }
        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new DamageTask(2, EntityType.ENEMIES) };
        }
    }
}
