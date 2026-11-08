using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Shapeshifter
    {
        [StandardCard("TIME_876", 119841)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = TurnStart(TriggerActivation.HAND) };
        }
        [StandardCard("TIME_876e", 119842)]
        internal static void ConfigureRecurrence(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.CopyHandTrigger = true; card.ExpiresOnHandExit = true;
            card.Power = new Power { RetainInHeldForm = true,
                Enchant = new Enchant(GameTag.SHIFTING_MINION, EffectOperator.SET, 1),
                Trigger = TurnStart(TriggerActivation.PLAY) };
        }
        private static Trigger TurnStart(TriggerActivation activation) => new Trigger(TriggerType.TURN_START) {
            TriggerActivation = activation,
            Condition = new SelfCondition(e => e is Enchantment || e.AppliedEnchantments?.Any(a => a.Card.Id == "TIME_876e") != true),
            SingleTask = new CustomTask((g, c, s, t, stack) => {
                var actor = s as Enchantment; var held = actor == null ? (IPlayable)s : (IPlayable)actor.Target;
                if (held.Zone != c.HandZone) return;
                var candidates = c.Opponent.HandZone.Where(e => e is Minion).ToArray();
                if (candidates.Length == 0) return;
                var definition = candidates[g.Random.Next(candidates.Length)].Card;
                var result = HandProgressRules.TransformHeld(held, definition, actor);
                if (result == null) return;
                if (definition.Id == "TIME_876") result.ActivatedTrigger?.Remove();
                if (actor == null) HandProgressRules.EnrollHeldForm(result, "TIME_876e");
            }) };
    }
}
