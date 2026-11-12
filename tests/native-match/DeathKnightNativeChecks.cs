using System;
using System.Linq;
using PegasusShared;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

internal static partial class Program
{
    private static void NativeDkSavePath()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dk-native-save-" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(root);
        var blood = Cards.FromId("CS2_182"); var frost = Cards.FromId("CS2_179");
        var b = (GameTag)2196; var f = (GameTag)2197;
        bool hadB = blood.Tags.TryGetValue(b, out int oldB), hadF = frost.Tags.TryGetValue(f, out int oldF);
        try
        {
            blood.Tags[b] = 2; frost.Tags[f] = 2;
            string path = System.IO.Path.Combine(root, "profile.xml");
            using var profile = new LocalProfile(path);
            var draft = profile.CreateDraft("DK native", "HERO_11");
            CollectionDeck Deck() => new CollectionDeck {
                ID = draft.Id, Name = draft.Name, HeroCardID = draft.Hero, Type = PegasusShared.DeckType.NORMAL_DECK,
                FormatType = NativeUIProbe.LocalDeckFormat, SourceType = DeckSourceType.DECK_SOURCE_TYPE_NORMAL
            };
            var working = Deck(); var prior = Deck();
            var header = NativeUIProbe.ExpectedHeader(draft.Id, LocalDecks.Draft(draft.Name, draft.Hero, draft.Cards), draft.Name, 1);
            void SetCards(params string[] ids)
            {
                working.GetSlots().Clear();
                foreach (string id in ids)
                {
                    var slot = new CollectionDeckSlot { CardID = id };
                    slot.SetCount(1, TAG_PREMIUM.NORMAL);
                    working.GetSlots().Add(slot);
                }
            }
            void Save()
            {
                var saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                NativeUIProbe.ProjectSavedDraft(saved, working, prior, header, 1);
            }
            SetCards(blood.Id);
            working.SetRuneOrder(new[] { RuneType.RT_NONE, RuneType.RT_BLOOD, RuneType.RT_BLOOD });
            Save();
            var expected = new[] { RuneType.RT_BLOOD, RuneType.RT_BLOOD, RuneType.RT_NONE };
            Check(working.GetRuneOrder().SequenceEqual(expected) && prior.GetRuneOrder().SequenceEqual(expected)
                && new[] { header.Rune1, header.Rune2, header.Rune3 }.SequenceEqual(expected)
                && profile.GetDraft(draft.Id).Cards.SequenceEqual(new[] { blood.Id }),
                "NativeDeckEditor legal save canonicalizes working/base/header runes");
            byte[] before = System.IO.File.ReadAllBytes(path);
            string headerBefore = System.Text.Json.JsonSerializer.Serialize(header, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
            void Rejected(string message)
            {
                bool rejected = false;
                try { Save(); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && System.IO.File.ReadAllBytes(path).SequenceEqual(before)
                    && System.Text.Json.JsonSerializer.Serialize(header, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }) == headerBefore
                    && prior.GetRuneOrder().SequenceEqual(expected) && prior.GetCardsWithCardID().SequenceEqual(new[] { blood.Id }), message);
            }
            SetCards(blood.Id, frost.Id);
            Rejected("NativeDeckEditor illegal BB+FF leaves profile/base/header unchanged");
            SetCards(frost.Id);
            working.SetRuneOrder(new[] { RuneType.RT_FROST, RuneType.RT_NONE, RuneType.RT_FROST });
            foreach (var mutate in new Action<CollectionDeck>[] {
                // Owned card backs, hero skins and the random-hero toggle are per-deck cosmetics (DeckCosmeticsSaveTests);
                // an unowned back (1) or coin (2) is still refused.
                deck => deck.CardBackID = 1, deck => deck.CosmeticCoinID = 2, deck => deck.PetID = 1,
                deck => deck.Locked = true })
            {
                working = Deck(); SetCards(frost.Id);
                working.SetRuneOrder(new[] { RuneType.RT_FROST, RuneType.RT_NONE, RuneType.RT_FROST });
                mutate(working);
                Rejected("NativeDeckEditor rune changes do not permit unrelated fixed metadata edits");
            }
            working = Deck(); SetCards(frost.Id);
            working.SetRuneOrder(new[] { RuneType.RT_FROST, RuneType.RT_NONE, RuneType.RT_FROST });
            Save();
            expected = new[] { RuneType.RT_FROST, RuneType.RT_FROST, RuneType.RT_NONE };
            Check(working.GetRuneOrder().SequenceEqual(expected) && prior.GetRuneOrder().SequenceEqual(expected)
                && new[] { header.Rune1, header.Rune2, header.Rune3 }.SequenceEqual(expected),
                "NativeDeckEditor legal rune change canonicalizes every projection");
            Console.WriteLine("PASS NativeDeckEditor DK save: canonical working/base/header; illegal runes atomic; fixed metadata rejected");
        }
        finally
        {
            if (hadB) blood.Tags[b] = oldB; else blood.Tags.Remove(b);
            if (hadF) frost.Tags[f] = oldF; else frost.Tags.Remove(f);
            System.IO.Directory.Delete(root, true);
        }
    }

    private static void DeathKnightNativeChecks()
    {
        NativeDkSavePath();
        var canonical = NativeDeckRunes.Order("HERO_11", new[] { "BE_325", "EDR_812" });
        Check(canonical.SequenceEqual(new[] { RuneType.RT_BLOOD, RuneType.RT_FROST, RuneType.RT_UNHOLY }), "HeaderRunesMatchCards rainbow");
        Check(NativeDeckRunes.Order("HERO_11", new[] { "TIME_611", "TIME_611" })
            .SequenceEqual(new[] { RuneType.RT_FROST, RuneType.RT_FROST, RuneType.RT_NONE }), "HeaderRunesMatchCards maxima");
        Check(NativeDeckRunes.Compatible("HERO_11", new[] { "BE_325", "EDR_812" }, canonical.Reverse())
            && NativeDeckRunes.Order("HERO_11", new[] { "EDR_812", "BE_325" }).SequenceEqual(canonical), "NativeRunePermutationNormalizes");
        Check(!NativeDeckRunes.Compatible("HERO_11", new[] { "TIME_611" }, canonical)
            && !NativeDeckRunes.Compatible("HERO_11", Array.Empty<string>(), new[] { (RuneType)99, RuneType.RT_NONE, RuneType.RT_NONE })
            && NativeDeckRunes.Order("HERO_08", new[] { "TIME_611" }).All(rune => rune == RuneType.RT_NONE), "native incompatible/unknown runes rejected; non-DK ignores runes");
        Check(NativeDeckRunes.Compatible("HERO_11", Array.Empty<string>(), new[] { RuneType.RT_BLOOD, RuneType.RT_BLOOD, RuneType.RT_BLOOD })
            && NativeDeckRunes.Order("HERO_11", Array.Empty<string>()).All(rune => rune == RuneType.RT_NONE), "unused rune selection canonicalizes to card minimum");
        foreach (string key in new[] { "mage", "test-deathknight-rainbow" })
        {
            var capture = new Capture();
            var match = new NativeMatch(capture.Send, key, "mage", 73);
            match.Start();
            var mulligan = capture.Last<PegasusGame.EntityChoices>(17);
            match.SendChoices(mulligan.Id, new System.Collections.Generic.List<int>());
            var game = match.Game; var player = game.Player1;
            Check(game.EnableCorpses, "live source match must enable corpses for every class");
            game.PowerHistory.Last.Clear();
            int cursor = capture.Packets.Count;
            CorpseRules.Gain(player, 5);
            Check(CorpseRules.TrySpend(player, 2), "corpse spend fixture");
            Check(!CorpseRules.TrySpend(player, 4), "insufficient corpse spend rejected");
            SendHistoryEntries(match, game.PowerHistory.Last);
            var changes = NativeHistory(capture, cursor).Where(entry => entry.HasTagChange
                && (entry.TagChange.Tag == 2186 || entry.TagChange.Tag == 2639)).Select(entry => entry.TagChange).ToArray();
            Check(changes.Length == 2 && changes[0].Entity == player.Id && changes[0].Tag == 2186 && changes[0].Value == 5
                && changes[1].Entity == player.Id && changes[1].Tag == 2639 && changes[1].Value == 2, "corpse gain/spend packets target player, cumulative counters");
            game.PowerHistory.Last.Clear(); cursor = capture.Packets.Count;
            Minion Summon(Controller controller)
            {
                var minion = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(controller, Cards.FromId("CS2_101t"));
                Generic.SummonBlock(game, minion, -1, null); return minion;
            }
            var first = Summon(player); var second = Summon(player); var enemy = Summon(game.Player2);
            first.Destroy(); second.Destroy(); enemy.Destroy(); game.DeathProcessingAndAuraUpdate(); game.GraveYard();
            SendHistoryEntries(match, game.PowerHistory.Last);
            changes = NativeHistory(capture, cursor).Where(entry => entry.HasTagChange
                && (entry.TagChange.Tag == 2186 || entry.TagChange.Tag == 2639)).Select(entry => entry.TagChange).ToArray();
            Check(changes.Length == 3 && changes.Count(change => change.Entity == player.Id) == 2
                && changes.Count(change => change.Entity == game.Player2.Id) == 1
                && changes.All(change => change.Tag == 2186), "simultaneous deaths emit exactly one earned change per minion");
            var playerTags = ReplayNativeEntityTags(NativeHistory(capture), player.Id);
            var enemyTags = ReplayNativeEntityTags(NativeHistory(capture), game.Player2.Id);
            Check(playerTags[2186] == 7 && playerTags[2639] == 2 && enemyTags[2186] == 1
                && CorpseRules.Available(player) == 5, "non-DK and DK corpse packet replay matches earned/spent/available");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS DK canonical/permuted runes; gain/spend/simultaneous/non-DK corpse packets");
    }
}
