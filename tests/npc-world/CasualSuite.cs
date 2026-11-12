using System;
using System.Collections.Generic;
using System.Linq;

// #37 Standard Casual: casual-decks-v1.json catalog, the merged library leaving the ranked world untouched, the Casual queue
// (75/25 mix, no-repeat rules, hidden-rating targeting, hosts, determinism), fairness of the Casual profile and the
// runtime seams (Casual decks served only while referenced).
internal static class CasualSuite
{
    internal static void Run()
    {
        var personalities = PersonalityCatalog.Parse(Program.Data("personalities-v1.json"));
        var notables = NpcNotables.Parse(Program.Data("notables-v1.json"));
        var names = NpcNameTables.Parse(Program.Data("names-v1.json"));
        var cosmetics = NpcCosmeticPool.Parse(Program.Data("cosmetics-v1.json"));
        var skills = SkillCatalog.Parse(Program.Data("skills-v1.json"));
        var ranked = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        var catalog = CasualDeckCatalog.Parse(Program.Data("casual-decks-v1.json"));

        Catalog(catalog, ranked, notables);
        var merged = catalog.MergeInto(ranked);
        var rankedWorld = NpcWorld.Generate(new NpcWorldCatalogs(ranked, skills, personalities, notables, names, cosmetics));
        var world = NpcWorld.Generate(new NpcWorldCatalogs(merged, skills, personalities, notables, names, cosmetics));
        RankedWorldUnchanged(rankedWorld, world, ranked, merged);
        var casual = new CasualMatchmaker(world, catalog);
        Queue(world, casual, catalog);
        Fairness(world, catalog);
        Runtime(world, catalog);
        BootResilience(ranked, catalog, skills, personalities, notables, names, cosmetics);
        Console.WriteLine("casual suite passed");
    }

    private static void Catalog(CasualDeckCatalog catalog, NpcDeckLibrary ranked, IReadOnlyList<NpcIdentity> notables)
    {
        Program.Require(catalog.Decks.Count >= 3 && catalog.Decks.Count <= 12, "Casual deck count: " + catalog.Decks.Count);
        Program.Require(catalog.Decks.All(d => d.Admission == "casual" && !d.Admitted && d.Id.StartsWith("npc-casual-", StringComparison.Ordinal)),
            "every Casual deck is admission casual (never served by the ladder)");
        Program.Require(catalog.Decks.All(d => d.Tags.Fun >= 4 && d.Tags.Annoyance <= 1 && d.ExpandedCardIds().Count() == LocalDecks.DeckSize(d.ExpandedCardIds())),
            "Casual decks: fun, low annoyance, 30 cards (40 for Fabled+ Rafaam)");
        Program.Require(catalog.Library.Validate().Count == 0, "Casual catalog structure: " + string.Join("; ", catalog.Library.Validate()));
        Program.Require(catalog.Decks.All(d => ranked.Find(d.Id) == null), "Casual ids never collide with ranked decks");
        Program.Require(catalog.HostDeck.Count > 0 && catalog.HostDeck.Keys.All(id => notables.Any(n => n.Id == id)), "Casual hosts are notables");
        var bad = new List<string>();
        try { CasualDeckCatalog.Parse(Program.Data("decks-v1.json")); } catch (System.IO.InvalidDataException) { bad.Add("ranked"); }
        Program.Require(bad.Count == 1, "a ranked library is refused as a Casual catalog");
    }

    // The merged library adds only non-admitted decks: generation, deck pools, skills and inference see the same world.
    private static void RankedWorldUnchanged(NpcWorld rankedWorld, NpcWorld world, NpcDeckLibrary ranked, NpcDeckLibrary merged)
    {
        Program.Require(rankedWorld.All.Count == world.All.Count, "same NPC count");
        for (int i = 0; i < world.All.Count; i++)
        {
            var a = rankedWorld.All[i]; var b = world.All[i];
            Program.Require(a.Id == b.Id && a.DisplayName == b.DisplayName && a.HomeRating == b.HomeRating && a.PersonalityId == b.PersonalityId
                && a.Taste == b.Taste && a.SkillId == b.SkillId && a.DeckIds.SequenceEqual(b.DeckIds) && a.CardBack == b.CardBack
                && a.HeroSkins.OrderBy(p => p.Key).SequenceEqual(b.HeroSkins.OrderBy(p => p.Key)), "Casual decks change no ranked NPC: " + a.Id);
        }
        Program.Require(ranked.Population().Select(p => p.Id).SequenceEqual(merged.Population().Select(p => p.Id)),
            "deck inference population is unchanged");
        Program.Require(world.All.All(n => n.DeckIds.All(id => !id.StartsWith("npc-casual-", StringComparison.Ordinal))), "no NPC owns a Casual deck on the ladder");
    }

    private static CasualRequest Request(int index, double rating, IEnumerable<PastEncounter> history = null, long seed = 4242) =>
        new CasualRequest(seed, index, 37, rating, history ?? new PastEncounter[0]);

    private static void Queue(NpcWorld world, CasualMatchmaker casual, CasualDeckCatalog catalog)
    {
        var funNpcs = world.All.Count(CasualMatchmaker.IsFunNpc);
        Program.Require(funNpcs > 300 && funNpcs < world.All.Count * 0.6, "Casual-leaning roster size: " + funNpcs);
        foreach (var host in catalog.HostDeck)
            Program.Require(casual.CasualDecksFor(world.Find(host.Key))[0].Id == host.Value, "host signature deck: " + host.Key);
        Program.Require(world.All.Where(CasualMatchmaker.IsFunNpc).Take(50).All(n => casual.CasualDecksFor(n).Count == 2
            && casual.CasualDecksFor(n).Distinct().Count() == 2), "fun NPCs carry two distinct Casual decks");
        // The three Rafaam lists are one family (owner 2026-10-28): it weighs as one deck and no NPC holds two of them.
        var rafaam = catalog.Decks.Where(d => catalog.FamilyOf(d.Id) == "rafaam").Select(d => d.Id).ToArray();
        Program.Require(rafaam.Length == 3 && catalog.FamilyOf("npc-casual-dragon-hoard") == "npc-casual-dragon-hoard", "Rafaam family: " + string.Join(",", rafaam));
        var funWorld = world.All.Where(CasualMatchmaker.IsFunNpc).ToArray();
        Program.Require(funWorld.All(n => casual.CasualDecksFor(n).Count(d => rafaam.Contains(d.Id)) <= 1), "no NPC holds two Rafaam lists");
        double rafaamHolders = funWorld.Count(n => casual.CasualDecksFor(n).Any(d => rafaam.Contains(d.Id))) / (double)funWorld.Length;
        int families = catalog.Decks.Select(d => catalog.FamilyOf(d.Id)).Distinct().Count();
        Program.Require(rafaamHolders < 3.0 / families, "Rafaam weighs as one family: " + rafaamHolders.ToString("F3") + " of fun NPCs");
        Program.Require(world.All.Where(n => !CasualMatchmaker.IsFunNpc(n) && !catalog.HostDeck.ContainsKey(n.Id)).All(n => casual.CasualDecksFor(n).Count == 0),
            "other NPCs carry no Casual deck");

        var a = casual.Choose(Request(9, 1450)); var b = new CasualMatchmaker(world, catalog).Choose(Request(9, 1450));
        Program.Require(a.Encounter.Npc.Id == b.Encounter.Npc.Id && a.Encounter.Deck.Id == b.Encounter.Deck.Id
            && a.Encounter.MatchSeed == b.Encounter.MatchSeed && a.Target == b.Target, "Casual queue is deterministic");

        // A Casual season of 600 games: the 75/25 mix, the no-repeat rules and the deck sources hold.
        var history = new List<PastEncounter>();
        int fun = 0, relaxed = 0;
        var casualIds = new HashSet<string>(catalog.Decks.Select(d => d.Id));
        for (int i = 0; i < 600; i++)
        {
            var pick = casual.Choose(Request(i, 1450, history));
            var e = pick.Encounter;
            if (pick.RelaxLevel > 0) relaxed++;
            bool funPick = e.Reason == "casual-fun";
            if (funPick) fun++;
            Program.Require(funPick ? casualIds.Contains(e.Deck.Id) && casual.CasualDecksFor(e.Npc).Contains(e.Deck)
                : e.Deck.Admitted && e.Deck.Tags.Fun >= 4 && e.Npc.DeckIds.Contains(e.Deck.Id), "deck source of pick " + i);
            Program.Require(!history.Skip(Math.Max(0, history.Count - 5)).Any(p => p.NpcId == e.Npc.Id), "no same NPC within 5 at " + i);
            Program.Require(!history.Skip(Math.Max(0, history.Count - 3)).Any(p => catalog.FamilyOf(p.DeckId) == catalog.FamilyOf(e.Deck.Id)), "no same deck family within 3 at " + i);
            history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, i % 2 == 0 ? "Won" : "Lost"));
        }
        Program.Require(Math.Abs(fun / 600.0 - 0.75) < 0.05, "fun share near 75%: " + fun);
        Program.Require(relaxed == 0, "queue rules never relaxed in the real world: " + relaxed);

        // The hidden Casual rating moves the opponent: a strong and a weak player meet differently rated NPCs.
        double Mean(double rating) => Enumerable.Range(0, 200).Average(i => casual.Choose(Request(i, rating)).Encounter.Rating);
        double low = Mean(1050), high = Mean(1750);
        Program.Require(Math.Abs(low - 1050) < 120 && Math.Abs(high - 1750) < 120 && high - low > 500, "rating targeting: " + low + " / " + high);
        Program.Require(Enumerable.Range(0, 200).All(i => { var e = casual.Choose(Request(i, 1400)).Encounter; return e.Skill != null && e.Personality != null; }),
            "every Casual encounter has a skill tier and personality");
    }

    // Casual changes presentation only: the pilot inputs (plan, budget, pacing, concession) equal the ranked build.
    private static void Fairness(NpcWorld world, CasualDeckCatalog catalog)
    {
        foreach (var personality in world.Catalogs.Personalities.Archetypes)
        {
            var npc = world.All.First(n => n.PersonalityId == personality.Id);
            var deck = catalog.Decks[0];
            var skill = world.Catalogs.Skills.Find(npc.SkillId) ?? world.Catalogs.Skills.Tiers[0];
            var rankedProfile = NpcEncounterProfiles.Build(npc, deck, personality, skill);
            var casualProfile = CasualProfiles.Build(npc, deck, personality, skill);
            var r = rankedProfile.Personality; var c = casualProfile.Personality;
            Program.Require(casualProfile.Id == rankedProfile.Id && casualProfile.DeckId == rankedProfile.DeckId && casualProfile.PlanId == rankedProfile.PlanId
                && ReferenceEquals(casualProfile.Budget, rankedProfile.Budget) && casualProfile.HeroSkin == rankedProfile.HeroSkin
                && c.PreTurnMinSeconds == r.PreTurnMinSeconds && c.PreTurnMaxSeconds == r.PreTurnMaxSeconds && c.ActionMinSeconds == r.ActionMinSeconds
                && c.ActionMaxSeconds == r.ActionMaxSeconds && c.ReplyBlendBeta == r.ReplyBlendBeta && c.ConcessionWillingness == r.ConcessionWillingness
                && c.ConcedeFromTurn == r.ConcedeFromTurn, "Casual profile keeps every pilot input: " + personality.Id);
            bool fun = CasualMatchmaker.FunArchetypes.Contains(personality.Id);
            Program.Require(fun ? c.Emotes.Rules.Count == r.Emotes.Rules.Count + CasualProfiles.OverlayRules.Count : ReferenceEquals(c.Emotes, r.Emotes),
                "Casual emote overlay only for fun archetypes: " + personality.Id);
            Program.Require(c.Emotes.Rules.Where(rule => !r.Emotes.Rules.Contains(rule)).All(rule => !rule.Mock && rule.Emote != "Threaten"
                && PersonalityCatalog.BotEmotes.Contains(rule.Emote)), "overlay emotes are friendly native emotes: " + personality.Id);
            foreach (var trigger in r.Emotes.Rules.Select(rule => rule.Trigger).Distinct())
                Program.Require(r.Emotes.Rules.Where(rule => rule.Trigger == trigger)
                    .SequenceEqual(c.Emotes.Rules.Where(rule => rule.Trigger == trigger && !CasualProfiles.OverlayRules.Contains(rule))),
                    "own rules keep their order after the overlay: " + personality.Id);
        }
    }

    // Review 2026-10-28: a Casual id colliding with a ranked deck, or a deck that does not build, is dropped alone; the ranked
    // world and the rest of the Casual queue still install.
    private static void BootResilience(NpcDeckLibrary ranked, CasualDeckCatalog catalog, SkillCatalog skills, PersonalityCatalog personalities,
        IReadOnlyList<NpcIdentity> notables, NpcNameTables names, NpcCosmeticPool cosmetics)
    {
        var template = catalog.Decks[0];
        NpcDeckEntry Copy(string id, IEnumerable<NpcDeckCard> cards) => new NpcDeckEntry(id, 1, template.Name, template.Class, template.Hero,
            template.Origin, "casual", null, template.Tags, template.Source, cards, template.Sideboards);
        var colliding = Copy(ranked.Admitted.First().Id, template.Cards);
        var broken = Copy("npc-casual-broken", new[] { new NpcDeckCard("NOT_A_CARD_001", 2) }.Concat(template.Cards.Skip(1)));
        var bad = new CasualDeckCatalog(new NpcDeckLibrary(1, 1, ranked.ActiveMeta, catalog.Decks.Concat(new[] { colliding, broken })), catalog.HostDeck.ToDictionary(p => p.Key, p => p.Value));
        var lines = new List<string>();
        NpcWorldRuntime.Uninstall();
        try
        {
            bool installed = NpcWorldRuntime.TryInstallClient(() => NpcWorld.Generate(new NpcWorldCatalogs(bad.MergeInto(ranked), skills, personalities,
                notables, names, cosmetics)), _ => null, lines.Add, bad);
            Program.Require(installed && NpcWorldRuntime.Installed && ReferenceEquals(NpcWorldRuntime.World.Catalogs.Decks.Find(colliding.Id), ranked.Find(colliding.Id)),
                "a colliding Casual id never replaces the ranked deck: " + string.Join(" | ", lines));
            Program.Require(NpcWorldRuntime.Casual != null && NpcWorldRuntime.Casual.Catalog.Decks.Count == catalog.Decks.Count
                && NpcWorldRuntime.Casual.Catalog.Decks.All(d => d.Id != colliding.Id && d.Id != broken.Id),
                "only the colliding and the broken Casual decks are dropped: " + string.Join(" | ", lines));
            Program.Require(lines.Count(line => line.Contains("casual deck dropped")) == 2, "both drops are logged: " + string.Join(" | ", lines));
        }
        finally { NpcWorldRuntime.Uninstall(); }
    }

    private static void Runtime(NpcWorld world, CasualDeckCatalog catalog)
    {
        NpcWorldRuntime.Uninstall();
        var rankedOnly = NpcWorld.Generate(new NpcWorldCatalogs(NpcDeckLibrary.Parse(Program.Data("decks-v1.json")), world.Catalogs.Skills,
            world.Catalogs.Personalities, world.Catalogs.Notables, world.Catalogs.Names, world.Catalogs.Cosmetics));
        NpcWorldRuntime.Install(rankedOnly, new LadderMatchmaker(rankedOnly));
        try
        {
            bool refused = false;
            try { NpcWorldRuntime.InstallCasual(catalog); } catch (InvalidOperationException) { refused = true; }
            Program.Require(refused && NpcWorldRuntime.Casual == null, "an unmerged Casual catalog is refused");
        }
        finally { NpcWorldRuntime.Uninstall(); }

        NpcWorldRuntime.Install(world, new LadderMatchmaker(world));
        try
        {
            NpcWorldRuntime.InstallCasual(catalog);
            NpcWorldRuntime.InstallClientSeams(_ => null);
            var e = NpcWorldRuntime.Casual.Next(Request(3, 1400));
            Program.Require(NpcWorldRuntime.DeckForId(catalog.Decks[0].Id) == null, "Casual decks are not served without an encounter");
            NpcWorldRuntime.Begin(e);
            var deck = NpcWorldRuntime.DeckForId(e.Deck.Id);
            Program.Require(deck != null && deck.Opponent == e.Npc.DisplayName && deck.CardIds.Count == LocalDecks.DeckSize(e.Deck.ExpandedCardIds()), "the active Casual encounter's deck resolves");
            Program.Require(NpcWorldRuntime.PlanForId(e.Deck.EffectivePlanId) != null, "Casual decks get a derived plan");
            NpcWorldRuntime.End();
            Program.Require(NpcWorldRuntime.DeckForId(e.Deck.Id) == null, "End unpins the Casual deck");
        }
        finally { NpcWorldRuntime.Uninstall(); }
        Program.Require(NpcWorldRuntime.Casual == null, "Uninstall clears the Casual queue");
    }
}
