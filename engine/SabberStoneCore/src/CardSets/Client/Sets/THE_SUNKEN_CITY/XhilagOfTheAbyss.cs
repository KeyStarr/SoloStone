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
    internal static class XhilagOfTheAbyss
    {
        [StandardCard("TSC_219", 73726)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareLayout(card, new[] { 0, 0, 1, 2 }, "TSC_219t", "TSC_219t2", "TSC_219t3", "TSC_219t4");
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g,c,s,t,k) => {
                    foreach (var stalk in c.BoardZone.Where(IsStalk).Where(m => !m.IsDead && !m.IsDormant).ToArray())
                        Generic.AddEnchantmentBlock(g, Cards.FromId("TSC_219e"), (IPlayable)s, stalk);
                }) } };
        }

        [StandardCard("TSC_219t", 73727)]
        [StandardCard("TSC_219t2", 85853)]
        [StandardCard("TSC_219t3", 85854)]
        [StandardCard("TSC_219t4", 85855)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g,c,s,t,k) => {
                    var enemies = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).Cast<ICharacter>().Append(c.Opponent.Hero).ToArray();
                    enemies[g.Random.Next(enemies.Length)].TakeDamage((IPlayable)s, s[GameTag.TAG_SCRIPT_DATA_NUM_1]);
                }) } };
        }

        [StandardCard("TSC_219e", 75066)]
        internal static void Growth(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.TAG_SCRIPT_DATA_NUM_1, EffectOperator.ADD, 1)) };
        }
        internal static bool IsStalk(Minion m) => m.Card.Id == "TSC_219t" || m.Card.Id == "TSC_219t2" || m.Card.Id == "TSC_219t3" || m.Card.Id == "TSC_219t4";
    }
}
