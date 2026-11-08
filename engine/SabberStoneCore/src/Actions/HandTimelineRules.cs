using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Opt-in hand schedules. State lives in existing payload tags and exported enchantments.</summary>
    public static class HandTimelineRules
    {
        public const int RuniKey = 121676;
        private const GameTag FireBoundary = PayloadRules.Generation;
        public static bool IsBurning(IPlayable card) => card.AppliedEnchantments?.Any(e => e.Card.Id == "CATA_EVENT_001e") == true;
        internal static void CopyFire(Enchantment source, Enchantment target)
        {
            if (source.Card.Id != "CATA_EVENT_001e") return;
            target[FireBoundary] = source[FireBoundary];
            target[GameTag.TAG_SCRIPT_DATA_NUM_2] = source[GameTag.TAG_SCRIPT_DATA_NUM_2];
        }
        internal static void TransferTransform(IPlayable source, IPlayable target)
        {
            foreach (var actor in source.AppliedEnchantments?.Where(e => e.Card.Id == "CATA_EVENT_001e").ToArray() ?? Array.Empty<Enchantment>())
            {
                source.AppliedEnchantments.Remove(actor);
                if (target.AppliedEnchantments == null) target.AppliedEnchantments = new System.Collections.Generic.List<Enchantment>();
                target.AppliedEnchantments.Add(actor);
                actor.RebindCardStateTarget(target);
            }
        }
        internal static bool IsReturning(IPlayable card) => card[PayloadRules.Key] == RuniKey && card[PayloadRules.Stored] == 0;

        public static void SendMinions(IPlayable source)
        {
            var owner = source.Controller;
            var cards = owner.HandZone.Where(c => c is Minion).ToArray();
            if (cards.Length == 0) return;
            var actor = Enchantment.GetInstance(owner, source, owner, Cards.FromId("TIME_EVENT_998e1"));
            TurnQueueRules.Arm(actor, owner, 2);
            using (owner.HandZone.BeginCardStateMutation())
                for (int i = 0; i < cards.Length; i++)
                {
                    var card = cards[i];
                    CardStateBoundary.EnrollAttachments(card);
                    // Leaving hand extinguishes fire, but escrow preserves other held modifiers.
                    foreach (var fire in card.AppliedEnchantments?.Where(e => e.Card.Id == "CATA_EVENT_001e").ToArray() ?? Array.Empty<Enchantment>()) fire.Remove();
                    owner.HandZone.DetachForEscrow(card);
                    PayloadRules.Retain(actor, RuniKey, card);
                    card[PayloadRules.Value] = i;
                    owner.SetasideZone.Add(card);
                }
        }

        internal static void ControllerStart(Controller owner)
        {
            foreach (var actor in owner.AppliedEnchantments?.Where(e => e.Card.Id == "TIME_EVENT_998e1").ToArray() ?? Array.Empty<Enchantment>())
            {
                if (!TurnQueueRules.Tick(actor)) continue;
                if (owner.Game.History) owner.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, actor.Id, "", -1, 0));
                using (owner.HandZone.BeginCardStateMutation())
                    foreach (var card in PayloadRules.Entries(actor, RuniKey).OrderBy(c => c[PayloadRules.Value]))
                    {
                        if (card.Zone != owner.SetasideZone || card.Controller != owner) { PayloadRules.Remove(card); continue; }
                        card[PayloadRules.Owner] = 0;
                        owner.SetasideZone.Remove(card);
                        card[PayloadRules.Stored] = 0;
                        card[GameTag.ZONE] = (int)Zone.HAND;
                        if (Generic.AddHandPhase(owner, card))
                            Generic.AddEnchantmentBlock(owner.Game, Cards.FromId("TIME_EVENT_998e2"), actor, card);
                        card[PayloadRules.Key] = 0; card[PayloadRules.Value] = 0;
                    }
                actor.Remove();
                if (owner.Game.History) owner.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }
        }

        public static void Ignite(IPlayable source, IPlayable card)
        {
            if (card == null || card.Controller != source.Controller || card.Zone != source.Controller.HandZone || IsBurning(card)) return;
            CardStateBoundary.EnrollAttachments(card);
            var actor = Enchantment.GetInstance(card.Controller, source, card, Cards.FromId("CATA_EVENT_001e"));
            actor[FireBoundary] = -1;
            actor[GameTag.TAG_SCRIPT_DATA_NUM_2] = 3;
        }

        internal static void ControllerEnd(Controller owner)
        {
            // Freeze this boundary's actors: discard reactions may create new schedules.
            var actors = owner.HandZone.SelectMany(c => c.AppliedEnchantments ?? Enumerable.Empty<Enchantment>())
                .Where(e => e.Card.Id == "CATA_EVENT_001e").ToArray();
            foreach (var actor in actors)
            {
                if (actor.Target is not IPlayable card || card.Zone != owner.HandZone || card.Controller != owner ||
                    card.AppliedEnchantments?.Contains(actor) != true || actor[FireBoundary] == owner.Game.Turn) continue;
                actor[FireBoundary] = owner.Game.Turn;
                if (--actor[GameTag.TAG_SCRIPT_DATA_NUM_2] > 0) continue;
                if (owner.Game.History) owner.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, actor.Id, "", -1, 0));
                Generic.DiscardBlock(owner, card);
                owner.Game.DeathProcessingAndAuraUpdate();
                if (!owner.BoardZone.IsFull)
                    Generic.SummonBlock(owner.Game, (Minion)Entity.FromCard(owner, Cards.FromId("CATA_EVENT_001")), -1, actor);
                owner.Game.DeathProcessingAndAuraUpdate();
                if (owner.Game.History) owner.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }
        }
    }
}
