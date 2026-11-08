using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Chronikar
    {
        [StandardCard("END_006",121063)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                Generic.AddEnchantmentBlock(g,Cards.FromId("END_006e"),(IPlayable)s,c,2);
                Effects.Attack_N(3).ApplyTo(c.Hero,true);
            }) };
        }
        [StandardCard("END_006e",123974)]
        internal static void Timer(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            // Unpinned_ChronikarOwnTurns: each next own start grants a separate one-turn buff.
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                Condition = SelfCondition.IsMyTurn,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    Effects.Attack_N(3).ApplyTo(c.Hero,true);
                    if (--s[GameTag.TAG_SCRIPT_DATA_NUM_1] <= 0) ((Enchantment)s).Remove();
                })
            } };
        }
    }
}
