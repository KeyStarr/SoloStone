using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

// Owner playtest 2026-11-03: multi-card deck shuffles flew one card after another. Retail SpawnToDeckSpell
// (AddPowerTargets) flies every DECK FULL of one SUB_SPELL together; PowerProcessor queues each SUB_SPELL as
// its own task list. One effect's consecutive insertions must therefore share one generated-shuffle scope.
internal static partial class Program
{
    private static readonly string[] HoggerLegends = { "JAIL_384", "EX1_012", "EX1_116", "EX1_014", "NEW1_029" };

    private static void ShuffleBatchHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            // Mid-game: Deathwing, Worldbreaker's Enthrall shuffles five Legendary Dragons (Generic.ShuffleIntoDeck loop).
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId("CATA_190h"), zone: owner.HandZone);
            source.Cost = 0;
            var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            int enthrall = owner.Choice.Choices.First(id => match.Game.IdEntityDic[id].Card.Id == "CATA_190t13");
            InvokeProcess(match, ChooseTask.Pick(owner, enthrall));
            FlushFieldHistory(match);
            var entries = WireHistory(capture, cursor);
            DumpShuffleBatch("deathwing-bot=" + bot, entries);
            var dragons = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
            string label = "Deathwing Enthrall/bot=" + bot;
            Check(dragons.Length == 5 && dragons.All(d => d.Cost == 1 && d.Card.IsRace(Race.DRAGON)), label + " shuffles five 1-Cost Dragons");
            AssertBatchedShuffle(entries, dragons, owner, source.Id, BlockType.POWER, label);
            AssertShufflePackets(capture, cursor, label);
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        {
            // Mid-game with an enchantment on each card: Azshara's Triumph buffs five minions before shuffling them.
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId("CATA_136"), zone: owner.HandZone);
            source.Cost = 0;
            var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            var entries = WireHistory(capture, cursor);
            DumpShuffleBatch("azshara-bot=" + bot, entries);
            var minions = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
            string label = "Azshara's Triumph/bot=" + bot;
            Check(minions.Length == 5, label + " shuffles five buffed minions");
            AssertBatchedShuffle(entries, minions, owner, source.Id, BlockType.POWER, label);
            AssertShufflePackets(capture, cursor, label);
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        foreach (string seats in new[] { "human", "bot", "both" })
        {
            // Start of Game: Chainbreaker Hogger copies every other Legendary (DeckZone.AddGenerated loop).
            bool human = seats != "bot", npc = seats != "human";
            var (sog, capture) = SogStart(human ? HoggerLegends : Array.Empty<string>(), npc ? HoggerLegends : Array.Empty<string>());
            var prior = sog.Game.IdEntityDic.Values.Select(c => c.Id).ToHashSet();
            SogComplete(sog, capture);
            var entries = WireHistory(capture);
            DumpShuffleBatch("hogger-" + seats, entries);
            var players = new[] { (On: human, Player: sog.Game.Player1), (On: npc, Player: sog.Game.Player2) };
            foreach (var (on, player) in players.Where(p => p.On))
            {
                string label = "Hogger Start of Game/" + seats + "/player=" + player.PlayerId;
                var hogger = player.DeckZone.Concat(player.HandZone).Concat(player.BoardZone).Single(c => c.Card.Id == "JAIL_384");
                var copies = player.DeckZone.Where(c => !prior.Contains(c.Id)).ToArray();
                Check(copies.Length == HoggerLegends.Length - 1 && copies.All(c => c.Card.Rarity == Rarity.LEGENDARY && c.Card.Id != "JAIL_384"),
                    label + " copies each other Legendary");
                AssertBatchedShuffle(entries, copies, player, hogger.Id, BlockType.TRIGGER, label);
            }
            Check(entries.Count(e => e.HasSubSpellStart && e.SubSpellStart.SpellPrefabGuid == GeneratedShufflePrefab) == (seats == "both" ? 2 : 1),
                "Hogger Start of Game/" + seats + " one batch per destination deck");
            AssertShufflePackets(capture, 0, "Hogger Start of Game/" + seats);
            AssertGeneratedShuffleAdmission(capture);
            capture.SerializeAll();
        }
        foreach (bool bot in new[] { false, true })
        foreach (string[] picks in new[] { new[] { "CATA_190t13", "CATA_190t10" }, new[] { "CATA_190t10", "CATA_190t13" } })
            MultiPickEnthrall(bot, picks);
        foreach (bool bot in new[] { false, true }) FeignDeathDoctor(bot);
        Console.WriteLine("PASS shuffle batching: Deathwing Enthrall (single and multi-pick), Azshara's Triumph, Feign Death mid-game, Chainbreaker Hogger Start of Game, both controllers; one spawn scope per effect, hidden identities");
    }

    // Deathwing tier 1: two picks, so a Choice leaves the POWER block open across the packet holding Enthrall's scope.
    private static void MultiPickEnthrall(bool bot, string[] picks)
    {
        var (match, capture) = AnimationMatch(bot);
        var owner = match.Game.CurrentPlayer;
        owner[SabberStoneCore.Actions.HeraldRules.Progress] = 2;
        var source = EngineEntity.FromCard(owner, Cards.FromId("CATA_190h"), zone: owner.HandZone);
        source.Cost = 0;
        var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
        FlushFieldHistory(match);
        foreach (string pick in picks)
        {
            InvokeProcess(match, ChooseTask.Pick(owner, owner.Choice.Choices.First(id => match.Game.IdEntityDic[id].Card.Id == pick)));
            FlushFieldHistory(match);
        }
        string label = "Deathwing tier 1 " + string.Join("+", picks) + "/bot=" + bot;
        Check(owner.Choice == null && !match.Game.Resolution.HasSuspendedEffect, label + " finishes both picks");
        var entries = WireHistory(capture, cursor);
        DumpShuffleBatch("deathwing-tier1-" + picks[0] + "-bot=" + bot, entries);
        var dragons = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
        Check(dragons.Length == 5, label + " shuffles five Dragons");
        AssertBatchedShuffle(entries, dragons, owner, source.Id, BlockType.POWER, label);
        AssertShufflePackets(capture, cursor, label);
        AssertGeneratedShuffleAdmission(capture);
        capture.SerializeAll();
    }

    // Real card path: Feign Death queues the Deathrattle in its own TRIGGER block, so the batch sits under the Doctor, not the cast.
    private static void FeignDeathDoctor(bool bot)
    {
        var (match, capture) = AnimationMatch(bot);
        var owner = match.Game.CurrentPlayer;
        var doctor = EngineEntity.FromCard(owner, Cards.FromId("JAIL_442"), zone: owner.BoardZone);
        var feign = EngineEntity.FromCard(owner, Cards.FromId("GVG_026"), zone: owner.HandZone);
        feign.Cost = 0;
        var before = owner.DeckZone.Select(c => c.Id).ToHashSet();
        FlushFieldHistory(match);
        int cursor = capture.Packets.Count;
        InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == feign.Id));
        FlushFieldHistory(match);
        string label = "Feign Death on Disguised Doctor/bot=" + bot;
        var blights = owner.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
        Check(blights.Length == 4 && blights.All(c => c.Card.Id == "JAIL_443t"), label + " shuffles four Blights");
        var entries = WireHistory(capture, cursor);
        DumpShuffleBatch("feign-doctor-bot=" + bot, entries);
        AssertBatchedShuffle(entries, blights, owner, doctor.Id, BlockType.TRIGGER, label);
        AssertShufflePackets(capture, cursor, label);
        AssertGeneratedShuffleAdmission(capture);
        capture.SerializeAll();
    }

    private static List<PowerHistoryData> WireHistory(Capture capture, int cursor = 0) =>
        capture.HistorySnapshots(cursor).SelectMany(packet => packet.List).ToList();

    // PowerProcessor.BuildTaskList: a SUB_SPELL_END ending a packet takes the no-continuation path and is tasked twice.
    private static void AssertShufflePackets(Capture capture, int cursor, string label)
    {
        foreach (var packet in capture.HistorySnapshots(cursor))
        {
            Check(packet.List.Count == 0 || !packet.List.Last().HasSubSpellEnd, label + " no packet ends on SUB_SPELL_END");
            Check(packet.List.Count(e => e.HasSubSpellStart) == packet.List.Count(e => e.HasSubSpellEnd), label + " spawn scope stays within one packet");
        }
    }

    // One zero-target spawn scope from the destination hero, inside the causal block, holding every inserted card's
    // anonymous DECK FULL, HIDE and TARGET, and no nested block or foreign rows.
    private static void AssertBatchedShuffle(List<PowerHistoryData> entries, IPlayable[] cards, Controller destination,
        int causal, BlockType causalType, string label)
    {
        var ids = cards.Select(c => c.Id).ToHashSet();
        int full = entries.FindIndex(e => e.HasFullEntity && ids.Contains(e.FullEntity.Entity));
        int start = full < 1 ? -1 : entries.FindLastIndex(full - 1, e => e.HasSubSpellStart || e.HasSubSpellEnd);
        Check(start >= 0 && entries[start].HasSubSpellStart, label + " first insertion opens a spawn scope");
        var scope = entries[start].SubSpellStart;
        Check(scope.SpellPrefabGuid == GeneratedShufflePrefab && scope.SourceEntityId == destination.Hero.Id && scope.TargetEntityIds.Count == 0,
            label + " zero-target generic spawn from the destination hero");
        int end = entries.FindIndex(start, e => e.HasSubSpellEnd);
        Check(end > start && !entries.Skip(start + 1).Take(end - start - 1).Any(e => e.HasSubSpellStart || e.HasPowerStart || e.HasPowerEnd),
            label + " scope holds no nested block or scope");
        var blocks = new Stack<PowerHistoryStart>();
        foreach (var entry in entries.Take(start))
        {
            if (entry.HasPowerStart) blocks.Push(entry.PowerStart);
            if (entry.HasPowerEnd) blocks.Pop();
        }
        Check(blocks.Any(b => b.Source == causal && (int)b.Type == (int)causalType), label + " scope inside causal " + causalType + " block");
        var inside = entries.Skip(start + 1).Take(end - start - 1).ToList();
        var flights = inside.Where(e => e.HasFullEntity && TagValue(e.FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.DECK)
            .Select(e => e.FullEntity.Entity).ToArray();
        Check(flights.Length == cards.Length && flights.ToHashSet().SetEquals(ids), label + " every inserted card is a flight target of the same scope");
        foreach (var card in cards)
        {
            int f = inside.FindIndex(e => e.HasFullEntity && e.FullEntity.Entity == card.Id);
            int h = inside.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == card.Id);
            Check(f >= 0 && h > f && inside[h].HideEntity.Zone == (int)EngineZone.DECK && h + 1 < inside.Count && inside[h + 1].HasMetaData
                && (int)inside[h + 1].MetaData.Type == (int)MetaDataType.TARGET && inside[h + 1].MetaData.Info.SequenceEqual(new[] { card.Id }),
                label + " FULL, HIDE then exact TARGET for " + card.Id);
            Check(entries.Where(e => SnapshotEntity(e) == card.Id).All(e => string.IsNullOrEmpty(SnapshotName(e))
                && HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.ShowEntity.Tags)), label + " hidden identity and definition tags for " + card.Id);
        }
        // Rows now inside the scope stay as hidden as the cards: no name, only the hidden-row tags, on every row of a buff.
        var game = cards[0].Game;
        var buffs = game.IdEntityDic.Values.OfType<SabberStoneCore.Model.Entities.Enchantment>().Where(e => e.Target != null && ids.Contains(e.Target.Id)).ToArray();
        foreach (var row in inside.Where(e => e.HasFullEntity || e.HasShowEntity))
            Check(string.IsNullOrEmpty(SnapshotName(row)) && HasOnlyHiddenTags(row.HasFullEntity ? row.FullEntity.Tags : row.ShowEntity.Tags),
                label + " in-scope row " + SnapshotEntity(row) + " is anonymous with only hidden tags");
        foreach (var buff in buffs)
            Check(entries.Where(e => SnapshotEntity(e) == buff.Id).All(e => string.IsNullOrEmpty(SnapshotName(e))
                && HasOnlyHiddenTags(e.HasFullEntity ? e.FullEntity.Tags : e.ShowEntity.Tags)), label + " buff " + buff.Card.Id + " stays anonymous");
        var hiddenRows = ids.Concat(buffs.Select(e => e.Id)).ToHashSet();
        Check(entries.Where(e => e.HasTagChange && hiddenRows.Contains(e.TagChange.Entity)).All(e => e.TagChange.Tag == (int)GameTag.ZONE
            || e.TagChange.Tag == (int)GameTag.ZONE_POSITION || e.TagChange.Tag == (int)GameTag.CONTROLLER || e.TagChange.Tag == (int)GameTag.ENTITY_ID),
            label + " tag changes on hidden rows carry only ZONE, ZONE_POSITION, CONTROLLER or ENTITY_ID");
        Check(inside.All(e => e.HasFullEntity || e.HasShowEntity || e.HasHideEntity || e.HasTagChange
            || e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.TARGET), label + " scope carries only creation rows");
        // Between insertions a scope may also hold an inserted card's own enchantment rows, never another entity's.
        var created = inside.Where(e => e.HasFullEntity).Select(e => e.FullEntity.Entity).ToHashSet();
        Check(inside.Where(e => e.HasTagChange).All(e => created.Contains(e.TagChange.Entity)
            && (!ids.Contains(e.TagChange.Entity) || e.TagChange.Tag != (int)GameTag.ZONE || e.TagChange.Value == (int)EngineZone.DECK)),
            label + " in-scope tag changes stay on rows created in the scope, inserted cards stay in DECK");
    }

    private static void DumpShuffleBatch(string name, List<PowerHistoryData> entries)
    {
        string output = Environment.GetEnvironmentVariable("HS_SHUFFLE_BATCH_OUTPUT");
        if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, name + ".txt"), entries.Select((e, i) =>
            i + " " + (e.HasPowerStart ? $"BLOCK_START {e.PowerStart.Type} source={e.PowerStart.Source} target={e.PowerStart.Target} effect={e.PowerStart.EffectCardId}/{e.PowerStart.EffectIndex} keyword={(e.PowerStart.HasTriggerKeyword ? e.PowerStart.TriggerKeyword : 0)}"
            : e.HasPowerEnd ? "BLOCK_END"
            : e.HasSubSpellStart ? $"SUB_SPELL_START source={e.SubSpellStart.SourceEntityId} targets={string.Join(",", e.SubSpellStart.TargetEntityIds)} prefab={e.SubSpellStart.SpellPrefabGuid}"
            : e.HasSubSpellEnd ? "SUB_SPELL_END"
            : e.HasMetaData ? $"META_DATA {e.MetaData.Type} data={e.MetaData.Data} info={string.Join(",", e.MetaData.Info)}"
            : e.HasTagChange ? $"TAG_CHANGE {e.TagChange.Entity} {(GameTag)e.TagChange.Tag}={e.TagChange.Value}"
            : e.HasShuffleDeck ? $"SHUFFLE_DECK {e.ShuffleDeck.PlayerId}"
            : e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity
                ? $"{(e.HasShowEntity ? "SHOW_ENTITY" : e.HasFullEntity ? "FULL_ENTITY" : "CHANGE_ENTITY")} {SnapshotEntity(e)} {SnapshotName(e)} "
                    + string.Join(" ", (e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.ChangeEntity).Tags.Select(t => $"{(GameTag)t.Name}={t.Value}"))
            : e.HasHideEntity ? $"HIDE_ENTITY {e.HideEntity.Entity} zone={e.HideEntity.Zone}" : "OTHER")));
    }
}
