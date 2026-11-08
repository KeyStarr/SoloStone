using System;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static partial class CostRules
    {
        private const GameTag LockedMana = (GameTag)31261,
            DisplayMana = (GameTag)31262, ScalarAuraCount = (GameTag)31263,
            ScalarAuraBase = (GameTag)31264, ScalarAuraReduction = (GameTag)31265;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 31261 && (int)tag <= 31267;
        private const GameTag DreamQualified = (GameTag)31266, DreamCastQualified = (GameTag)31267;
        internal static int ScalarReduction(IPlayable source) => source[ScalarAuraReduction];
        internal static int? CopyableScalar(Playable source) => source.NativeTags.TryGetValue(ScalarAuraBase, out int value) ? value : source._modifiedCost;
        public static bool QualifiedHeldDreams(IPlayable source) => source[DreamQualified] != 0;
        public static bool QualifiedDreams(IPlayable source) => source[DreamCastQualified] != 0;
        internal static void ClearHeldQualification(IPlayable source) => source.NativeTags.Remove(DreamQualified);
        internal static void ClearAutomaticQualification(IPlayable source) => source.NativeTags.Remove(DreamCastQualified);
        internal static void CaptureHeldQualifications(Controller payer, IPlayable source, int paidCost)
        {
            if (source.Card.Id == "EDR_846") source[DreamCastQualified] = source[DreamQualified];
            foreach (var held in payer.HandZone.Where(c => c.Id != source.Id && c.Card.Id == "EDR_846").ToArray())
                if (paidCost > held.Cost) held[DreamQualified] = 1;
        }
        internal static int HandCountForCost(IPlayable source) => source.Controller.HandZone.Count + (source.Id == 0 ? 1 : 0);
        public static void GrantDeathwingDiscount(IPlayable source, int amount)
        {
            var owner = source.Controller;
            var before = Capture(owner.HandZone.Concat(owner.DeckZone).OfType<Playable>());
            Enchantment.GetInstance(owner, source, owner, Cards.FromId("CATA_497e"), amount);
            Publish(before);
        }

        internal static int DeathwingDiscount(IPlayable source)
        {
            switch (source.Card.Id)
            {
                case "NEW1_030": case "CORE_NEW1_030": case "OG_317": case "CORE_OG_317":
                case "DRG_026": case "CORE_DRG_026": case "CS3_036": case "CORE_CS3_036": case "CATA_190h":
                    return source.Controller.AppliedEnchantments?.Where(e => e.Card.Id == "CATA_497e")
                        .Sum(e => e[GameTag.TAG_SCRIPT_DATA_NUM_1]) ?? 0;
                default: return 0;
            }
        }

        public static int LastPlayedCost(Controller controller) => controller.Game.HistoryFacts.Query.Plays()
            .LastOrDefault(f => f.ControllerId == controller.Id)?.PaidCost ?? 0;
        // DeckCards is the retained original definition list, independent of current deck entities.
        public static int StartingMinionCost(Controller controller) => controller.DeckCards
            .Where(c => c.Type == CardType.MINION).Sum(c => c.Cost);

        public static int DeathCost(IPlayable source) => source.Zone == source.Controller.BoardZone
            ? source.Cost : source.Game.HistoryFacts.Query.Deaths().LastOrDefault(f => f.EntityId == source.Id)?.CostAtDeath ?? source.Cost;

        internal static bool HasManaNumbers(IPlayable source) => source.Card[(GameTag)4958] != 0;
        internal static void ResetManaNumbersForTransform(IPlayable source, Card definition)
        {
            if (!HasManaNumbers(source) && source[DisplayMana] == 0 && source[LockedMana] == 0) return;
            // Transform resets the stats that DisplayMana measured, even for the same definition.
            source.NativeTags.Remove(DisplayMana);
            source.NativeTags.Remove(LockedMana);
            source[GameTag.TAG_SCRIPT_DATA_NUM_1] = definition[GameTag.TAG_SCRIPT_DATA_NUM_1];
            source.NativeTags.Remove(GameTag.TAG_SCRIPT_DATA_NUM_1);
        }
        internal static void EnterHand(IPlayable source)
        {
            if (!HasManaNumbers(source)) return;
            if (source[LockedMana] > 0) { source[DisplayMana] = 0; source[LockedMana] = 0; }
            RefreshManaNumbers(source);
        }
        public static int ManaNumbers(IPlayable source) => source[LockedMana] > 0
            ? source[LockedMana] - 1 : Math.Max(1, source.Controller.RemainingMana);
        internal static int PersistentInput(Playable source)
        {
            if (InfinityRules.HasCost(source)) return InfinityRules.EvaluateCost(source);
            if (!HasManaNumbers(source))
                return source._costManager?.TransformBaseCost ?? source._modifiedCost ?? source.Card.Cost;
            int value = ManaNumbers(source);
            foreach (var op in source._costManager?.PersistentOperations() ?? Array.Empty<(SabberStoneCore.Enchants.EffectOperator Operator, int Value)>())
                value = Model.Costs.EntityCostState.ApplyOperation(op.Operator, value, op.Value);
            return value;
        }
        internal static void RefreshManaNumbers(IPlayable source)
        {
            if (!HasManaNumbers(source) || source.Zone != source.Controller.HandZone) return;
            using var handBuff = source.Game.History ? source.Game.PowerHistory.HandBuff(source) : null;
            int number = ManaNumbers(source);
            int previous = source[DisplayMana] > 0 ? source[DisplayMana] - 1 : source.Card.Cost;
            if (source is Minion minion && number != previous)
            {
                minion.AttackDamage += number - previous;
                minion.BaseHealth += number - previous;
            }
            source[DisplayMana] = number + 1;
            source[GameTag.TAG_SCRIPT_DATA_NUM_1] = number;
            if (source.Game.History)
                source.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(source.Id, GameTag.COST, source.Cost));
        }
        internal static void FreezeManaNumbers(IPlayable source)
        {
            if (!HasManaNumbers(source)) return;
            RefreshManaNumbers(source);
            source[LockedMana] = ManaNumbers(source) + 1;
        }
        internal static void CopyMemory(IPlayable source, IPlayable copy)
        {
            if (source.Card.Id == "EDR_846") copy[DreamQualified] = source[DreamQualified];
            if (!HasManaNumbers(source)) return;
            foreach (var tag in new[] { LockedMana, DisplayMana, GameTag.TAG_SCRIPT_DATA_NUM_1 }) copy[tag] = source[tag];
            foreach (var operation in ((Playable)source)._costManager?.PersistentOperations()
                ?? Array.Empty<(SabberStoneCore.Enchants.EffectOperator Operator, int Value)>())
                Manager((Playable)copy).AddCostEnchantment(operation.Operator, operation.Value);
        }
        // Summoning Portal's legacy scalar arithmetic stays intact; only its aura-free input is remembered.
        internal static void BeginScalarAura(Playable source, int reduction)
        {
            if (source[ScalarAuraCount] == 0) source.NativeTags[ScalarAuraBase] = source._modifiedCost ?? source.Card.Cost;
            source[ScalarAuraCount]++;
            source[ScalarAuraReduction] += reduction;
        }
        internal static void EndScalarAura(Playable source, int reduction)
        {
            if (source[ScalarAuraCount] <= 1) ClearScalarAuraMemory(source);
            else { source[ScalarAuraCount]--; source[ScalarAuraReduction] -= reduction; }
        }
        internal static void ClearScalarAuraMemory(Playable source)
        {
            source.NativeTags.Remove(ScalarAuraCount); source.NativeTags.Remove(ScalarAuraBase);
            source.NativeTags.Remove(ScalarAuraReduction);
        }
        internal static void RememberScalarSet(Playable source, int value)
        {
            if (source[ScalarAuraCount] > 0) source.NativeTags[ScalarAuraBase] = value;
        }
        internal static void RememberScalarModifier(Playable source, SabberStoneCore.Enchants.EffectOperator operation, int value)
        {
            if (source[ScalarAuraCount] > 0)
                source.NativeTags[ScalarAuraBase] = Model.Costs.EntityCostState.ApplyOperation(operation, source.NativeTags[ScalarAuraBase], value);
        }
        public static int PersistentCost(Playable source)
        {
            int input = InfinityRules.HasCost(source) ? InfinityRules.EvaluateCost(source)
                : source.NativeTags.TryGetValue(ScalarAuraBase, out int remembered) ? remembered : PersistentInput(source);
            int value = source._costOverlay?.Apply(input) ?? input;
            return AfterExternalCostAuras(source, source._costManager?.WithoutExternalAuras(value) ?? Math.Max(0, value));
        }
        public static void TransformHandKeepingValues(Minion source, Card definition, IPlayable creator,
            string statsEnchantment, string costEnchantment)
        {
            if (source.Zone != source.Controller.HandZone || definition.Type != CardType.MINION)
                throw new ArgumentException("A hand minion transform is required.");
            int attack = source.AttackDamage, health = source.Health, cost = PersistentCost(source);
            var target = Generic.ChangeEntityBlock(source.Controller, source, definition, true);
            Generic.AddEnchantmentBlock(source.Game, Cards.FromId(statsEnchantment), creator, target, attack, health);
            Generic.AddEnchantmentBlock(source.Game, Cards.FromId(costEnchantment), creator, target, cost);
        }
        public static void RestorePrintedCost(Playable source)
        {
            var before = Capture(new[] { source });
            // Remove numeric ownership only; payment replacements and unrelated buffs remain.
            var handles = source._costOverlay?.Modifiers.Select(m => m.Handle).ToArray() ?? Array.Empty<long>();
            foreach (long handle in handles) RemoveApplication(source, handle);
            RemoveNumericEnchantments(source);
            source._costManager?.ResetEnchantments();
            InfinityRules.RecordCost(source, SabberStoneCore.Enchants.EffectOperator.SET, source.Card.Cost);
            RememberScalarSet(source, source.Card.Cost);
            source._modifiedCost = source.Card.Cost - source[ScalarAuraReduction];
            Publish(before);
        }
        internal static bool RemoveNumericEnchantments(Playable source)
        {
            var enchantments = source.AppliedEnchantments?.Where(e => e.Card.Id == "TIME_030e1" || e.Power.Enchant != null
                && e.Power.Enchant.Effects.All(effect => effect.Tag == GameTag.COST)).ToArray() ?? Array.Empty<Enchantment>();
            foreach (var enchantment in enchantments) enchantment.Remove();
            return enchantments.Length != 0;
        }

        // Detached cost probes share the evaluator, never enter zones or subscribe to game events.
        public static Card[] AffordableSpells(Controller controller, System.Collections.Generic.IEnumerable<Card> candidates)
        {
            var payer = controller;
            var result = new System.Collections.Generic.List<Card>();
            foreach (var card in candidates)
            {
                if (card.Type != CardType.SPELL) throw new ArgumentException("Spell candidates required.");
                var probe = new Spell(payer, card, costProbe: true);
                probe.ZonePosition = payer.HandZone.Count;
                foreach (var aura in payer.Game.Auras.OfType<HandAdjacencyCostAura>()) aura.ApplyCostToProbe(probe);
                // AuraUpdate drains ordinary auras newest first, after immediate hand adjacency.
                foreach (var aura in payer.HandZone.Auras.AsEnumerable().Reverse()) aura.ApplyCostToProbe(probe);
                if (card.Power?.Aura is AdaptiveCostEffect adaptive) adaptive.BindCostProbe(probe);
                if (EffectiveCost(probe) <= controller.RemainingMana) result.Add(card);
            }
            return result.ToArray();
        }
    }
}
