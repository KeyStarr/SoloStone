using SabberStoneCore.Enums;

// Moved from tests/transport-invariants/RewindTransportProof.cs: pure NativeUIProjection table, no client.
internal static class EndpointZeroTagsProjectionTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal static void Run()
    {
        foreach (var (type, tag, equivalent) in new[] {
            (CardType.MINION, GameTag.COLLECTIBLE, false),
            (CardType.SPELL, GameTag.COLLECTIBLE, false),
            (CardType.HERO_POWER, GameTag.COLLECTIBLE, false),
            (CardType.ENCHANTMENT, GameTag.COLLECTIBLE, false),
            (CardType.LOCATION, GameTag.COLLECTIBLE, false),
            (CardType.ENCHANTMENT, GameTag.DAMAGE, true),
            (CardType.ENCHANTMENT, GameTag.ATK, true),
            (CardType.ENCHANTMENT, GameTag.HEALTH, true),
            (CardType.ENCHANTMENT, GameTag.COST, true),
            (CardType.ENCHANTMENT, GameTag.RARITY, false),
            (CardType.ENCHANTMENT, GameTag.ATTACHED, false),
            (CardType.ENCHANTMENT, GameTag.CREATOR, false),
            (CardType.ENCHANTMENT, GameTag.IS_NIGHTMARE_BONUS, false),
            (CardType.MINION, GameTag.ATK, false),
            (CardType.MINION, GameTag.HEALTH, false),
            (CardType.SPELL, GameTag.ATK, false),
            (CardType.SPELL, GameTag.HEALTH, false),
            (CardType.SPELL, GameTag.COST, false),
            (CardType.MINION, GameTag.DAMAGE, false),
            (CardType.HERO, GameTag.DAMAGE, false),
            (CardType.LOCATION, GameTag.DAMAGE, false),
            (CardType.HERO_POWER, GameTag.DAMAGE, false),
            (CardType.MINION, GameTag.SPELLPOWER, false),
            (CardType.MINION, GameTag.CANT_BE_TARGETED_BY_SPELLS, false)
        })
        foreach (bool nativePresent in new[] { false, true })
        {
            Check(NativeUIProjection.TestControlTransportTagMatches((int)tag, type, nativePresent, 0, !nativePresent, 0) == equivalent,
                $"endpoint zero-presence scope: {type}/{tag}/{nativePresent}");
            Check(!NativeUIProjection.TestControlTransportTagMatches((int)tag, type, true, 1, false, 0)
                && !NativeUIProjection.TestControlTransportTagMatches((int)tag, type, false, 0, true, 1)
                && !NativeUIProjection.TestControlTransportTagMatches((int)tag, type, true, 1, true, 2),
                $"endpoint rejects real nonzero mismatch: {type}/{tag}");
            Check(!NativeUIProjection.TransportTagMatches(nativePresent, 0, !nativePresent, 0),
                "strict transport audit still rejects every zero presence mismatch");
        }
        Console.WriteLine("PASS endpoint zero tags: only enchantment HIDE stats; identity/attachment/nonzero/gameplay mismatches remain strict");
    }
}
