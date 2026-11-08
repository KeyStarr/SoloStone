using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HatchingCeremony
    {
        [StandardCard("DINO_405", 118413)]
        internal static void Configure(Card card) => ObjectiveRules.Declare(card, 2,
            new AddEnchantmentTask("DINO_405e", EntityType.MINIONS), ownerEnd: true);
        [StandardCard("DINO_405e", 118415)]
        internal static void Buff(Card card) {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
