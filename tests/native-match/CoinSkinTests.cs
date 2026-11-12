using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class CoinSkinTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(NativeMatch match, string name, object value) => typeof(NativeMatch).GetField(name, Private).SetValue(match, value);
    private static PegasusGame.PowerHistoryData Convert(NativeMatch match, IPowerHistoryEntry entry) =>
        (PegasusGame.PowerHistoryData)typeof(NativeMatch).GetMethod("ToNativeHistory", Private).Invoke(match, new object[] { entry });

    private static void Transport(int startPlayer)
    {
        var deck = LocalDecks.Get("mage");
        var game = new SabberStoneCore.Model.Game(new GameConfig {
            StartPlayer = startPlayer, RandomSeed = 7, Shuffle = true, History = true, SkipMulligan = false,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = deck.Build(), Player2Deck = deck.Build(), FormatType = FormatType.FT_WILD
        });
        game.StartGame();
        var packets = new List<object>();
        var match = new NativeMatch((id, packet) => packets.Add(packet), "mage", "warrior", 7);
        typeof(NativeMatch).GetProperty("Game").SetValue(match, game);
        Set(match, "cosmeticCoin", 5); Set(match, "cosmeticCoinCardId", "BAR_COIN3");
        typeof(NativeMatch).GetMethod("IdentifyStartingCoin", Private).Invoke(match, null);
        var second = game.FirstPlayer.Opponent;
        var coin = second.HandZone.Single(card => card.Card.Id == "GAME_005");
        string expected = startPlayer == 2 ? "BAR_COIN3" : "GAME_005";
        string visible = startPlayer == 2 ? expected : "";
        Check(match.TransportCardId(0, "GAME_005") == "GAME_005", "anonymous history source never maps a coin");
        Check(match.CosmeticCoinEntityId == (startPlayer == 2 ? coin.Id : 0), "only human second gift entity recorded");
        Check(coin[GameTag.CREATOR] == second.PlayerId && coin.Card.Id == "GAME_005", "engine coin id and gift creator unchanged");
        var full = game.PowerHistory.Last.OfType<PowerHistoryFullEntity>().Single(row => row.Entity.Id == coin.Id);
        Check(Convert(match, full).FullEntity.Name == visible, "FULL_ENTITY coin mapping preserves opponent privacy");
        Check(Convert(match, PowerHistoryBuilder.ShowEntity(coin)).ShowEntity.Name == visible, "SHOW_ENTITY coin mapping");
        var snapshot = (PegasusGame.PowerHistoryEntity)typeof(NativeMatch).GetMethod("ToNativeHistoryEntity", Private)
            .Invoke(match, new object[] { PowerHistoryBuilder.ShowEntity(coin).Entity, true, true, null, false });
        Check(snapshot.Name == expected && match.TransportCardId(coin.Id, coin.Card.Id) == expected, "snapshot/readback coin mapping");
        var block = new PowerHistoryBlockStart { Source = coin.Id, EffectCardId = "GAME_005", BlockType = BlockType.PLAY };
        Check(Convert(match, block).PowerStart.EffectCardId == expected, "history effect card id mapping");
        Convert(match, new PowerHistoryBlockEnd());
        var replaced = second.HandZone.Where(card => card.Id != coin.Id).Select(card => card.Id).ToArray();
        Check(game.Process(ChooseTask.Mulligan(second, new List<int>())), "replace all non-kept starting cards");
        Check(second.HandZone.Any(card => card.Id == coin.Id && card.Card.Id == "GAME_005")
            && replaced.All(id => !second.HandZone.Any(card => card.Id == id)), "mulligan replacement excludes canonical coin");
        var generated = Generic.DrawCard(second, Cards.FromId("GAME_005"));
        Check(match.TransportCardId(generated.Id, generated.Card.Id) == "GAME_005"
            && Convert(match, PowerHistoryBuilder.ShowEntity(generated)).ShowEntity.Name == (startPlayer == 2 ? "GAME_005" : ""), "later generated coin stays plain");
        Check(match.IsMappedStartingCoin(coin.Id, expected) == (startPlayer == 2), "mulligan recognizes per-deck starting coin without favorites");
        Check(!match.IsMappedStartingCoin(generated.Id, "BAR_COIN3") && !match.IsMappedStartingCoin(coin.Id, "GAME_005")
            && !match.IsMappedStartingCoin(0, "BAR_COIN3") && !match.IsMappedStartingCoin(coin.Id, "OTHER"), "mulligan coin predicate rejects copies, default identity and unrelated cards");
        if (startPlayer == 2)
        {
            // Tracking uses transport-only deck actors; Cultist Map uses bound engine offer actors.
            Check(game.Process(ChooseTask.Mulligan(game.Player2, new List<int>())), "finish first player mulligan");
            if (game.Step == Step.BEGIN_MULLIGAN) game.MainBegin();
            if (game.CurrentPlayer == game.Player2) Check(game.Process(EndTurnTask.Any(game.Player2)), "reach human turn");
            foreach (string discover in new[] { "CORE_DS1_184", "TLC_515" })
            {
                foreach (var old in second.HandZone.Concat(second.DeckZone).Where(p => p.Id != coin.Id).ToArray())
                    second.SetasideZone.Add(old.Zone.Remove(old));
                second.DeckZone.Add(coin.Zone.Remove(coin));
                second.BaseMana = 10; second.UsedMana = 0;
                var source = Generic.DrawCard(second, Cards.FromId(discover));
                Check(game.Process(second.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id)), "play coin discover " + discover);
                typeof(NativeMatch).GetMethod("SendHistory", Private).Invoke(match, new object[] { game.PowerHistory.Last });
                packets.Clear();
                typeof(NativeMatch).GetMethod("SendHumanChoice", Private).Invoke(match, null);
                var choice = packets.OfType<PegasusGame.EntityChoices>().Last();
                int offer = choice.Entities.Single();
                var names = packets.OfType<PegasusGame.PowerHistory>().SelectMany(p => p.List)
                    .SelectMany(row => new[] { row.FullEntity, row.ShowEntity }).Where(p => p != null && p.Entity == offer).ToArray();
                // Bound actors were sent in SendHistory, transport-only actors in SendHumanChoice.
                string offered = discover == "CORE_DS1_184" ? names.Single().Name
                    : Convert(match, PowerHistoryBuilder.ShowEntity(game.IdEntityDic[offer])).ShowEntity.Name;
                Check(offered == "BAR_COIN3" && match.TransportCardId(offer, "GAME_005") == "BAR_COIN3", "starting coin offer retains skin " + discover);
                typeof(NativeMatch).GetMethod("CompleteHumanChoice", Private).Invoke(match, new object[] { choice.Id, new List<int> { offer }, false });
                Check(coin.Zone == second.HandZone && coin.Card.Id == "GAME_005"
                    && Convert(match, PowerHistoryBuilder.ShowEntity(coin)).ShowEntity.Name == "BAR_COIN3", "chosen original draws with same skin " + discover);
            }
            foreach (var old in second.HandZone.Concat(second.DeckZone).Where(p => p.Id != coin.Id).ToArray())
                second.SetasideZone.Add(old.Zone.Remove(old));
            second.DeckZone.Add(coin.Zone.Remove(coin));
            second.BaseMana = 10; second.UsedMana = 0;
            var fate = Generic.DrawCard(second, Cards.FromId("TIME_432"));
            Check(game.Process(second.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == fate.Id)), "play bound copy discover");
            typeof(NativeMatch).GetMethod("SendHumanChoice", Private).Invoke(match, null);
            var copyChoice = packets.OfType<PegasusGame.EntityChoices>().Last();
            int copyOffer = copyChoice.Entities.Single();
            Check(game.IdEntityDic[copyOffer][DiscoverRealRules.BoundEntity] == coin.Id
                && Convert(match, PowerHistoryBuilder.ShowEntity(game.IdEntityDic[copyOffer])).ShowEntity.Name == "GAME_005",
                "Intertwined Fate's bound new-copy preview stays plain");
            typeof(NativeMatch).GetMethod("CompleteHumanChoice", Private).Invoke(match, new object[] { copyChoice.Id, new List<int> { copyOffer }, false });
            typeof(NativeMatch).GetMethod("SendHumanChoice", Private).Invoke(match, null);
            copyChoice = packets.OfType<PegasusGame.EntityChoices>().Last();
            typeof(NativeMatch).GetMethod("CompleteHumanChoice", Private).Invoke(match, new object[] { copyChoice.Id, new List<int> { copyChoice.Entities[0] }, false });
            var copiedCoin = second.HandZone.Single(card => card.Card.Id == "GAME_005");
            Check(coin.Zone == second.DeckZone && copiedCoin.Id != coin.Id && match.TransportCardId(copiedCoin.Id, copiedCoin.Card.Id) == "GAME_005",
                "bound discover delivers a plain copy and leaves skinned original in deck");
            second.HandZone.Add(second.DeckZone.Remove(coin));
            var copy = SabberStoneCore.Model.Entities.Entity.FromCard(second, Cards.FromId("GAME_005"), zone: second.SetasideZone);
            Generic.CreateChoice(second, generated, ChoiceType.GENERAL, ChoiceAction.HAND, new List<int> { copy.Id });
            Check(Convert(match, PowerHistoryBuilder.ShowEntity(copy)).ShowEntity.Name == "GAME_005"
                && !match.IsMappedStartingCoin(copy.Id, "BAR_COIN3"), "unbound discovered GAME_005 copy stays plain");
            Check(game.Process(ChooseTask.Pick(second, copy.Id)) && match.TransportCardId(copy.Id, copy.Card.Id) == "GAME_005", "discovered generated coin remains plain in hand");
        }
        // Aya upgrades in place. Its definition must win, including historical CHANGE_ENTITY snapshots.
        Generic.ChangeEntityBlock(second, coin, Cards.FromId("JAIL_504t"), true);
        var change = game.PowerHistory.Last.OfType<PowerHistoryChangeEntity>().Last(row => row.Entity.Id == coin.Id);
        Check(Convert(match, change).ChangeEntity.Name == (startPlayer == 2 ? "JAIL_504t" : "")
            && match.TransportCardId(coin.Id, "JAIL_504t") == "JAIL_504t", "Aya transform drops skin mapping");
        Check(!match.IsMappedStartingCoin(coin.Id, "BAR_COIN3"), "mulligan predicate rejects transformed starting entity");
        // Historical entries still describe their own point in time after the live entity transforms.
        Check(Convert(match, full).FullEntity.Name == visible, "old history keeps starting coin identity");
    }

    private static CollectionDeck Native(LocalCustomDeck deck) => new CollectionDeck {
        ID = deck.Id, Name = deck.Name, HeroCardID = deck.Hero, Type = PegasusShared.DeckType.NORMAL_DECK,
        FormatType = NativeUIProbe.LocalDeckFormat, SourceType = PegasusShared.DeckSourceType.DECK_SOURCE_TYPE_NORMAL,
        CosmeticCoinID = deck.Coin, RandomCoinUseFavorite = deck.RandomCoinUseFavorite
    };

    internal static void Run()
    {
        using var fixture = new CosmeticsFixture(coins: true, coinFive: true);
        string dir = Path.Combine(Path.GetTempPath(), "coin-skin-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "profile.xml");
        var oldSource = NativeMatch.NativeCoinCard;
        try
        {
            NativeMatch.NativeCoinCard = id => id == 5 ? "BAR_COIN3" : id == 1 ? "GAME_005" : null;
            using (var profile = new LocalProfile(path)) { profile.GrantWelcomeIfNew(); }
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
            data.GoldBalance = 10000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            long deckId;
            using (var profile = new LocalProfile(path))
            {
                profile.Purchase("coin-5", 1, 300, "coin-five");
                profile.SetCoinFavorite(5, true); profile.SetCoinFavorite(1, false);
                var saved = profile.CreateDraft("Coin Mage", "HERO_08"); deckId = saved.Id;
                Check(saved.Coin == null && saved.RandomCoinUseFavorite, "old/default deck random favorites");
                var prior = Native(saved); var working = Native(saved);
                working.CosmeticCoinID = 5; working.RandomCoinUseFavorite = false;
                saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                var header = new NetCache.DeckHeader();
                NativeUIProbe.ProjectSavedDraft(saved, working, prior, header, 3);
                Check(saved.Coin == 5 && !saved.RandomCoinUseFavorite && header.CosmeticCoin == 5
                    && !header.RandomCoinUseFavorite && prior.CosmeticCoinID == 5, "deck coin saves/projects to native");
                byte[] before = File.ReadAllBytes(path);
                NativeMatch.NativeCoinCard = id => null;
                var unavailable = NativeUIProbe.ExpectedHeader(saved.Id,
                    LocalProfile.LedgerDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards, false),
                    saved.Name, 3, profile.DeckCosmetics(saved.Id));
                Check(unavailable.CosmeticCoin == null && profile.GetDraft(saved.Id).Coin == 5
                    && profile.Purchases.Single().Granted.Coin == 5 && before.SequenceEqual(File.ReadAllBytes(path)),
                    "missing native coin row/card filtered from deck cache without changing saved selection or receipt");
                var missingNative = Native(saved); missingNative.CosmeticCoinID = unavailable.CosmeticCoin;
                Check(NativeUIProbe.NativeDeckCosmetics(profile, missingNative).Coin == 5,
                    "unrelated deck edit preserves an unavailable stored coin");
                NativeMatch.NativeCoinCard = id => id == 5 ? "BAR_COIN3" : id == 1 ? "GAME_005" : null;
                working.CosmeticCoinID = 3;
                bool refused = false;
                try { NativeUIProbe.CommitLocalDraft(profile, working, prior); } catch (InvalidOperationException) { refused = true; }
                Check(refused && before.SequenceEqual(File.ReadAllBytes(path)), "unowned coin refused without writes");
                working.CosmeticCoinID = 1;
                profile.CommitFailure = point => { if (point == "write") throw new IOException("coin save failure"); };
                bool failed = false;
                try { NativeUIProbe.CommitLocalDraft(profile, working, prior); } catch (IOException) { failed = true; }
                finally { profile.CommitFailure = null; }
                Check(failed && profile.GetDraft(deckId).Coin == 5 && before.SequenceEqual(File.ReadAllBytes(path)) && prior.CosmeticCoinID == 5,
                    "coin save failure keeps old bytes, memory and projection");
            }
            using (var profile = new LocalProfile(path))
            {
                Check(profile.GetDraft(deckId).Coin == 5 && !profile.GetDraft(deckId).RandomCoinUseFavorite
                    && profile.DeckCoin(deckId, new Random(1)) == 5, "deck coin survives restart");
                var working = Native(profile.GetDraft(deckId)); var prior = Native(profile.GetDraft(deckId));
                working.CosmeticCoinID = null; working.RandomCoinUseFavorite = true;
                NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(Enumerable.Range(0, 20).All(seed => profile.DeckCoin(deckId, new Random(seed)) == 5), "deck random favorites only");
                working.RandomCoinUseFavorite = false;
                NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(Enumerable.Range(0, 100).Select(seed => profile.DeckCoin(deckId, new Random(seed))).Distinct().OrderBy(id => id)
                    .SequenceEqual(new[] { 1, 5 }), "deck random all owned");
                profile.SetCoinFavorite(1, true);
                var rollA = Enumerable.Range(0, 40).Select(seed => profile.MatchCoin(new Random(NativeMatch.CoinSeed(seed)))).ToArray();
                var rollB = Enumerable.Range(0, 40).Select(seed => profile.MatchCoin(new Random(NativeMatch.CoinSeed(seed)))).ToArray();
                Check(rollA.SequenceEqual(rollB) && rollA.Distinct().Count() == 2, "seeded coin roll repeatable");
                var pairs = Enumerable.Range(0, 200).Select(seed => (
                    profile.MatchCoin(new Random(NativeMatch.CoinSeed(seed))),
                    new Random(NativeMatch.CardBackSeed(seed)).Next(2))).Distinct().Count();
                Check(pairs == 4 && NativeMatch.CoinSeed(7) != NativeMatch.CardBackSeed(7), "coin and back seed streams independent");
                NativeMatch.NativeCoinCard = id => id == 5 ? "BAR_COIN3" : null;
                NativeMatch.CosmeticsProfile = () => profile;
                profile.SetCoinFavorite(1, false);
                // Explicit deck choice wins even when the global favorite is default.
                profile.SaveDraftCosmetics(deckId, "Coin Mage", new LocalDeckCosmetics { Coin = 5 });
                profile.SetCoinFavorite(1, true); profile.SetCoinFavorite(5, false);
                var deckMatch = new NativeMatch((id, packet) => { }, "mage", "warrior", 7) { CosmeticDeckId = deckId };
                deckMatch.Start();
                Check(deckMatch.CosmeticCoin == 5 && deckMatch.CosmeticCoinCardId == "BAR_COIN3", "deck coin overrides global favorite at Start");
                NativeMatch.NativeCoinCard = id => null;
                var unresolved = new NativeMatch((id, packet) => { }, "mage", "warrior", 7) { CosmeticDeckId = deckId };
                unresolved.Start();
                Check(unresolved.CosmeticCoin == 5 && unresolved.CosmeticCoinCardId == null, "unresolvable cosmetic keeps plain presentation");
                NativeMatch.NativeCoinCard = id => id == 5 ? "BAR_COIN3" : null;
                profile.SetCoinFavorite(5, true); profile.SetCoinFavorite(1, false);
                profile.Begin("mage", "warrior", 7);
                var first = new NativeMatch((id, packet) => { }, "mage", "warrior", 7);
                first.Start();
                Check(first.CosmeticCoin == 5 && first.CosmeticCoinCardId == "BAR_COIN3" && first.CosmeticCoinEntityId == 0
                    && profile.Pending.CosmeticCoin == 5 && profile.Pending.CosmeticCoinCardId == "BAR_COIN3", "Start freezes resolved coin and human first maps nothing");
            }
            using (var profile = new LocalProfile(path))
            {
                profile.SetCoinFavorite(1, true); profile.SetCoinFavorite(5, false);
                NativeMatch.NativeCoinCard = id => throw new Exception("recovery must not resolve again");
                NativeMatch.CosmeticsProfile = () => profile;
                var resumed = new NativeMatch((id, packet) => { }, "mage", "warrior", 7);
                resumed.Start();
                Check(resumed.CosmeticCoin == 5 && resumed.CosmeticCoinCardId == "BAR_COIN3", "recovered match keeps persisted coin despite favorite/DBF change");
            }
            using (var profile = new LocalProfile(Path.Combine(dir, "default.xml")))
            {
                NativeMatch.CosmeticsProfile = () => profile;
                var plain = new NativeMatch((id, packet) => { }, "mage", "warrior", 7);
                plain.Start();
                Check(plain.CosmeticCoin == 1 && plain.CosmeticCoinCardId == null, "default coin never resolves a cosmetic mapping");
            }
            using (var profile = new LocalProfile(path))
            {
                NativeMatch.CosmeticsProfile = () => profile;
                NativeMatch.NativeCoinCard = id => id == 5 ? "BAR_COIN3" : null;
                profile.SetCoinFavorite(5, true); profile.SetCoinFavorite(1, false);
                foreach (string seat in new[] { "first", "second" })
                {
                    var staged = new NativeMatch((id, packet) => { }, "mage", "warrior", 7) {
                        DebugScenario = DebugScenario.Parse("humanSeat=" + seat) };
                    staged.StartingPlayer = staged.DebugScenario.FirstPlayer;
                    staged.Start();
                    Check(staged.DebugScenario.Canonical() == "humanSeat=" + seat
                        && staged.Game.FirstPlayer.PlayerId == (seat == "first" ? 1 : 2), "scenario seat round-trip and start");
                    var gift = staged.Game.FirstPlayer.Opponent.HandZone.Single(card => card.Card.Id == "GAME_005");
                    Check(staged.PresentationCardId(gift.Id, "GAME_005", gift.Controller.PlayerId)
                        == (seat == "second" ? "BAR_COIN3" : "GAME_005"), "scenario seat uses shared coin presentation");
                }
                var rejected = new NativeMatch((id, packet) => { }, "mage", "warrior", 7) {
                    StartingPlayer = 2, DebugScenario = DebugScenario.Parse("humanSeat=second\nhumanSkin=HERO_01bb") };
                rejected.Start();
                Check(rejected.DebugScenario == null && rejected.Game.FirstPlayer.PlayerId == 2
                    && rejected.CosmeticCoinEntityId > 0, "rejected skin scenario preserves frozen seat before coin mapping");
                bool invalidSeat = false;
                try { DebugScenario.Parse("humanSeat=third"); } catch (ArgumentException) { invalidSeat = true; }
                Check(invalidSeat, "unknown scenario seat rejected");
            }
            NativeMatch.CosmeticsProfile = null;
            Transport(2); Transport(1);
            Console.WriteLine("PASS coin skins: starting entity only, engine identity, mulligan, Aya, generated coin, history, deck save/restart, random and recovery");
        }
        finally { NativeMatch.CosmeticsProfile = null; NativeMatch.NativeCoinCard = oldSource; Directory.Delete(dir, true); }
    }
}
