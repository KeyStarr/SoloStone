using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void HandTextCounters()
    {
        CostMemoryChecks();
        GrimlynxTextTracksCurrentStats();
        EnchantmentTextCapturesInstanceValues();
        DeathAndAlternateTextUpdates();
        RagerTextTracksOwnTurns();
        StandardTextDefaultsReachFirstReveal();
        FormattedTextDefaultsReachFirstReveal();
        Console.WriteLine("PASS live hand text counters, reveal defaults, alternate text and hidden privacy");
    }

    private static int SentTextTag(Capture capture, int entity, int tag)
    {
        var tags = ReplayNativeEntityTags(NativeHistory(capture), entity);
        Check(tags.ContainsKey(tag), $"entity {entity} missing native text tag {tag}");
        return tags[tag];
    }

    private static void FormattedTextDefaultsReachFirstReveal()
    {
        using var file = System.IO.File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
        using var export = System.Text.Json.JsonDocument.Parse(gzip);
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        // SilverHandCardTextBuilder composes {0} from controller Recruit buffs, not script numbers.
        foreach (string id in new[] { "CORE_GVG_061", "OG_273", "AT_100" })
        {
            var definition = Cards.FromId(id);
            string text = export.RootElement.GetProperty("cards").EnumerateArray()
                .Single(card => card.GetProperty("cardId").GetString() == id).GetProperty("text").GetString();
            Check(text.Contains("{0}") && !text.Contains("@"), id + " formatted text without alternate text");
            // Legacy engine definitions predate composed Recruit text; exercise the pinned client's definition.
            var textProperty = typeof(SabberStoneCore.Model.Card).GetProperty("Text");
            string original = definition.Text;
            textProperty.SetValue(definition, text);
            try
            {
                var held = Generic.DrawCard(match.Game.Player1, definition);
                var hidden = Generic.DrawCard(match.Game.Player2, definition);
                FlushHistory(match);
                var reveal = NativeHistory(capture).First(e => e.HasFullEntity && e.FullEntity.Entity == held.Id).FullEntity;
                foreach (int tag in new[] { 2, 3, 955, 1115, 1116 })
                {
                    Check(reveal.Tags.SingleOrDefault(t => t.Name == tag)?.Value == 0, id + " first FULL default " + tag);
                    Check(!ReplayNativeEntityTags(NativeHistory(capture), hidden.Id).ContainsKey(tag), id + " hidden text privacy " + tag);
                }
                int cursor = capture.Packets.Count;
                SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> {
                    SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(held)
                });
                var show = NativeHistory(capture, cursor).Single(e => e.HasShowEntity && e.ShowEntity.Entity == held.Id).ShowEntity;
                Check(show.Tags.Single(t => t.Name == 2).Value == 0 && show.Tags.Single(t => t.Name == 3).Value == 0,
                    id + " SHOW preserves zero defaults");
            }
            finally { textProperty.SetValue(definition, original); }
        }
        var plain = Generic.DrawCard(match.Game.Player1, Cards.FromId("CS2_231"));
        FlushHistory(match);
        var plainReveal = NativeHistory(capture).First(e => e.HasFullEntity && e.FullEntity.Entity == plain.Id).FullEntity;
        Check(!plainReveal.Tags.Any(t => t.Name == 2 || t.Name == 3), "plain text gains no script defaults");
        capture.SerializeAll();
    }

    private static void RagerTextTracksOwnTurns()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        var owner = game.Player1;
        var held = (Minion)Generic.DrawCard(owner, Cards.FromId("TIME_048"));
        var hidden = Generic.DrawCard(game.Player2, Cards.FromId("TIME_048"));
        game.DeathProcessingAndAuraUpdate();
        FlushHistory(match);
        var firstTurnSnapshot = SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(held);
        for (int turn = 1; turn <= 5; turn++)
        {
            Check(game.CurrentPlayer == owner && SentTextTag(capture, held.Id, 2) == turn,
                $"Clockwork Rager must show {turn} own begun turns in native hand text");
            Check(held.Health == 1, "text refresh must not apply Rager's Battlecry");
            var privateTags = ReplayNativeEntityTags(NativeHistory(capture), hidden.Id);
            Check(!privateTags.ContainsKey(2) && !privateTags.ContainsKey(3) && !privateTags.ContainsKey(955),
                "opponent hand leaked script data or alternate text");
            Check(!NativeHistory(capture).Any(e => (e.HasFullEntity && e.FullEntity.Entity == hidden.Id && e.FullEntity.Name != "")
                || (e.HasShowEntity && e.ShowEntity.Entity == hidden.Id && e.ShowEntity.Name != "")),
                "opponent Rager identity leaked");
            if (turn == 5) break;
            Check(game.Process(EndTurnTask.Any(owner)), "human end accepted");
            FlushHistory(match);
            Check(SentTextTag(capture, held.Id, 2) == turn, "enemy turn must not increase Rager text");
            Check(game.Process(EndTurnTask.Any(game.Player2)), "enemy end accepted");
            FlushHistory(match);
        }
        int historicalCursor = capture.Packets.Count;
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> { firstTurnSnapshot });
        Check(ReplayNativeEntityTags(NativeHistory(capture, historicalCursor), held.Id)[2] == 1,
            "historical first-turn snapshot must not receive today's turn-five getter");
        var late = Generic.DrawCard(owner, Cards.FromId("TIME_048"));
        game.DeathProcessingAndAuraUpdate();
        FlushHistory(match);
        Check(SentTextTag(capture, late.Id, 2) == 5, "late reveal must include turns before draw");
        owner.BaseMana = 10; owner.UsedMana = 0;
        Check(game.Process(PlayCardTask.Any(owner, held)), "Rager play accepted");
        FlushHistory(match);
        Check(held.Health == 6 && SentTextTag(capture, held.Id, 2) == 5,
            "turn-five Rager keeps correct Battlecry and board text");
        game.AllOptionsMap.Clear(); // Transport option caches contain non-persistable PlayerTask lists.
        var state = game.SaveSnapshot();
        string history = game.PowerHistory.Print(false);
        FlushHistory(match);
        Check(state.SequenceEqual(game.SaveSnapshot()) && history == game.PowerHistory.Print(false),
            "projection must not change engine snapshot (including RNG) or history");
        capture.SerializeAll();
    }

    private static void GrimlynxTextTracksCurrentStats()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        var grim = (Minion)Generic.DrawCard(game.Player1, Cards.FromId("BE_850t"));
        var egg = Generic.DrawCard(game.Player1, Cards.FromId("CATA_210"));
        FlushHistory(match);
        Check(SentTextTag(capture, grim.Id, 2) == 5 && SentTextTag(capture, grim.Id, 3) == 2,
            "unbuffed Grimlynx static text is 5/2");
        Check(SentTextTag(capture, egg.Id, 2) == 2 && SentTextTag(capture, egg.Id, 3) == 1,
            "Twilight Egg static text is 2/1");
        foreach (var entry in NativeHistory(capture))
        {
            var snapshot = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : null;
            if (snapshot == null || (snapshot.Entity != grim.Id && snapshot.Entity != egg.Id) || snapshot.Name == "") continue;
            int n1 = snapshot.Tags.Single(tag => tag.Name == 2).Value;
            int n2 = snapshot.Tags.Single(tag => tag.Name == 3).Value;
            Check(n1 == (snapshot.Entity == grim.Id ? 5 : 2) && n2 == (snapshot.Entity == grim.Id ? 2 : 1),
                "static Grimlynx/Twilight Egg numbers reach initial FULL/SHOW before reconciliation");
        }
        grim.AttackDamage = 3; grim.BaseHealth = 7;
        var buffed = SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(grim);
        int cursor = capture.Packets.Count;
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> { buffed });
        var tags = ReplayNativeEntityTags(NativeHistory(capture, cursor), grim.Id);
        Check(tags[2] == 7 && tags[3] == 3, "buffed Grimlynx first SHOW uses current reversed stats");
        grim.AttackDamage = 5; grim.BaseHealth = 9; grim.Damage = 2;
        game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        Check(SentTextTag(capture, grim.Id, 2) == 7 && SentTextTag(capture, grim.Id, 3) == 5,
            "Grimlynx live hand buffs update current Health and Attack");
        foreach (var entry in new SabberStoneCore.Kettle.IPowerHistoryEntry[] {
            SabberStoneCore.Kettle.PowerHistoryBuilder.FullEntity(grim),
            SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(grim) })
        {
            cursor = capture.Packets.Count;
            SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> { entry });
            tags = ReplayNativeEntityTags(NativeHistory(capture, cursor), grim.Id);
            Check(tags[2] == 7 && tags[3] == 5, "Grimlynx FULL/SHOW both use current Health after damage");
        }
        cursor = capture.Packets.Count;
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> { buffed });
        tags = ReplayNativeEntityTags(NativeHistory(capture, cursor), grim.Id);
        Check(tags[2] == 7 && tags[3] == 3, "historical Grimlynx reveal retains captured reversed stats");
        game.AllOptionsMap.Clear();
        var state = game.SaveSnapshot();
        string history = game.PowerHistory.Print(false);
        FlushHistory(match);
        Check(state.SequenceEqual(game.SaveSnapshot()) && history == game.PowerHistory.Print(false),
            "Grimlynx display projection leaves rules, RNG and engine history unchanged");
        capture.SerializeAll();
    }

    private static void EnchantmentTextCapturesInstanceValues()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        foreach (string id in new[] { "EDR_850pe1", "FIR_914e", "TLC_426e2" })
        {
            foreach (var owner in new[] { game.Player1, game.Player2 })
            foreach (bool deck in new[] { false, true })
            {
                var target = Generic.DrawCard(owner, Cards.FromId("BE_850t"));
                if (deck) owner.DeckZone.Add(owner.HandZone.Remove(target));
                Generic.AddEnchantmentBlock(game, Cards.FromId(id), owner.Hero, target, 3, 5);
                var enchantment = target.AppliedEnchantments.Last();
                var snapshot = game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryShowEntity>()
                    .LastOrDefault(entry => entry.Entity.Id == enchantment.Id);
                // Deliberately defeat a current-getter fallback: SHOW must carry the creation-time values.
                enchantment.NativeTags[GameTag.TAG_SCRIPT_DATA_NUM_1] = 8;
                enchantment.NativeTags[GameTag.TAG_SCRIPT_DATA_NUM_2] = 9;
                int cursor = capture.Packets.Count;
                FlushHistory(match);
                var packets = NativeHistory(capture, cursor);
                var tags = ReplayNativeEntityTags(packets, enchantment.Id);
                if (owner == game.Player1 && !deck)
                {
                    Check(tags.TryGetValue(2, out int n1) && n1 == 3 && tags.TryGetValue(3, out int n2) && n2 == 5,
                        id + " SHOW forwards captured instance N1/N2");
                    Check(snapshot.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3
                        && snapshot.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_2] == 5, id + " engine SHOW captures payload");
                }
                else
                {
                    Check(!tags.ContainsKey(2) && !tags.ContainsKey(3), id + " hidden attachment leaks no numbers");
                    Check(!packets.Any(entry => (entry.HasShowEntity && entry.ShowEntity.Entity == enchantment.Id
                        && entry.ShowEntity.Name != "") || (entry.HasFullEntity && entry.FullEntity.Entity == enchantment.Id
                        && entry.FullEntity.Name != "")), id + " hidden hand/deck attachment leaks no identity");
                }
            }
        }
        var internalPayload = Enchantment.GetInstance(game.Player1, game.Player1.Hero, game.Player1.Hero,
            Cards.FromId("TLC_442e"), 79, 80);
        var internalShow = game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryShowEntity>()
            .Last(entry => entry.Entity.Id == internalPayload.Id);
        Check(!internalShow.Entity.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_1)
            && !internalShow.Entity.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2),
            "enchantments without numeric text must not forward internal script payloads");
        capture.SerializeAll();
    }

    private static void StandardTextDefaultsReachFirstReveal()
    {
        // These placeholders contain captured names or composed Zombeast text, not numeric values.
        var names = new HashSet<string> { "CATA_563e2", "CATA_897e3", "EDR_494e", "ICC_828t", "ICC_828e", "TLC_841e", "TLC_841t", "EDR_454t", "MEND_046t" };
        var inventory = SabberStoneCore.CardSets.Client.EffectModules.ByDbfId.Values
            .Concat(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds).Distinct()
            .Select(Cards.FromId).Where(card => !names.Contains(card.Id)
                // Entity-builder cards send an entity id (declared hosts) or nothing (masked): tag 2 is not a number there.
                && !SabberStoneCore.Actions.PayloadRules.ReferencesEntity(card, GameTag.TAG_SCRIPT_DATA_NUM_1)
                && !SabberStoneCore.Actions.PayloadRules.MasksReference(card)
                && card[SabberStoneCore.Actions.HeraldRules.Keyword] == 0
                && (card.Text?.Contains("@") == true
                    || System.Text.RegularExpressions.Regex.IsMatch(card.Text ?? "", @"\{\d+\}")))
            .OrderBy(card => card.Id).ToArray();
        Check(inventory.Any(card => card.Id == "BE_850t") && inventory.Any(card => card.Id == "CATA_210")
            && inventory.Any(card => card.Id == "EDR_850pe1"), "numeric inventory includes static tokens and attachments");
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        foreach (var definition in inventory)
        {
            foreach (var card in game.Player1.HandZone.ToArray())
                game.Player1.SetasideZone.Add(game.Player1.HandZone.Remove(card));
            if (definition.Type == CardType.ENCHANTMENT)
            {
                // Granted Deathrattles need a minion host; numeric-only attachments still use the hero.
                IPlayable host = definition.Power?.DeathrattleTask == null ? game.Player1.Hero
                    : EngineEntity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.BoardZone);
                var attachment = Enchantment.GetInstance(game.Player1, game.Player1.Hero, host, definition, 4, 6);
                FlushHistory(match);
                Check(SentTextTag(capture, attachment.Id, 2) == 4 && SentTextTag(capture, attachment.Id, 3) == 6,
                    definition.Id + " inventory attachment instance payload");
                continue;
            }
            var held = definition.Shatter?.ParentId == definition.Id
                ? SabberStoneCore.Actions.ShatterRules.GiveCombined(game.Player1, definition.Id, game.Player1.Hero)
                : EngineEntity.FromCard(game.Player1, definition, zone: game.Player1.HandZone);
            game.DeathProcessingAndAuraUpdate();
            FlushHistory(match);
            int number = definition.Id == "BE_850t" ? ((Minion)held).Health : held[GameTag.TAG_SCRIPT_DATA_NUM_1];
            int number2 = definition.Id == "BE_850t" ? ((Minion)held).AttackDamage : held[GameTag.TAG_SCRIPT_DATA_NUM_2];
            var textTags = ReplayNativeEntityTags(NativeHistory(capture), held.Id);
            Check(textTags.TryGetValue(2, out int sentNumber) && sentNumber == number, definition.Id + " first visible N1");
            Check(textTags.TryGetValue(3, out int sentNumber2) && sentNumber2 == number2, definition.Id + " first visible N2");
            if (definition.Id == "CATA_585")
            {
                Check(!held.NativeTags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_1), "Torch fixture uses an unset definition default");
                var reveal = NativeHistory(capture).First(e => e.HasFullEntity && e.FullEntity.Entity == held.Id).FullEntity;
                Check(reveal.Tags.Single(tag => tag.Name == 2).Value == 8,
                    "first FULL_ENTITY must carry Torch's definition-backed eight damage");
            }
        }
        Console.WriteLine($"PASS definition-derived numeric text inventory ({inventory.Length} cards)");
        capture.SerializeAll();
    }

    private static void DeathAndAlternateTextUpdates()
    {
        var capture = new Capture();
        var match = StartAtHumanTurn(capture);
        var game = match.Game;
        var owner = game.Player1;
        foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
        var starsurge = Generic.DrawCard(owner, Cards.FromId("EDR_941"));
        var aessina = Generic.DrawCard(owner, Cards.FromId("EDR_430"));
        var reality = Generic.DrawCard(owner, Cards.FromId("END_009"));
        var smoldering = new[] { "FIR_911", "FIR_914", "FIR_916" }
            .Select(id => Generic.DrawCard(owner, Cards.FromId(id))).ToArray();
        var brood = Generic.DrawCard(owner, Cards.FromId("CATA_132"));
        game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        Check(SentTextTag(capture, owner.Id, 412) == 0, "Aessina initial threshold count");
        CheckAessinaText(20);
        for (int count = 1; count <= 20; count++)
        {
            var victim = (Minion)EngineEntity.FromCard(owner, Cards.FromId(count == 1 ? "END_009t" : "CS2_231"), zone: owner.BoardZone);
            victim.ToBeDestroyed = true;
            game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
            Check(SentTextTag(capture, starsurge.Id, 2) == count + 1, "maintained Starsurge counter updates live");
            Check(SentTextTag(capture, owner.Id, 412) == count, "Aessina controller threshold updates through 19 left to Ready");
            Check(SentTextTag(capture, aessina.Id, 2) == count, "Aessina numeric notification invalidates native threshold text");
            Check(SentTextTag(capture, reality.Id, 2) == 1, "Reality counts friendly Treant deaths only");
            if (count >= 19) CheckAessinaText(20 - count);
        }
        void CheckAessinaText(int left)
        {
            // Same packet inputs and variant selection as pinned PlayerTagThresholdCardTextBuilder.
            int tag = SentTextTag(capture, aessina.Id, 1115);
            int threshold = SentTextTag(capture, aessina.Id, 1116);
            Check(tag == 412 && threshold == 20, "Aessina forwards definition-backed threshold metadata");
            var snapshots = NativeHistory(capture).Select(entry => entry.HasFullEntity ? entry.FullEntity
                : entry.HasShowEntity ? entry.ShowEntity : null)
                .Where(snapshot => snapshot != null && snapshot.Entity == aessina.Id && snapshot.Name != "").ToArray();
            Check(snapshots.Length >= 2 && snapshots.All(snapshot => snapshot.Tags.Any(t => t.Name == 1115 && t.Value == 412)
                && snapshot.Tags.Any(t => t.Name == 1116 && t.Value == 20)), "Aessina FULL/SHOW each capture threshold metadata");
            int deaths = SentTextTag(capture, owner.Id, tag);
            var variants = aessina.Card.Text.Split('@');
            string text = variants[0] + (deaths >= threshold ? variants[2]
                : string.Format(variants[1], threshold - deaths));
            Check(text.Contains(left == 0 ? "(Ready!)" : $"({left} left!)"), "Aessina packet text at " + deaths + " deaths");
        }
        owner.BaseMana = 10; owner.UsedMana = 0;
        var payment = Generic.DrawCard(owner, Cards.FromId("CS2_029")); payment.Cost = 8;
        Check(game.Process(PlayCardTask.Any(owner, payment, game.Player2.Hero)), "eight Mana payment accepted");
        FlushHistory(match);
        Check(SentTextTag(capture, brood.Id, 2) == 0 && SentTextTag(capture, brood.Id, 955) == 1,
            "Broodwatcher switches from remaining Mana to Ready");
        owner.UsedMana = 0;
        Check(game.Process(PlayCardTask.Any(owner, brood)), "ready Broodwatcher accepted"); FlushHistory(match);
        Check(SentTextTag(capture, brood.Id, 955) == 2, "board Broodwatcher uses progress-free text");
        for (int end = 0; end < 4; end++)
        {
            Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "end accepted"); FlushHistory(match);
            foreach (var spell in smoldering)
            {
                Check(SentTextTag(capture, spell.Id, 2) == (end < 2 ? 2 : 3), "Smoldering magnitude");
                Check(SentTextTag(capture, spell.Id, 3) == (end < 2 ? 2 : 1), "Smoldering discard countdown");
                Check(SentTextTag(capture, spell.Id, 955) == (end < 2 ? 0 : 1), "Smoldering final-turn whole-text variant");
            }
        }
        foreach (var spell in smoldering)
        {
            owner.SetasideZone.Add(owner.HandZone.Remove(spell));
            owner.HandZone.Add(owner.SetasideZone.Remove(spell));
        }
        game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        foreach (var spell in smoldering)
            Check(SentTextTag(capture, spell.Id, 2) == 1 && SentTextTag(capture, spell.Id, 3) == 3
                && SentTextTag(capture, spell.Id, 955) == 0, "Smoldering reentry clears stale final-turn text");
        capture.SerializeAll();
    }
}
