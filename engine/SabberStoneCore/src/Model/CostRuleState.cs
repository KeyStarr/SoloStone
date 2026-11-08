using System.Collections.Generic;
using System.Numerics;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model.Costs
{
    public enum CostPaymentKind { Mana, Health, Armor, Corpses }

    public readonly struct CostModifier
    {
        public readonly long Handle;
        public readonly EffectOperator Operator;
        public readonly int Value;
        public readonly int? ExpiryTurn;
        public readonly int EnchantmentId;
        public CostModifier(long handle, EffectOperator operation, int value, int? expiryTurn, int enchantmentId)
        { Handle = handle; Operator = operation; Value = value; ExpiryTurn = expiryTurn; EnchantmentId = enchantmentId; }
    }

    internal sealed class EntityCostState
    {
        internal readonly List<CostModifier> Modifiers = new List<CostModifier>();
        internal readonly List<CostPaymentOverride> Payments = new List<CostPaymentOverride>();
        internal EntityCostState Clone()
        {
            var copy = new EntityCostState();
            copy.Modifiers.AddRange(Modifiers);
            copy.Payments.AddRange(Payments);
            return copy;
        }
        internal static int ApplyOperation(EffectOperator operation, int cost, int value)
            => Actions.AttributeArithmetic.Apply(cost, operation, value);
        internal int Apply(int cost)
        {
            // Keep intermediate excess/discounts until the whole ordered overlay has evaluated.
            BigInteger result = cost;
            foreach (var m in Modifiers)
                result = m.Operator switch {
                    EffectOperator.ADD => result + m.Value, EffectOperator.SUB => result - m.Value,
                    EffectOperator.SET => m.Value, EffectOperator.MUL => result * m.Value,
                    _ => throw new System.ArgumentOutOfRangeException()
                };
            return (int)BigInteger.Max(int.MinValue, BigInteger.Min(int.MaxValue, result));
        }

    }

    public readonly struct CostPaymentOverride
    {
        public readonly long AppliedAt;
        public readonly long Handle;
        public readonly CostPaymentKind Kind;
        public readonly int? ExpiryTurn;
        public readonly int EnchantmentId;
        public CostPaymentOverride(long handle, CostPaymentKind kind, int? expiryTurn, int enchantmentId, long appliedAt)
        { AppliedAt = appliedAt; Handle = handle; Kind = kind; ExpiryTurn = expiryTurn; EnchantmentId = enchantmentId; }
    }

    public readonly struct CostOneUseRule
    {
        public readonly long AppliedAt;
        public readonly long Handle;
        public readonly int ControllerId;
        public readonly bool HeroPower;
        public readonly CardType? CardType;
        public readonly Race? Race;
        public readonly int? MaxCost;
        public readonly CostPaymentKind? Kind;
        public readonly int? SetCost;
        public readonly int? ExpiryTurn;
        public readonly int EnchantmentId;
        public readonly bool OnlyTemporary;
        public readonly int Reduction;
        public CostOneUseRule(long handle, int controllerId, bool heroPower, CardType? cardType, Race? race,
            int? maxCost, CostPaymentKind? kind, int? setCost, int? expiryTurn, int enchantmentId, bool onlyTemporary = false, int reduction = 0, long appliedAt = 0)
        {
            AppliedAt = appliedAt; Handle = handle; ControllerId = controllerId; HeroPower = heroPower; CardType = cardType;
            Race = race; MaxCost = maxCost; Kind = kind; SetCost = setCost; ExpiryTurn = expiryTurn; EnchantmentId = enchantmentId;
            OnlyTemporary = onlyTemporary; Reduction = reduction;
        }
    }

    public sealed class CostQuote
    {
        public int SourceId { get; }
        public int ControllerId { get; }
        public int Amount { get; }
        public CostPaymentKind Kind { get; }
        internal readonly long[] Rules;
        internal CostQuote(int sourceId, int controllerId, int amount, CostPaymentKind kind, long[] rules)
        { SourceId = sourceId; ControllerId = controllerId; Amount = amount; Kind = kind; Rules = rules; }
    }

    internal sealed class CostRuleState
    {
        internal long Counter;
        internal int OverlayCount;
        internal long PaymentClock;
        internal readonly List<CostOneUseRule> Rules = new List<CostOneUseRule>();
        internal CostRuleState Clone()
        {
            var clone = new CostRuleState { Counter = Counter, PaymentClock = PaymentClock, OverlayCount = OverlayCount };
            clone.Rules.AddRange(Rules);
            return clone;
        }
    }
}
