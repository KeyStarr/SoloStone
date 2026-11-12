using System;
using System.Linq;

// Preserve native premium values at the boundary into the receipt proof (no Unity actors needed).
internal static class NativePackRevealChecks
{
    internal static void Run()
    {
        var cards = new[] {
            Card("CS2_179", TAG_PREMIUM.GOLDEN), Card("EX1_561", TAG_PREMIUM.SIGNATURE),
            Card("EX1_007", TAG_PREMIUM.NORMAL), Card("CS2_087", TAG_PREMIUM.NORMAL), Card("CS2_179", TAG_PREMIUM.NORMAL)
        };
        string[] keys = cards.Select(card => LocalPackCard.ReceiptKey(card.Def.Name, (int)card.Def.Premium)).ToArray();
        bool[] up = { true, true, true, true, true };
        string[] batch = keys.Concat(Enumerable.Repeat("CS2_087", 45)).ToArray();
        Check(NativeUIProjection.CanAcknowledgePackReveal(true, true, true, true, batch, keys.Reverse().ToArray(), up, false, keys),
            "ten-pack native highlight Done accepts Golden/Signature identities");
        Check(NativeUIProjection.CanAcknowledgePackReveal(true, true, true, true, keys, keys.Reverse().ToArray(), up, false),
            "single native pack Done preserves Golden/Signature identities");
        cards[1].Def.Premium = TAG_PREMIUM.GOLDEN;
        string[] forged = cards.Select(card => LocalPackCard.ReceiptKey(card.Def.Name, (int)card.Def.Premium)).ToArray();
        Check(!NativeUIProjection.CanAcknowledgePackReveal(true, true, true, true, batch, forged, up, false, keys),
            "native same-card premium substitution cannot prove a highlight");
    }
    private static NetCache.BoosterCard Card(string id, TAG_PREMIUM premium)
    {
        var card = new NetCache.BoosterCard(); card.Def.Name = id; card.Def.Premium = premium; return card;
    }
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
