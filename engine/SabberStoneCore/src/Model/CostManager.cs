using System;
using SabberStoneCore.Enchants;

namespace SabberStoneCore.Actions
{
    // Keep signed intermediate discounts; effective values apply their usual nonnegative floor.
    internal static class AttributeArithmetic
    {
        internal static int Clamp(long value) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value));
        internal static int Apply(int left, EffectOperator operation, int right) => Clamp(operation switch {
            EffectOperator.ADD => (long)left + right,
            EffectOperator.SUB => (long)left - right,
            EffectOperator.MUL => (long)left * right,
            EffectOperator.SET => right,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        });
    }
}
