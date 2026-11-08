using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MirrexTheCrystalline
    {
        [StandardCard("DINO_407", 118481)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { HandEntryTask = new CustomTask((g, c, s, t, stack) =>
                HandProgressRules.EnrollHeldForm((IPlayable)s, "DINO_407e")) };
        }
        [StandardCard("DINO_407e", 118482)]
        internal static void ConfigureRecurrence(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.CopyHandTrigger = true; card.ExpiresOnHandExit = true;
            card.Power = new Power { RetainInHeldForm = true,
                Enchant = new Enchant(GameTag.SHIFTING_MINION, EffectOperator.SET, 1),
                Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.ENEMY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var actor = (Enchantment)s; var held = (IPlayable)actor.Target;
                    if (held.Zone != c.HandZone || !(t is Minion)) return;
                    var result = HandProgressRules.TransformHeld(held, t.Card, actor);
                    if (result == null) return;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("DINO_407e2"), result, result);
                }) } };
        }
        [StandardCard("DINO_407e2", 121270)]
        internal static void ConfigureStats(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(3), Effects.SetMaxHealth(4), Effects.SetCost(3)) };
        }
    }
}
