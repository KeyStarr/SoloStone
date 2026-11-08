using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ebyssian
    {
        internal const int DbfId = 122871;
        [StandardCard("CATA_553", DbfId)]
        internal static void Configure(Card card)
        {
            ConfigureDragon(card);
            card.Power.Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerActivation = TriggerActivation.HAND,
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.DRAGON)),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (s.Zone == c.HandZone)
                        Generic.ChangeEntityBlock(c, (SabberStoneCore.Model.Entities.IPlayable)s, Cards.FromId("CATA_553t"), true);
                })
            };
        }
        [StandardCard("CATA_553t", 122872)]
        internal static void ConfigureDragon(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CATA_553e", EntityType.CONTROLLER) };
        }
        [StandardCard("CATA_553e", 122873)]
        internal static void ConfigurePlayerEffect(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new Aura(AuraType.BOARD, "CATA_553e2") {
                Condition = new SelfCondition(p => p.Card.IsRace(Race.DRAGON))
            } };
        }
        [StandardCard("CATA_553e2", 127638)]
        internal static void ConfigureRush(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.RUSH, EffectOperator.SET, 1)) };
        }
    }
}
