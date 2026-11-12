using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineZone = SabberStoneCore.Enums.Zone;

// Premium deck copies and the golden hero are presentation only: entity tag PREMIUM plus quality flags.
internal static partial class Program
{
    private const int DiamondQuality = 1932, SignatureQuality = 2589;

    private static void PremiumDeckAndGoldenHeroHistory()
    {
        LocalDeck source = LocalDecks.Get("mage");
        LocalDeck deck = LocalDecks.Custom("Premium", source.Hero, source.CardIds);
        // Pick a seed whose starting hand has a minion plus two more distinct card IDs.
        int seed = 0;
        string[] handIds = null;
        for (int candidate = 1; candidate < 200 && handIds == null; candidate++)
        {
            var probe = new NativeMatch((_, __) => { }, deck, "warrior", candidate);
            probe.Start();
            var hand = probe.Game.Player1.HandZone.ToArray();
            var minion = hand.FirstOrDefault(card => card is Minion);
            var distinct = hand.Select(card => card.Card.Id).Distinct(StringComparer.Ordinal).ToArray();
            if (minion == null || distinct.Length < 3) continue;
            seed = candidate;
            handIds = new[] { minion.Card.Id }.Concat(distinct.Where(id => id != minion.Card.Id)).Take(3).ToArray();
        }
        Check(handIds != null, "premium fixture found no usable starting hand");
        var premiumById = new Dictionary<string, int>(StringComparer.Ordinal)
            { [handIds[0]] = 1, [handIds[1]] = 2, [handIds[2]] = 3 };
        string mixed = deck.CardIds.GroupBy(id => id).First(group => group.Count() == 2 && !premiumById.ContainsKey(group.Key)).Key;
        bool mixedSeen = false;
        var entries = deck.CardIds.Select(id => {
            int premium = premiumById.TryGetValue(id, out int value) ? value : 0;
            if (id == mixed && !mixedSeen) { mixedSeen = true; premium = 1; }
            return new LocalDeckCard { Id = id, Premium = premium };
        }).ToList();

        // Presentation only: mismatched or invalid entries fall back to Normal and never refuse the match.
        var warnings = new List<string>();
        Check(NativeMatch.PremiumEntries(deck.CardIds, entries.Skip(1), warnings.Add) == null && warnings.Count == 1,
            "mismatched premium entries were not dropped with a warning");
        Check(NativeMatch.PremiumEntries(deck.CardIds, entries.Select(card => new LocalDeckCard { Id = card.Id, Premium = 9 })) == null
            && NativeMatch.PremiumEntries(deck.CardIds, new LocalDeckCard[] { null }) == null
            && NativeMatch.PremiumEntries(null, entries) == null && NativeMatch.PremiumEntries(deck.CardIds, null) == null,
            "invalid premium entries were not dropped");
        Check(NativeMatch.PremiumEntries(deck.CardIds, entries)?.Length == entries.Count, "matching premium entries were dropped");
        var mismatched = new Capture();
        var fallback = new NativeMatch(mismatched.Send, deck, "warrior", seed, 0, entries.Skip(1));
        fallback.Start();
        Check(fallback.Game.State == State.RUNNING && NativeHistory(mismatched).All(entry => SnapshotTag(entry, GameTag.PREMIUM) == 0
            && !(entry.HasTagChange && entry.TagChange.Tag == (int)GameTag.PREMIUM)), "mismatched premium entries did not start Normal");

        var baseline = new Capture();
        var normal = new NativeMatch(baseline.Send, deck, "warrior", seed);
        normal.Start();
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, deck, "warrior", seed, 0, entries);
        match.Start();
        var game = match.Game;
        Check(EngineIdentity(normal.Game) == EngineIdentity(game), "premium entries changed the engine deal");

        // FULL/SHOW: every visible snapshot of a premium copy carries its premium and quality flag.
        var initial = NativeHistory(capture);
        foreach (var card in game.Player1.HandZone)
        {
            int expected = premiumById.TryGetValue(card.Card.Id, out int value) ? value : card.Card.Id == mixed ? -1 : 0;
            var snapshots = initial.Where(entry => SnapshotEntity(entry) == card.Id && SnapshotName(entry).Length > 0).ToList();
            Check(snapshots.Count > 0, "premium hand card was never revealed: " + card.Card.Id);
            foreach (var entry in snapshots)
                if (expected >= 0) AssertPremiumSnapshot(entry, expected, "starting hand " + card.Card.Id);
        }
        Check(initial.Where(entry => SnapshotName(entry).Length > 0 && SnapshotEntity(entry) != 0
                && game.IdEntityDic.TryGetValue(SnapshotEntity(entry), out var owner) && owner.Controller == game.Player2)
            .All(entry => SnapshotTag(entry, GameTag.PREMIUM) == 0), "opponent card was published as premium");
        var premiums = (Dictionary<int, int>)typeof(NativeMatch).GetField("entityPremiums",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match);
        var mixedEntities = game.Player1.DeckZone.Concat(game.Player1.HandZone).Where(card => card.Card.Id == mixed).ToArray();
        Check(mixedEntities.Length == 2 && mixedEntities.Count(card => premiums.TryGetValue(card.Id, out int p) && p == 1) == 1,
            "mixed Normal/Golden copies did not keep one premium per copy");

        var mulligan = capture.Last<EntityChoices>(17);
        match.SendChoices(mulligan.Id, mulligan.Entities.ToList());
        Check(game.Player1.HandZone.Any(card => card.Card.Id == handIds[0]), "keep-all mulligan replaced the golden minion");
        game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
        var golden = (Minion)game.Player1.HandZone.First(card => card.Card.Id == handIds[0]);

        // Zone move: HAND -> PLAY keeps PREMIUM on the same entity.
        int cursor = capture.Packets.Count;
        InvokeProcess(match, game.Player1.Options().First(task => task is PlayCardTask && task.Source?.Id == golden.Id));
        FlushHistory(match);
        Check(golden.Zone?.Type == EngineZone.PLAY, "golden minion fixture was not played");
        Check(NativeHistory(capture, cursor).All(entry => !(entry.HasTagChange && entry.TagChange.Entity == golden.Id
            && entry.TagChange.Tag == (int)GameTag.PREMIUM && entry.TagChange.Value != 1)), "zone move cleared PREMIUM");
        Check(ReplayNativeEntityTags(NativeHistory(capture), golden.Id).TryGetValue((int)GameTag.PREMIUM, out int played) && played == 1,
            "played golden minion lost PREMIUM");

        // SHOW after a draw: a premium copy still in the deck reveals with its premium.
        var deckCopy = game.Player1.DeckZone.FirstOrDefault(card => premiums.ContainsKey(card.Id));
        Check(deckCopy != null, "premium fixture has no premium copy left in the deck");
        int deckPremium = premiums[deckCopy.Id];
        cursor = capture.Packets.Count;
        Generic.Draw(game.Player1, deckCopy);
        FlushHistory(match);
        var drawn = NativeHistory(capture, cursor).Where(entry => SnapshotEntity(entry) == deckCopy.Id && SnapshotName(entry).Length > 0).ToList();
        Check(drawn.Count > 0, "drawn premium copy was not revealed");
        foreach (var entry in drawn) AssertPremiumSnapshot(entry, deckPremium, "drawn " + deckCopy.Card.Id);

        // Cards created outside any block (no creating card), even with a premium card's ID, stay Normal.
        cursor = capture.Packets.Count;
        var generated = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId(handIds[0]), zone: game.Player1.HandZone);
        FlushHistory(match);
        var generatedSnapshots = NativeHistory(capture, cursor).Where(entry => SnapshotEntity(entry) == generated.Id).ToList();
        Check(generatedSnapshots.Count > 0 && generatedSnapshots.All(entry => SnapshotTag(entry, GameTag.PREMIUM) == 0),
            "generated copy was published as premium");
        Check(!ReplayNativeEntityTags(NativeHistory(capture), generated.Id).TryGetValue((int)GameTag.PREMIUM, out int copyPremium)
            || copyPremium == 0, "generated copy carries PREMIUM");
        // Engine-authored premium (Zola's Golden copy) is not owned premium and passes through unchanged.
        cursor = capture.Packets.Count;
        generated[GameTag.PREMIUM] = 1;
        FlushHistory(match);
        Check(NativeHistory(capture, cursor).Any(entry => entry.HasTagChange && entry.TagChange.Entity == generated.Id
            && entry.TagChange.Tag == (int)GameTag.PREMIUM && entry.TagChange.Value == 1), "engine-authored PREMIUM was overridden");

        // CHANGE: a transformed premium entity keeps its premium (same entity ID).
        var polymorph = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_022"), zone: game.Player1.HandZone);
        cursor = capture.Packets.Count;
        InvokeProcess(match, game.Player1.Options().First(task => task.Source?.Id == polymorph.Id && task.Target?.Id == golden.Id));
        FlushHistory(match);
        var change = NativeHistory(capture, cursor).Single(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == golden.Id);
        AssertPremiumSnapshot(change, 1, "transformed golden minion");
        Check(match.SnapshotVisibleTransportTags(game.IdEntityDic[golden.Id])[(int)GameTag.PREMIUM] == 1,
            "visible transport snapshot lost PREMIUM");

        // A Golden copy of a card that prints the Signature flag clears it (FULL, printed stats, snapshot audit).
        var signedCard = Cards.All.First(card => card.Collectible && card.Implemented && card.Type == CardType.MINION && card[(GameTag)SignatureQuality] == 1);
        var signed = SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, signedCard, zone: game.Player1.HandZone);
        premiums[signed.Id] = 1;
        cursor = capture.Packets.Count;
        FlushHistory(match);
        var signedFull = NativeHistory(capture, cursor).Single(entry => entry.HasFullEntity && entry.FullEntity.Entity == signed.Id);
        Check(signedFull.FullEntity.Tags.Any(tag => tag.Name == SignatureQuality && tag.Value == 0)
            && signedFull.FullEntity.Tags.Any(tag => tag.Name == DiamondQuality && tag.Value == 0),
            "Golden copy kept a printed quality flag");
        AssertPremiumReplay(capture, signed.Id, 1, "Golden copy of a Signature-flagged card");
        Check(match.SnapshotVisibleTransportTags(signed)[SignatureQuality] == 0, "snapshot audit kept a conflicting quality flag");
        foreach (var card in game.Player1.HandZone.Where(card => premiums.ContainsKey(card.Id)))
            AssertPremiumReplay(capture, card.Id, premiums[card.Id], "hand " + card.Card.Id);

        var derivedMap = DerivedPremiums(match);
        // Before the checkpoint: a real Golden Polymorph on a Normal own minion; its derived premium must survive the Rewind.
        game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
        var morphed = PremiumCard(match, "CS2_182", 0, board: true);
        var morphSpell = PremiumCard(match, "CS2_022", 1);
        PlayPremiumCard(match, capture, morphSpell, morphed);
        Check(derivedMap.TryGetValue(morphed.Id, out int morphedPremium) && morphedPremium == 1, "pre-checkpoint Golden Polymorph derived nothing");
        var transformed = PremiumCard(match, "CS2_182", 0); // A Normal minion in hand at the checkpoint.
        FlushHistory(match);

        // Rewind: GAME_RESET (SourceIsRewind) restores a mid-game snapshot; its FULLs keep each copy's premium.
        game.Player1.BaseMana = 10; game.Player1.UsedMana = 0;
        var rewindCard = Generic.DrawCard(game.Player1, Cards.FromId("TIME_EVENT_999"));
        FlushHistory(match);
        var premiumHand = game.Player1.HandZone.Where(card => premiums.ContainsKey(card.Id)).Select(card => card.Id).ToArray();
        Check(premiumHand.Length > 0, "rewind fixture has no premium hand card");
        int rewindCursor = capture.Packets.Count;
        InvokeProcess(match, game.Player1.Options().First(task => task is PlayCardTask && task.Source?.Id == rewindCard.Id));
        FlushHistory(match);
        // After the checkpoint (a transform/creation the Rewind undoes; a real play is impossible with the choice pending).
        derivedMap[900001] = 1; derivedMap[transformed.Id] = 1;
        var sendChoice = typeof(NativeMatch).GetMethod("SendHumanChoice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        for (int guard = 0; guard < 6 && !NativeHistory(capture, rewindCursor).Any(entry => entry.HasResetGame); guard++)
        {
            int before = capture.Packets.Count;
            sendChoice.Invoke(match, null);
            Check(capture.Packets.Skip(before).Any(packet => packet.Id == 17), "rewind fixture has no human choice at step " + guard);
            var offer = capture.Last<EntityChoices>(17);
            int pick = game.Rewind?.RewindEntityId is int rewindId && offer.Entities.Contains(rewindId) ? rewindId : offer.Entities[0];
            match.SendChoices(offer.Id, new List<int> { pick });
        }
        var afterRewind = NativeHistory(capture, rewindCursor);
        var reset = afterRewind.FirstOrDefault(entry => entry.HasResetGame);
        Check(reset != null && reset.ResetGame.SourceIsRewind, "rewind fixture produced no Rewind GAME_RESET");
        var restoredHand = premiumHand.Where(id => game.IdEntityDic.ContainsKey(id) && game.IdEntityDic[id].Zone?.Type == EngineZone.HAND).ToArray();
        Check(restoredHand.Length > 0, "rewind left no premium card in hand to verify");
        foreach (int id in restoredHand)
        {
            var restored = afterRewind.SkipWhile(entry => !entry.HasResetGame).Where(entry => entry.HasFullEntity && entry.FullEntity.Entity == id).ToList();
            Check(restored.Count > 0, "rewind did not restore premium hand card " + id);
            foreach (var entry in restored) AssertPremiumSnapshot(entry, premiums[id], "rewind restore " + id);
            AssertPremiumReplay(capture, id, premiums[id], "after rewind " + id);
            Check(match.SnapshotVisibleTransportTags(game.IdEntityDic[id])[(int)GameTag.PREMIUM] == premiums[id],
                "snapshot audit after rewind lost PREMIUM");
        }
        // Rewind purge: derived entries for IDs the snapshot never had are dropped; older ones stay.
        int resetMax = afterRewind.SkipWhile(entry => !entry.HasResetGame).Where(entry => entry.HasFullEntity).Max(entry => entry.FullEntity.Entity);
        var restoredTransformed = afterRewind.SkipWhile(entry => !entry.HasResetGame)
            .Where(entry => entry.HasFullEntity && entry.FullEntity.Entity == transformed.Id).ToList();
        Check(restoredTransformed.Count > 0, "rewind did not restore the Normal minion");
        foreach (var entry in restoredTransformed) AssertPremiumSnapshot(entry, 0, "rewind restore of an undone transform");
        AssertPremiumReplay(capture, transformed.Id, 0, "after rewind of an undone transform");
        Check(!derivedMap.ContainsKey(transformed.Id) && !derivedMap.ContainsKey(900001) && morphed.Id <= resetMax
            && derivedMap.TryGetValue(morphed.Id, out int keptPremium) && keptPremium == 1 && derivedMap.Keys.All(id => id <= resetMax),
            "rewind did not restore the checkpoint's derived premiums");
        var restoredMorph = afterRewind.SkipWhile(entry => !entry.HasResetGame)
            .Where(entry => entry.HasFullEntity && entry.FullEntity.Entity == morphed.Id).ToList();
        Check(restoredMorph.Count > 0, "rewind did not restore the polymorphed minion");
        foreach (var entry in restoredMorph) AssertPremiumSnapshot(entry, 1, "rewind restore of a pre-checkpoint transform");
        AssertPremiumReplay(capture, morphed.Id, 1, "after rewind of a pre-checkpoint transform");
        capture.SerializeAll();

        // Presentation only: the same seed plays to the same outcome with and without premium copies.
        var normalRun = new Capture();
        var normalMatch = new NativeMatch(normalRun.Send, deck, "warrior", seed);
        normalMatch.Start();
        normalMatch.SendChoices(normalRun.Last<EntityChoices>(17).Id, new List<int>());
        DriveToTerminal(normalMatch, normalRun);
        var premiumRun = new Capture();
        var premiumMatch = new NativeMatch(premiumRun.Send, deck, "warrior", seed, 0, entries);
        premiumMatch.Start();
        premiumMatch.SendChoices(premiumRun.Last<EntityChoices>(17).Id, new List<int>());
        DriveToTerminal(premiumMatch, premiumRun);
        Check(premiumMatch.Game.State == State.COMPLETE && normalMatch.Game.State == State.COMPLETE
            && premiumMatch.Game.Turn == normalMatch.Game.Turn
            && premiumMatch.Game.Player1.PlayState == normalMatch.Game.Player1.PlayState
            && premiumMatch.Game.Player1.Hero.Health == normalMatch.Game.Player1.Hero.Health
            && premiumMatch.Game.Player2.Hero.Health == normalMatch.Game.Player2.Hero.Health,
            "premium copies changed the match outcome");
        Check(HistorySignature(NativeHistory(normalRun)).SequenceEqual(HistorySignature(NativeHistory(premiumRun))),
            "premium copies changed non-premium native history");
        Check(NativeHistory(premiumRun).Any(entry => SnapshotTag(entry, GameTag.PREMIUM) != 0),
            "premium terminal run never published PREMIUM");
        premiumRun.SerializeAll();

        DerivedPremiumHistory();
        GoldenHeroHistory();
        Console.WriteLine("PASS premium deck copies and golden hero publish PREMIUM without changing play");
    }

    private static Dictionary<int, int> DerivedPremiums(NativeMatch match) => (Dictionary<int, int>)typeof(NativeMatch)
        .GetField("derivedPremiums", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match);

    private static SabberStoneCore.Model.Entities.IPlayable PremiumCard(NativeMatch match, string id, int premium, bool board = false, bool opponent = false)
    {
        var owner = opponent ? match.Game.Player2 : match.Game.Player1;
        var card = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(id), zone: board ? owner.BoardZone : owner.HandZone);
        if (premium != 0)
        {
            ((Dictionary<int, int>)typeof(NativeMatch).GetField("entityPremiums",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(match))[card.Id] = premium;
        }
        return card;
    }

    // Plays source (optionally at target) as the human; returns the native history of the play.
    private static List<PowerHistoryData> PlayPremiumCard(NativeMatch match, Capture capture, SabberStoneCore.Model.Entities.IPlayable source,
        SabberStoneCore.Model.Entities.IPlayable target = null)
    {
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, match.Game.Player1.Options().First(task => task.PlayerTaskType == PlayerTaskType.PLAY_CARD
            && task.Source?.Id == source.Id && task.Target?.Id == target?.Id));
        FlushFieldHistory(match);
        return History(capture, cursor);
    }

    private static void AssertDerived(Capture capture, List<PowerHistoryData> played, int entity, int premium, string label)
    {
        var snapshots = played.Where(entry => SnapshotEntity(entry) == entity && SnapshotName(entry).Length > 0).ToList();
        Check(snapshots.Count > 0, label + " was never published");
        foreach (var entry in snapshots) AssertPremiumSnapshot(entry, premium, label);
        AssertPremiumReplay(capture, entity, premium, label);
    }

    // Retail: a created card takes the highest quality among the cards involved in creating it.
    private static void DerivedPremiumHistory()
    {
        // Resolver: Diamond -> Diamond/Signature/Golden by the result card's own quality flags.
        var diamondCard = Cards.All.FirstOrDefault(card => card[(GameTag)DiamondQuality] == 1);
        Check(diamondCard != null, "no Diamond-flagged card definition exists to test the resolver with");
        var signatureCard = Cards.All.First(card => card[(GameTag)SignatureQuality] == 1 && card[(GameTag)DiamondQuality] != 1);
        var plainCard = Cards.FromId("CS2_182");
        Check(plainCard[(GameTag)DiamondQuality] == 0 && plainCard[(GameTag)SignatureQuality] == 0, "plain fixture card carries a quality flag");
        Check(NativeMatch.ResolvePremium(2, signatureCard) == 3 && NativeMatch.ResolvePremium(2, plainCard) == 1
            && NativeMatch.ResolvePremium(3, signatureCard) == 3 && NativeMatch.ResolvePremium(3, plainCard) == 1
            && NativeMatch.ResolvePremium(1, signatureCard) == 1 && NativeMatch.ResolvePremium(1, plainCard) == 1
            && NativeMatch.ResolvePremium(0, signatureCard) == 0 && NativeMatch.ResolvePremium(2, null) == 1,
            "premium resolver fallback is wrong");
        Check(NativeMatch.ResolvePremium(2, diamondCard) == 2 && NativeMatch.ResolvePremium(3, diamondCard) == 1
                && NativeMatch.ResolvePremium(1, diamondCard) == 1, "premium resolver ignored the Diamond definition");

        // a. Golden First Flame -> Second Flame is Golden; a Normal one stays Normal; Signature/Diamond fall back to Golden.
        foreach (int sourcePremium in new[] { 0, 1, 3, 2 })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var flame = PremiumCard(match, "CORE_SW_108", sourcePremium);
            var target = PremiumCard(match, "CS2_182", 0, board: true);
            var before = game.Player1.HandZone.Select(card => card.Id).ToHashSet();
            var played = PlayPremiumCard(match, capture, flame, target);
            var created = game.Player1.HandZone.Where(card => !before.Contains(card.Id)).ToArray();
            Check(created.Length == 1, "First Flame did not add exactly one card");
            int expected = NativeMatch.ResolvePremium(sourcePremium, created[0].Card);
            Check(sourcePremium == 0 ? expected == 0 : expected == 1 || (sourcePremium == 3 && created[0].Card[(GameTag)SignatureQuality] == 1),
                "unexpected resolved premium for source " + sourcePremium);
            AssertDerived(capture, played, created[0].Id, expected, "Second Flame from source " + sourcePremium);
            Check(sourcePremium == 0 ? !DerivedPremiums(match).ContainsKey(created[0].Id) : DerivedPremiums(match)[created[0].Id] == expected,
                "derived premium bookkeeping for source " + sourcePremium);
            Check(played.Where(entry => SnapshotEntity(entry) == target.Id).All(entry => SnapshotTag(entry, GameTag.PREMIUM) == 0),
                "First Flame's Normal target gained premium");
            capture.SerializeAll();
        }

        // c. A golden minion's battlecry token is Golden.
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var hunter = PremiumCard(match, "CS2_196", 1);
            var before = game.Player1.BoardZone.Select(card => card.Id).ToHashSet();
            var played = PlayPremiumCard(match, capture, hunter);
            var token = game.Player1.BoardZone.Single(card => !before.Contains(card.Id) && card.Id != hunter.Id);
            AssertDerived(capture, played, token.Id, 1, "golden battlecry token");
            AssertDerived(capture, played, hunter.Id, 1, "golden summoner");
            capture.SerializeAll();
        }

        // d. A Normal copier of a targeted golden minion (Faceless Manipulator) becomes Golden; of a Normal minion stays Normal.
        foreach (int targetPremium in new[] { 1, 0 })
        {
            var (match, capture) = AnimationMatch();
            var copier = PremiumCard(match, "EX1_564", 0);
            var target = PremiumCard(match, "CS2_182", targetPremium, board: true);
            var played = PlayPremiumCard(match, capture, copier, target);
            var change = played.Where(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == copier.Id).ToList();
            Check(change.Count == 1 && change[0].ChangeEntity.Name == "CS2_182", "Faceless Manipulator did not become a copy");
            AssertPremiumSnapshot(change[0], targetPremium, "Faceless copy of premium " + targetPremium);
            AssertPremiumReplay(capture, copier.Id, targetPremium, "Faceless copy of premium " + targetPremium);
            capture.SerializeAll();
        }

        // e. Golden Polymorph transforms a Normal minion on either side to Golden (wiki: quality is contagious); Normal stays Normal.
        foreach (var scenario in new[] { (Spell: 1, Opponent: false, Expected: 1), (Spell: 1, Opponent: true, Expected: 1),
                                         (Spell: 0, Opponent: false, Expected: 0), (Spell: 3, Opponent: false, Expected: 1) })
        {
            var (match, capture) = AnimationMatch();
            var spell = PremiumCard(match, "CS2_022", scenario.Spell);
            var target = PremiumCard(match, "CS2_182", 0, board: true, opponent: scenario.Opponent);
            var played = PlayPremiumCard(match, capture, spell, target);
            var change = played.Where(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == target.Id).ToList();
            Check(change.Count == 1, "Polymorph did not change its target");
            string label = "Polymorph spell=" + scenario.Spell + " opponent=" + scenario.Opponent;
            AssertPremiumSnapshot(change[0], scenario.Expected, label);
            AssertPremiumReplay(capture, target.Id, scenario.Expected, label);
            capture.SerializeAll();
        }

        // b. A Signature or Diamond entity transformed into a card without those flags is Golden (PREMIUM=1, both flags 0).
        foreach (int owned in new[] { 3, 2 })
        foreach (int spellPremium in new[] { 0, 1 })
        {
            var (match, capture) = AnimationMatch();
            var spell = PremiumCard(match, "CS2_022", spellPremium);
            var target = PremiumCard(match, "CS2_182", owned, board: true);
            var played = PlayPremiumCard(match, capture, spell, target);
            var change = played.Single(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == target.Id);
            string label = "premium " + owned + " minion polymorphed by spell " + spellPremium;
            AssertPremiumSnapshot(change, 1, label);
            AssertPremiumReplay(capture, target.Id, 1, label);
            capture.SerializeAll();
        }

        // A held premium resolves downward on a transform whoever controls the entity (a stolen Signature minion, Hexed).
        {
            var (match, capture) = AnimationMatch();
            var spell = PremiumCard(match, "CS2_022", 0);
            var stolen = PremiumCard(match, "CS2_182", 3, board: true, opponent: true);
            var played = PlayPremiumCard(match, capture, spell, stolen);
            var change = played.Single(entry => entry.HasChangeEntity && entry.ChangeEntity.Entity == stolen.Id);
            AssertPremiumSnapshot(change, 1, "stolen Signature minion polymorphed");
            AssertPremiumReplay(capture, stolen.Id, 1, "stolen Signature minion polymorphed");
            capture.SerializeAll();
        }

        // Enchantment-owned deathrattles count the granting card and the card they sit on (Soul of the Forest).
        foreach (var scenario in new[] { (Soul: 1, Minion: 0, Expected: 1), (Soul: 0, Minion: 1, Expected: 1), (Soul: 0, Minion: 0, Expected: 0) })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var minion = PremiumCard(match, "CS2_182", scenario.Minion, board: true);
            PlayPremiumCard(match, capture, PremiumCard(match, "EX1_158", scenario.Soul));
            var before = game.Player1.BoardZone.Select(card => card.Id).ToHashSet();
            var played = PlayPremiumCard(match, capture, PremiumCard(match, "CS2_029", 0), minion);
            var treant = game.Player1.BoardZone.Single(card => !before.Contains(card.Id));
            Check(treant.Card.Id == "EX1_158t", "Soul of the Forest deathrattle did not summon a Treant");
            AssertDerived(capture, played, treant.Id, scenario.Expected, "Soul of the Forest soul=" + scenario.Soul + " minion=" + scenario.Minion);
            capture.SerializeAll();
        }

        // Trigger blocks target the card that fired them: only the trigger owner's quality counts (Archmage Antonidas).
        foreach (var scenario in new[] { (Owner: 0, Spell: 1, Expected: 0), (Owner: 1, Spell: 0, Expected: 1) })
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            PremiumCard(match, "EX1_559", scenario.Owner, board: true);
            var fireball = PremiumCard(match, "CS2_029", scenario.Spell);
            var victim = PremiumCard(match, "CS2_200", 0, board: true, opponent: true);
            var before = game.Player1.HandZone.Select(card => card.Id).ToHashSet();
            var played = PlayPremiumCard(match, capture, fireball, victim);
            var created = game.Player1.HandZone.Where(card => !before.Contains(card.Id)).ToArray();
            Check(created.Length == 1 && created[0].Card.Id == "CS2_029", "Antonidas did not add a Fireball");
            string label = "Antonidas owner=" + scenario.Owner + " spell=" + scenario.Spell;
            AssertDerived(capture, played, created[0].Id, scenario.Expected, label);
            capture.SerializeAll();
        }

        // e. A card discovered from a Golden source ends Golden in hand.
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            int maxBefore = game.IdEntityDic.Values.Max(en => en.Id);
            var scarab = PremiumCard(match, "BOT_243", 1);
            PlayPremiumCard(match, capture, scarab);
            typeof(NativeMatch).GetMethod("SendHumanChoice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(match, null);
            Check(game.Player1.Choice != null, "BOT_243 offered no Discover");
            var choice = capture.Last<EntityChoices>(17);
            int pick = choice.Entities[0];
            Check(pick > maxBefore + 1, "discover offer was not a created card");
            match.SendChoices(choice.Id, new List<int> { pick });
            FlushFieldHistory(match);
            Check(game.IdEntityDic.TryGetValue(pick, out var picked) && picked.Zone?.Type == EngineZone.HAND, "discovered card is not in hand");
            AssertPremiumReplay(capture, pick, 1, "discovered card from a Golden source");
            Check(match.SnapshotVisibleTransportTags(picked)[(int)GameTag.PREMIUM] == 1, "snapshot audit lost discovered PREMIUM");
            capture.SerializeAll();
        }

        // f. A golden King Mukla gives the opponent Golden Bananas, hidden (no PREMIUM on the wire) while in their hand.
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var king = PremiumCard(match, "EX1_014", 1);
            var before = game.Player2.HandZone.Select(card => card.Id).ToHashSet();
            var played = PlayPremiumCard(match, capture, king);
            var gifts = game.Player2.HandZone.Where(card => !before.Contains(card.Id)).ToArray();
            Check(gifts.Length == 2, "King Mukla did not give two Bananas");
            foreach (var gift in gifts)
            {
                Check(DerivedPremiums(match).TryGetValue(gift.Id, out int derivedGift) && derivedGift == 1, "opponent gift did not derive Golden");
                Check(played.Where(entry => SnapshotEntity(entry) == gift.Id).All(entry => SnapshotTag(entry, GameTag.PREMIUM) == 0
                    && SnapshotTag(entry, (GameTag)DiamondQuality) == 0 && SnapshotTag(entry, (GameTag)SignatureQuality) == 0),
                    "hidden opponent gift was published as premium");
                Check(!ReplayNativeEntityTags(NativeHistory(capture), gift.Id).TryGetValue((int)GameTag.PREMIUM, out int giftPremium) || giftPremium == 0,
                    "opponent gift carries PREMIUM");
            }
            capture.SerializeAll();
        }

        // g. A card shuffled into the player's deck is hidden (no PREMIUM) until a draw reveals it as Golden.
        {
            var (match, capture) = AnimationMatch();
            var game = match.Game;
            var torch = PremiumCard(match, "LOE_002", 1);
            var victim = PremiumCard(match, "CS2_200", 0, board: true, opponent: true);
            var before = game.Player1.DeckZone.Select(card => card.Id).ToHashSet();
            var played = PlayPremiumCard(match, capture, torch, victim);
            var shuffled = game.Player1.DeckZone.Single(card => !before.Contains(card.Id));
            Check(DerivedPremiums(match).TryGetValue(shuffled.Id, out int shuffledPremium) && shuffledPremium == 1, "shuffled card has no derived premium");
            var hiddenFull = played.Single(entry => entry.HasFullEntity && entry.FullEntity.Entity == shuffled.Id);
            Check(hiddenFull.FullEntity.Name == "" && SnapshotTag(hiddenFull, GameTag.PREMIUM) == 0
                && SnapshotTag(hiddenFull, (GameTag)DiamondQuality) == 0 && SnapshotTag(hiddenFull, (GameTag)SignatureQuality) == 0,
                "hidden shuffled FULL leaked premium");
            Check(played.Where(entry => entry.HasTagChange && entry.TagChange.Entity == shuffled.Id).All(entry => entry.TagChange.Tag != (int)GameTag.PREMIUM),
                "hidden shuffled card published a PREMIUM tag change");
            Check(!ReplayNativeEntityTags(NativeHistory(capture), shuffled.Id).TryGetValue((int)GameTag.PREMIUM, out int hiddenPremium) || hiddenPremium == 0,
                "hidden shuffled card carries PREMIUM on the client");
            int cursor = capture.Packets.Count;
            Generic.Draw(game.Player1, shuffled);
            FlushFieldHistory(match);
            var drawn = History(capture, cursor).Where(entry => entry.HasShowEntity && entry.ShowEntity.Entity == shuffled.Id).ToList();
            Check(drawn.Count > 0, "drawn shuffled card was not revealed");
            foreach (var entry in drawn) AssertPremiumSnapshot(entry, 1, "drawn shuffled card");
            AssertPremiumReplay(capture, shuffled.Id, 1, "drawn shuffled card");
            capture.SerializeAll();
        }
    }

    private static void AssertPremiumSnapshot(PowerHistoryData entry, int premium, string label)
    {
        Check(SnapshotTag(entry, GameTag.PREMIUM) == premium, label + " PREMIUM " + SnapshotTag(entry, GameTag.PREMIUM) + " != " + premium);
        if (premium == 0) return;
        Check(SnapshotTag(entry, (GameTag)DiamondQuality) == (premium == 2 ? 1 : 0), label + " has the wrong Diamond quality flag");
        Check(SnapshotTag(entry, (GameTag)SignatureQuality) == (premium == 3 ? 1 : 0), label + " has the wrong Signature quality flag");
    }

    // Merged client state after every FULL/SHOW/CHANGE and tag change, including printed-stat corrections.
    private static void AssertPremiumReplay(Capture capture, int entityId, int premium, string label)
    {
        var tags = ReplayNativeEntityTags(NativeHistory(capture), entityId);
        int Tag(int name) => tags.TryGetValue(name, out int value) ? value : 0;
        Check(Tag((int)GameTag.PREMIUM) == premium && Tag(DiamondQuality) == (premium == 2 ? 1 : 0)
            && Tag(SignatureQuality) == (premium == 3 ? 1 : 0), label + " merged premium tags are inconsistent");
    }

    private static string EngineIdentity(SabberStoneCore.Model.Game game) => string.Join(",",
        game.IdEntityDic.Values.OrderBy(entity => entity.Id).Select(entity => entity.Id + ":" + entity.Card.Id + ":" + entity.Zone?.Type));

    private static IEnumerable<string> HistorySignature(IEnumerable<PowerHistoryData> history)
    {
        bool Premium(int tag) => tag == (int)GameTag.PREMIUM || tag == DiamondQuality || tag == SignatureQuality;
        foreach (var entry in history)
        {
            var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity
                : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (snapshot != null)
                yield return (entry.HasFullEntity ? "F" : entry.HasShowEntity ? "S" : "C") + snapshot.Entity + ":" + snapshot.Name + ":"
                    + string.Join(",", snapshot.Tags.Where(tag => !Premium(tag.Name)).Select(tag => tag.Name + "=" + tag.Value));
            else if (entry.HasTagChange)
            {
                if (!Premium(entry.TagChange.Tag))
                    yield return "T" + entry.TagChange.Entity + ":" + entry.TagChange.Tag + "=" + entry.TagChange.Value;
            }
            else yield return entry.HasPowerStart ? "B" + entry.PowerStart.Source + ":" + entry.PowerStart.Type
                : entry.HasPowerEnd ? "E" : entry.HasHideEntity ? "H" + entry.HideEntity.Entity : "O";
        }
    }

    private static void GoldenHeroHistory()
    {
        using var fixture = new CosmeticsFixture();
        string path = Path.Combine(Path.GetTempPath(), "premium-hero-" + Guid.NewGuid() + ".xml");
        try
        {
            using (var created = new LocalProfile(path)) created.SelectCardBack(0);
            {
                var setup = new XmlSerializer(typeof(LocalProfileData));
                LocalProfileData seeded;
                using (var file = File.OpenRead(path)) seeded = (LocalProfileData)setup.Deserialize(file);
                seeded.GoldBalance = 10000;
                using (var file = File.Create(path)) setup.Serialize(file, seeded);
                using var buyer = new LocalProfile(path);
                buyer.Purchase(LocalShop.Default.Products.Single(product => product.HeroSkin == "HERO_08a").Id, 1, 1000, "premium-skin");
            }
            int HeroPremium(bool owned, string favorite, Action<LocalProfile> pick = null)
            {
                var serializer = new XmlSerializer(typeof(LocalProfileData));
                LocalProfileData data;
                using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
                data.PremiumCollection.RemoveAll(card => card.Id == "HERO_08");
                if (owned) data.PremiumCollection.Add(new LocalCardCount { Id = "HERO_08", Premium = 1, Count = 1, Acquired = 1 });
                data.FavoriteHeroSkins.RemoveAll(row => row.Class == "HERO_08");
                if (favorite != null) data.FavoriteHeroSkins.Add(new LocalHeroFavorite { Class = "HERO_08", HeroCardId = favorite });
                using (var file = File.Create(path)) serializer.Serialize(file, data);
                using var profile = new LocalProfile(path);
                pick?.Invoke(profile);
                NativeMatch.CosmeticsProfile = () => profile;
                var capture = new Capture();
                var match = new NativeMatch(capture.Send, "mage", "warrior", 1337);
                match.Start();
                var hero = NativeHistory(capture).Select(entry => entry.FullEntity)
                    .First(entity => entity != null && entity.Entity == match.Game.Player1.Hero.Id);
                Check(hero.Name == profile.FavoriteHero("HERO_08") && match.Game.Player1.Hero.Card.Id == "HERO_08",
                    "golden hero changed the portrait identity");
                Check(TagValue(hero.Tags, GameTag.PREMIUM)
                    == NativeUIProjection.FavoritePremium(profile, "HERO_08", NativeUIProjection.FavoriteHero(profile, "HERO_08")),
                    "match hero premium differs from the native favorite premium");
                var opponent = NativeHistory(capture).Select(entry => entry.FullEntity)
                    .First(entity => entity != null && entity.Entity == match.Game.Player2.Hero.Id);
                Check(TagValue(opponent.Tags, GameTag.PREMIUM) == 0, "opponent hero was published as premium");
                capture.SerializeAll();
                return TagValue(hero.Tags, GameTag.PREMIUM);
            }
            Check(HeroPremium(false, null) == 0, "unowned golden hero was published");
            Check(HeroPremium(true, null) == 1, "owned golden hero is not the class default");
            // A stored favorite equal to the vanilla hero means "no skin" (the picker stores it on every touch).
            Check(HeroPremium(true, "HERO_08") == 1, "vanilla favorite dropped the golden hero");
            Check(HeroPremium(false, "HERO_08") == 0, "unowned golden hero was published for the vanilla favorite");
            Check(HeroPremium(true, "HERO_08a") == 0, "alternate skin was published as golden");
            Check(HeroPremium(true, "HERO_08a", profile => NativeUIProjection.SelectHeroVersion(profile, "HERO_08", "HERO_08", true, 1)) == 1,
                "picking the Golden vanilla hero did not publish golden");
            Check(HeroPremium(true, "HERO_08a", profile => NativeUIProjection.SelectHeroVersion(profile, "HERO_08", "HERO_08a", false, 0)) == 1,
                "unfavoriting the skin (picker stores the vanilla hero) did not publish golden");
        }
        finally
        {
            NativeMatch.CosmeticsProfile = null;
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + "*")) File.Delete(file);
        }
    }
}
