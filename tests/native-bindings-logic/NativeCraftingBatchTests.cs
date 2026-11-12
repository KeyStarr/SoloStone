using System;

// Only the native pending DTO is substituted; TryReadBatch is extracted from production.
internal enum CraftDirection { Craft, Disenchant, Upgrade }
internal enum TAG_PREMIUM { NORMAL = 0, GOLDEN = 1, DIAMOND = 2, SIGNATURE = 3 }
internal sealed class CraftingPendingTransaction
{
    internal bool HasPendingTransactions() => NormalCreateCount != 0 || NormalDisenchantCount != 0 || GoldenCreateCount != 0
        || GoldenDisenchantCount != 0 || GoldenUpgradeFromNormalCount != 0 || GoldenUpgradeFromNothingCount != 0
        || SignatureDisenchantCount != 0 || DiamondDisenchantCount != 0;
    internal bool GetLastTransactionWasCrafting() => NormalCreateCount > 0 || GoldenCreateCount > 0 || GoldenUpgradeFromNormalCount > 0;
    internal bool GetLastTransactionWasDisenchant() => NormalDisenchantCount > 0 || GoldenDisenchantCount > 0
        || SignatureDisenchantCount > 0 || DiamondDisenchantCount > 0;
    internal string CardID = "TEST_CARD";
    internal TAG_PREMIUM Premium = TAG_PREMIUM.NORMAL;
    internal int NormalCreateCount, NormalDisenchantCount, GoldenCreateCount = 0, GoldenDisenchantCount = 0;
    internal int GoldenUpgradeFromNormalCount = 0, GoldenUpgradeFromNothingCount = 0, SignatureDisenchantCount = 0, DiamondDisenchantCount = 0;
}

internal static partial class NativeCrafting
{
    internal static void RunBatchTests()
    {
        Assert(!TryReadBatch(null, out _, out _, out _), "null batch refused");
        var pending = new CraftingPendingTransaction();
        Assert(!TryReadBatch(pending, out _, out _, out _), "fully undone batch refused");
        pending.NormalCreateCount = 3;
        Assert(TryReadBatch(pending, out var direction, out int count, out _) && direction == CraftDirection.Craft && count == 3,
            "craft batch keeps exact operation count, including extras");
        pending.NormalDisenchantCount = 1;
        Assert(!TryReadBatch(pending, out _, out _, out _), "mixed batch refused");
        pending.NormalCreateCount = 0;
        Assert(TryReadBatch(pending, out direction, out count, out _) && direction == CraftDirection.Disenchant && count == 1,
            "disenchant batch decoded");
        pending.NormalDisenchantCount = -1;
        Assert(!TryReadBatch(pending, out _, out _, out _), "negative count refused");
        pending.NormalDisenchantCount = 1;
        foreach (TAG_PREMIUM premium in new[] { TAG_PREMIUM.GOLDEN, TAG_PREMIUM.SIGNATURE, TAG_PREMIUM.DIAMOND })
        {
            pending.Premium = premium;
            Assert(TryReadBatch(pending, out direction, out count, out _) && direction == CraftDirection.Disenchant && count == 1,
                premium + " view keeps pending Normal operations");
        }
        pending.Premium = TAG_PREMIUM.NORMAL;
        foreach (string name in new[] { "GoldenCreateCount", "GoldenDisenchantCount", "GoldenUpgradeFromNormalCount",
            "GoldenUpgradeFromNothingCount", "SignatureDisenchantCount", "DiamondDisenchantCount" })
        {
            var field = typeof(CraftingPendingTransaction).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(pending, 1);
            Assert(!TryReadBatch(pending, out _, out _, out _), name + " counter refused");
            field.SetValue(pending, -1);
            Assert(!TryReadBatch(pending, out _, out _, out _), "negative " + name + " refused");
            field.SetValue(pending, 0);
        }
        pending.NormalDisenchantCount = 0;
        pending.GoldenUpgradeFromNormalCount = 2;
        Assert(TryReadBatch(pending, out direction, out count, out var upgradedPremium)
            && direction == CraftDirection.Upgrade && count == 2 && upgradedPremium == TAG_PREMIUM.GOLDEN,
            "upgrade batch consumes Normal and selects Golden independently of view");
        foreach (TAG_PREMIUM view in Enum.GetValues<TAG_PREMIUM>()) {
            pending.Premium = view;
            Assert(TryReadBatch(pending, out direction, out count, out upgradedPremium)
                && direction == CraftDirection.Upgrade && count == 2 && upgradedPremium == TAG_PREMIUM.GOLDEN, "upgrade independent of " + view + " view");
        }
        pending.GoldenDisenchantCount = 1;
        Assert(!TryReadBatch(pending, out _, out _, out _), "upgrade mixed with golden sale refused");
        pending.GoldenDisenchantCount = 0;
        pending.GoldenUpgradeFromNormalCount = -1;
        Assert(!TryReadBatch(pending, out _, out _, out _), "negative pure upgrade refused");
        pending.GoldenUpgradeFromNormalCount = 0;
        pending.GoldenCreateCount = 1;
        Assert(!TryReadBatch(pending, out _, out _, out _), "pure GoldenCreate refused");
        pending.GoldenCreateCount = 0;
        pending.GoldenUpgradeFromNothingCount = 1;
        Assert(!TryReadBatch(pending, out _, out _, out _), "pure upgrade from nothing refused");
        pending.GoldenUpgradeFromNothingCount = 0;
        foreach (var sale in new[] { ("GoldenDisenchantCount", TAG_PREMIUM.GOLDEN),
            ("DiamondDisenchantCount", TAG_PREMIUM.DIAMOND), ("SignatureDisenchantCount", TAG_PREMIUM.SIGNATURE) }) {
            var field = typeof(CraftingPendingTransaction).GetField(sale.Item1, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(pending, 2);
            Assert(TryReadBatch(pending, out direction, out count, out var premium)
                && direction == CraftDirection.Disenchant && count == 2 && premium == sale.Item2,
                sale.Item2 + " disenchant selects counter quality, independent of current view");
            pending.NormalCreateCount = 1;
            Assert(!TryReadBatch(pending, out _, out _, out _), "mixed craft and premium sale refused");
            pending.NormalCreateCount = 0;
            field.SetValue(pending, 0);
        }
        pending.CardID = "";
        Assert(!TryReadBatch(pending, out _, out _, out _), "missing card refused");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS crafting: " + message);
    }
}
