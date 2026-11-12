using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

public static class MulliganTests
{
    private static void Expect(DeckPlan plan, CardClass opponent, bool coin, string offers, params int[] expected)
    {
        var cards = offers.Split(' ').Select((id, i) => new MulliganCard(i + 1, id, Cards.FromId(id).Cost)).ToArray();
        var policy = new DeckPlanMulligan(plan);
        var view = new MulliganView(cards, coin, LocalDecks.Get(plan.DeckId).CardIds, opponent);
        var actual = BotDriver.Mulligan(view, policy);
        if (!actual.SequenceEqual(expected)) throw new InvalidOperationException(offers + ": expected " + string.Join(",", expected) + " got " + string.Join(",", actual));
        var reversed = new MulliganView(cards.Reverse(), coin, view.OwnDeck.Reverse(), opponent);
        if (!actual.SequenceEqual(BotDriver.Mulligan(reversed, policy))) throw new InvalidOperationException("Order changed keeps");
    }

    public static void TestDragonDevelopmentAndExpensiveDiscards()
    {
        Expect(DeckPlans.DragonWarrior, CardClass.HUNTER, true, "CATA_556 EDR_889 CAP_107", 1, 2, 3);
        Expect(DeckPlans.DragonWarrior, CardClass.MAGE, true, "JAIL_384 TLC_600 END_033");
        Expect(DeckPlans.DragonWarrior, CardClass.HUNTER, true, "CATA_556 JAIL_384 EDR_492 CATA_585", 1, 3, 4);
        Expect(DeckPlans.DragonWarrior, CardClass.WARRIOR, true, "CATA_556 EDR_889 EDR_457 TIME_034", 1, 2, 3, 4);
    }

    public static void TestDragonDependenciesAndCopyLimits()
    {
        Expect(DeckPlans.DragonWarrior, CardClass.WARRIOR, true, "EDR_456 EDR_457 TLC_600 CAP_107", 4);
        Expect(DeckPlans.DragonWarrior, CardClass.MAGE, true, "EDR_456 CATA_556 EDR_457", 1, 2, 3);
        Expect(DeckPlans.DragonWarrior, CardClass.MAGE, true, "CATA_556 CATA_556 EDR_889 EDR_889", 1, 2, 3);
        Expect(DeckPlans.DragonWarrior, CardClass.MAGE, true, "GAME_005 EDR_456 EDR_457");
        Expect(DeckPlans.DragonWarrior, CardClass.WARRIOR, true, "EDR_456 EDR_457 TIME_034 JAIL_421");
    }

    public static void TestDragonMatchups()
    {
        Expect(DeckPlans.DragonWarrior, CardClass.HUNTER, true, "CATA_556 CATA_585 CAP_105 FIR_939", 1, 2, 4);
        Expect(DeckPlans.DragonWarrior, CardClass.MAGE, true, "CATA_556 CATA_585 CAP_105 FIR_939", 1, 3);
        Expect(DeckPlans.DragonWarrior, CardClass.WARRIOR, true, "CATA_556 CAP_107 EDR_492 JAIL_421", 1, 2, 3, 4);
        Expect(DeckPlans.DragonWarrior, CardClass.MAGE, true, "CATA_556 CAP_107 EDR_492 JAIL_421", 1, 2);
        Expect(DeckPlans.DragonWarrior, (CardClass)0, true, "CATA_556 CAP_107 EDR_492 JAIL_421", 1, 2, 3, 4);
    }

    public static void TestPaladinAuraDependencies()
    {
        Expect(DeckPlans.PurePaladin, CardClass.HUNTER, true, "CORE_ICC_038 JAIL_327 TIME_019 TIME_009", 1, 2, 3);
        Expect(DeckPlans.PurePaladin, CardClass.HUNTER, true, "TIME_019 TIME_009 TIME_009t1");
    }

    public static void TestPaladinControlCurve()
    {
        Expect(DeckPlans.PurePaladin, CardClass.MAGE, true, "CORE_ICC_038 CATA_472 END_011 CATA_473", 1, 2, 3, 4);
        Expect(DeckPlans.PurePaladin, CardClass.HUNTER, true, "CORE_ICC_038 CATA_472 END_011 CATA_473", 1, 2);
        Expect(DeckPlans.PurePaladin, CardClass.MAGE, true, "CATA_473 CATA_479 TIME_009");
    }

    public static void TestShamanCoinAndCheapMinions()
    {
        Expect(DeckPlans.ZeeShaman, CardClass.MAGE, true, "JAIL_457 CORE_GIL_531 CORE_UNG_809 GAME_005", 1, 2, 3);
        Expect(DeckPlans.ZeeShaman, CardClass.MAGE, false, "JAIL_457 CORE_GIL_531 CORE_UNG_809", 2, 3);
        Expect(DeckPlans.ZeeShaman, CardClass.MAGE, true, "JAIL_457 JAIL_802 CORE_UNG_809", 2, 3);
    }

    public static void TestShamanOneDropAndMatchup()
    {
        Expect(DeckPlans.ZeeShaman, CardClass.HUNTER, true, "CORE_UNG_809 CORE_ULD_191 DINO_411 CATA_565", 1, 2, 3, 4);
        Expect(DeckPlans.ZeeShaman, CardClass.MAGE, true, "CORE_GIL_531 CORE_ULD_191 DINO_411 CATA_565", 1);
    }

    public static void TestMulliganViewContainsOnlyValues()
    {
        CheckType(typeof(MulliganView));
    }

    private static void CheckType(System.Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        { CheckType(type.GetGenericArguments()[0]); return; }
        if (type != typeof(MulliganView) && type != typeof(MulliganCard))
            throw new InvalidOperationException("Unexpected mulligan reference: " + type.FullName);
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) CheckType(field.FieldType);
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) CheckType(property.PropertyType);
    }
}
