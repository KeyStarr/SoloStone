using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TrastathSoulParasite
    {
        internal const int DbfId = 126222;
        [StandardCard("JAIL_721", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.DEMON)),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (t is Minion demon)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_721e"), (IPlayable)s,
                            (IPlayable)s, demon.AttackDamage, demon.Health);
                })
            } };
        }

        [StandardCard("JAIL_721e", 131257)]
        internal static void ConfigureStats(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(0)) { UseScriptTag = true } };
        }
    }
}
