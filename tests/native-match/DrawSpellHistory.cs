using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static void DrawSpellHistory()
    {
        DrawEntryHistory();
        var failures = new List<string>();
        int cases = 0;
        foreach (bool bot in new[] { false, true })
        foreach (string card in new[] { "CORE_BT_491", "RLK_708" })
        foreach (string variant in new[] { "minion", "spell", "weapon", "gift", "created", "FIR_901", "TIME_614", "JAIL_441", "FIR_900", "EDR_815" })
        foreach (string mode in card == "CORE_BT_491"
            ? new[] { "middle", "left", "right", "empty-hand", "full-hand", "empty-deck" }
            : new[] { "middle", "empty-hand", "full-hand", "empty-deck", "deathrattle", "deathrattle-empty-hand", "deathrattle-empty-deck", "deathrattle-full" })
        {
            string name = $"{card}-{variant}-{mode}-{(bot ? "bot" : "human")}";
            var (match, capture) = AnimationMatch(bot, card == "CORE_BT_491" ? "showcase-demon-hunter-outcast" : "showcase-death-knight-dark-gift");
            var game = match.Game;
            var owner = game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray())
                owner.SetasideZone.Add(old.Zone.Remove(old));
            bool deathrattle = mode.StartsWith("deathrattle", StringComparison.Ordinal);
            bool emptyDeck = mode is "empty-deck" or "deathrattle-empty-deck";
            var source = EngineEntity.FromCard(owner, Cards.FromId(card), zone: deathrattle ? owner.BoardZone : owner.HandZone);
            int held = mode is "empty-hand" or "deathrattle-empty-hand" ? 0 : mode == "full-hand" ? 9 : mode == "deathrattle-full" ? 10 : 2;
            for (int i = 0; i < held; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.HandZone);
            if (!deathrattle)
            {
                owner.HandZone.Remove(source);
                owner.HandZone.Add(source, mode == "left" || mode == "full-hand" ? 0 : mode == "middle" || mode == "empty-deck" ? 1 : -1);
            }
            var drawn = new List<IPlayable>();
            if (!emptyDeck)
                for (int i = 0; i < 2; i++)
                {
                    var definition = Cards.FromId(variant.Contains('_') ? variant
                        : variant == "spell" ? "CS2_029" : variant == "weapon" ? "EDR_842" : "CS2_182");
                    var next = variant == "created"
                        ? Generic.Copy(owner, source, EngineEntity.FromCard(owner, definition, zone: owner.SetasideZone), EngineZone.DECK)
                        : EngineEntity.FromCard(owner, definition, zone: owner.DeckZone);
                    if (variant == "gift")
                    {
                        var gift = EngineEntity.FromCard(owner, Cards.FromId("EDR_100t3"), zone: owner.SetasideZone);
                        Generic.AddEnchantmentBlock(game, Cards.FromId("EDR_100t3e"), gift, next);
                    }
                    drawn.Add(next);
                }
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            int handBefore = owner.HandZone.Count, deckBefore = owner.DeckZone.Count;
            if (deathrattle)
            {
                ((Minion)source).ToBeDestroyed = true;
                game.DeathProcessingAndAuraUpdate();
            }
            else InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            DumpDrawHistory(name, capture, cursor);
            AssertDrawImpactTargets(capture, source.Id, deathrattle, name, failures);
            if (card == "RLK_708" && !deathrattle)
            {
                int power = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 3);
                if (power < 0 || power + 1 >= data.Count || !data[power + 1].HasMetaData
                    || (int)data[power + 1].MetaData.Type != 0 || data[power + 1].MetaData.Data != 0
                    || !data[power + 1].MetaData.Info.SequenceEqual(new[] { source.Id }))
                    failures.Add(name + ": Baron POWER must target its visible source before drawing (retail 7507-7508)");
            }
            if (deathrattle)
            {
                int trigger = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                    && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
                int graveyard = data.FindIndex(e => IsZone(e, source.Id, EngineZone.GRAVEYARD));
                Check(graveyard >= 0 && trigger > graveyard, name + ": deathrattle source enters graveyard before TRIGGER");
            }
            int attempts = card == "CORE_BT_491" && mode is "left" or "right" or "empty-hand" or "full-hand" ? 2 : 1;
            int draws = Math.Min(deckBefore, attempts);
            int room = 10 - handBefore + (deathrattle ? 0 : 1);
            Check(owner.HandZone.Count == handBefore - (deathrattle ? 0 : 1) + Math.Min(room, draws)
                && owner.DeckZone.Count == deckBefore - draws, name + " gameplay draw counts");
            if (emptyDeck) Check(owner.Hero.Fatigue == 1, name + " fatigue without draw target");
            foreach (var next in drawn.Where(c => c.Zone == owner.HandZone))
            {
                var show = data.Single(e => e.HasShowEntity && e.ShowEntity.Entity == next.Id);
                // Retail SHOW is the draw transition, not a DECK identity reload before another zone task.
                if (SnapshotTag(show, GameTag.ZONE) != (int)EngineZone.HAND)
                    failures.Add(name + ": draw SHOW_ENTITY " + next.Id + " must carry destination HAND, got " + SnapshotTag(show, GameTag.ZONE));
                Check(bot ? string.IsNullOrEmpty(show.ShowEntity.Name) : show.ShowEntity.Name == next.Card.Id,
                    name + " draw identity privacy");
                if (!bot && variant == "gift") Check(SnapshotTag(show, GameTag.HAS_DARK_GIFT) == 1, name + " gift retained");
                if (!bot && variant == "created") Check(SnapshotTag(show, GameTag.DISPLAYED_CREATOR) == source.Id, name + " copy creator retained");
            }
            var burnt = drawn.Where(c => c.Zone == owner.GraveyardZone).ToArray();
            AssertBurns(data, burnt);
            Check(!data.Any(e => e.HasMetaData && (int)e.MetaData.Type == 0 && e.MetaData.Info.Any(id => drawn.Any(c => c.Id == id))),
                name + " ordinary draw has no invented TARGET range");
            capture.SerializeAll();
            cases++;
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS draw spell history: {cases} cases; Spectral Sight normal/both Outcast edges, Baron battlecry/deathrattle; minion/spell/weapon/gift/copied; hand/deck boundaries; both players");
    }

    private static void AssertDrawImpactTargets(Capture capture, int source, bool deathrattle,
        string name, List<string> failures)
    {
        var history = History(capture, 0);
        int start = history.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == source
            && (int)e.PowerStart.Type == (int)(deathrattle ? BlockType.TRIGGER : BlockType.POWER));
        Check(start >= 0, name + ": draw effect block exists");
        var types = new Dictionary<int, int>();
        var zones = new Dictionary<int, int>();
        foreach (var entry in history.Take(start))
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity
                : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (entity != null)
            {
                types[entity.Entity] = entity.Tags.FirstOrDefault(t => t.Name == (int)GameTag.CARDTYPE)?.Value ?? 0;
                zones[entity.Entity] = entity.Tags.FirstOrDefault(t => t.Name == (int)GameTag.ZONE)?.Value ?? 0;
            }
            if (entry.HasTagChange && entry.TagChange.Tag == (int)GameTag.ZONE)
                zones[entry.TagChange.Entity] = entry.TagChange.Value;
        }
        var tasks = new List<PowerHistoryData>();
        int depth = 0;
        foreach (var entry in history.Skip(start + 1))
        {
            if (entry.HasPowerEnd && depth-- == 0) break;
            if (depth == 0 && !entry.HasPowerStart) tasks.Add(entry);
            if (entry.HasPowerStart) depth++;
        }
        var targets = tasks.Where(e => e.HasMetaData && (int)e.MetaData.Type == 0
                && (e.MetaData.Data == 0 || e.MetaData.Data == source))
            .SelectMany(e => e.MetaData.Info).ToArray();
        if (targets.Length == 0)
            targets = tasks.Where(e => e.HasTagChange).Select(e => e.TagChange.Entity).Distinct()
                .Where(id => id != source && zones.GetValueOrDefault(id) != (int)EngineZone.SETASIDE
                    && types.GetValueOrDefault(id) != (int)CardType.ENCHANTMENT && zones.ContainsKey(id)).ToArray();
        // Both pinned impact FSMs branch on logical CARDTYPE, not SHOW's realtime card type.
        foreach (int target in targets)
            if (types.GetValueOrDefault(target) is not (3 or 4 or 5 or 7 or 10))
                failures.Add(name + ": impact target " + target + " has no supported logical CARDTYPE before draw SHOW; FSM cannot reach SpellFinishAction");
    }

    private static void DrawEntryHistory()
    {
        var failures = new List<string>();
        int cases = 0;
        foreach (bool bot in new[] { false, true })
        foreach (bool indexed in new[] { false, true })
        foreach (string id in new[] { "CS2_182", "SW_439t", "JAIL_386t", "TLC_513t2", "BOT_511t", "DAL_582t", "EDR_260t", "TIME_025t", "EDR_445pt3" })
        foreach (bool fullBoard in id is "TLC_513t2" or "EDR_260t" ? new[] { false, true } : new[] { false })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            foreach (var old in owner.HandZone.Concat(owner.DeckZone).ToArray())
                owner.SetasideZone.Add(old.Zone.Remove(old));
            if (fullBoard)
                while (!owner.BoardZone.IsFull) Generic.SummonBlock(match.Game, (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_231")), -1, null);
            var replacement = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.DeckZone);
            var drawn = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
            bool automatic = id != "CS2_182";
            Check((drawn.Power?.TopdeckTask != null) == automatic, id + " automatic draw fixture");
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            if (indexed) Generic.Draw(owner, owner.DeckZone.Count - 1);
            else Generic.Draw(owner, drawn); // Selected-card draw also covers the tutor entry path.
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            string name = $"{id}-{(indexed ? "indexed" : "selected")}-{(bot ? "bot" : "human")}{(fullBoard ? "-full-board" : "")}";
            DumpDrawHistory(name, capture, cursor);
            int show = data.FindIndex(e => e.HasShowEntity && e.ShowEntity.Entity == drawn.Id);
            Check(show >= 0, name + " draw SHOW exists");
            if (SnapshotTag(data[show], GameTag.ZONE) != (int)(automatic ? EngineZone.DECK : EngineZone.HAND))
                failures.Add(name + ": automatic SHOW must retain DECK; ordinary/tutor SHOW must carry HAND");
            int hand = data.FindIndex(e => IsZone(e, drawn.Id, EngineZone.HAND));
            Check(hand > show, name + " hand move follows draw SHOW");
            int trigger = data.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == drawn.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
            // An Illusion that cannot fit keeps its hand residence (Draw.cs TOPDECK).
            bool held = id == "EDR_260t" && fullBoard;
            Check(automatic ? trigger > hand && (drawn.Zone != owner.HandZone) != held : trigger < 0 && drawn.Zone == owner.HandZone,
                name + " automatic trigger follows hand move; ordinary draw stays in hand");
            // Name before the public move so native presentation starts with the correct definition.
            // A later SHOW can refresh identity but cannot guarantee recovery of hidden null-zone actors.
            int named = data.FindIndex(e => (e.HasShowEntity || e.HasFullEntity) && SnapshotEntity(e) == drawn.Id
                && !string.IsNullOrEmpty(SnapshotName(e)));
            int publicMove = data.FindIndex(e => IsZone(e, drawn.Id, EngineZone.PLAY) || IsZone(e, drawn.Id, EngineZone.GRAVEYARD));
            if (publicMove >= 0 && (named < 0 || named > publicMove))
                failures.Add(name + $": public move at {publicMove} precedes the named SHOW/FULL at {named}");
            // Board bodies reveal during the draw trigger, at their retained HAND origin, before direct PLAY.
            // Spells/failed summons still resolve through SETASIDE; a held Illusion remains private.
            if (automatic && !held && bot)
            {
                bool boardBody = drawn.Zone == owner.BoardZone;
                int parking = data.FindIndex(e => IsZone(e, drawn.Id, EngineZone.SETASIDE));
                Check(boardBody
                    ? parking < 0 && named > trigger && SnapshotTag(data[named], GameTag.ZONE) == (int)EngineZone.HAND
                    : named >= 0 && named == parking + 1 && SnapshotTag(data[named], GameTag.ZONE) == (int)EngineZone.SETASIDE,
                    name + " when-drawn reveal follows native board/spell transition contract");
            }
            // Hoisted identity keeps the snapshot's state tags, so the bound actor starts its Stealth state spell once.
            if (id == "TLC_513t2" && !fullBoard)
                Check(SnapshotTag(data[named], GameTag.STEALTH) == 1 && data.Count(e => e.HasShowEntity && e.ShowEntity.Entity == drawn.Id
                    && SnapshotTag(e, GameTag.STEALTH) == 1) <= 2, name + " named Ninja carries Stealth before it is bound");
            if (held && bot)
                Check(!data.Any(e => (e.HasShowEntity || e.HasFullEntity) && SnapshotEntity(e) == drawn.Id && !string.IsNullOrEmpty(SnapshotName(e))),
                    name + " held Illusion stays private");
            // A held automatic draw keeps its DECK-located SHOW (checked above).
            foreach (var next in new[] { drawn, replacement }.Where(c => c.Zone == owner.HandZone && !(held && c == drawn)))
            {
                var reveal = data.Single(e => e.HasShowEntity && e.ShowEntity.Entity == next.Id);
                Check(SnapshotTag(reveal, GameTag.ZONE) == (int)EngineZone.HAND &&
                    (bot ? string.IsNullOrEmpty(reveal.ShowEntity.Name) : reveal.ShowEntity.Name == next.Card.Id),
                    name + " ordinary/replacement draw destination and privacy");
            }
            capture.SerializeAll();
            cases++;
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS draw entry history: {cases} cases; ordinary/tutor and automatic draws, both overloads and players; DECK SHOW before HAND before TOPDECK");
    }

    private static void DumpDrawHistory(string name, Capture capture, int cursor)
    {
        string output = Environment.GetEnvironmentVariable("HS_DRAW_HISTORY_OUTPUT");
        if (string.IsNullOrEmpty(output)) return;
        string directory = Path.Combine(output, name);
        Directory.CreateDirectory(directory);
        int packetIndex = 0;
        foreach (var packet in capture.Packets.Skip(cursor).Where(p => p.Id == 19))
        {
            using var stream = File.Create(Path.Combine(directory, $"{packetIndex++}.bin"));
            packet.Message.Serialize(stream);
        }
        File.WriteAllLines(Path.Combine(directory, "history.txt"), History(capture, cursor).Select((e, i) =>
            i + " " + (e.HasPowerStart ? $"BLOCK_START {e.PowerStart.Type} source={e.PowerStart.Source} target={e.PowerStart.Target} effect={e.PowerStart.EffectCardId}/{e.PowerStart.EffectIndex} keyword={(e.PowerStart.HasTriggerKeyword ? e.PowerStart.TriggerKeyword : 0)}"
            : e.HasPowerEnd ? "BLOCK_END"
            : e.HasResetGame ? "GAME_RESET"
            : e.HasSubSpellStart ? $"SUB_SPELL_START source={e.SubSpellStart.SourceEntityId} targets={string.Join(",", e.SubSpellStart.TargetEntityIds)} prefab={e.SubSpellStart.SpellPrefabGuid}"
            : e.HasSubSpellEnd ? "SUB_SPELL_END"
            : e.HasMetaData ? $"META_DATA {e.MetaData.Type} data={e.MetaData.Data} info={string.Join(",", e.MetaData.Info)}"
            : e.HasTagChange ? $"TAG_CHANGE {e.TagChange.Entity} {(GameTag)e.TagChange.Tag}={e.TagChange.Value}"
            : e.HasFullEntity || e.HasShowEntity || e.HasChangeEntity
                ? $"{(e.HasShowEntity ? "SHOW_ENTITY" : e.HasFullEntity ? "FULL_ENTITY" : "CHANGE_ENTITY")} {SnapshotEntity(e)} {SnapshotName(e)} "
                    + string.Join(" ", (e.HasShowEntity ? e.ShowEntity : e.HasFullEntity ? e.FullEntity : e.ChangeEntity).Tags.Select(t => $"{(GameTag)t.Name}={t.Value}"))
            : e.HasHideEntity ? $"HIDE_ENTITY {e.HideEntity.Entity} zone={e.HideEntity.Zone}" : "OTHER")));
    }
}
