using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowOfDemise
    {
        [StandardCard("CORE_RLK_567", 126088)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { HandEntryTask = new CustomTask((g, c, s, t, stack) =>
                HandProgressRules.EnrollHeldForm((IPlayable)s, "RLK_567e2")) };
        }

        [StandardCard("RLK_567e2", 86233)]
        internal static void ConfigureRecurrence(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.CopyHandTrigger = true; card.ExpiresOnHandExit = true;
            card.Power = new Power { RetainInHeldForm = true,
                Enchant = new Enchant(GameTag.SHIFTING_SPELL, EffectOperator.SET, 1),
                Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var actor = (Enchantment)s; var held = (IPlayable)actor.Target;
                    if (held.Zone == c.HandZone && t is Spell && held.Id != t.Id)
                    {
                        var transformed = HandProgressRules.TransformHeld(held, t.Card, actor);
                        if (transformed != null) ObjectiveRules.CopyComposition(t, transformed);
                    }
                }) } };
        }
    }
}
