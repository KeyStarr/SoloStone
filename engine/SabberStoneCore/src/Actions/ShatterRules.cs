using System;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Presentation;

namespace SabberStoneCore.Actions
{
    public sealed class ShatterDefinition
    {
        public string ParentId { get; }
        public string FirstHalfId { get; }
        public string SecondHalfId { get; }
        public ShatterDefinition(string parentId, string firstHalfId, string secondHalfId)
        {
            ParentId = parentId; FirstHalfId = firstHalfId; SecondHalfId = secondHalfId;
        }
        internal void Validate()
        {
            var parent = Cards.FromId(ParentId); var first = Cards.FromId(FirstHalfId); var second = Cards.FromId(SecondHalfId);
            if (parent == null || first == null || second == null || first == second ||
                parent[(GameTag)4239] != 1 || first[(GameTag)4240] != 1 || second[(GameTag)4240] != 1 ||
                parent[GameTag.TAG_SCRIPT_DATA_NUM_1] != first.AssetId || parent[GameTag.TAG_SCRIPT_DATA_NUM_2] != second.AssetId)
                throw new ArgumentException("Invalid exported Shatter family.");
        }
    }

    public static class ShatterRules
    {
        // Private GameTags persist through clones/snapshots; no synthetic native enchantment identity.
        public const GameTag AlreadyCombined = (GameTag)31120;
        public const GameTag PrivateHandOwner = (GameTag)31121;
        internal const GameTag Residence = (GameTag)31122;
        private const GameTag AttemptedResidence = (GameTag)31123;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31120 && (int)tag <= 31124;
        private static bool InHand(IPlayable card) => card != null && card.Zone == card.Controller.HandZone &&
            card.Game.IdEntityDic.TryGetValue(card.Id, out var actual) && ReferenceEquals(card, actual);
        internal static bool Ready(Game game) => game.Step >= Step.MAIN_BEGIN || game.Player1[(GameTag)31124] != 0 || game.Player2[(GameTag)31124] != 0;

        // HandZone.Add holds this parent's ZONE event until its halves are placed (card-state.md section 3.2).
        internal static bool WillSplit(IPlayable card) => card.Card.Shatter?.ParentId == card.Card.Id
            && card[AlreadyCombined] == 0 && Ready(card.Game);

        public static void AfterHandEntry(IPlayable card, int spaceBeforeEntry = -1)
        {
            var definition = card?.Card.Shatter;
            if (definition == null || !InHand(card) || !Ready(card.Game)) return;
            if (card.Controller.HandZone.CardStateDeferred)
            { card.Controller.HandZone.QueueCardStateEntry(card); return; }
            if (card.Card.Id == definition.ParentId && card[AlreadyCombined] == 0 &&
                card[AttemptedResidence] != card[Residence])
            {
                card[AttemptedResidence] = card[Residence];
                // Owner 2026-11-03 (wiki "one space left"): space counted before the parent took its slot, so a
                // later change in hand size cannot admit a half the ruling excluded. Transforms and opening
                // parents have no entry count: their own slot is the space.
                int hand = card.Controller.HandZone.FreeSpace;
                int free = spaceBeforeEntry >= 0 ? spaceBeforeEntry : hand + 1;
                // Unpinned_ShatterObserverFill: a draw observer filled the hand before the split, so only one half fits.
                if (free > 0) Split(card, definition, free > 1 && hand > 0);
            }
        }

        internal static void SettleOpening(Controller controller)
        {
            controller[(GameTag)31124] = 1;
            foreach (var card in controller.HandZone.ToArray()) AfterHandEntry(card);
            StabilizeHand(controller);
        }

        private static void Split(IPlayable parent, ShatterDefinition definition, bool both)
        {
            var c = parent.Controller; var hand = c.HandZone;
            var applications = new Applications(parent);
            using (hand.BeginCardStateMutation())
            {
                IPlayable first = null, second = null;
                int[] held;
                hand.HoldZoneEvents();
                ShatterHistory.Begin(c);
                try
                {
                    // Stamp hand-bound halves before allocation so even immediately retired cards remain private.
                    first = NewHandCard(c, definition.FirstHalfId, parent);
                    // Overflow uses the same DECK reveal/graveyard path as an ordinary overdraw.
                    second = both ? NewHandCard(c, definition.SecondHalfId, parent) :
                        Entity.FromCard(c, Cards.FromId(definition.SecondHalfId), new EntityData {
                            [GameTag.ZONE] = (int)Zone.DECK, [GameTag.DISPLAYED_CREATOR] = parent.Id });
                    applications.Apply(first);
                    applications.Apply(second);
                    ShatterHistory.Pulse(c, ShatterHistory.Separate, parent);
                    ShatterHistory.Pulse(c, ShatterHistory.SeparatedSpawn, both ? new[] { first, second } : new[] { first });
                    if (!both)
                    {
                        // One space: the left half takes the parent's slot and the right half burns as an overdraw.
                        Retire(parent);
                        hand.Add(first, 0);
                        c.Game.Facts.CardBurned(second);
                        c.GraveyardZone.Add(second);
                        if (c.Game.History) c.Game.PowerHistory.BurnedCard(second.Id);
                    }
                    else
                    {
                        Retire(parent);
                        hand.Add(first, 0);
                        // Opposite hand edges keep a leftmost opening parent from immediately recombining.
                        hand.Add(second);
                    }
                }
                finally { ShatterHistory.End(c); held = hand.TakeHeldZoneEvents(); }
                // Reactions see the settled layout; what they generate overflows by the ordinary rules.
                foreach (int id in held)
                    if (c.Game.IdEntityDic.TryGetValue(id, out var entered)) c.Game.TriggerManager.OnZoneTrigger(entered);
                var previous = c.Game.CurrentEventData;
                c.Game.CurrentEventData = new EventMetaData(parent, null);
                c.Game.TaskQueue.StartEvent();
                try
                {
                    c.Game.TriggerManager.Events.Raise(new ShatterEvent(c.Id, parent.Id, first.Id, second.Id), parent);
                    c.Game.ProcessTasks();
                }
                finally { c.Game.TaskQueue.EndEvent(); c.Game.CurrentEventData = previous; }
            }
        }

        public static void StabilizeHand(Controller controller)
        {
            var hand = controller.HandZone;
            if (!Ready(controller.Game) || hand.CardStateDeferred) return;
            for (int i = 0; i + 1 < hand.Count;)
            {
                var left = hand[i]; var right = hand[i + 1]; var family = left.Card.Shatter;
                if (family == null || right.Card.Shatter?.ParentId != family.ParentId ||
                    !((left.Card.Id == family.FirstHalfId && right.Card.Id == family.SecondHalfId) ||
                      (left.Card.Id == family.SecondHalfId && right.Card.Id == family.FirstHalfId)))
                { i++; continue; }
                var a = new Applications(left); var b = new Applications(right);
                using (hand.BeginCardStateMutation())
                {
                    // Native local-play acknowledgment ignores its PLAY zone list, so replacements need a child list.
                    ShatterHistory.Begin(controller);
                    try
                    {
                        var combined = NewHandCard(controller, family.ParentId, left, combined: true);
                        a.Apply(combined); b.Apply(combined);
                        ShatterHistory.Pulse(controller, ShatterHistory.Combine, left, right);
                        Retire(left); Retire(right);
                        ShatterHistory.Pulse(controller, ShatterHistory.CombinedSpawn, combined);
                        hand.Add(combined, i);
                    }
                    finally { ShatterHistory.End(controller); }
                }
                i = Math.Max(0, i - 1);
            }
        }

        public static IPlayable GiveCombined(Controller controller, string parentId, IEntity source)
        {
            var card = Cards.FromId(parentId);
            if (controller == null || card?.Shatter?.ParentId != parentId || source == null || source.Game != controller.Game) return null;
            ShatterHistory.Begin(controller);
            try
            {
                var result = NewHandCard(controller, parentId, source, combined: true);
                if (!controller.HandZone.IsFull) ShatterHistory.Pulse(controller, ShatterHistory.CombinedSpawn, result);
                Generic.AddHandPhase(controller, result);
                return result;
            }
            finally { ShatterHistory.End(controller); }
        }

        private static IPlayable NewHandCard(Controller c, string id, IEntity source, bool combined = false)
        {
            // SETASIDE staging is presentation only: History-off zones never rewrite ZONE on entry, so keep HAND there.
            var tags = new EntityData { [GameTag.ZONE] = (int)(c.Game.History ? Zone.SETASIDE : Zone.HAND), [PrivateHandOwner] = c.PlayerId,
                [GameTag.DISPLAYED_CREATOR] = source.Id };
            if (combined) { tags[AlreadyCombined] = 1; tags[(GameTag)4239] = 0; }
            return Entity.FromCard(c, Cards.FromId(id), tags);
        }

        private static void Retire(IPlayable card)
        {
            var c = card.Controller;
            foreach (var enchant in card.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>()) enchant.Remove();
            c.SetasideZone.Add(c.HandZone.Remove(card));
            card[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
            if (c.Game.History) c.Game.PowerHistory.Add(new PowerHistoryHideEntity { EntityID = card.Id, Zone = Zone.REMOVEDFROMGAME });
        }

        internal static void CopyCosts(IPlayable source, IPlayable copy)
        {
            var original = (Playable)source; var target = (Playable)copy;
            var operations = original._costManager?.PersistentOperations() ?? Array.Empty<(EffectOperator Operator, int Value)>();
            if (operations.Length == 0) { target._costManager?.QueueUpdate(); return; }
            target._costManager = new Playable.CostManager();
            target._modifiedCost = target.Card.Cost;
            foreach (var op in operations) Cost.Effect(op.Operator, op.Value).ApplyTo(target);
        }

        internal static void CopyState(IPlayable source, IPlayable copy)
        {
            if (source.Card.Shatter == null || source[AlreadyCombined] == 0) return;
            copy[AlreadyCombined] = 1; copy[(GameTag)4239] = 0;
        }

        internal static void ChangingDefinition(IPlayable card, Card target)
        {
            if (card.Card.Id == target.Id) return;
            if (card[AlreadyCombined] != 0) { card[AlreadyCombined] = 0; card[(GameTag)4239] = target[(GameTag)4239]; }
            if (card.Card.Shatter != null)
            { card[AttemptedResidence] = 0; card[Residence] = 0; }
        }

        // Invocation-local snapshots survive Remove's cost reset; each replay owns fresh handles/attachments.
        private sealed class Applications
        {
            private readonly (EffectOperator Operator, int Value)[] operations;
            private readonly Model.Costs.EntityCostState overlay;
            private readonly IPlayable temporarySource;
            private readonly (int Id, Card Card, IPlayable Creator, int One, int Two, bool Expires, int BonusState)[] enchantments;
            internal Applications(IPlayable source)
            {
                var p = (Playable)source;
                operations = p._costManager?.PersistentOperations() ?? Array.Empty<(EffectOperator, int)>();
                if (operations.Length == 0 && p._modifiedCost.HasValue && p._modifiedCost != p.Card.Cost)
                    operations = new[] { (EffectOperator.ADD, p._modifiedCost.Value - p.Card.Cost) };
                overlay = p._costOverlay?.Clone();
                temporarySource = TemporaryRules.IsTemporary(source) ? source : null;
                enchantments = (source.AppliedEnchantments ?? new System.Collections.Generic.List<Enchantment>())
                    .Where(e => e.Card.Id != "GBL_999e" && !(e.Creator?.OngoingEffect is HandAdjacencyCostAura aura && aura.OwnsPresentation(e)))
                    .Select(e => (e.Id, e.Card, e.Creator, e.ScriptTag1, e.ScriptTag2, e.IsOneTurnActive, e[BonusEffects.State])).ToArray();
            }
            internal void Apply(IPlayable target)
            {
                var p = (Playable)target;
                if (temporarySource != null) TemporaryRules.CopyState(temporarySource, target, preserveExpiry: true);
                var ids = new System.Collections.Generic.Dictionary<int, int>();
                foreach (var e in enchantments)
                {
                    var copy = Enchantment.GetInstance(target.Controller, e.Creator, target, e.Card, e.One, e.Two);
                    if (e.BonusState != 0) copy[BonusEffects.State] = e.BonusState;
                    ids[e.Id] = copy.Id;
                    var enchant = e.Card.Power?.Enchant;
                    if (enchant != null)
                        for (int i = 0; i < enchant.Effects.Length; i++)
                        {
                            var effect = enchant.Effects[i];
                            if (effect.Tag == GameTag.COST) continue;
                            if (enchant.UseScriptTag && i < 2) effect = effect.ChangeValue(i == 1 && e.Two > 0 ? e.Two : e.One);
                            effect.ApplyTo(target, e.Expires);
                        }
                    if (e.Expires) copy.ExpireAtEndOfTurn();
                }
                foreach (var op in operations) Cost.Effect(op.Operator, op.Value).ApplyTo(p);
                if (overlay == null) return;
                foreach (var m in overlay.Modifiers)
                    CostRules.AddModifier(p, m.Operator, m.Value, m.ExpiryTurn, ids.TryGetValue(m.EnchantmentId, out int id) ? id : 0);
                // The last left-to-right application wins; never combine Health and Armor resources.
                foreach (var m in overlay.Payments)
                {
                    if (p._costOverlay != null) p._costOverlay.Payments.Clear();
                    CostRules.AddPaymentOverride(p, m.Kind, m.ExpiryTurn, ids.TryGetValue(m.EnchantmentId, out int id) ? id : 0);
                }
            }
        }
    }
}

namespace SabberStoneCore.Model.Events
{
    public readonly record struct ShatterEvent : IGameEvent
    {
        public int ControllerId { get; }
        public int ParentEntityId { get; }
        public int FirstHalfId { get; }
        public int SecondHalfId { get; }
        public ShatterEvent(int controllerId, int parentEntityId, int firstHalfId, int secondHalfId)
        {
            ControllerId = controllerId; ParentEntityId = parentEntityId; FirstHalfId = firstHalfId; SecondHalfId = secondHalfId;
        }
        public IEntity Sender => null;
        public string Channel => "ShatterTrigger";
    }
}
