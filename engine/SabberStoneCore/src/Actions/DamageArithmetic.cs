using System;
namespace SabberStoneCore.Actions
{
    public static class DamageArithmetic
    {
        public static int Cap(long amount) => (int)Math.Max(0L, Math.Min(int.MaxValue, amount));
        public static int Add(int left, int right) => Cap((long)left + right);
        public static int Double(int amount, int count)
        {
            if (amount <= 0) return 0;
            return count >= 31 ? int.MaxValue : Cap((long)amount << Math.Max(0, count));
        }
    }
}
