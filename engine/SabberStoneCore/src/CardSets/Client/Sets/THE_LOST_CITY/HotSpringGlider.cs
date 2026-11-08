using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HotSpringGlider
    {
        internal const int DbfId = 117382;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) =>
                Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_428e"), (IPlayable)s, c,
                    KindredRules.BeginMatchedClause((IPlayable)s) > 0 ? 1 : 0)) };
        }
        internal static void ConfigureGrant(Card card)
        {
            card.Implemented = true;
            // Cower in Fear's persistent next-race aura; normal cost quotation/payment owns consumption timing.
            card.Power = new Power {
                Aura = new Aura(AuraType.HAND, Effects.ReduceCost(1)) {
                    Condition = new SelfCondition(p => p is Minion && p.Card.IsRace(Race.MURLOC))
                },
                Trigger = new Trigger(TriggerType.PLAY_MINION) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    Condition = new SelfCondition(p => p.Card.IsRace(Race.MURLOC)),
                    SingleTask = new CustomTask((g, c, s, t, stack) => {
                        if (((Enchantment)s).ScriptTag1 == 1)
                            new SetGameTagTask(GameTag.DIVINE_SHIELD, 1, EntityType.TARGET).Process(g, c, s, t, stack);
                        ((Enchantment)s).Remove();
                    })
                }
            };
        }
    }
}
