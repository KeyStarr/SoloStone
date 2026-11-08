using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // Entity applications precede the unchanged aura layer; own adaptive text remains last (A1).
    public static partial class CostRules
    {
        // Retail CARD_ALTERNATE_COST=4; semantic grant lives in an exported controller attachment.
        private const CostPaymentKind OpponentHealth = (CostPaymentKind)4;
        internal static long NextPaymentOrder(Game game) => checked(++game.CostState.PaymentClock);
        private static Enchantment[] OpponentHealthGrants(Controller controller) => controller.AppliedEnchantments?
            .Where(e => e.Card.Id == "EDR_489e1").ToArray() ?? Array.Empty<Enchantment>();

        public static void GrantOpponentHealth(IPlayable source)
        {
            var owner = source.Controller;
            var before = Capture(owner.HandZone.OfType<Playable>());
            Enchantment.GetInstance(owner, source, owner, Cards.FromId("EDR_489e1"));
            Publish(before);
        }

        internal static Playable.CostManager Manager(Playable target)
            => target._costManager ?? (target._costManager = new Playable.CostManager());

        private static EntityCostState Overlay(Playable target)
            => target._costOverlay ?? (target._costOverlay = new EntityCostState());

        public static long AddModifier(Playable target, EffectOperator operation, int value,
            int? expiryTurn = null, int enchantmentId = 0)
        {
            if (operation != EffectOperator.ADD && operation != EffectOperator.SUB && operation != EffectOperator.SET
                && operation != EffectOperator.MUL) throw new ArgumentOutOfRangeException(nameof(operation));
            if (!expiryTurn.HasValue)
            {
                // Permanent applications retain HEAD's scalar/cache arithmetic and are not removable handles.
                Cost.Effect(operation, value).ApplyTo(target);
                return 0;
            }
            var before = Capture(new[] { target });
            long handle = checked(++target.Game.CostState.Counter);
            Overlay(target).Modifiers.Add(new CostModifier(handle, operation, value, expiryTurn, enchantmentId));
            target.Game.CostState.OverlayCount++;
            Publish(before);
            return handle;
        }

        // Modules may use Effects.SetCost(value).ApplyTo(target, true) or this explicit handle API.
        public static long SetCostThisTurn(Playable target, int value)
            => AddModifier(target, EffectOperator.SET, value, target.Game.Turn);

        public static long AddPaymentOverride(Playable target, CostPaymentKind kind,
            int? expiryTurn = null, int enchantmentId = 0)
        {
            ValidateKind(kind);
            var before = Capture(new[] { target });
            long handle = checked(++target.Game.CostState.Counter);
            Overlay(target).Payments.Add(new CostPaymentOverride(handle, kind, expiryTurn, enchantmentId, NextPaymentOrder(target.Game)));
            target.Game.CostState.OverlayCount++;
            Publish(before);
            return handle;
        }

        // Values are captured before mutation and copied into history, never repaired from later live state.
        private static (Playable Target, int Cost, CostPaymentKind Kind)[] Capture(System.Collections.Generic.IEnumerable<Playable> targets)
            => targets.Distinct().Select(p => (p, p.Cost, PaymentKind(p))).ToArray();

        private static void Publish((Playable Target, int Cost, CostPaymentKind Kind)[] before)
        {
            foreach (var old in before)
            {
                var target = old.Target;
                if (!target.Game.History) continue;
                int cost = target.Cost;
                var kind = PaymentKind(target);
                if (old.Cost != cost)
                    target.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.COST, cost));
                if (old.Kind != kind)
                    target.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.CARD_ALTERNATE_COST, (int)kind));
            }
        }

        private static Playable[] RuleTargets(Game game)
            => game.IdEntityDic.Values.OfType<Playable>().ToArray();

        public static void RemoveApplication(Playable target, long handle)
        {
            var manager = target._costOverlay;
            if (manager == null) return;
            var ids = manager.Modifiers.Where(m => m.Handle == handle).Select(m => m.EnchantmentId)
                .Concat(manager.Payments.Where(m => m.Handle == handle).Select(m => m.EnchantmentId)).ToArray();
            if (ids.Length == 0) return;
            var before = Capture(new[] { target });
            target.Game.CostState.OverlayCount -= manager.Modifiers.RemoveAll(m => m.Handle == handle);
            target.Game.CostState.OverlayCount -= manager.Payments.RemoveAll(m => m.Handle == handle);
            RetireActors(target.Game, ids);
            Publish(before);
        }

        internal static void ResetOverlay(Playable target)
        {
            if (target._costOverlay == null) return;
            var handles = target._costOverlay.Modifiers.Select(m => m.Handle)
                .Concat(target._costOverlay.Payments.Select(m => m.Handle)).ToArray();
            foreach (long handle in handles) RemoveApplication(target, handle);
        }

        private static void RetireActors(Game game, System.Collections.Generic.IEnumerable<int> ids)
        {
            foreach (int id in ids.Where(id => id != 0).Distinct())
            {
                bool bound = game.CostState.Rules.Any(r => r.EnchantmentId == id)
                    || game.IdEntityDic.Values.OfType<Playable>().Any(p => p._costOverlay != null
                        && (p._costOverlay.Modifiers.Any(m => m.EnchantmentId == id)
                            || p._costOverlay.Payments.Any(m => m.EnchantmentId == id)));
                if (!bound && game.IdEntityDic.TryGetValue(id, out var entity) && entity is Enchantment actor
                    && (actor.Target.AppliedEnchantments?.Contains(actor) ?? false)) actor.Remove();
            }
        }

        internal static void RemoveEnchantment(Game game, int enchantmentId)
        {
            var targets = game.IdEntityDic.Values.OfType<Playable>().Where(p => p._costOverlay != null
                && (p._costOverlay.Modifiers.Any(m => m.EnchantmentId == enchantmentId)
                    || p._costOverlay.Payments.Any(m => m.EnchantmentId == enchantmentId))).ToArray();
            bool rules = game.CostState.Rules.Any(r => r.EnchantmentId == enchantmentId);
            if (targets.Length == 0 && !rules) return;
            var before = Capture(rules ? RuleTargets(game) : targets);
            foreach (var target in targets)
            {
                target.Game.CostState.OverlayCount -= target._costOverlay.Modifiers.RemoveAll(m => m.EnchantmentId == enchantmentId);
                target.Game.CostState.OverlayCount -= target._costOverlay.Payments.RemoveAll(m => m.EnchantmentId == enchantmentId);
            }
            game.CostState.Rules.RemoveAll(r => r.EnchantmentId == enchantmentId);
            Publish(before);
        }

        internal static void RefreshHandAuras(Game game)
        {
            if (game.Auras == null) return;
            foreach (var aura in game.Auras.Where(a => a is ConditionalHeroPowerCostAura || a is HandAdjacencyCostAura).ToArray())
                aura.Update();
        }

        // Read-only boundary probe for fair simulations; includes overlays without enchantment actors.
        public static bool HasTurnExpiry(Game game, int endingTurn)
        {
            var rules = game.CostState.Rules;
            for (int i = 0; i < rules.Count; i++)
                if (rules[i].ExpiryTurn <= endingTurn) return true;
            if (game.CostState.OverlayCount == 0) return false;
            var entries = game.IdEntityDic.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (!(entries[i] is Playable p) || p._costOverlay == null) continue;
                var modifiers = p._costOverlay.Modifiers;
                for (int j = 0; j < modifiers.Count; j++)
                    if (modifiers[j].ExpiryTurn <= endingTurn) return true;
                var payments = p._costOverlay.Payments;
                for (int j = 0; j < payments.Count; j++)
                    if (payments[j].ExpiryTurn <= endingTurn) return true;
            }
            return false;
        }

        public static void ExpireTurn(Game game, int endingTurn)
        {
            // Every turn end: walk the id slots instead of copying Values through OfType/Where/ToArray.
            var entities = game.IdEntityDic;
            List<Playable> expiring = null;
            for (int id = 0, n = entities.Capacity; id < n; id++)
            {
                if (!(entities.At(id) is Playable p) || p._costOverlay == null) continue;
                if (p._costOverlay.Modifiers.Any(m => m.ExpiryTurn <= endingTurn) || p._costOverlay.Payments.Any(m => m.ExpiryTurn <= endingTurn))
                    (expiring ??= new List<Playable>()).Add(p);
            }
            var targets = expiring?.ToArray() ?? Array.Empty<Playable>();
            var rules = game.CostState.Rules.Where(r => r.ExpiryTurn <= endingTurn).ToArray();
            if (targets.Length == 0 && rules.Length == 0) return;
            var before = Capture(rules.Length != 0 ? RuleTargets(game) : targets);
            var ids = rules.Select(r => r.EnchantmentId).Concat(targets.SelectMany(p =>
                p._costOverlay.Modifiers.Where(m => m.ExpiryTurn <= endingTurn).Select(m => m.EnchantmentId)
                    .Concat(p._costOverlay.Payments.Where(m => m.ExpiryTurn <= endingTurn).Select(m => m.EnchantmentId)))).ToArray();
            foreach (var target in targets)
            {
                target.Game.CostState.OverlayCount -= target._costOverlay.Modifiers.RemoveAll(m => m.ExpiryTurn <= endingTurn);
                target.Game.CostState.OverlayCount -= target._costOverlay.Payments.RemoveAll(m => m.ExpiryTurn <= endingTurn);
            }
            game.CostState.Rules.RemoveAll(r => r.ExpiryTurn <= endingTurn);
            RetireActors(game, ids);
            Publish(before);
        }

        internal static void CopyState(Playable source, Playable copy)
        {
            InfinityRules.CopyCost(source, copy);
            if (source._costOverlay == null || source._costOverlay.Modifiers.Count == 0 && source._costOverlay.Payments.Count == 0) return;
            var manager = Overlay(copy);
            copy.Game.CostState.OverlayCount += source._costOverlay.Modifiers.Count + source._costOverlay.Payments.Count;
            foreach (var m in source._costOverlay.Modifiers)
                manager.Modifiers.Add(new CostModifier(checked(++copy.Game.CostState.Counter), m.Operator, m.Value,
                    m.ExpiryTurn, CopyEnchantment(source, copy, m.EnchantmentId)));
            foreach (var m in source._costOverlay.Payments)
                manager.Payments.Add(new CostPaymentOverride(checked(++copy.Game.CostState.Counter), m.Kind,
                    m.ExpiryTurn, CopyEnchantment(source, copy, m.EnchantmentId), NextPaymentOrder(copy.Game)));
        }

        private static int CopyEnchantment(Playable source, Playable copy, int id)
        {
            if (id == 0 || source.AppliedEnchantments == null || copy.AppliedEnchantments == null) return 0;
            int index = source.AppliedEnchantments.FindIndex(e => e.Id == id);
            if (index < 0) return 0;
            // Some card-specific attachments are deliberately excluded by Copy.
            var original = source.AppliedEnchantments[index];
            int ordinal = source.AppliedEnchantments.Take(index).Count(e => e.Card.Id == original.Card.Id);
            return copy.AppliedEnchantments.Where(e => e.Card.Id == original.Card.Id).Skip(ordinal).FirstOrDefault()?.Id ?? 0;
        }

        public static long AddNextCardRule(Controller controller, CostPaymentKind? kind = null,
            CardType? cardType = null, Race? race = null, int? maxEffectiveCost = null,
            int? setCost = null, int? expiryTurn = null, int enchantmentId = 0)
            => AddRule(controller, false, kind, cardType, race, maxEffectiveCost, setCost, expiryTurn, enchantmentId);

        public static long AddNextHeroPowerRule(Controller controller, int? setCost = null,
            CostPaymentKind? kind = null, int? expiryTurn = null, int enchantmentId = 0)
            => AddRule(controller, true, kind, null, null, null, setCost, expiryTurn, enchantmentId);

        public static long AddNextTemporaryRule(Controller controller, int reduction, int enchantmentId = 0)
        {
            if (reduction < 0) throw new ArgumentOutOfRangeException(nameof(reduction));
            return AddRule(controller, false, null, null, null, null, null, null, enchantmentId, true, reduction);
        }

        private static long AddRule(Controller controller, bool heroPower, CostPaymentKind? kind,
            CardType? cardType, Race? race, int? maxCost, int? setCost, int? expiryTurn, int enchantmentId, bool onlyTemporary = false, int reduction = 0)
        {
            if (kind.HasValue) ValidateKind(kind.Value);
            if (setCost < 0 || maxCost < 0) throw new ArgumentOutOfRangeException(nameof(setCost));
            var before = Capture(RuleTargets(controller.Game));
            long handle = checked(++controller.Game.CostState.Counter);
            controller.Game.CostState.Rules.Add(new CostOneUseRule(handle, controller.Id, heroPower, cardType,
                race, maxCost, kind, setCost, expiryTurn, enchantmentId, onlyTemporary, reduction, kind.HasValue ? NextPaymentOrder(controller.Game) : 0));
            Publish(before);
            return handle;
        }

        private static bool Matches(CostOneUseRule rule, IPlayable source, int numericCost)
            => rule.ControllerId == source.Controller.Id && rule.HeroPower == (source is HeroPower)
                && (rule.HeroPower ? source.Controller.HeroPowers.Contains(source) : source.Zone == source.Controller.HandZone)
                && (!rule.OnlyTemporary || TemporaryRules.IsTemporary(source))
                && (!rule.CardType.HasValue || source.Card.Type == rule.CardType)
                && (!rule.Race.HasValue || source.Card.IsRace(rule.Race.Value))
                && (!rule.MaxCost.HasValue || numericCost <= rule.MaxCost);

        internal static int AdjustCost(Playable source, int numericCost)
        {
            int amount = Math.Max(0, numericCost - CthunRules.CostReduction(source));
            foreach (var rule in source.Game.CostState.Rules)
                if (Matches(rule, source, amount))
                {
                    if (rule.SetCost.HasValue) amount = rule.SetCost.Value;
                    if (rule.OnlyTemporary) amount = Math.Max(0, amount - rule.Reduction);
                }
            return amount;
        }

        internal static int AfterExternalCostAuras(Playable source, int amount)
        {
            if (source.Zone == source.Controller.HandZone)
                amount -= source.AppliedEnchantments?.Where(e => e.Card.Id == "TIME_030e1")
                    .Sum(e => e[GameTag.TAG_SCRIPT_DATA_NUM_1]) ?? 0;
            return Math.Max(0, amount - DeathwingDiscount(source));
        }

        public static int EffectiveCost(IPlayable source) => source.Cost;
        public static CostPaymentKind PaymentKind(IPlayable source) => Quote(source.Controller, source).Kind;

        public static CostQuote Quote(Controller payer, IPlayable source)
        {
            int numeric = source is Playable p ? p.NumericCost : source.Cost;
            int amount = source is Playable playable ? AdjustCost(playable, numeric) : numeric;
            var kind = (CostPaymentKind)source.Card[GameTag.CARD_ALTERNATE_COST];
            ValidateKind(kind);
            // Printed alternate cost is oldest. Intrinsic conditional text has no application event;
            // use baseline order 0, before all runtime replacements (rules-evidence/cost-rules/mixed-alternate-cost.yaml).
            long newest = -1;
            void Replace(CostPaymentKind next, long appliedAt)
            {
                if (appliedAt < newest) return;
                kind = next;
                newest = appliedAt;
            }
            if (source.Card.HealthCostCondition?.Invoke(source) == true) Replace(CostPaymentKind.Health, 0);
            if (source is Spell && payer.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] == 1)
                Replace(CostPaymentKind.Health, payer.ControllerAuraEffects.HealthCostAppliedAt);
            if (source.AuraEffects?.CardCostHealth ?? false)
                Replace(CostPaymentKind.Health, source.AuraEffects.HealthCostAppliedAt);
            if (source is Playable target && target._costOverlay != null)
                foreach (var replacement in target._costOverlay.Payments) Replace(replacement.Kind, replacement.AppliedAt);
            // Resolve numeric rules first, then match resource predicates; freeze all applied handles.
            var applied = new System.Collections.Generic.List<CostOneUseRule>();
            int running = numeric;
            foreach (var rule in payer.Game.CostState.Rules)
                if ((rule.SetCost.HasValue || rule.OnlyTemporary) && Matches(rule, source, running))
                {
                    if (rule.SetCost.HasValue) running = rule.SetCost.Value;
                    if (rule.OnlyTemporary) running = Math.Max(0, running - rule.Reduction);
                    applied.Add(rule);
                }
            foreach (var rule in payer.Game.CostState.Rules)
                if (rule.Kind.HasValue && Matches(rule, source, amount))
                {
                    Replace(rule.Kind.Value, rule.AppliedAt);
                    if (!applied.Any(r => r.Handle == rule.Handle)) applied.Add(rule);
                }
            if (!(source is HeroPower) && source.Zone == payer.HandZone)
                foreach (var grant in OpponentHealthGrants(payer)) Replace(OpponentHealth, grant.CostPaymentAppliedAt);
            var rules = applied.OrderBy(r => r.Handle).ToArray();
            return new CostQuote(source.Id, payer.Id, Math.Max(0, amount), kind, rules.Select(r => r.Handle).ToArray());
        }

        private static void ValidateKind(CostPaymentKind kind)
        {
            if (kind != OpponentHealth && (kind < CostPaymentKind.Mana || kind > CostPaymentKind.Corpses))
                throw new NotSupportedException("Unsupported alternate cost: " + kind);
        }
        public static bool CanPay(Controller payer, CostQuote quote)
        {
            if (payer.Id != quote.ControllerId || quote.Amount < 0) return false;
            if (quote.Amount == 0) return true;
            switch (quote.Kind)
            {
                case CostPaymentKind.Mana: return payer.RemainingMana >= quote.Amount;
                case CostPaymentKind.Health: return HealingRules.ReplacesHealthLoss(payer) || payer.Hero.Health > quote.Amount;
                case OpponentHealth: return quote.Amount <= 10;
                case CostPaymentKind.Armor: return payer.Hero.Armor >= quote.Amount;
                case CostPaymentKind.Corpses: return payer.Game.EnableCorpses && CorpseRules.Available(payer) >= quote.Amount;
                default: return false;
            }
        }

        public static bool CommitPayment(Controller payer, IPlayable source, CostQuote quote)
        {
            if (source.Game != payer.Game || source.Controller != payer || source.Id != quote.SourceId) return false;
            var current = Quote(payer, source);
            if (quote.ControllerId != current.ControllerId || quote.Amount != current.Amount || quote.Kind != current.Kind
                || !quote.Rules.SequenceEqual(current.Rules) || !CanPay(payer, quote)) return false;
            int amount = quote.Amount;
            if (!(source is HeroPower) && payer.Game.CurrentEventData?.EventSource == source) CaptureHeldQualifications(payer, source, amount);
            FreezeManaNumbers(source);
            source[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] = amount;
            // History: every committed payment (zero included) records one ManaSpent fact, the play annotation.
            if (amount > 0)
            {
                switch (quote.Kind)
                {
                    case CostPaymentKind.Mana:
                        Generic.SpendMana(payer, source, amount);
                        break;
                    case CostPaymentKind.Health:
                    case OpponentHealth:
                        var hero = quote.Kind == OpponentHealth ? payer.Opponent.Hero : payer.Hero;
                        int oldHealth = hero.Health;
                        using (payer.Game.Facts.SuppressHealth())
                            if (!HealingRules.ReplaceHealthLoss(hero, amount)) hero.Damage = DamageArithmetic.Add(hero.Damage, amount);
                        payer.Game.Facts.HealthChanged(hero, oldHealth, HistoryHealthChangeReason.Payment);
                        payer.Game.Facts.Paid(payer, source, 0, amount);
                        break;
                    case CostPaymentKind.Corpses:
                        if (!CorpseRules.TrySpend(payer, amount,source)) return false;
                        payer.Game.Facts.Paid(payer, source, 0);
                        break;
                    case CostPaymentKind.Armor:
                        payer.Hero.Armor -= amount;
                        payer.Game.Facts.Paid(payer, source, 0);
                        break;
                }
            }
            else Generic.SpendMana(payer, source, 0);
            ConsumeRules(payer, quote);
            if (!(source is HeroPower) && source.Zone == payer.HandZone)
            {
                var grants = OpponentHealthGrants(payer);
                if (grants.Length > 0)
                {
                    var before = Capture(payer.HandZone.OfType<Playable>());
                    foreach (var grant in grants) grant.Remove();
                    Publish(before);
                }
            }
            if (quote.Kind == OpponentHealth && payer.Opponent.Hero.ToBeDestroyed) payer.Game.GraveYard();
            return true;
        }

        internal static void ConsumeRules(Controller payer, CostQuote quote)
        {
            if (quote.Rules.Length != 0)
            {
                var before = Capture(RuleTargets(payer.Game));
                var ids = payer.Game.CostState.Rules.Where(r => quote.Rules.Contains(r.Handle)).Select(r => r.EnchantmentId).ToArray();
                payer.Game.CostState.Rules.RemoveAll(r => quote.Rules.Contains(r.Handle));
                RetireActors(payer.Game, ids);
                Publish(before);
            }
        }
    }
}
