internal static class CraftingChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Refused(CraftPlan plan) => Check(!plan.Ok && !string.IsNullOrEmpty(plan.Error)
        && plan.DustDelta == 0 && plan.Cards.Count == 0, "refusal must contain no partial changes");
    static CraftLedgerView View(long dust = 1000, int owned = 0, int acquired = 0) => new CraftLedgerView {
        Dust = dust, Counts = new Dictionary<string, int> { ["CATA_452"] = owned },
        Acquired = new Dictionary<string, int> { ["CATA_452"] = acquired }
    };

    internal static void Run()
    {
        foreach (var price in new[] { ("CATA_452", 40, 20), ("EDR_001", 100, 50),
            ("CATA_493", 400, 200), ("CATA_458", 1600, 800) })
            Check(LocalCrafting.TryGetPrice(price.Item1, out int buy, out int sell)
                && buy == price.Item2 && sell == price.Item3, "normal rarity prices: " + price.Item1 + " (" + LocalCrafting.GetIneligibility(price.Item1) + ")");
        // Owner 2026-10-30: Fabled primaries and sideboard owners are ordinary craftable legendaries; companions are not.
        foreach (var card in LocalDecks.KnownBundleAndSideboardCards.Where(LocalCardCatalog.InActiveMeta))
            Check(card.Collectible ? LocalCrafting.TryGetPrice(card.Id, out int buy, out int sell) && buy == 1600 && sell == 800
                : !LocalCrafting.TryGetPrice(card.Id, out _, out _), "Fabled/sideboard crafting price: " + card.Id);
        foreach (var reason in new[] {
            ("NO_SUCH_CARD", CraftIneligibility.UnknownCard),
            ("CS2_boar", CraftIneligibility.NotCollectible),
            ("CORE_EX1_012", CraftIneligibility.CoreCard),
            ("CS3_038", CraftIneligibility.CoreCard),
            ("CS2_029", CraftIneligibility.FreeCard),
            ("EX1_007", CraftIneligibility.FreeCard),
            ("GVG_001", CraftIneligibility.NotInCraftableSets),
            ("EX1_008", CraftIneligibility.NotInCraftableSets),
            ("HERO_08", CraftIneligibility.HeroOrNonCard),
            ("CS2_034", CraftIneligibility.HeroOrNonCard),
            ("TB_BaconUps_002", CraftIneligibility.Premium) }) {
            Check(LocalCrafting.GetIneligibility(reason.Item1) == reason.Item2, "eligibility: " + reason.Item1);
            Check(!LocalCrafting.TryGetPrice(reason.Item1, out int buy, out int sell) && buy == 0 && sell == 0,
                "ineligible price: " + reason.Item1);
            Refused(LocalCrafting.PlanBatch(View(), reason.Item1, CraftDirection.Craft, 1, 0));
        }
        foreach (string id in LocalWelcomeGift.Cards.Select(card => card.CardId).Distinct()) {
            Check(LocalCrafting.GetIneligibility(id) == CraftIneligibility.GrantOnly, "welcome gift is grant-only: " + id);
            Refused(LocalCrafting.PlanBatch(View(100000), id, CraftDirection.Craft, 1, 0));
            foreach (int premium in new[] { 0, 1, 2, 3 }) {
                Check(!LocalCrafting.TryGetPrice(id, premium, out int buy, out int sell) && buy == 0 && sell == 0,
                    "grant-only has no price at any quality: " + id);
                foreach (int count in new[] { 1, 3 }) {
                    var gifts = new CraftLedgerView { Dust = 100000,
                        Counts = new Dictionary<string, int> { [id] = 5 }, Acquired = new Dictionary<string, int> { [id] = 2 } };
                    Refused(LocalCrafting.PlanBatch(gifts, id, CraftDirection.Craft, count, 5, premium));
                    Refused(LocalCrafting.PlanBatch(gifts, id, CraftDirection.Disenchant, count, 5, premium));
                    Refused(LocalCrafting.PlanMassDisenchant(gifts));
                }
            }
        }
        Check(LocalCrafting.GetIneligibility(null) == CraftIneligibility.UnknownCard
            && LocalCrafting.GetIneligibility("") == CraftIneligibility.UnknownCard, "empty IDs");
        Check(LocalCrafting.Playset("CATA_458") == 1 && LocalCrafting.Playset("CATA_452") == 2, "playsets");

        var view = View();
        var plan = LocalCrafting.PlanBatch(view, "CATA_452", CraftDirection.Craft, 2, 0);
        Check(plan.Ok && plan.DustDelta == -80 && plan.Cards.Count == 1, "two-card craft cost");
        var delta = plan.Cards.Single();
        Check(delta.CardId == "CATA_452" && delta.CountBefore == 0 && delta.CountDelta == 2
            && delta.AcquiredBefore == 0 && delta.AcquiredAfter == 2, "two-card craft ownership/history");
        Check(view.Dust == 1000 && view.Counts["CATA_452"] == 0 && view.Acquired["CATA_452"] == 0, "planning is pure");
        Refused(LocalCrafting.PlanBatch(View(1000, 2, 2), "CATA_452", CraftDirection.Craft, 3, 2));
        Refused(LocalCrafting.PlanBatch(View(1000, 1, 1), "CATA_452", CraftDirection.Craft, 2, 1));
        plan = LocalCrafting.PlanBatch(View(0, 1, 1), "CATA_452", CraftDirection.Disenchant, 1, 1);
        Check(plan.Ok && plan.Cards.Single().AcquiredAfter == 1, "first disenchant preserves lifetime acquisition");
        delta = plan.Cards.Single();
        var recraft = View(40, delta.CountBefore + delta.CountDelta, delta.AcquiredAfter);
        plan = LocalCrafting.PlanBatch(recraft, "CATA_452", CraftDirection.Craft, 1, 0);
        Check(plan.Ok && plan.Cards.Single().AcquiredBefore == 1 && plan.Cards.Single().AcquiredAfter == 2,
            "recraft after disenchant increments lifetime acquisition");
        plan = LocalCrafting.PlanBatch(View(40, 0, 2), "CATA_452", CraftDirection.Craft, 1, 0);
        Check(plan.Ok && plan.Cards.Single().AcquiredAfter == 2, "recraft preserves acquired");
        plan = LocalCrafting.PlanBatch(View(0, 2, 2), "CATA_452", CraftDirection.Disenchant, 2, 2);
        Check(plan.Ok && plan.DustDelta == 40 && plan.Cards.Single().CountDelta == -2
            && plan.Cards.Single().AcquiredBefore == 2 && plan.Cards.Single().AcquiredAfter == 2, "disenchant to zero preserves history");
        Refused(LocalCrafting.PlanBatch(View(0, 2, 2), "CATA_452", CraftDirection.Disenchant, 3, 2));
        Refused(LocalCrafting.PlanBatch(View(79), "CATA_452", CraftDirection.Craft, 2, 0));
        Refused(LocalCrafting.PlanBatch(View(), "CATA_452", CraftDirection.Craft, 1, 1));
        foreach (int count in new[] { 0, -1, int.MinValue })
            Refused(LocalCrafting.PlanBatch(View(), "CATA_452", CraftDirection.Craft, count, 0));
        Refused(LocalCrafting.PlanBatch(View(), "CATA_452", (CraftDirection)42, 1, 0));
        view = View(); view.PackRevealPending = true;
        Refused(LocalCrafting.PlanBatch(view, "CATA_452", CraftDirection.Craft, 1, 0));
        Refused(LocalCrafting.PlanMassDisenchant(view));
        Refused(LocalCrafting.PlanBatch(View(long.MaxValue, int.MaxValue, 2), "CATA_452", CraftDirection.Craft, 1, int.MaxValue));
        Refused(LocalCrafting.PlanBatch(View(long.MaxValue, 1, 2), "CATA_452", CraftDirection.Disenchant, 1, 1));
        Refused(LocalCrafting.PlanBatch(View(long.MaxValue), "CATA_452", CraftDirection.Craft, int.MaxValue, 0));
        plan = LocalCrafting.PlanBatch(View(0, int.MaxValue, 2), "CATA_452", CraftDirection.Disenchant, int.MaxValue, int.MaxValue);
        Check(plan.Ok && plan.DustDelta == 42949672940L && plan.Cards.Single().AcquiredAfter == 2,
            "packed extras stay disenchantable with long arithmetic");
        view = new CraftLedgerView { Dust = 0, Counts = new Dictionary<string, int>(), Acquired = new Dictionary<string, int>() };
        plan = LocalCrafting.PlanBatch(view, "CATA_458", CraftDirection.Craft, 2, 0);
        Refused(plan);
        view.Dust = 3200;
        plan = LocalCrafting.PlanBatch(view, "CATA_458", CraftDirection.Craft, 2, 0);
        Refused(plan);
        plan = LocalCrafting.PlanBatch(view, "CATA_458", CraftDirection.Craft, 1, 0);
        Check(plan.Ok && plan.DustDelta == -1600 && plan.Cards.Single().AcquiredAfter == 1, "legendary craft history cap");
        view.Counts = new Dictionary<string, int> { ["CATA_458"] = 1 };
        Refused(LocalCrafting.PlanBatch(view, "CATA_458", CraftDirection.Craft, 1, 1));

        view = new CraftLedgerView { Dust = 17,
            Counts = new Dictionary<string, int> { ["CATA_452"] = 5, ["CATA_458"] = 3, ["EDR_001"] = 2,
                ["CS2_029"] = 8, ["CORE_EX1_012"] = 9, ["GVG_001"] = 7, ["NO_SUCH_CARD"] = 5, ["CATA_EVENT_000"] = 5, ["BE_036"] = 5 },
            Acquired = new Dictionary<string, int> { ["CATA_452"] = 2, ["CATA_458"] = 1 } };
        plan = LocalCrafting.PlanMassDisenchant(view);
        Check(plan.Ok && plan.DustDelta == 1660 && plan.Cards.Count == 2, "mass credits eligible extras once");
        Check(plan.Cards.Single(c => c.CardId == "CATA_452").CountDelta == -3
            && plan.Cards.Single(c => c.CardId == "CATA_458").CountDelta == -2
            && plan.Cards.All(c => c.AcquiredBefore == c.AcquiredAfter), "mass keeps playsets/history");
        Check(view.Dust == 17 && view.Counts["CATA_452"] == 5 && view.Counts["CATA_458"] == 3, "mass planning is pure");
        view.Dust = long.MaxValue - 100;
        Refused(LocalCrafting.PlanMassDisenchant(view));
        view = View(0, 2, 2);
        Refused(LocalCrafting.PlanMassDisenchant(view));
        Refused(LocalCrafting.PlanMassDisenchant(View()));
        Refused(LocalCrafting.PlanBatch(null, "CATA_452", CraftDirection.Craft, 1, 0));
        Refused(LocalCrafting.PlanMassDisenchant(null));
        Refused(LocalCrafting.PlanBatch(View(-1), "CATA_452", CraftDirection.Craft, 1, 0));
        Refused(LocalCrafting.PlanBatch(View(1000, -1), "CATA_452", CraftDirection.Craft, 1, -1));
        // #74 craft tokens: a Normal craft of one copy of a held token's rarity costs 0 dust and names that token.
        CraftLedgerView Tokened(long dust, string id, int owned, LocalCardTokens tokens) => new CraftLedgerView { Dust = dust,
            Counts = new Dictionary<string, int> { [id] = owned }, Acquired = new Dictionary<string, int> { [id] = owned }, Tokens = tokens };
        foreach (var rarity in new[] { ("CATA_452", LocalTokenRarity.Common), ("EDR_001", LocalTokenRarity.Rare),
            ("CATA_493", LocalTokenRarity.Epic), ("CATA_458", LocalTokenRarity.Legendary) })
            Check(LocalCrafting.TryGetTokenRarity(rarity.Item1, out var found) && found == rarity.Item2, "token rarity: " + rarity.Item1);
        Check(!LocalCrafting.TryGetTokenRarity("CORE_EX1_012", out _) && !LocalCrafting.TryGetTokenRarity("EX1_008", out _)
            && !LocalCrafting.TryGetTokenRarity(null, out _), "uncraftable cards take no token");
        var legendaryToken = new LocalCardTokens { Legendary = 1 };
        plan = LocalCrafting.PlanBatch(Tokened(0, "CATA_458", 0, legendaryToken), "CATA_458", CraftDirection.Craft, 1, 0);
        Check(plan.Ok && plan.DustDelta == 0 && plan.TokenRarity == LocalTokenRarity.Legendary && plan.Cards.Single().CountDelta == 1
            && plan.Cards.Single().AcquiredAfter == 1 && legendaryToken.Legendary == 1, "legendary token craft with 0 dust; planning is pure");
        plan = LocalCrafting.PlanBatch(Tokened(40, "CATA_452", 0, legendaryToken), "CATA_452", CraftDirection.Craft, 1, 0);
        Check(plan.Ok && plan.DustDelta == -40 && plan.TokenRarity == null, "non-matching token: full dust");
        Refused(LocalCrafting.PlanBatch(Tokened(0, "CATA_452", 0, legendaryToken), "CATA_452", CraftDirection.Craft, 1, 0));
        var epicTokens = new LocalCardTokens { Epic = 2 };
        Refused(LocalCrafting.PlanBatch(Tokened(800, "CATA_493", 0, epicTokens), "CATA_493", CraftDirection.Craft, 2, 0));
        plan = LocalCrafting.PlanBatch(Tokened(0, "CATA_493", 1, epicTokens), "CATA_493", CraftDirection.Craft, 1, 1);
        Check(plan.Ok && plan.DustDelta == 0 && plan.TokenRarity == LocalTokenRarity.Epic, "second epic copy, second token");
        plan = LocalCrafting.PlanBatch(Tokened(0, "CATA_493", 2, epicTokens), "CATA_493", CraftDirection.Disenchant, 1, 2);
        Check(plan.Ok && plan.DustDelta == 200 && plan.TokenRarity == null, "disenchant ignores tokens and pays the normal value");
        Refused(LocalCrafting.PlanBatch(Tokened(10000, "CATA_458", 0, legendaryToken), "CATA_458", CraftDirection.Craft, 1, 0, 1));
        Refused(LocalCrafting.PlanBatch(Tokened(10000, "CATA_458", 1, legendaryToken), "CATA_458", CraftDirection.Craft, 1, 1));
        Refused(LocalCrafting.PlanBatch(Tokened(10000, "CATA_458", 0, new LocalCardTokens { Legendary = 1, Rare = -1 }),
            "CATA_458", CraftDirection.Craft, 1, 0));
        plan = LocalCrafting.PlanBatch(Tokened(1600, "CATA_458", 0, null), "CATA_458", CraftDirection.Craft, 1, 0);
        Check(plan.Ok && plan.DustDelta == -1600 && plan.TokenRarity == null, "no tokens: unchanged price");
        var massTokens = Tokened(0, "CATA_458", 3, legendaryToken);
        plan = LocalCrafting.PlanMassDisenchant(massTokens);
        Check(plan.Ok && plan.DustDelta == 1600 && plan.TokenRarity == null, "mass disenchant unaffected by tokens");
        Console.WriteLine("PASS craft token planning: rarity match, one copy, no premium, disenchant and mass unaffected");
        Console.WriteLine("PASS crafting prices, eligibility, batch and mass planning");
    }
}
