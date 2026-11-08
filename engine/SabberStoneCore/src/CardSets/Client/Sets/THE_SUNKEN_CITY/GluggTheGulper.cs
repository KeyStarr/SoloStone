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
    internal static class GluggTheGulper
    {
        [StandardCard("TSC_639", 72553)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareLayout(card, new[] { 1, 1, 1 }, "TSC_639t", "TSC_639t2", "TSC_639t3");
            card.Power = new Power { Trigger = new Trigger(TriggerType.DEATH) {
                TriggerSource = TriggerSource.MINIONS_EXCEPT_SELF,
                SingleTask = new CustomTask((g,c,s,t,k) => {
                    if (t != null) Generic.AddEnchantmentBlock(g, Cards.FromId("TSC_639e"), (IPlayable)s, s, t.Card.ATK, t.Card.Health);
                }) } };
        }

        [StandardCard("TSC_639t", 72554)]
        [StandardCard("TSC_639t2", 85860)]
        [StandardCard("TSC_639t3", 85861)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }

        [StandardCard("TSC_639e", 72557)]
        [StandardCard("TSC_639e2", 93063)]
        internal static void Buff(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(ATK.Effect(EffectOperator.ADD, 0), Health.Effect(EffectOperator.ADD, 0)) { UseScriptTag = true } };
        }
    }
}
