using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StoryOfSulfuras
    {
        [StandardCard("TLC_632", 117874)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => BasicHeroPowerRules.SwapForTwoUses(c, s)) };
        }
        [StandardCard("TLC_632t2", 119640)]
        internal static void ConfigureLastPower(Card card) => ConfigurePower(card);
        [StandardCard("TLC_632t", 119639)]
        internal static void ConfigurePower(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 8) };
        }
    }
}
