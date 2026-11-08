using System;
using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class ClassHeroes
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(78065, "HERO_11", card => {
                card.Implemented = true;
                card.ExcludeFromLegacyRandomPools = true;
            });
            register(78162, "HERO_11bp", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                // Default summon appends using BoardZone's mixed minion/location slot count.
                card.Power = new Power { PowerTask = new SummonTask("HERO_11bpt") };
                card.Implemented = true;
                card.ExcludeFromLegacyRandomPools = true;
            });
            register(56550, "HERO_10", card => {
                card.Implemented = true;
                // Explicit hero selection must not expand legacy basic-power Discover.
                card.ExcludeFromLegacyRandomPools = true;
            });
            register(60224, "HERO_10bp", card => {
                card.Power = new Power { PowerTask = new AddEnchantmentTask("HERO_10bpe", EntityType.HERO) };
                card.Implemented = true;
            });
            register(56723, "HERO_10bpe", card => {
                card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) { IsOneTurnEffect = true } };
                card.Implemented = true;
            });
        }
    }
}
