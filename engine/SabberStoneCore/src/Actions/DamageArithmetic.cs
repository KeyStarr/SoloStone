using System;
namespace SabberStoneCore.Actions
{
    public static class DamageArithmetic
    {
        // Damage and healing saturate at int.MaxValue instead of wrapping around on overflow. Negative results clamp to
        // 0. Double() returns int.MaxValue from 31 doublings on: any positive amount is already past int.MaxValue
        // there, and C# takes long shift counts mod 64, so a larger count would wrap.
        public static int Cap(long amount) => (int)Math.Max(0L, Math.Min(int.MaxValue, amount));
        public static int Add(int left, int right) => Cap((long)left + right);
        public static int Double(int amount, int count)
        {
            if (amount <= 0) return 0;
            return count >= 31 ? int.MaxValue : Cap((long)amount << Math.Max(0, count));
        }
    }
}
