using System.Text.RegularExpressions;
using SabberStoneCore.Model;

internal static class ShowcaseChecks
{
    internal static void Run()
    {
#if ENGINE_SOURCE
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        if (!File.Exists("docs/decks/showcase-decks.md")) { Console.WriteLine("SKIP showcase deck notes: docs/decks is not in this checkout"); return; }
        var document = File.ReadAllText("docs/decks/showcase-decks.md");
        var sections = Regex.Matches(document, @"(?ms)^## (showcase-[^\r\n]+)\r?\n(.*?)(?=^## |\z)");
        var players = new List<string>();
        var opponents = new List<string>();
        foreach (Match section in sections)
        {
            string key = section.Groups[1].Value, body = section.Groups[2].Value;
            string Field(string label) => Regex.Match(body, @"\*\*" + label + @":\*\* ([^\r\n]+)").Groups[1].Value;
            var metadata = Regex.Match(Field("Hero / style / role"), @"`([^`]+)` / `([^`]+)` / `([^`]+)`");
            bool player = metadata.Groups[3].Value == "player";
            var deck = LocalDecks.Get(key);
            var recipe = Regex.Match(body, @"pairs: ([^\r\n]*)\r?\nsingles:([^\r\n]*)");
            string[] Ids(int group) => recipe.Groups[group].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Check(deck.CardIds.SequenceEqual(Ids(1).SelectMany(id => new[] { id, id }).Concat(Ids(2))), "showcase doc recipe: " + key);
            // Native tray label is "Loaner: " + Name; the pinned client caps deck names at 24 characters.
            Check(deck.Name == Field("Display name") && (!player || ("Loaner: " + deck.Name).Length <= 24) && deck.Name.Length <= 32
                && deck.Hero == metadata.Groups[1].Value
                && deck.Style.ToString() == metadata.Groups[2].Value, "showcase metadata: " + key);
            Check(deck.Build().Count == 30 && deck.Build().All(card => card.Implemented
                && SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.Contains(card.Id)
                && LocalDecks.IsAllowedInDeck(card, Cards.FromId(deck.Hero).Class))
                && LocalCardCatalog.WithinCopyLimits(deck.CardIds), "showcase registered legal list: " + key);
            LocalDecks.ValidateRunes(deck.Build(), Cards.FromId(deck.Hero).Class);
            if (player)
            {
                Check(LocalDecks.TestLoaners.Contains(deck) && NativeUIProjection.DeckId(key) == 900000200L + players.Count,
                    "showcase player identity: " + key);
                players.Add(key);
            }
            else
            {
                Check(LocalDecks.TestOpponents.Contains(deck) && !LocalDecks.TestLoaners.Contains(deck)
                    && deck.Opponent == Field("Opponent"), "showcase opponent identity: " + key);
                bool hasNativeId = true;
                try { NativeUIProjection.DeckId(key); } catch (ArgumentException) { hasNativeId = false; }
                Check(!hasNativeId, "showcase opponents have no tray ID: " + key);
                opponents.Add(key);
            }
        }
        string[][] legacyGroups = {
            new[] { LocalDecks.TestDecisionsId, LocalDecks.TestStandardBatch1Id, "test-standard-batch-2-warrior", "test-standard-batch-2-priest", "test-standard-batch-2-mage", "test-standard-batch-2-paladin", "test-standard-batch-2-druid", "test-standard-batch-2-rogue", "test-standard-batch-2-warlock", "test-standard-batch-2-hunter", "test-standard-batch-2-shaman", "test-standard-batch-2-demonhunter", LocalDecks.TestRushSilenceId },
            new[] { "test-standard-extra-warrior", "test-standard-extra-priest", "test-standard-extra-druid", "test-standard-extra-hunter", "test-standard-extra-demonhunter", "test-standard-extra-rogue", "test-standard-azalina-priest" },
            new[] { "test-deathknight-rainbow", "test-deathknight-frost", "test-deathknight-unholy", "test-deathknight-blood", "test-deathknight-buu" },
            new[] { "test-standard-more-warrior", "test-standard-more-priest", "test-standard-more-mage", "test-standard-more-paladin", "test-standard-more-druid", "test-standard-more-rogue", "test-standard-more-warlock", "test-standard-more-hunter", "test-standard-more-shaman", "test-standard-more-demonhunter" },
            new[] { "test-standard-b10-warrior", "test-standard-b10-priest", "test-standard-b10-mage", "test-standard-b10-paladin", "test-standard-b10-druid", "test-standard-b10-rogue", "test-standard-b10-warlock", "test-standard-b10-hunter", "test-standard-b10-shaman", "test-standard-b10-demonhunter", "test-standard-b10-deathknight", "test-standard-b10-deathknight-2" },
            new[] { "test-standard-mecha-warrior", "test-standard-mecha-priest", "test-standard-mecha-mage", "test-standard-mecha-paladin", "test-standard-mecha-druid", "test-standard-mecha-rogue", "test-standard-mecha-warlock", "test-standard-mecha-hunter", "test-standard-mecha-shaman", "test-standard-mecha-demonhunter", "test-standard-mecha-deathknight", "test-standard-mecha-deathknight-2" },
            new[] { "test-standard-rewind-rogue", "test-standard-rewind-mage", "test-standard-rewind-warlock", "test-standard-rewind-shaman", "test-standard-rewind-paladin", "test-standard-rewind-druid", "test-standard-rewind-warrior", "test-standard-rewind-priest", "test-standard-rewind-demonhunter", "test-standard-rewind-hunter" },
            new[] { "test-keywords-cfa-paladin", "test-keywords-cfa-hunter", "test-keywords-cfa-warrior", "test-keywords-cfa-shaman", "test-keywords-cfa-priest", "test-keywords-cfa-mage" },
            new[] { "test-standard-cardstate-mage", "test-standard-cardstate-shaman", "test-standard-cardstate-warlock", "test-standard-cardstate-warrior", "test-standard-cardstate-druid", "test-standard-cardstate-hunter", "test-standard-cardstate-paladin", "test-standard-cardstate-priest" }
        };
        for (int i = 0; i < legacyGroups.Length; i++)
            Check(NativeUIProjection.TestLoanerGroups[i].SequenceEqual(legacyGroups[i]), "legacy group membership and order: " + (i + 1));
        Check(players.Count == 12 && opponents.Count == 3, "twelve showcase players and three opponents");
        Check(NativeUIProjection.TestLoanerGroups.Count == 11 && NativeUIProjection.TestLoanerGroups[9].Count == 9
            && NativeUIProjection.TestLoanerGroups[10].SequenceEqual(players), "showcase group 11 in doc order; group 10 retains nine batch-11 seats");
        Check(NativeUIProjection.CanonicalTestLoanerGroup(" ShOwCaSe ") == "11"
            && NativeUIProjection.CanonicalTestLoanerGroup("default") == "1", "showcase and legacy default aliases");
        Check(NativeUIProjection.ProjectedLoanerKeys(0).SequenceEqual(NativeUIProjection.PlayerLoanerKeys)
            && NativeUIProjection.ProjectedLoanerKeys(0, "showcase").All(key => !players.Contains(key)),
            "player defaults stay separate from historical showcase fixtures");
        foreach (string player in players)
        {
            var history = new List<string>();
            for (int i = 0; i < 7; i++)
            {
                var next = LocalDecks.OpponentFor(player, history);
                Check(next.Id == opponents[i % 3], "showcase rotation: " + player + "/" + i);
                history.Add(next.Id); history.Add("mage"); history.Add("test-standard-batch-2-opp-hunter");
            }
            Check(LocalDecks.OpponentFor(player, new[] { opponents[1], "mage" }).Id == opponents[2], "last showcase opponent controls next seat");
        }
        var playerCatalog = LocalDecks.PlayerStarterDecks.ToArray();
        var nativeIds = LocalDecks.All.Concat(LocalDecks.TestLoaners).Concat(playerCatalog)
            .Select(deck => NativeUIProjection.DeckId(deck.Id)).ToArray();
        Check(nativeIds.Distinct().Count() == nativeIds.Length && !nativeIds.Contains(900000179L)
            && playerCatalog.Select(deck => NativeUIProjection.DeckId(deck.Id))
                .SequenceEqual(Enumerable.Range(0, 11).Select(i => 900000215L + i).Concat(new[] { 900000212L, 900000213L, 900000214L })),
            "historical and player defaults keep unique reserved native IDs");
        string root = Path.Combine(Path.GetTempPath(), "showcase-rotation-" + Guid.NewGuid());
        try
        {
            string path = Path.Combine(root, "profile.xml");
            for (int i = 0; i < 4; i++)
            {
                using var profile = new LocalProfile(path);
                string player = players[i];
                var opponent = LocalDecks.OpponentFor(player, profile.History.Select(match => match.Opponent));
                Check(opponent.Id == opponents[i % 3], "showcase rotation persists across reload and player changes");
                var match = profile.Begin(player, opponent.Id, 8427 + i);
                profile.Settle(match.Id, "Won", 12, false);
                profile.AcknowledgeRewardPresentation(match.Id);
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        Console.WriteLine("PASS showcase contracts: 12 players, 3 AI, group 11, doc recipes, rotation");
#endif
    }
}
