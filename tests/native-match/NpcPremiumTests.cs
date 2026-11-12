using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// NPC deck premium finishes (owner, 2026-10-28): deterministic per copy, monotone in displayed rank, the card's best
// finish at Legend, hidden until revealed, presentation only. Created cards take the best quality of every public card
// involved, from either side (Hearthstone Wiki "Quality"/"Golden card"); a hidden NPC card credits nothing.
internal static partial class Program
{
    private static string NpcData(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", name));
    private static int QualityRank(int premium) => premium == 2 ? 3 : premium == 3 ? 2 : premium;
    private static int BestFinish(string id) => NativeMatch.ResolvePremium(2, Cards.FromId(id));

    private static void NpcPremiumTests()
    {
        NpcPremiumAssignment();
        NpcPremiumMatchHistory();
        NpcPremiumDerivation();
        Console.WriteLine("PASS NPC premium finishes: deterministic, rank-monotone, best at Legend, hidden until revealed");
    }

    private static void NpcPremiumAssignment()
    {
        NpcWorldJson.Parser = EmbeddedJson.Parse;
        var library = NpcDeckLibrary.Parse(NpcData("decks-v1.json"));
        var personalities = PersonalityCatalog.Parse(NpcData("personalities-v1.json"));
        var decks = library.Decks.Select(deck => deck.ExpandedCardIds().ToArray()).ToArray();
        var ids = library.Decks.Select(deck => deck.Id).ToArray();
        Func<string, int> best = BestFinish;

        // Shares rise with rank for every flair; Legend ignores flair; Bronze 10 shows nothing.
        foreach (string personality in personalities.Archetypes.Select(p => p.Id).Concat(new string[] { null, "unknown" }))
        {
            double flair = NpcPremiums.FlairFor(personality);
            for (int index = 0; index < LadderRules.LegendIndex; index++)
                Check(NpcPremiums.Share(index, flair) <= NpcPremiums.Share(index + 1, flair) + 1e-12
                    && NpcPremiums.Upgrade(index) <= NpcPremiums.Upgrade(index + 1) + 1e-12, "NPC premium share monotone " + personality);
            Check(NpcPremiums.Share(LadderRules.LegendIndex, flair) == NpcPremiums.Share(LadderRules.LegendIndex)
                && NpcPremiums.Share(0, flair) == 0, "Legend and Bronze 10 ignore flair " + personality);
        }
        Check(NpcPremiums.Share(LadderRules.LegendIndex) >= 0.95 && NpcPremiums.Upgrade(LadderRules.LegendIndex) == 1
            && NpcPremiums.Share(-5) == 0 && NpcPremiums.Share(999) == NpcPremiums.Share(LadderRules.LegendIndex),
            "NPC premium anchors and clamps");
        // Every flair key names a real personality, so a renamed archetype cannot silently fall back to neutral.
        foreach (string id in new[] { "showboat", "pro", "veteran", "chaotic", "grinder", "speedster", "friendly" })
            Check(personalities.Find(id) != null && NpcPremiums.FlairFor(id) != 1, "NPC premium flair personality " + id);

        for (int d = 0; d < decks.Length; d++)
        {
            var first = NpcPremiums.Assign("npc.g.0001", ids[d], 100, "pro", decks[d], best);
            Check(first.SequenceEqual(NpcPremiums.Assign("npc.g.0001", ids[d], 100, "pro", decks[d].ToList(), best)),
                "NPC premium assignment deterministic " + ids[d]);
            Check(NpcPremiums.Assign(null, ids[d], 150, null, decks[d], best).All(p => p == 0)
                && NpcPremiums.Assign("npc.g.0001", "", 150, null, decks[d], best).All(p => p == 0), "missing NPC seat is Normal");
            int[] previous = NpcPremiums.Assign("npc.g.0001", ids[d], 0, "showboat", decks[d], best);
            Check(previous.All(p => p == 0), "Bronze 10 shows Normal cards");
            for (int index = 1; index <= LadderRules.LegendIndex; index++)
            {
                int[] next = NpcPremiums.Assign("npc.g.0001", ids[d], index, "showboat", decks[d], best);
                for (int i = 0; i < next.Length; i++)
                    Check(QualityRank(next[i]) >= QualityRank(previous[i])
                        && (previous[i] == 0 || next[i] == previous[i] || next[i] == best(decks[d][i])),
                        "higher rank removed or downgraded an NPC premium copy " + ids[d]);
                previous = next;
            }
            for (int i = 0; i < previous.Length; i++)
                Check(previous[i] == 0 || previous[i] == best(decks[d][i]), "Legend NPC copy is not its best finish " + ids[d]);
        }

        // Population rates: Legend near-complete, low ranks sparse, Golden dominant below Legend; real finishes appear.
        int[] Population(int index) => Enumerable.Range(0, 20).SelectMany(n => decks.SelectMany((deck, d) =>
            NpcPremiums.Assign("npc.g." + n.ToString("0000"), ids[d], index, null, deck, best))).ToArray();
        var legend = Population(LadderRules.LegendIndex);
        var diamondFive = Population(135);
        double Rate(int[] premiums, Func<int, bool> pick) => premiums.Average(p => pick(p) ? 1.0 : 0.0);
        Check(Rate(legend, p => p != 0) > 0.93 && Rate(legend, p => p != 0) < 0.97, "Legend NPC premium share " + Rate(legend, p => p != 0));
        Check(Rate(Population(15), p => p != 0) < 0.07 && Rate(Population(15), p => p != 0) > 0.03, "Bronze NPC premium share");
        Check(Rate(diamondFive, p => p != 0) > 0.5 && Rate(diamondFive, p => p != 0) < 0.6, "Diamond NPC premium share");
        Check(Rate(diamondFive, p => p == 1) > Rate(diamondFive, p => p == 2 || p == 3), "Golden does not dominate below Legend");
        Check(legend.Contains(2) && legend.Contains(3) && diamondFive.Contains(2) && diamondFive.Contains(3),
            "NPC decks never show Diamond or Signature finishes");

        // Casual: the hidden Casual rating never sets the finishes; the player's visible ladder rank does.
        Check(NpcPremiums.SeatStarIndex(CasualRules.Mode, 2400, 15) == 15 && NpcPremiums.SeatStarIndex(CasualRules.Mode, 600, 15) == 15,
            "Casual NPC finishes follow the player's ladder rank");
        Check(NpcPremiums.SeatStarIndex(null, 2400, 15) == LadderRules.IndexForRating(2400), "Ranked NPC finishes follow the NPC's medal");

        // Pinned output: a hash or salt change would silently restyle every NPC, so it must be deliberate.
        string pinned = string.Join("", NpcPremiums.Assign("npc.n.oblivia", "npc-dragon-warrior", 105, "pro",
            LocalDecks.Get("warrior").Build().Select(card => card.Id).ToArray(), best));
        Check(pinned == PinnedWarriorFinishes, "NPC premium assignment changed: " + pinned);

        // Different NPCs and copies roll independently.
        Check(!decks.SelectMany((deck, d) => NpcPremiums.Assign("npc.n.alpha", ids[d], 120, null, deck, best))
            .SequenceEqual(decks.SelectMany((deck, d) => NpcPremiums.Assign("npc.n.beta", ids[d], 120, null, deck, best))),
            "NPC id does not change the finishes");
        Check(decks.Select((deck, d) => (deck, premiums: NpcPremiums.Assign("npc.n.alpha", ids[d], 105, null, deck, best)))
            .Any(pair => Enumerable.Range(1, pair.deck.Length - 1)
                .Any(i => pair.deck[i] == pair.deck[i - 1] && pair.premiums[i] != pair.premiums[i - 1])),
            "two copies of a card never roll independently");
    }

    private const string PinnedWarriorFinishes = "001000100011100001010011000000";

    private static Dictionary<int, int> OwnedPremiums(NativeMatch match) => (Dictionary<int, int>)typeof(NativeMatch)
        .GetField("entityPremiums", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match);

    private static void NpcPremiumMatchHistory()
    {
        const int seed = 4242;
        var seat = new NpcPremiumSeat("npc.n.premium-test", "warrior", LadderRules.LegendIndex, null);
        var expected = NpcPremiums.Assign(seat.NpcId, seat.DeckId, seat.StarIndex, null,
            LocalDecks.Get("warrior").Build().Select(card => card.Id).ToArray(), BestFinish);
        Check(expected.Count(p => p != 0) >= 20, "Legend fixture deck has too few premium copies");

        var normalRun = new Capture();
        var normal = new NativeMatch(normalRun.Send, "mage", "warrior", seed);
        normal.Start();
        var premiumRun = new Capture();
        var match = new NativeMatch(premiumRun.Send, "mage", "warrior", seed) { OpponentPremiumSeat = seat };
        match.Start();
        var game = match.Game;
        Check(EngineIdentity(normal.Game) == EngineIdentity(game), "NPC premium changed the engine deal");

        // Owned premium per original deck copy: the same (card, premium) multiset the assignment produced.
        var owned = OwnedPremiums(match);
        var npcCards = game.Player2.DeckZone.Concat(game.Player2.HandZone).Where(card => card.Card.Id != "GAME_005").ToArray();
        string Multiset(IEnumerable<string> rows) => string.Join(",", rows.OrderBy(row => row, StringComparer.Ordinal));
        Check(Multiset(npcCards.Select(card => card.Card.Id + ":" + (owned.TryGetValue(card.Id, out int p) ? p : 0)))
            == Multiset(LocalDecks.Get("warrior").Build().Select((card, i) => card.Id + ":" + expected[i])),
            "NPC deck copies do not carry their assigned premium");
        Check(game.Player1.DeckZone.Concat(game.Player1.HandZone).All(card => !owned.ContainsKey(card.Id)), "NPC seat gave the human premium");

        normal.SendChoices(normalRun.Last<EntityChoices>(17).Id, new List<int>());
        match.SendChoices(premiumRun.Last<EntityChoices>(17).Id, new List<int>());
        DriveToTerminal(normal, normalRun);
        DriveToTerminal(match, premiumRun);
        Check(match.Game.State == State.COMPLETE && normal.Game.State == State.COMPLETE && match.Game.Turn == normal.Game.Turn
            && match.Game.Player1.PlayState == normal.Game.Player1.PlayState
            && match.Game.Player1.Hero.Health == normal.Game.Player1.Hero.Health
            && match.Game.Player2.Hero.Health == normal.Game.Player2.Hero.Health, "NPC premium changed the match outcome");
        Check(HistorySignature(NativeHistory(normalRun)).SequenceEqual(HistorySignature(NativeHistory(premiumRun))),
            "NPC premium changed non-premium native history");

        // Hidden NPC cards never carry premium; a revealed untransformed copy shows exactly its owned premium.
        var original = npcCards.ToDictionary(card => card.Id, card => card.Card.Id);
        var revealed = new HashSet<int>();
        var changed = new HashSet<int>();
        int revealedPremium = 0;
        bool Premium(int tag) => tag == (int)GameTag.PREMIUM || tag == DiamondQuality || tag == SignatureQuality;
        foreach (var entry in NativeHistory(premiumRun))
        {
            int id = SnapshotEntity(entry);
            if (id != 0 && original.ContainsKey(id))
            {
                if (entry.HasChangeEntity) changed.Add(id);
                if (SnapshotName(entry).Length == 0)
                    Check(SnapshotTag(entry, GameTag.PREMIUM) == 0 && SnapshotTag(entry, (GameTag)DiamondQuality) == 0
                        && SnapshotTag(entry, (GameTag)SignatureQuality) == 0, "hidden NPC card snapshot carries premium");
                else
                {
                    revealed.Add(id);
                    if (!changed.Contains(id) && SnapshotName(entry) == original[id])
                    {
                        int ownedPremium = owned.TryGetValue(id, out int p) ? p : 0;
                        AssertPremiumSnapshot(entry, ownedPremium, "revealed NPC card " + original[id]);
                        if (ownedPremium != 0) revealedPremium++;
                    }
                }
            }
            if (entry.HasTagChange && original.ContainsKey(entry.TagChange.Entity) && Premium(entry.TagChange.Tag))
                Check(revealed.Contains(entry.TagChange.Entity), "premium tag change on a hidden NPC card");
            if (entry.HasHideEntity) revealed.Remove(entry.HideEntity.Entity);
        }
        Check(revealedPremium > 0, "Legend NPC never revealed a premium card");
        premiumRun.SerializeAll();
    }

    // Plays source (optionally at target) for the current player; returns the native history of the play.
    private static List<PowerHistoryData> PlayFor(Controller player, NativeMatch match, Capture capture, IPlayable source, IPlayable target = null)
    {
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, player.Options().First(task => task.PlayerTaskType == PlayerTaskType.PLAY_CARD
            && task.Source?.Id == source.Id && task.Target?.Id == target?.Id));
        FlushFieldHistory(match);
        return History(capture, cursor);
    }

    private static void NpcPremiumDerivation()
    {
        // A Golden NPC minion's battlecry token is Golden on the NPC's board.
        {
            var (match, capture) = AnimationMatch(botTurn: true);
            var npc = match.Game.Player2;
            var hunter = PremiumCard(match, "CS2_196", 1, opponent: true);
            var before = npc.BoardZone.Select(card => card.Id).ToHashSet();
            var played = PlayFor(npc, match, capture, hunter);
            var token = npc.BoardZone.Single(card => !before.Contains(card.Id) && card.Id != hunter.Id);
            AssertDerived(capture, played, token.Id, 1, "Golden NPC battlecry token");
            AssertDerived(capture, played, hunter.Id, 1, "Golden NPC summoner");
            capture.SerializeAll();
        }
        // A card a Golden NPC spell adds to the NPC's hand stays hidden there, then reveals Golden.
        {
            var (match, capture) = AnimationMatch(botTurn: true);
            var npc = match.Game.Player2;
            var flame = PremiumCard(match, "CORE_SW_108", 1, opponent: true);
            var target = PremiumCard(match, "CS2_182", 0, board: true);
            var before = npc.HandZone.Select(card => card.Id).ToHashSet();
            var played = PlayFor(npc, match, capture, flame, target);
            var created = npc.HandZone.Single(card => !before.Contains(card.Id));
            Check(played.Where(entry => SnapshotEntity(entry) == created.Id).All(entry => SnapshotName(entry).Length == 0
                && SnapshotTag(entry, GameTag.PREMIUM) == 0), "NPC's created hand card leaked identity or premium");
            Check(DerivedPremiums(match).TryGetValue(created.Id, out int derived) && derived == 1, "NPC's created card derived nothing");
            npc.BaseMana = 10; npc.UsedMana = 0;
            var shown = PlayFor(npc, match, capture, created, target.Zone?.Type == SabberStoneCore.Enums.Zone.PLAY ? target : null);
            AssertDerived(capture, shown, created.Id, 1, "revealed NPC Second Flame");
            capture.SerializeAll();
        }
        // Premium crosses sides: the NPC's Golden Polymorph gives the human a Golden Sheep, and the human's
        // Normal Faceless Manipulator copying a Golden NPC minion becomes Golden.
        {
            var (match, capture) = AnimationMatch(botTurn: true);
            var spell = PremiumCard(match, "CS2_022", 1, opponent: true);
            var target = PremiumCard(match, "CS2_182", 0, board: true);
            var played = PlayFor(match.Game.Player2, match, capture, spell, target);
            var change = played.Single(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == target.Id);
            AssertPremiumSnapshot(change, 1, "human minion polymorphed by a Golden NPC spell");
            AssertPremiumReplay(capture, target.Id, 1, "human minion polymorphed by a Golden NPC spell");
            capture.SerializeAll();
        }
        {
            var (match, capture) = AnimationMatch();
            var copier = PremiumCard(match, "EX1_564", 0);
            var target = PremiumCard(match, "CS2_182", 1, board: true, opponent: true);
            var played = PlayPremiumCard(match, capture, copier, target);
            var change = played.Single(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == copier.Id);
            AssertPremiumSnapshot(change, 1, "human copy of a Golden NPC minion");
            AssertPremiumReplay(capture, copier.Id, 1, "human copy of a Golden NPC minion");
            capture.SerializeAll();
        }
        // A stolen Golden minion still credits a copy, whichever side stole it.
        foreach (bool npcSteals in new[] { true, false })
        {
            var (match, capture) = AnimationMatch(botTurn: npcSteals);
            var game = match.Game;
            var victim = PremiumCard(match, "CS2_182", 1, board: true, opponent: !npcSteals);
            var thief = npcSteals ? game.Player2 : game.Player1;
            PlayFor(thief, match, capture, PremiumCard(match, "CS1_113", 0, opponent: npcSteals), victim);
            Check(victim.Controller == thief, "Mind Control did not steal the Golden minion");
            if (npcSteals)
            {
                InvokeProcess(match, game.Player2.Options().Single(task => task.PlayerTaskType == PlayerTaskType.END_TURN));
                FlushFieldHistory(match);
            }
            game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
            var copier = PremiumCard(match, "EX1_564", 0);
            var played = PlayFor(game.Player1, match, capture, copier, victim);
            var change = played.Single(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == copier.Id);
            const int expected = 1;
            string label = "human copy of a Golden minion stolen by " + (npcSteals ? "the NPC" : "the human");
            AssertPremiumSnapshot(change, expected, label);
            AssertPremiumReplay(capture, copier.Id, expected, label);
            capture.SerializeAll();
        }
        // A hidden NPC card credits nothing: a public card it creates never reveals the source's premium.
        {
            var (match, _) = AnimationMatch(botTurn: true);
            var game = match.Game;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var blocks = (Stack<KeyValuePair<int, int>>)typeof(NativeMatch).GetField("premiumBlocks", flags).GetValue(match);
            var derive = typeof(NativeMatch).GetMethod("DeriveCreatedPremium", flags);
            var hidden = PremiumCard(match, "CS2_196", 1, opponent: true);
            var shown = PremiumCard(match, "CS2_196", 1, board: true, opponent: true);
            FlushFieldHistory(match);
            foreach (var (source, expected) in new[] { (hidden, false), (shown, true) })
            {
                var token = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_boar"), zone: game.Player2.BoardZone);
                blocks.Push(new KeyValuePair<int, int>(source.Id, 0));
                derive.Invoke(match, new object[] { token.Id, token.Card.Id });
                blocks.Pop();
                Check(DerivedPremiums(match).ContainsKey(token.Id) == expected,
                    (expected ? "a public" : "a hidden") + " Golden NPC source " + (expected ? "did not credit" : "credited") + " its creation");
            }
        }
    }
}
