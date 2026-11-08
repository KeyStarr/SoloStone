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
    internal static class Colaque
    {
        [StandardCard("TSC_026", 71712)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareLayout(card, new[] { 1 }, "TSC_026t");
            card.Power = new Power { Aura = new AdaptiveEffect(new SelfCondition(p =>
                p.Controller.BoardZone.Any(m => m.Card.Id == "TSC_026t" && !m.IsDead && !m.IsDormant)), GameTag.IMMUNE) };
        }

        [StandardCard("TSC_026t", 71828)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new ArmorTask(8) };
        }
    }
}
