using System;
using System.Collections.Generic;
using System.Linq;

// Synthetic NPC world for the matchmaker tests and the world climb simulation: the spec's rating spread
// (85% piecewise-uniform over [920,1600) with league weights, 15% Legend field 1600 + Exp(70) capped at 1900),
// a deck library covering every archetype / annoyance / band, no engine data. Pure function of the seed.
// NOTE: a copy of this class lives in tests/npc-world/MatchmakerSuite.cs (that project cannot link this file).
public static class SyntheticNpcWorld
{
    private static ulong Mix(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
    private static double Unit(ref ulong state) { return (Mix(ref state) >> 11) * (1.0 / 9007199254740992.0); }

    public static NpcDeckLibrary Library(int deckCount = 60, long seed = 7)
    {
        var decks = new List<NpcDeckEntry>();
        ulong state = unchecked((ulong)seed);
        var archetypes = new[] { DeckArchetype.Aggro, DeckArchetype.Tempo, DeckArchetype.Midrange, DeckArchetype.Control, DeckArchetype.Combo };
        for (int i = 0; i < deckCount; i++)
        {
            double roll = Unit(ref state);
            int annoyance = roll < .5 ? 0 : roll < .75 ? 1 : roll < .9 ? 2 : 3;
            double minRoll = Unit(ref state), maxRoll = Unit(ref state);
            var min = minRoll < .7 ? LadderBand.Bronze : minRoll < .85 ? LadderBand.Silver : LadderBand.Gold;
            var max = maxRoll < .8 ? LadderBand.Legend : maxRoll < .9 ? LadderBand.Diamond : LadderBand.Platinum;
            var tags = new NpcDeckTags(archetypes[i % archetypes.Length], 1 + (int)(Unit(ref state) * 5), 1 + (int)(Unit(ref state) * 5),
                annoyance, 1 + (int)(Unit(ref state) * 5), min, max);
            decks.Add(new NpcDeckEntry("synth-deck-" + i.ToString("D2"), 1, "Synthetic " + i, "WARRIOR", "HERO_01", "golden", "admitted", null,
                tags, null, new NpcDeckCard[0], new NpcDeckSideboard[0]));
        }
        return new NpcDeckLibrary(1, 1, "violet-hold", decks);
    }

    public static SkillCatalog Skills()
    {
        var tiers = new List<SkillProfile>();
        for (int tier = 1; tier <= 7; tier++)
            tiers.Add(new SkillProfile("t" + tier, tier, 900 + (tier - 1) * 120, 900 + tier * 120, new Budget(1, 1, 1, 1, 1, 1), 0, 1, 1, null));
        return new SkillCatalog(1, tiers);
    }

    public static PersonalityCatalog Personalities() => new PersonalityCatalog(1, new[] {
        new PersonalityArchetype("pro", "Pro", .15, .45, .1, .3, .35, 1, 8, 0, 0, false, new EmoteProfile(0, 0)) });

    // generated + notables; HomeRating distribution as the spec's population section.
    public static NpcWorld World(int generated = NpcWorld.GeneratedCount, int notables = 40, long seed = 20261025)
    {
        var library = Library();
        var skills = Skills();
        ulong state = unchecked((ulong)seed ^ 0x57A1UL);
        var all = new List<NpcIdentity>();
        var leagueWeights = new[] { .15, .18, .20, .20, .27 };
        for (int i = 0; i < generated + notables; i++)
        {
            bool notable = i < notables;
            double rating;
            if (!notable && Unit(ref state) < .15) rating = Math.Min(1900, 1600 - 70 * Math.Log(1 - Unit(ref state)));
            else if (notable && i % 6 == 5) rating = 1600 + Unit(ref state) * 250;            // notables span every league
            else if (notable) rating = 1000 + (i % 6) * 120 + 5 + Unit(ref state) * 110;
            else
            {
                double pick = Unit(ref state), acc = 0; int league = 4;
                for (int l = 0; l < leagueWeights.Length; l++) { acc += leagueWeights[l]; if (pick < acc) { league = l; break; } }
                double low = league == 0 ? 920 : 1000 + league * 120, high = 1000 + (league + 1) * 120;
                rating = low + Unit(ref state) * (high - low);
            }
            var band = NpcWorld.BandForRating(rating);
            var pool = library.ForBand(band).ToList();
            int deckCount = 1 + (int)(Unit(ref state) * 3);
            var picked = new List<string>();
            for (int d = 0; d < deckCount && pool.Count > 0; d++)
            {
                int at = (int)(Unit(ref state) * pool.Count);
                picked.Add(pool[at].Id); pool.RemoveAt(at);
            }
            int tier = Math.Max(1, Math.Min(7, 1 + (int)((rating - 900) / 120)));
            string id = notable ? "npc.n.synth" + i.ToString("D3") : "npc.g." + i.ToString("D4");
            all.Add(new NpcIdentity(id, 1, "Synth " + id, notable ? "bio" : null, notable, rating, "pro", DeckTaste.Netdecker,
                new string[0], picked, null, null, 0, "t" + tier));
        }
        all.Sort((a, b) => { int c = b.HomeRating.CompareTo(a.HomeRating); return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id); });
        var catalogs = new NpcWorldCatalogs(library, skills, Personalities(), all.Where(n => n.Notable), null, null);
        return new NpcWorld(1, seed, catalogs, all);
    }

    // The profile stub for tests: the matchmaker only needs a builder, not WP3's real profile construction.
    public static NpcProfile StubProfile(NpcIdentity npc, NpcDeckEntry deck, PersonalityArchetype personality, SkillProfile skill) => null;
}
