using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class Program
{
    // The last tag 955 value sent for ENTITY in ROWS (snapshot or TAG_CHANGE), or -1.
    private static int LastVariant(List<PowerHistoryData> rows, int entity)
    {
        int value = -1;
        foreach (var row in rows)
        {
            var identity = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.HasChangeEntity ? row.ChangeEntity : null;
            if (identity != null && identity.Entity == entity && identity.Tags.FirstOrDefault(t => t.Name == 955) is PegasusGame.Tag text) value = text.Value;
            else if (row.HasTagChange && !row.TagChange.ChangeDef && row.TagChange.Entity == entity && row.TagChange.Tag == 955) value = row.TagChange.Value;
        }
        return value;
    }

    // The client definition of TAG for ENTITY after ROWS, starting from START: a FULL/SHOW/CHANGE reloads it (DefTags, else the
    // printed PRINTED), a ChangeDef TAG_CHANGE writes it.
    private static int Defined(List<PowerHistoryData> rows, int entity, GameTag tag, int start, int printed)
    {
        int value = start;
        foreach (var row in rows)
        {
            var identity = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.HasChangeEntity ? row.ChangeEntity : null;
            if (identity != null && identity.Entity == entity && identity.Name.Length > 0)
                value = identity.DefTags.FirstOrDefault(t => t.Name == (int)tag)?.Value ?? printed;
            else if (row.HasTagChange && row.TagChange.ChangeDef && row.TagChange.Entity == entity && row.TagChange.Tag == (int)tag)
                value = row.TagChange.Value;
        }
        return value;
    }

    // #192: a higher-tier Soldier keeps its one card id (CATA_580t) and scales its printed stats. The client colours ATK/HEALTH green
    // above the entity's definition, so the definition must carry the scaled printed stats: DefTags on a FULL/SHOW/CHANGE_ENTITY
    // (which reload it), else a ChangeDef TAG_CHANGE after the stat change that scaled it. Text variant 955 follows the
    // controller's count: 0,1,0,1,2 (Lean, derived from the card text). A real buff still shows green; an opponent's hidden
    // Soldier gets none of it.
    private static void HeraldDefinitionHistory()
    {
        var capture = new Capture();
        var match = new NativeMatch(capture.Send, "warrior", "mage", 1);
        match.Start(); match.SendChoices(capture.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game; var player = game.Player1;
        game.PowerHistory.Last.Clear();
        var hidden = Generic.DrawCard(player.Opponent, Cards.FromId("CATA_580t"));
        FlushHistory(match);
        var soldiers = new List<Minion>();
        for (int count = 1; count <= 5; count++)
        {
            game.PowerHistory.Last.Clear();
            int id = new SabberStoneCore.EffectSystem.EffectContext(game, player.Id, player.Hero.Id).Herald();
            new SabberStoneCore.EffectSystem.EffectContext(game, player.Opponent.Id, player.Opponent.Hero.Id).Herald();
            game.DeathProcessingAndAuraUpdate();
            FlushHistory(match);
            soldiers.Add((Minion)game.IdEntityDic[id]);
        }
        var top = soldiers.Last();
        game.PowerHistory.Last.Clear();
        new AddEnchantmentTask("CS2_009e", EntityType.TARGET).Process(game, player, player.Hero, top);
        var shownTop = SabberStoneCore.Kettle.PowerHistoryBuilder.ShowEntity(top);
        FlushHistory(match);
        SendHistoryEntries(match, new List<SabberStoneCore.Kettle.IPowerHistoryEntry> { shownTop });
        var wire = capture.HistorySnapshots().SelectMany(packet => packet.List).ToList();
        foreach (var soldier in soldiers)
        {
            int factor = Math.Max(1, soldier[HeraldRules.RealizedFactor]);
            Check(factor == new[] { 1, 1, 2, 2, 4 }[soldiers.IndexOf(soldier)], "Soldier factor follows the tier at its summon");
            var definition = new Dictionary<int, int>();
            int scaledAt = -1, definedAt = -1;
            var variants = new List<int>();
            for (int i = 0; i < wire.Count; i++)
            {
                var row = wire[i];
                var identity = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.HasChangeEntity ? row.ChangeEntity : null;
                if (identity != null && identity.Entity == soldier.Id)
                {
                    definition.Clear();
                    foreach (var tag in identity.DefTags) definition[tag.Name] = tag.Value;
                    if (identity.DefTags.Any(tag => tag.Name == (int)GameTag.ATK)) definedAt = i;
                    var text = identity.Tags.FirstOrDefault(tag => tag.Name == 955);
                    if (text != null && (variants.Count == 0 || variants[^1] != text.Value)) variants.Add(text.Value);
                }
                if (!row.HasTagChange || row.TagChange.Entity != soldier.Id) continue;
                if (row.TagChange.ChangeDef) { definition[row.TagChange.Tag] = row.TagChange.Value; definedAt = i; continue; }
                if (row.TagChange.Tag == (int)GameTag.ATK && row.TagChange.Value == soldier.Card.ATK * factor && scaledAt < 0) scaledAt = i;
                if (row.TagChange.Tag == 955 && (variants.Count == 0 || variants[^1] != row.TagChange.Value)) variants.Add(row.TagChange.Value);
            }
            Check(definition.GetValueOrDefault((int)GameTag.ATK, soldier.Card.ATK) == soldier.Card.ATK * factor
                && definition.GetValueOrDefault((int)GameTag.HEALTH, soldier.Card.Health) == soldier.Card.Health * factor,
                $"Soldier {soldier.Id} (factor {factor}): the client definition holds the scaled printed stats");
            if (factor > 1 && soldier != top)
                Check(scaledAt >= 0 && definedAt > scaledAt, $"Soldier {soldier.Id}: the scaled definition (DefTags or ChangeDef) comes after the stat change that scaled it: scaled={scaledAt} defined={definedAt} rows="
                    + string.Join(" | ", wire.Select((row, i) => (row, i)).Where(x => x.row.HasTagChange && x.row.TagChange.Entity == soldier.Id
                        || x.row.HasFullEntity && x.row.FullEntity.Entity == soldier.Id || x.row.HasShowEntity && x.row.ShowEntity.Entity == soldier.Id)
                        .Select(x => x.i + ":" + (x.row.HasTagChange ? $"TC {x.row.TagChange.Tag}={x.row.TagChange.Value}{(x.row.TagChange.ChangeDef ? "D" : "")}"
                            : x.row.HasFullEntity ? "FULL atk=" + x.row.FullEntity.Tags.FirstOrDefault(t => t.Name == 47)?.Value + " def=" + x.row.FullEntity.DefTags.Count : "SHOW"))));
            // Sent after the Herald that made it (count k + 1), then refreshed after each Herald up to count 5.
            var expected = new List<int>();
            foreach (int value in new[] { 0, 1, 0, 1, 2, 2 }.Skip(soldiers.IndexOf(soldier) + 1))
                if (expected.Count == 0 || expected[^1] != value) expected.Add(value);
            Check(variants.SequenceEqual(expected), $"Soldier {soldier.Id}: text variants sent {string.Join(",", variants)}, expected {string.Join(",", expected)}");
        }
        var show = wire.Last(row => row.HasShowEntity && row.ShowEntity.Entity == top.Id).ShowEntity;
        Check(show.DefTags.Single(tag => tag.Name == (int)GameTag.ATK).Value == top.Card.ATK * 4
            && show.DefTags.Single(tag => tag.Name == (int)GameTag.HEALTH).Value == top.Card.Health * 4
            && show.Tags.Single(tag => tag.Name == (int)GameTag.ATK).Value == top.Card.ATK * 4 + 2
            && show.Tags.Single(tag => tag.Name == (int)GameTag.HEALTH).Value == top.Card.Health * 4 + 2,
            "a real buff on a tier 3 Soldier stays above its scaled definition (green)");
        Check(HeraldRules.Variant(player.Opponent) == 2 && wire.All(row =>
                !(row.HasTagChange && row.TagChange.Entity == hidden.Id && (row.TagChange.Tag == 955 || row.TagChange.ChangeDef))
                && !(row.HasFullEntity && row.FullEntity.Entity == hidden.Id && (row.FullEntity.Name.Length > 0 || row.FullEntity.DefTags.Count > 0
                    || row.FullEntity.Tags.Any(tag => tag.Name == 955)))),
            "an opponent's Soldier in hand gets no identity, text variant or definition");
        // Return to hand resets the stats to printed: the definition follows by ChangeDef. Replayed from hand at count 5 the Soldier
        // scales to tier 3 inside the human's PLAY block; its definition follows after that stat change, and R7 still holds.
        var bounced = soldiers[2];
        int before = capture.HistorySnapshots().Count();
        game.PowerHistory.Last.Clear();
        new ReturnHandTask(EntityType.TARGET).Process(game, player, player.Hero, bounced, new SabberStoneCore.Model.TaskStack());
        game.DeathProcessingAndAuraUpdate();
        FlushHistory(match);
        var back = capture.HistorySnapshots().Skip(before).SelectMany(packet => packet.List).ToList();
        Check(bounced.Zone == player.HandZone && Defined(back, bounced.Id, GameTag.ATK, bounced.Card.ATK * 2, bounced.Card.ATK) == bounced.Card.ATK
            && Defined(back, bounced.Id, GameTag.HEALTH, bounced.Card.Health * 2, bounced.Card.Health) == bounced.Card.Health,
            "returned to hand: the definition is back to the printed stats");
        Check(LastVariant(back, bounced.Id) == 2 || LastVariant(back, bounced.Id) == -1 && LastVariant(wire, bounced.Id) == 2,
            "returned to hand: the Soldier keeps the current text variant (2), not a reset 0");
        before = capture.HistorySnapshots().Count();
        game.PowerHistory.Last.Clear(); player.UsedMana = 0;
        Check(game.Process(PlayCardTask.Any(player, bounced)), "replay accepted");
        FlushHistory(match);
        var replay = capture.HistorySnapshots().Skip(before).ToList();
        var rows = replay.SelectMany(packet => packet.List).ToList();
        int scaled = rows.FindIndex(row => row.HasTagChange && !row.TagChange.ChangeDef && row.TagChange.Entity == bounced.Id
            && row.TagChange.Tag == (int)GameTag.ATK && row.TagChange.Value == bounced.Card.ATK * 4);
        int defined = rows.FindLastIndex(row => row.HasTagChange && row.TagChange.ChangeDef && row.TagChange.Entity == bounced.Id
            && row.TagChange.Tag == (int)GameTag.ATK || (row.HasShowEntity && row.ShowEntity.Entity == bounced.Id && row.ShowEntity.DefTags.Count > 0));
        Check(bounced[HeraldRules.RealizedFactor] == 4 && scaled >= 0 && defined > scaled
            && Defined(rows, bounced.Id, GameTag.ATK, bounced.Card.ATK, bounced.Card.ATK) == bounced.Card.ATK * 4
            && Defined(rows, bounced.Id, GameTag.HEALTH, bounced.Card.Health, bounced.Card.Health) == bounced.Card.Health * 4,
            "replayed at count 5: the tier 3 definition arrives after the stat change that scaled it; rows: "
            + string.Join(" | ", rows.Select((row, i) => (row, i)).Where(x => x.row.HasTagChange && x.row.TagChange.Entity == bounced.Id
                || x.row.HasShowEntity && x.row.ShowEntity.Entity == bounced.Id || x.row.HasFullEntity && x.row.FullEntity.Entity == bounced.Id)
                .Select(x => x.i + ":" + (x.row.HasTagChange ? $"TC {x.row.TagChange.Tag}={x.row.TagChange.Value}{(x.row.TagChange.ChangeDef ? "D" : "")}" : "SHOW/FULL"))));
        // Silence resets an upgraded Soldier to its printed stats; no snapshot follows, so the definition comes back by ChangeDef.
        var silenced = soldiers[3];
        before = capture.HistorySnapshots().Count();
        game.PowerHistory.Last.Clear();
        silenced.Silence(); game.DeathProcessingAndAuraUpdate();
        FlushHistory(match);
        var quiet = capture.HistorySnapshots().Skip(before).SelectMany(packet => packet.List).ToList();
        Check(silenced.AttackDamage == silenced.Card.ATK && Defined(quiet, silenced.Id, GameTag.ATK, silenced.Card.ATK * 2, silenced.Card.ATK) == silenced.Card.ATK
            && Defined(quiet, silenced.Id, GameTag.HEALTH, silenced.Card.Health * 2, silenced.Card.Health) == silenced.Card.Health,
            "a silenced upgraded Soldier gets its printed definition back (ChangeDef)");
        // A transformed Soldier drops the Soldier's text variant with its card.
        var changed = soldiers[1];
        before = capture.HistorySnapshots().Count();
        game.PowerHistory.Last.Clear();
        Generic.ChangeEntityBlock(player, changed, Cards.FromId("CS2_182"), true);
        FlushHistory(match);
        var turned = capture.HistorySnapshots().Skip(before).SelectMany(packet => packet.List).ToList();
        Check(LastVariant(turned, changed.Id) == 0, "a transformed Soldier sends text variant 0 for its new card, not a stale 2");
        var faults = NativeAdmission.ValidatePackets(replay.Select(packet => (IReadOnlyList<PowerHistoryData>)packet.List));
        Check(faults.Count == 0, "R7 admission holds for the replayed Soldier's PLAY: " + string.Join("; ", faults));
        capture.SerializeAll();
        Console.WriteLine("PASS Herald Soldier definitions: scaled printed stats as DefTags/ChangeDef, 955 = 0,1,0,1,2, real buff green, hidden hand private");
    }
}
