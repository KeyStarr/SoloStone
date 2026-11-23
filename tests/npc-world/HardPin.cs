using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// Hard = today's NPCs (owner 2026-11-15). A fingerprint of Hard ladder, rematch and Casual picks, captured on main before
// NPC difficulty existed (main 84fde88, the branch base). This file compiles against that main as well: to re-capture,
// copy it into a main checkout and print HardPin.Fingerprint() from tests/npc-world. Ratings are hashed at 3 decimals so
// last-digit math-library differences between platforms (macOS capture, Linux CI) do not count. A diff here is a behaviour
// change of Hard: it goes to the owner and is never re-pinned to make the suite pass. An intentional, reviewed change to
// data/npc (deck admission, notables, names) re-captures only with the owner's OK.
internal static class HardPin
{
    internal const string Expected = "96249d89269afbe10050a7a4d6e60e009d484b1f9346c0d05b3bc14c8f0a56a6";

    private const long Profile = 777001;

    private static NpcWorld World(bool casual)
    {
        var decks = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        if (casual) decks = CasualDeckCatalog.Parse(Program.Data("casual-decks-v1.json")).MergeInto(decks);
        return NpcWorld.Generate(new NpcWorldCatalogs(decks, SkillCatalog.Parse(Program.Data("skills-v1.json")),
            PersonalityCatalog.Parse(Program.Data("personalities-v1.json")), NpcNotables.Parse(Program.Data("notables-v1.json")),
            NpcNameTables.Parse(Program.Data("names-v1.json")), NpcCosmeticPool.Parse(Program.Data("cosmetics-v1.json"))));
    }

    private static string Line(string kind, int i, NpcEncounter e, double target) => string.Join("|", kind, i.ToString(CultureInfo.InvariantCulture),
        e.Npc.Id, e.Deck.Id, e.Skill.Id, e.Rating.ToString("F3", CultureInfo.InvariantCulture), e.MatchSeed.ToString(CultureInfo.InvariantCulture),
        e.Reason, target.ToString("F3", CultureInfo.InvariantCulture));

    internal static string Fingerprint()
    {
        var lines = new List<string>();
        int[] stars = { 0, 5, 20, 40, 75, 100, 125, 135, 140, 150 };
        var ladder = new LadderMatchmaker(World(false));
        var history = new List<PastEncounter>();
        for (int i = 0; i < 300; i++)
        {
            int star = stars[i % stars.Length];
            double hidden = LadderRules.RatingForIndex(star) + (i * 37 % 121 - 60);
            int? legend = star >= LadderRules.LegendIndex ? 1 + i % 500 : (int?)null;
            var pick = ladder.Choose(new MatchRequest(Profile, i, star, hidden, legend, history));
            var e = pick.Encounter;
            lines.Add(Line("ladder", i, e, pick.Target));
            string result = i % 3 == 0 ? "Lost" : "Won";
            if (i % 10 == 4 && result == "Lost" || i % 10 == 9)
            {
                var past = history.Concat(new[] { new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, "Lost") }).ToList();
                var again = ladder.Rematch(new MatchRequest(Profile, i + 1, star, hidden, legend, past), e.Npc.Id, e.Deck.Id, out string refusal);
                lines.Add(again == null ? "rematch|" + i + "|refused|" + refusal : Line("rematch", i, again, 0));
            }
            history.Add(new PastEncounter(e.Npc.Id, e.Deck.Id, e.Deck.Tags.Archetype, result));
            if (history.Count > 8) history.RemoveAt(0);
        }
        var world = World(true);
        var casual = new CasualMatchmaker(world, CasualDeckCatalog.Parse(Program.Data("casual-decks-v1.json")), new NpcLegendField(world));
        var casualHistory = new List<PastEncounter>();
        for (int i = 0; i < 150; i++)
        {
            var pick = casual.Choose(new CasualRequest(Profile, i, i * 2, 1000 + i * 7 % 900, casualHistory));
            lines.Add(Line("casual", i, pick.Encounter, pick.Target));
            casualHistory.Add(new PastEncounter(pick.Encounter.Npc.Id, pick.Encounter.Deck.Id, pick.Encounter.Deck.Tags.Archetype, i % 2 == 0 ? "Won" : "Lost"));
            if (casualHistory.Count > 8) casualHistory.RemoveAt(0);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();
    }

    internal static void Run()
    {
        string actual = Fingerprint();
        Program.Require(actual == Expected, "Hard picks differ from main (behaviour change of Hard; owner call, never re-pin): " + actual);
    }
}
