using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class DeathKnightRulesProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    private static Card Runes(int blood, int frost, int unholy)
    {
        var card = Cards.FromId("CS2_182").Clone();
        card.Tags[(GameTag)2196] = blood; card.Tags[(GameTag)2197] = frost; card.Tags[(GameTag)2198] = unholy;
        return card;
    }
    internal static void Run()
    {
        Check(Cards.FromId("HERO_11bpt").GetRawRace() == Race.UNDEAD && Cards.FromId("HERO_11bpt").IsRace(Race.UNDEAD), "UndeadPrimaryAndSecondary primary");
        Check(Cards.FromId("END_004").GetRawRace() != Race.UNDEAD && Cards.FromId("END_004").IsRace(Race.UNDEAD), "UndeadPrimaryAndSecondary secondary");
        Check(Cards.FromId("GIL_681").IsRace(Race.UNDEAD), "AllRaceMatchesUndead");
        var ff = Runes(0, 2, 0);
        var maxima = RuneRules.FromCards(new[] { ff, ff });
        Check(maxima.Blood == 0 && maxima.Frost == 2 && maxima.Unholy == 0 && maxima.Total == 2, "RuneMaximaNotSum");
        Check(RuneRules.FromCards(Array.Empty<Card>()).Total == 0, "empty runes");
        var three = RuneRules.FromCards(new[] { Runes(2, 0, 0), Runes(0, 1, 0) });
        var four = RuneRules.FromCards(new[] { Runes(2, 0, 0), ff });
        Check(three.Total == 3 && RuneRules.IsLegalForClass(three, CardClass.DEATHKNIGHT)
            && four.Total == 4 && !RuneRules.IsLegalForClass(four, CardClass.DEATHKNIGHT), "RuneThreeSlotBoundary");
        Check(RuneRules.FromCards(new[] { Runes(1, 1, 0), Runes(1, 0, 1) }).Total == 3, "mixed rune maxima");
        Check(RuneRules.IsLegalForClass(four, CardClass.MAGE), "NonDkIgnoresRunes");
        foreach (var cls in new[] { CardClass.DEATHKNIGHT, CardClass.ROGUE })
            Check(CardClassRules.IsAllowedInDeck(Cards.FromId("END_002"), cls), "END_002 legal " + cls);
        Check(!CardClassRules.IsAllowedInDeck(Cards.FromId("END_002"), CardClass.MAGE), "END_002 illegal Mage");
        foreach (var cls in new[] { CardClass.DEATHKNIGHT, CardClass.DEMONHUNTER })
            Check(CardClassRules.IsAllowedInDeck(Cards.FromId("END_004"), cls), "END_004 legal " + cls);
        // CATA_722 is intentionally unregistered; its export-verified six-class mask is 9153.
        var herald = Cards.FromId("END_002").Clone(); herald.Tags[GameTag.MULTIPLE_CLASSES] = 9153;
        Check(!CardClassRules.IsAllowedInDeck(herald, CardClass.MAGE)
            && CardClassRules.IsAllowedInDeck(herald, CardClass.DEATHKNIGHT), "six-class card is not globally neutral");
        Check(CardClassRules.IsAllowedInDeck(Cards.FromId("CS2_182"), CardClass.DEATHKNIGHT)
            && !CardClassRules.IsAllowedInDeck(Cards.FromId("CS2_029"), CardClass.DEATHKNIGHT), "neutral/single class");
        var old = Cards.FromId("CS2_182").Clone(); old.Tags[GameTag.MULTI_CLASS_GROUP] = 1;
        Check(CardClassRules.IsAllowedInDeck(old, CardClass.PALADIN) && !CardClassRules.IsAllowedInDeck(old, CardClass.MAGE)
            && !CardClassRules.IsAllowedInDeck(old, CardClass.DEATHKNIGHT), "tag 480 retains historical group semantics");
        Console.WriteLine("PASS DK rune maxima/three-slot boundary/non-DK; modern masks and legacy groups");
    }
}
