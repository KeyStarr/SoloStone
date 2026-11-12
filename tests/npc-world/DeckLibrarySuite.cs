using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

// WP1 owns this file: library schema, admission import, AutoDeckPlan derivation, seams S2/S3/S5.
internal static class DeckLibrarySuite
{
    internal static void Run()
    {
        var library = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        Program.Require(library.ActiveMeta == "violet-hold", "decks-v1 activeMeta");
        var problems = library.Validate();
        Program.Require(problems.Count == 0, "decks-v1 Validate: " + string.Join("; ", problems.Take(5)));
        Program.Require(library.Decks.All(d => d.Tags.MinBand <= d.Tags.MaxBand), "bands ordered");
        var parsed = NpcWorldJson.Parse(Program.Data("decks-v1.json"));
        var twice = NpcDeckLibrary.Load(parsed);
        Program.Require(NpcDeckLibrary.Load(parsed).Find("npc-recipe-manastorm-mage").ExpandedCardIds()
            .SequenceEqual(twice.Find("npc-recipe-manastorm-mage").ExpandedCardIds()), "NPC fallback load leaves parsed input unchanged");
        foreach (var field in new[] { "needs", "branch" })
        {
            var root = NpcWorldJson.Object(NpcWorldJson.Parse(Program.Data("decks-v1.json")));
            var row = NpcWorldJson.List(root, "decks").Cast<object>().Select(NpcWorldJson.Object).First(d => d.ContainsKey("pending"));
            NpcWorldJson.Object(row["pending"])[field] = field == "needs" ? (object)new List<object>() : " ";
            bool rejected = false;
            try { NpcDeckLibrary.Load(root); } catch (System.IO.InvalidDataException) { rejected = true; }
            Program.Require(rejected, "NPC pending rejects invalid " + field);
        }

        Program.Require(library.Admitted.Count() >= 3, "admitted decks");
        foreach (var pair in new[] { new[] { "npc-recipe-egg-death-knight", "BE_EVENT_100", "JAIL_454" },
            new[] { "npc-recipe-manastorm-mage", "BE_EVENT_101", "CORE_CS2_023" } })
        {
            var entry = library.Find(pair[0]);
            bool admitted = !NpcDeckLibrary.IsBlackEmpire(pair[1]);
            Program.Require(entry.Cards.Any(card => card.Id == pair[1]) == admitted
                && (admitted || entry.Source.Changes.Contains(pair[2])), "NPC catalog fallback " + pair[0]);
            Playable(entry);
        }
        foreach (var id in new[] { "npc-recipe-aya-coin-rogue", "npc-recipe-mug-shaman" }) Playable(library.Find(id));
        foreach (var pair in new[] { new[] { "test-standard-extra-demonhunter", "BE_036" }, new[] { "test-standard-mecha-priest", "BE_EVENT_102" },
            new[] { "test-standard-mecha-deathknight-2", "BE_EVENT_100" }, new[] { "test-standard-b11-mage", "BE_EVENT_101" } })
            Program.Require(LocalDecks.Get(pair[0]).CardIds.Contains(pair[1]) == LocalCardCatalog.InActiveMeta(Cards.FromId(pair[1]))
                && LocalDecks.Get(pair[0]).Build().Count == 30, "loaner seat follows installed catalog " + pair[0]);

        var thief = library.Find("npc-priest-thief");
        Program.Require(thief.Admitted && thief.Tags.Allows(LadderBand.Legend), "Azalina is admitted at Legend");
        Program.Require(library.LocalDeckResolver()(thief.Id).Build().Count == 20, "Azalina resolves and builds twenty cards");
        Program.Require(library.Population().Any(deck => deck.Id == thief.Id), "Azalina is in the admitted population");

        // No shipped deck is pending now, so measurement mode is pinned on a copy with one deck turned back to pending.
        var pendingRoot = NpcWorldJson.Object(NpcWorldJson.Parse(Program.Data("decks-v1.json")));
        NpcWorldJson.List(pendingRoot, "decks").Cast<object>().Select(NpcWorldJson.Object).First(d => (string)d["id"] == thief.Id)["admission"] = "pending";
        var pendingLibrary = NpcDeckLibrary.Load(pendingRoot);
        Program.Require(!pendingLibrary.Find(thief.Id).Admitted, "a pending deck is not admitted");
        Program.Require(pendingLibrary.LocalDeckResolver(admittedOnly: false)(thief.Id).Build().Count == 20,
            "a pending deck resolves and builds for measurement");
        Program.Require(pendingLibrary.LocalDeckResolver()(thief.Id) == null && pendingLibrary.Population().All(deck => deck.Id != thief.Id),
            "a pending deck stays outside the admitted population");

        // Every playable deck (admitted, or pending on measurement only) builds, is in the active meta, has no BE card,
        // and derives a usable plan.
        var playable = library.Decks.Where(d => d.Admission != "rejected" && d.Cards.Sum(c => c.Count) == LocalDecks.DeckSize(d.ExpandedCardIds())).ToArray();
        Program.Require(playable.Any(d => d.Admitted && d.Cards.Sum(c => c.Count) == 40), "an admitted 40-card Fabled+ deck is checked");
        foreach (var entry in playable) Playable(entry);

        Program.Require(DeckPlans.All.All(p => p.Holds.All(h => h.Condition != HoldCondition.AoeWideBoard)), "golden plans unchanged (no AoE hold)");
        Classifier();
        Casual();
        Golden(library);
        Seams(library);
        StarterInference(library);
        Console.WriteLine("PASS deck library: " + library.Decks.Count + " decks (" + library.Admitted.Count() + " admitted, "
            + library.Admitted.Select(d => d.Class).Distinct().Count() + " classes), " + playable.Length + " build + derive");
    }

    // #63 (owner 2026-10-28): the 11 Core starters are inference candidates beside the library lists, scored by revealed
    // cards only, and never NPC decks: not in the library, its population, a band pool or the NPC deck resolver.
    private static void StarterInference(NpcDeckLibrary library)
    {
        var starters = LocalDecks.PlayerCoreStarterDecks;
        Program.Require(starters.Count == 11, "11 Core starters");
        Program.Require(starters.All(d => library.Find(d.Id) == null && library.LocalDeckResolver(admittedOnly: false)(d.Id) == null)
            && library.Population().All(p => starters.All(d => d.Id != p.Id))
            && Enum.GetValues(typeof(LadderBand)).Cast<LadderBand>().All(band => library.ForBand(band).All(e => starters.All(d => d.Id != e.Id))),
            "starters are not NPC library decks, population or band pools");
        var population = library.Population();
        try
        {
            NpcWorldHooks.Population = () => population;
            var candidates = DeckInference.Candidates();
            Program.Require(candidates.Take(population.Count).Select(d => d.Id).SequenceEqual(population.Select(d => d.Id))
                && candidates.Skip(population.Count).Select(d => d.Id).SequenceEqual(starters.Select(d => d.Id)),
                "inference candidates: library lists, then the 11 starters");
            foreach (var starter in starters)
            {
                var heroClass = LocalDecks.HeroClass(starter.Hero);
                var listed = new HashSet<string>(population.Where(d => d.Class == heroClass).SelectMany(d => d.Cards), StringComparer.Ordinal);
                var tells = starter.CardIds.Where(id => !listed.Contains(id)).Distinct().Take(2).ToArray();
                var posterior = DeckInference.Posterior(heroClass, tells);
                Program.Require(tells.Length == 2 && posterior.OrderByDescending(p => p.Value).First().Key == starter.Id,
                    "two starter-only cards read as the starter: " + starter.Id);
                // Flat prior: before any card, a starter weighs exactly one library list of its class.
                var empty = DeckInference.Posterior(heroClass, new string[0]);
                Program.Require(empty.All(p => Math.Abs(p.Value - empty[0].Value) < 1e-12) && empty.Any(p => p.Key == starter.Id),
                    "flat starter prior: " + starter.Id);
                Program.Require(!DeckInference.Posterior(heroClass, tells, starters.Select(d => d.Id)).Any(p => p.Key == starter.Id),
                    "excludedDecks drops a starter like any list");
            }
        }
        finally { NpcWorldHooks.Reset(); }
    }

    // Builds, active-meta cards only, BE window admissions allowed, and a usable derived plan (ranked and casual libraries alike).
    private static void Playable(NpcDeckEntry entry)
    {
        foreach (var id in new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" })
            Program.Require(NpcDeckLibrary.IsBlackEmpire(id) == !LocalCardCatalog.ActiveMetaExtraCards.Contains(id), "installed window admission " + id);
        foreach (var id in new[] { "BE_037", "BE_EVENT_103", "CORE_BE_1" })
            Program.Require(NpcDeckLibrary.IsBlackEmpire(id), "unadmitted BE stays banned " + id);
        var deck = NpcDeckLibrary.ToLocalDeck(entry, "Probe");
        var built = deck.Build();
        Program.Require(built.Count == LocalDecks.DeckSize(entry.ExpandedCardIds()) && deck.Opponent == "Probe" && deck.Id == entry.Id, "ToLocalDeck shape " + entry.Id);
        foreach (var id in entry.ExpandedCardIds().Concat(entry.Sideboards.SelectMany(b => b.Cards.Select(c => c.Id))))
        {
            Program.Require(!NpcDeckLibrary.IsBlackEmpire(id), "BE card " + id + " in " + entry.Id);
            Program.Require(LocalCardCatalog.InActiveMeta(Cards.FromId(id)), "card outside active meta " + id + " in " + entry.Id);
        }
        var plan = AutoDeckPlan.Derive(entry);
        Program.Require(plan.Id == AutoDeckPlan.PlanIdFor(entry.Id) && plan.DeckId == entry.Id, "derived plan ids " + entry.Id);
        Program.Require(plan.MulliganRules.Count > 0 && plan.CardRoles.Count > 0 && plan.AggressionCurve.Count > 0 && plan.Weights != null,
            "derived plan is empty for " + entry.Id);
        Program.Require(plan.Hints.Count == 0, "derived plans carry no PlanHints");
        var aoe = CardRoleClassifier.ClassifyDeck(entry).Where(c => (c.Auto & AutoRole.ImmediateClear) != 0).Select(c => c.CardId).ToArray();
        Program.Require(aoe.Length == 0 || plan.Holds.Any(h => h.Condition == HoldCondition.AoeWideBoard && aoe.All(h.CardIds.Contains)),
            "AoE spells held until a wide board in " + entry.Id);
        Program.Require(plan.Holds.Where(h => h.Condition == HoldCondition.AoeWideBoard).SelectMany(h => h.CardIds)
            .All(id => Cards.FromId(id).Type == CardType.SPELL), "AoE development is not held as a clear in " + entry.Id);
        var cardIds = new HashSet<string>(entry.Cards.Select(c => c.Id));
        Program.Require(plan.MulliganRules.All(r => cardIds.Contains(r.CardId)) && plan.CardRoles.All(r => cardIds.Contains(r.CardId)),
            "derived plan references a card outside " + entry.Id);
        Program.Require(plan.MulliganRules.All(r => Cards.FromId(r.CardId).Cost <= 4), "derived mulligan keeps a card above 4 in " + entry.Id);
        Program.Require(Fingerprint(plan) == Fingerprint(AutoDeckPlan.Derive(entry)), "Derive is not deterministic for " + entry.Id);
        // A derived mulligan on the deck's first four cards keeps only cheap cards and is legal.
        var offered = deck.CardIds.Take(4).Select((id, i) => new MulliganCard(i + 1, id, Cards.FromId(id).Cost)).ToArray();
        var keep = new DeckPlanMulligan(plan).Keep(new MulliganView(offered, true, deck.CardIds, CardClass.MAGE));
        Program.Require(keep.All(k => offered.Any(c => c.EntityId == k && c.Cost <= 4)), "derived mulligan keep outside offer " + entry.Id);
    
    }

    // data/npc/casual-decks-v1.json (backlog #37): Casual-queue-only decks, separate from the ranked library.
    // Same checks as playable ranked decks; Validate() problems other than the casual-curated origin must be empty.
    private static void Casual()
    {
        var casual = NpcDeckLibrary.Parse(Program.Data("casual-decks-v1.json"));
        Program.Require(casual.ActiveMeta == "violet-hold" && casual.Decks.Count >= 1, "casual decks activeMeta");
        var problems = casual.Validate().Where(p => !p.EndsWith("bad origin casual-curated", StringComparison.Ordinal)).ToArray();
        Program.Require(problems.Length == 0, "casual Validate: " + string.Join("; ", problems.Take(5)));
        var ranked = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        foreach (var entry in casual.Decks)
        {
            Program.Require(entry.Id.StartsWith("npc-casual-", StringComparison.Ordinal) && entry.Admission == "casual"
                && entry.Origin == "casual-curated" && !entry.Admitted, "casual entry shape " + entry.Id);
            Program.Require(ranked.Find(entry.Id) == null, "casual id collides with ranked library " + entry.Id);
            Program.Require(entry.Tags.Fun == 5 && entry.Tags.Annoyance <= 1 && entry.Tags.PowerTier >= 4, "casual tags " + entry.Id);
            // Plain lists: 30 cards, or 40 for the owner-approved Timethief Rafaam decks (Fabled+, 2026-10-28).
            Program.Require(entry.Cards.Sum(c => c.Count) == (entry.Cards.Any(c => c.Id == "TIME_005") ? 40 : 30) && entry.Sideboards.Count == 0,
                "casual decks are plain 30-card (Rafaam 40-card) lists " + entry.Id);
            Playable(entry);
        }
        Console.WriteLine("PASS casual decks: " + casual.Decks.Count + " build + derive");
    }

    private static void Classifier()
    {
        Program.Require((CardRoleClassifier.Classify("CS2_029").Auto & AutoRole.Burn) != 0, "Fireball is burn");
        Program.Require((CardRoleClassifier.Classify("CS2_029").Roles & CardRole.Removal) != 0, "Fireball is removal");
        Program.Require((CardRoleClassifier.Classify("CS2_032").Auto & AutoRole.Aoe) != 0, "Flamestrike is AoE");
        Program.Require((CardRoleClassifier.Classify("CS2_182").Roles & CardRole.Development) == 0, "Chillwind (4) is not cheap development");
        Program.Require((CardRoleClassifier.Classify("CS2_172").Roles & CardRole.Development) != 0, "Bloodfen Raptor is development");
        Program.Require((CardRoleClassifier.Classify("EX1_295").Auto & AutoRole.Secret) != 0, "Ice Block is a Secret");
    }

    // Golden decks keep their hand-written plans exactly: the planId resolves statically, Overlay keeps every
    // non-empty hand list (same objects), and the library list equals the LocalDecks.NpcDecks list.
    private static void Golden(NpcDeckLibrary library)
    {
        foreach (var hand in DeckPlans.All)
        {
            var entry = library.Find(hand.DeckId);
            Program.Require(entry != null && entry.Origin == "golden" && entry.Admitted && entry.PlanId == hand.Id, "golden entry " + hand.DeckId);
            Program.Require(ReferenceEquals(DeckPlans.ForId(entry.EffectivePlanId), hand), "golden plan resolves statically " + hand.Id);
            Program.Require(NpcDeckLibrary.ToLocalDeck(entry, "x").CardIds.OrderBy(c => c, StringComparer.Ordinal)
                .SequenceEqual(LocalDecks.Get(entry.Id).CardIds.OrderBy(c => c, StringComparer.Ordinal)), "golden list drift " + entry.Id);
            var overlay = AutoDeckPlan.Overlay(AutoDeckPlan.Derive(entry), hand);
            Program.Require(overlay.Id == hand.Id && overlay.DeckId == hand.DeckId && ReferenceEquals(overlay.Weights, hand.Weights),
                "overlay identity " + hand.Id);
            Program.Require(hand.Hints.SequenceEqual(overlay.Hints) && hand.MatchupClasses.SequenceEqual(overlay.MatchupClasses), "overlay hints " + hand.Id);
            Same(hand.MulliganRules, overlay.MulliganRules, hand.Id + " mulligan");
            Same(hand.Holds, overlay.Holds, hand.Id + " holds");
            Same(hand.RemovalThresholds, overlay.RemovalThresholds, hand.Id + " thresholds");
            Same(hand.AggressionCurve, overlay.AggressionCurve, hand.Id + " aggression");
            Same(hand.DiscoverPreferences, overlay.DiscoverPreferences, hand.Id + " discover");
            Same(hand.RewindPreferences, overlay.RewindPreferences, hand.Id + " rewind");
            Program.Require(hand.CardRoles.All(r => overlay.CardRoles.Any(o => ReferenceEquals(o, r)))
                && overlay.CardRoles.Select(r => r.CardId).Distinct().Count() == overlay.CardRoles.Count, "overlay roles " + hand.Id);
        }
    }

    private static void Same<T>(IReadOnlyList<T> hand, IReadOnlyList<T> overlay, string what)
    {
        if (hand.Count == 0) return; // filled from the derived plan by design
        // DeckPlan copies its lists; the same element objects in the same order mean the hand-written list survived.
        Program.Require(hand.Count == overlay.Count && hand.Zip(overlay, (a, b) => ReferenceEquals(a, b)).All(x => x),
            "overlay replaced hand-written " + what);
    }

    private static void Seams(NpcDeckLibrary library)
    {
        var entry = library.Decks.First(d => d.Origin != "golden" && d.Cards.Sum(c => c.Count) == 30 && d.Admission != "rejected");
        string auto = AutoDeckPlan.PlanIdFor(entry.Id);
        try
        {
            // Hooks null: today's behaviour.
            Program.Require(DeckPlans.ForId(auto) == null, "auto plan resolves without the world");
            bool missing = false;
            try { LocalDecks.Get(entry.Id); } catch (InvalidOperationException) { missing = true; }
            Program.Require(missing, "library deck resolves without the world");
            var profile = new NpcProfile("npc.test.1", 1, "Tester", entry.Id, null, 0, NpcCatalog.All[0].Personality,
                NpcCatalog.All[0].Budget, auto);
            Program.Require(NpcPilots.Create(profile) == null, "pilot without a resolvable plan");
            Program.Require(NpcPilots.Create(NpcCatalog.All[0]) is GoldenPilot, "golden pilot unchanged");

            NpcWorldHooks.PlanForId = library.PlanFor;
            Program.Require(entry.Admitted || library.LocalDeckResolver()(entry.Id) == null, "ladder resolver serves admitted decks only");
            LocalDecks.ExternalDecks = library.LocalDeckResolver(id => id == entry.Id ? "Tester" : null, admittedOnly: false);
            var plan = DeckPlans.ForId(auto);
            Program.Require(plan != null && plan.DeckId == entry.Id && ReferenceEquals(plan, DeckPlans.ForId(auto)), "S2 auto plan (cached)");
            Program.Require(ReferenceEquals(DeckPlans.ForId("dragon-warrior.v1"), DeckPlans.DragonWarrior), "S2 static plans first");
            Program.Require(DeckPlans.ForId("auto:npc-no-such-deck") == null && DeckPlans.ForId("nope") == null, "S2 unknown ids");
            var local = LocalDecks.Get(entry.Id);
            Program.Require(local.Opponent == "Tester" && local.CardIds.Count == 30, "S5 external deck named for the NPC");
            Program.Require(ReferenceEquals(LocalDecks.Get("npc-dragon-warrior"), LocalDecks.NpcDecks[0]), "S5 static decks first");
            var pilot = NpcPilots.Create(profile) as GoldenPilot;
            Program.Require(pilot != null && ReferenceEquals(pilot.Plan, plan), "S3 any resolvable plan gets the golden brain");
            IGoldenPilot seen = null;
            NpcWorldHooks.DecoratePilot = (p, inner) => { seen = inner; return inner; };
            Program.Require(NpcPilots.Create(profile) is GoldenPilot && seen is GoldenPilot, "S3 DecoratePilot sees the golden pilot");
        }
        finally { NpcWorldHooks.Reset(); LocalDecks.ExternalDecks = null; }
        Guards(library, entry, auto);
    }

    // Seams never let a throwing hook abort match start: null plan (legacy search, empty mulligan), unknown deck,
    // undecorated pilot. A deck an active/pending encounter references stays resolvable even when un-admitted.
    private static void Guards(NpcDeckLibrary library, NpcDeckEntry entry, string auto)
    {
        var profile = new NpcProfile("npc.test.2", 1, "Tester", entry.Id, null, 0, NpcCatalog.All[0].Personality, NpcCatalog.All[0].Budget, auto);
        var logs = new List<string>();
        try
        {
            NpcWorldHooks.PlanForId = id => { throw new InvalidOperationException("boom plan"); };
            Program.Require(DeckPlans.ForId(auto) == null && DeckPlans.LastSeamError.Contains("boom plan"), "S2 guard");
            Program.Require(ReferenceEquals(DeckPlans.ForId("zee-shaman.v1"), DeckPlans.ZeeShaman), "S2 guard keeps static plans");
            Program.Require(NpcPilots.Create(profile, logs.Add) == null && logs.Any(l => l.Contains("unresolved")), "S3 null plan -> legacy search");
            NpcWorldHooks.PlanForId = library.PlanFor;
            NpcWorldHooks.DecoratePilot = (p, inner) => { throw new InvalidOperationException("boom skill"); };
            Program.Require(NpcPilots.Create(profile, logs.Add) is GoldenPilot && logs.Any(l => l.Contains("boom skill")), "S3 decorator guard");
            string failed = null;
            LocalDecks.ExternalDecks = id => { throw new InvalidOperationException("boom deck"); };
            LocalDecks.ExternalDecksLog = line => failed = line;
            bool unknown = false;
            try { LocalDecks.Get(entry.Id); } catch (InvalidOperationException) { unknown = true; }
            Program.Require(unknown && failed != null && failed.Contains("boom deck"), "S5 guard");
            Program.Require(LocalDecks.Get("npc-zee-shaman") == LocalDecks.NpcDecks[2], "S5 guard keeps static decks");
            var pending = library.Decks.FirstOrDefault(d => !d.Admitted && d.Admission != "rejected" && d.Cards.Sum(c => c.Count) == 30)
                ?? library.Decks.First(d => d.Admission == "rejected");
            var ladder = library.LocalDeckResolver();
            Program.Require(ladder(pending.Id) == null, "ladder resolver hides un-admitted decks");
            var resumed = library.LocalDeckResolver(referenced: id => id == pending.Id);
            Program.Require(resumed(pending.Id) != null && resumed(pending.Id).Id == pending.Id, "referenced deck stays resolvable after un-admission");
            Program.Require(library.LocalDeckResolver(referenced: id => { throw new Exception("x"); })(pending.Id) == null, "throwing referenced predicate");
        }
        finally { NpcWorldHooks.Reset(); LocalDecks.ExternalDecks = null; LocalDecks.ExternalDecksLog = null; }
    }

    private static string Fingerprint(DeckPlan plan) => string.Join("|",
        plan.MulliganRules.Select(r => r.CardId + ":" + r.Matchups + ":" + r.Condition + ":" + r.MaxCopies + ":" + r.MinimumEarlyPlays)
        .Concat(plan.CardRoles.Select(r => r.CardId + "=" + r.Roles))
        .Concat(plan.Holds.Select(h => h.Condition + ":" + h.Bonus + ":" + string.Join(",", h.CardIds)))
        .Concat(plan.RemovalThresholds.Select(t => t.CardId + ">" + t.MinimumTargetAttack + "/" + t.MinimumTargetHealth))
        .Concat(plan.AggressionCurve.Select(a => a.FromTurn + "," + a.OwnLifeAtMost + "," + a.OpponentLifeAtMost + "," + a.FaceWeight)));
}
