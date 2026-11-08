using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Crabatoa
    {
        [StandardCard("TSC_937", 72536)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "TSC_937t", "TSC_937t3");
            card.Power = new Power { Aura = new MultiAura(
                new Aura(AuraType.BOARD, "TSC_937e") { Condition = new SelfCondition(m => m.Card.Id == "TSC_937t" || m.Card.Id == "TSC_937t3") },
                new Aura(AuraType.WEAPON, "TSC_937e") { Restless = true, Condition = new SelfCondition(w => w.Card.Id == "TSC_937t2") }) };
        }

        [StandardCard("TSC_937t", 72537)]
        [StandardCard("TSC_937t3", 85859)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new WeaponTask("TSC_937t2") };
        }

        [StandardCard("TSC_937t2", 72580)]
        internal static void Weapon(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }

        [StandardCard("TSC_937e", 72538)]
        internal static void Buff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(ATK.Effect(EffectOperator.ADD, 2)) };
        }
    }
}
