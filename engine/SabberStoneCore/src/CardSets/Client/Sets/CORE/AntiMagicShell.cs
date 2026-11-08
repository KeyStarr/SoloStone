using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AntiMagicShell
    {
        [StandardCard("RLK_048", 78342)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("RLK_048e", EntityType.MINIONS) };
        }
        [StandardCard("RLK_048e", 78557)]
        internal static void ConfigureShell(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(1),
                CantBeTargetedBySpells.Effect(),
                new Effect(GameTag.CANT_BE_TARGETED_BY_HERO_POWERS, EffectOperator.SET, 1)) };
        }
    }
}
