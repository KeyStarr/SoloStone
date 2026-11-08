using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
    public enum HandProgressMode { OwnerSpells, OwnerTurnEnds }
}

namespace SabberStoneCore.Actions
{
    public sealed class HandProgressDefinition
    {
        public string TargetId { get; }
        public int Initial { get; }
        public HandProgressMode Mode { get; }
        public HandProgressDefinition(string targetId, int initial, HandProgressMode mode)
        {
            if (initial <= 0 || string.IsNullOrEmpty(targetId) || !Enum.IsDefined(typeof(HandProgressMode), mode))
                throw new ArgumentException("Invalid hand progress definition.");
            TargetId = targetId; Initial = initial; Mode = mode;
        }
    }

    public static class HandProgressRules
    {
        internal const GameTag Residence = (GameTag)31140;
        public const GameTag PrivateAttachmentTarget = (GameTag)31141;
        private const GameTag LastEnd = (GameTag)31142;
        internal const GameTag HeldForm = (GameTag)31143;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31140 && (int)tag <= 31143;
        private static bool InHand(IPlayable card) => card != null && card.Zone == card.Controller.HandZone &&
            card.Game.IdEntityDic.TryGetValue(card.Id, out var actual) && ReferenceEquals(actual, card);

        public static void OnHandEntry(IPlayable card, bool freshResidence)
        {
            var definition = card?.Card.HandProgress;
            if (definition == null || !InHand(card) || !freshResidence) return;
            if (card.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] != definition.Initial)
                throw new InvalidOperationException("Hand progress must start at the exported counter.");
            card[Residence] = checked(card[Residence] + 1);
            card[PrivateAttachmentTarget] = card.Id;
            card[LastEnd] = 0;
            card[GameTag.TAG_SCRIPT_DATA_NUM_1] = definition.Initial;
        }

        public static (int EntityId, int ResidenceGeneration)[] CaptureCast(Spell spell)
        {
            if (spell == null || !spell.Game.IdEntityDic.TryGetValue(spell.Id, out var actual) || !ReferenceEquals(actual, spell))
                return Array.Empty<(int, int)>();
            return spell.Controller.HandZone.Where(c => c.Id != spell.Id && c.Card.HandProgress?.Mode == HandProgressMode.OwnerSpells)
                .Select(c => (c.Id, c[Residence])).ToArray();
        }

        public static void CompleteCast(Controller controller, (int EntityId, int ResidenceGeneration)[] candidates,
            HistoryCastKind kind, HistoryCastOutcome outcome)
        {
            if (controller == null || candidates == null) return;
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!controller.Game.IdEntityDic.TryGetValue(candidate.EntityId, out var card) ||
                    card.Controller != controller || !InHand(card) || card[Residence] != candidate.ResidenceGeneration ||
                    card.Card.HandProgress?.Mode != HandProgressMode.OwnerSpells) continue;
                // Each resolution owns its cohort; consume before callbacks without overwriting an outer cast.
                candidates[i] = default;
                if (kind == HistoryCastKind.Player && outcome == HistoryCastOutcome.Completed) Advance(card);
            }
        }

        public static void CaptureControllerEnd(Controller controller)
        {
            if (controller == null || controller.Game.CurrentPlayer != controller) return;
            var held = controller.HandZone.Where(c => c.Card.HandProgress?.Mode == HandProgressMode.OwnerTurnEnds)
                .Select(c => (c.Id, Generation: c[Residence])).ToArray();
            foreach (var entry in held)
                if (controller.Game.IdEntityDic.TryGetValue(entry.Id, out var card) && card.Controller == controller &&
                    InHand(card) && card[Residence] == entry.Generation && card.Card.HandProgress?.Mode == HandProgressMode.OwnerTurnEnds &&
                    card[LastEnd] != controller.Game.Turn + 1)
                {
                    card[LastEnd] = controller.Game.Turn + 1;
                    Advance(card);
                }
        }

        private static void Advance(IPlayable card)
        {
            int remaining = card[GameTag.TAG_SCRIPT_DATA_NUM_1];
            if (remaining <= 0) return;
            card[GameTag.TAG_SCRIPT_DATA_NUM_1] = remaining - 1;
            if (remaining == 1) Upgrade(card);
        }

        public static IPlayable Upgrade(IPlayable card)
        {
            var definition = card?.Card.HandProgress;
            if (definition == null || !InHand(card) || card[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0) return card;
            var old = card.Card; var target = Cards.FromId(definition.TargetId); var c = card.Controller;
            // Enrollment belongs to the entity, including attachments granted by its new definition.
            card[PrivateAttachmentTarget] = card.Id;
            // Detach hand auras before resetting local costs; their current conditions reapply to the target.
            c.HandZone.Auras.ForEach(a => a.DeApply(card));
            var attachments = card.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>();
            int generation = attachments.Any(e => e.Power?.RetainInHeldForm == true) ? checked(card[Residence] + 1) : 0;
            ((Playable)card).ResetCost();
            foreach (var tag in old.Tags.Keys.Except(target.Tags.Keys).ToArray())
                if (tag != GameTag.COST && card.NativeTags.ContainsKey(tag)) card[tag] = 0;
            // Cross-type Temporary transfer removes its helper from the old attachment list.
            var retained = card.AppliedEnchantments?.ToList();
            var result = Generic.ChangeEntityBlock(c, card, target, false);
            // Cross-type ChangeEntity constructs a new CLR object with the same registry identity.
            result.AppliedEnchantments = retained;
            foreach (var e in attachments.Where(e => result.AppliedEnchantments?.Contains(e) == true)) e.RebindCardStateTarget(result);
            result[Residence] = generation; result[LastEnd] = 0;
            if (generation != 0) result[HeldForm] = 1;
            result[GameTag.TAG_SCRIPT_DATA_NUM_1] = target[GameTag.TAG_SCRIPT_DATA_NUM_1];
            if (target.Type == CardType.MINION && result is Minion minion)
            {
                result.NativeTags.Remove(GameTag.ATK); result.NativeTags.Remove(GameTag.HEALTH);
                minion.AttackDamage = target.ATK; minion.BaseHealth = target.Health;
                foreach (var attachment in attachments)
                {
                    var enchant = attachment.Card.Power?.Enchant;
                    if (enchant == null) continue;
                    for (int i = 0; i < enchant.Effects.Length; i++)
                    {
                        var effect = enchant.Effects[i];
                        if (effect.Tag != GameTag.ATK && effect.Tag != GameTag.HEALTH) continue;
                        if (enchant.UseScriptTag && i < 2)
                            effect = effect.ChangeValue(i == 1 && attachment.ScriptTag2 >= 0 ? attachment.ScriptTag2 : attachment.ScriptTag1);
                        // Expiry already follows this entity id; replaying must not register it twice.
                        effect.ApplyTo(result);
                    }
                }
            }
            // Explicit old-only zeros belong to this opt-in transform, not unrelated legacy changes.
            if (c.Game.History)
            {
                var change = c.Game.PowerHistory.Last.OfType<PowerHistoryChangeEntity>().Last(e => e.Entity.Id == result.Id);
                foreach (var tag in old.Tags.Keys.Union(target.Tags.Keys)) change.Entity.Tags[tag] = target[tag];
                change.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = result[GameTag.TAG_SCRIPT_DATA_NUM_1];
                change.Entity.Tags[GameTag.COST] = result.Cost;
                if (result is Minion current)
                { change.Entity.Tags[GameTag.ATK] = current.AttackDamage; change.Entity.Tags[GameTag.HEALTH] = current.BaseHealth; }
                change.Entity.Tags[GameTag.CARD_ALTERNATE_COST] = (int)CostRules.PaymentKind(result);
            }
            CostRules.RefreshHandAuras(c.Game);
            return result;
        }

        internal static void CopyState(IPlayable source, IPlayable copy)
        {
            if (copy.Card.HandProgress == null) return;
            copy[Residence] = 0; copy[LastEnd] = 0;
            copy[GameTag.TAG_SCRIPT_DATA_NUM_1] = copy.Card.HandProgress.Initial;
        }

        public static void EnrollHeldForm(IPlayable card, string enchantmentId)
        {
            if (!InHand(card)) return;
            card[HeldForm] = 1;
            card[PrivateAttachmentTarget] = card.Id;
            if (card.AppliedEnchantments?.Any(e => e.Card.Id == enchantmentId) == true) return;
            Generic.AddEnchantmentBlock(card.Game, Cards.FromId(enchantmentId), card, card);
        }

        public static IPlayable TransformHeld(IPlayable card, Card definition, Enchantment recurrence = null)
        {
            if (!InHand(card) || definition == null || recurrence != null &&
                (recurrence.Target != card || card.AppliedEnchantments?.Contains(recurrence) != true)) return null;
            using var entry = card.Controller.HandZone.DeferCardStateEntry();
            int generation = checked(card[Residence] + 1);
            card[HeldForm] = 1;
            card[PrivateAttachmentTarget] = card.Id;
            // A copied form can contribute another recurring ability; retain each subscribed identity.
            var retained = card.AppliedEnchantments?.Where(e => e.Power?.RetainInHeldForm == true).ToArray() ?? Array.Empty<Enchantment>();
            foreach (var actor in retained) card.AppliedEnchantments.Remove(actor);
            var result = Generic.ChangeEntity(card.Controller, card, definition, true, resetHeldState: true);
            foreach (var actor in retained)
            {
                if (result.AppliedEnchantments == null) result.AppliedEnchantments = new System.Collections.Generic.List<Enchantment>();
                result.AppliedEnchantments.Add(actor);
                actor.RebindCardStateTarget(result);
                // ChangeEntity reset the old modifiers; retained abilities still own their hand-state visuals.
                actor.Power?.Enchant?.ActivateTo(result, actor.ScriptTag1, actor.ScriptTag2);
            }
            result[GameTag.TAG_SCRIPT_DATA_NUM_1] = definition[GameTag.TAG_SCRIPT_DATA_NUM_1];
            result[GameTag.TAG_SCRIPT_DATA_NUM_2] = definition[GameTag.TAG_SCRIPT_DATA_NUM_2];
            result[Residence] = generation;
            CardStateBoundary.EnrollAttachments(result);
            result.Power?.HandEntryTask?.Process(result.Game, result.Controller, result, null);
            CostRules.RefreshHandAuras(card.Game);
            return result;
        }
    }
}
