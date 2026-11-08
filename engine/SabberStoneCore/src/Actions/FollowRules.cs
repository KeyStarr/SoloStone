using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    public static class FollowRules
    {
        public static bool IsGrant(Enchantment actor) => actor.Card.Id == "CAP_002e"
            || actor.Card.Id == "CAP_101e" || actor.Card.Id == "CAP_402e" || actor.Card.Id == "CAP_802e1";

        internal static void HandExit(IPlayable bearer, bool trade = false)
        {
            var frame = bearer.Game.Resolution.Current;
            bool playing = !trade && frame.Kind == ResolutionKind.PlayCard && frame.SourceId == bearer.Id
                && bearer.Game.CurrentEventData?.EventSource == bearer;
            foreach (var actor in bearer.AppliedEnchantments?.Where(IsGrant).ToArray() ?? Array.Empty<Enchantment>())
                if (!playing) actor.Remove();
        }

        public static void Grant(IPlayable creator, IPlayable recipient, string enchantment)
        {
            if (recipient.Zone != creator.Controller.HandZone) return;
            CardStateBoundary.EnrollAttachments(recipient);
            Generic.AddEnchantmentBlock(creator.Game, Cards.FromId(enchantment), creator, recipient);
        }

        public static void Handoff(IPlayable source, string enchantment, bool pirate = false)
        {
            var c = source.Controller;
            var candidates = c.HandZone.Where(p => p.Cost <= c.RemainingMana
                && (!pirate || p.Card.IsRace(Race.PIRATE))).ToArray();
            if (candidates.Length != 0)
                Grant(source, candidates[c.Game.Random.Next(candidates.Length)], enchantment);
        }

        public static Trigger AfterPlay(ISimpleTask payload) => new Trigger(TriggerType.AFTER_PLAY_CARD) {
            TriggerSource = TriggerSource.ENCHANTMENT_TARGET,
            RemoveAfterTriggered = true,
            SingleTask = payload
        };

        public static void ConfigureGrant(Card card, ISimpleTask payload)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.CopyHandTrigger = true;
            card.Power = new Enchants.Power { Trigger = AfterPlay(payload) };
        }

        public static readonly ISimpleTask Consume = new CustomTask((g, c, s, t, stack) => ((Enchantment)s).Remove());
    }
}
