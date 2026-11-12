using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Presentation;
using EngineZone = SabberStoneCore.Enums.Zone;
using EngineHistory = SabberStoneCore.Kettle;

internal static partial class Program
{
    private static void ShatterFxHistory()
    {
        foreach (string parentId in new[] { "CATA_134", "CATA_306", "CATA_479", "CATA_489", "CATA_820" })
        foreach (bool enemy in new[] { false, true })
        foreach (bool reverse in new[] { false, true })
        {
            var (match, capture) = SogStart(Array.Empty<string>(), Array.Empty<string>());
            SogComplete(match, capture);
            var game = match.Game; var owner = enemy ? game.Player2 : game.Player1;
            void Flush() { SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear(); }
            game.PowerHistory.Last.Clear();
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            Generic.DrawCard(owner, Cards.FromId("CS2_231")); Flush();
            int cursor = capture.WirePackets.Count;
            var original = Generic.DrawCard(owner, Cards.FromId(parentId)); Flush();
            var split = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            var family = Cards.FromId(parentId).Shatter;
            var left = owner.HandZone.Single(c => c.Card.Id == family.FirstHalfId);
            var right = owner.HandZone.Single(c => c.Card.Id == family.SecondHalfId);
            Check(owner.HandZone.First() == left && owner.HandZone.Last() == right, "split preserves opposite edges");
            AssertShatterPulse(split, "0384874a085f04440af9f66618dfb8bc", owner.Hero.Id, original.Id);
            AssertShatterSpawn(split, "ad8ed082d78aad5478b1d0e682159ab4", owner.Hero.Id, left.Id, right.Id);
            if (enemy) AssertShatterPrivate(split, new[] { original.Id, left.Id, right.Id });
            if (reverse)
            {
                owner.HandZone.Remove(left); owner.HandZone.Remove(right);
                owner.HandZone.Add(right, 0); owner.HandZone.Add(left);
                Flush();
            }
            var pair = owner.HandZone.Where(c => c.Card.Shatter != null).Select(c => c.Id).ToArray();
            cursor = capture.WirePackets.Count;
            var separator = owner.HandZone.Single(c => c.Card.Shatter == null);
            Check(game.CurrentPlayer == owner || game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(game.CurrentPlayer)), "combine owner turn");
            // Real player PLAY path, including native's predicted local-play list suppression.
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Minion(owner, separator)), "separator play accepted");
            Flush();
            var combine = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            var combined = owner.HandZone.Single(c => c.Card.Id == parentId);
            int pulse = AssertShatterPulse(combine, "2795f3f54446b6c45a96f1981cda0df8", owner.Hero.Id, pair);
            Check(pulse >= 2 && combine[pulse - 2].HasTagChange && combine[pulse - 1].HasTagChange,
                "combine publishes both directional indices before activation");
            for (int i = 0; i < 2; i++)
            {
                var setup = combine[pulse - 2 + i].TagChange;
                var clear = combine[pulse + 2 + i].TagChange;
                Check(setup.Entity == pair[i] && setup.Tag == 1436 && setup.Value == i
                    && clear != null && clear.Entity == pair[i] && clear.Tag == 1436 && clear.Value == 0,
                    "physical left/right indices clear after conceal on both viewers");
                Check(combine.FindIndex(r => r.HasHideEntity && r.HideEntity.Entity == pair[i]) > pulse,
                    "conceal precedes half retirement");
            }
            AssertShatterSpawn(combine, "83e833360c0c11a4285a07390ded073d", owner.Hero.Id, combined.Id);
            Check(combined[ShatterRules.AlreadyCombined] == 1 && combined[(GameTag)4239] == 0
                && combined.ZonePosition == 0, "combined marker and lower slot survive FX");
            if (enemy) AssertShatterPrivate(combine, pair.Concat(new[] { combined.Id }).ToArray());
            cursor = capture.WirePackets.Count;
            var given = ShatterRules.GiveCombined(owner, parentId, owner.Hero); Flush();
            var grant = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            AssertShatterSpawn(grant, "83e833360c0c11a4285a07390ded073d", owner.Hero.Id, given.Id);
            Check(!grant.Any(r => r.HasSubSpellStart && ShatterHistory.IsConceal(r.SubSpellStart.SpellPrefabGuid)),
                "GiveCombined has arrival only, no invented halves or split event");
            if (enemy) AssertShatterPrivate(grant, new[] { given.Id });
            // An ordinary private tag and an unaudited subspell remain masked.
            cursor = capture.WirePackets.Count;
            game.PowerHistory.Add(new EngineHistory.PowerHistoryTagChange { EntityId = given.Id, Tag = (GameTag)1436, Value = 1 });
            game.PowerHistory.Add(new EngineHistory.PowerHistorySubSpellStart { Source = owner.Hero.Id,
                SpellPrefabGuid = "unqualified", Targets = new List<int> { given.Id } });
            game.PowerHistory.Add(new EngineHistory.PowerHistorySubSpellEnd()); Flush();
            if (enemy) Check(!capture.HistorySnapshots(cursor).SelectMany(p => p.List).Any(r => r.HasSubSpellStart
                || r.HasTagChange && r.TagChange.Tag == 1436), "no broad hidden tag/subspell exception");
            capture.SerializeAll();
        }
        ShatterFxCapacityAndStolenPower();
        ShatterFxReactions();
        Console.WriteLine("PASS Shatter FX: five families, both controllers/orientations, split/combine/GiveCombined, serialized ordering and privacy");
    }

    private static void ShatterFxCapacityAndStolenPower()
    {
        foreach (bool enemy in new[] { false, true })
        {
            var (match, capture) = SogStart(Array.Empty<string>(), Array.Empty<string>());
            SogComplete(match, capture);
            var game = match.Game; var owner = enemy ? game.Player2 : game.Player1;
            void Flush() { SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear(); }
            game.PowerHistory.Last.Clear();
            // Nine held cards leave one space before entry: only the left half is admitted.
            while (owner.HandZone.Count < 9) Generic.DrawCard(owner, Cards.FromId("CS2_231"));
            Flush();
            int cursor = capture.WirePackets.Count;
            var parent = Generic.DrawCard(owner, Cards.FromId("CATA_489")); Flush();
            var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            var half = owner.HandZone.Single(c => c.Card.Id == "CATA_489t");
            AssertShatterPulse(rows, "0384874a085f04440af9f66618dfb8bc", owner.Hero.Id, parent.Id);
            AssertShatterSpawn(rows, "ad8ed082d78aad5478b1d0e682159ab4", owner.Hero.Id, half.Id);
            Check(owner.HandZone.Count == 10 && !owner.HandZone.Any(c => c.Card.Id == "CATA_489t2"), "one-slot split overrides only admitted half");
            cursor = capture.WirePackets.Count;
            Generic.DrawCard(owner, Cards.FromId("CATA_134"));
            ShatterRules.GiveCombined(owner, "CATA_820", owner.Hero); Flush();
            Check(!capture.HistorySnapshots(cursor).SelectMany(p => p.List).Any(r => r.HasSubSpellStart),
                "full hand burns the parent and the combined grant; no unused override");
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            if (game.CurrentPlayer != owner) Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(game.CurrentPlayer)), "Stolen Power turn");
            owner.BaseMana = 10; owner.UsedMana = 0;
            var stolen = Generic.DrawCard(owner, Cards.FromId("CATA_202")); Flush();
            cursor = capture.WirePackets.Count;
            Check(game.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(owner, stolen)), "real Stolen Power cast"); Flush();
            rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            var result = owner.HandZone.Single(c => c.Card.Shatter != null);
            AssertShatterSpawn(rows, "83e833360c0c11a4285a07390ded073d", owner.Hero.Id, result.Id);
            Check(result[ShatterRules.AlreadyCombined] == 1 && !rows.Any(r => r.HasSubSpellStart
                && ShatterHistory.IsConceal(r.SubSpellStart.SpellPrefabGuid)), "Stolen Power produces one permanently combined parent without conceal");
            if (enemy) AssertShatterPrivate(rows, new[] { result.Id });
        }
    }

    // Owner 2026-11-03: reactions cannot change what the split admits; the override covers admitted halves only.
    private static void ShatterFxReactions()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (bool observer in new[] { false, true })
        {
            var (match, capture) = SogStart(Array.Empty<string>(), Array.Empty<string>());
            SogComplete(match, capture);
            var game = match.Game; var owner = enemy ? game.Player2 : game.Player1;
            void Flush() { SogSendHistory(match, game.PowerHistory.Last); game.PowerHistory.Last.Clear(); }
            game.PowerHistory.Last.Clear();
            while (owner.HandZone.Count < 8) Generic.DrawCard(owner, Cards.FromId("CS2_231"));
            Flush();
            bool filled = false;
            if (observer) game.TriggerManager.DrawTrigger += _ => { if (!filled) { filled = true; Generic.DrawCard(owner, Cards.FromId("CS2_231")); } };
            else SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("BE_997"), zone: owner.BoardZone);
            Flush();
            int cursor = capture.WirePackets.Count;
            // A real deck draw: Generic.DrawCard raises no draw observer.
            var parent = (SabberStoneCore.Model.Entities.IPlayable)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CATA_489"), zone: owner.DeckZone);
            Generic.Draw(owner, parent); Flush();
            var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            var left = owner.HandZone.Single(c => c.Card.Id == "CATA_489t");
            Check(owner.HandZone.Count == 10 && owner.HandZone.First() == left, "reaction: hand ends full with the left half first");
            AssertShatterPulse(rows, "0384874a085f04440af9f66618dfb8bc", owner.Hero.Id, parent.Id);
            if (observer)
            {
                var right = owner.GraveyardZone.Single(c => c.Card.Id == "CATA_489t2");
                AssertShatterSpawn(rows, "ad8ed082d78aad5478b1d0e682159ab4", owner.Hero.Id, left.Id);
                var full = rows.First(r => r.HasFullEntity && r.FullEntity.Entity == right.Id).FullEntity;
                Check(!enemy || full.Name == "", "a burning bot half is anonymous until its burn reveal");
                Check(!rows.Any(r => r.HasTagChange && r.TagChange.Entity == right.Id
                    && r.TagChange.Tag == (int)GameTag.ZONE && r.TagChange.Value == (int)EngineZone.HAND), "burned half never enters hand");
                if (enemy) AssertShatterPrivate(rows, new[] { parent.Id, left.Id });
            }
            else
            {
                var right = owner.HandZone.Single(c => c.Card.Id == "CATA_489t2");
                Check(owner.HandZone.Last() == right, "reaction cannot displace the right half");
                AssertShatterSpawn(rows, "ad8ed082d78aad5478b1d0e682159ab4", owner.Hero.Id, left.Id, right.Id);
                if (enemy) AssertShatterPrivate(rows, new[] { parent.Id, left.Id, right.Id });
            }
            capture.SerializeAll();
        }
    }

    private static int AssertShatterPulse(List<PowerHistoryData> rows, string prefab, int source, params int[] targets)
    {
        int index = rows.FindIndex(r => r.HasSubSpellStart && r.SubSpellStart.SpellPrefabGuid == prefab);
        Check(index >= 0, "missing Shatter prefab " + prefab);
        var spell = rows[index].SubSpellStart;
        Check(spell.SourceEntityId == source && spell.TargetEntityIds.SequenceEqual(targets)
            && rows[index + 1].HasSubSpellEnd, "explicit hero origin, ordered targets, balanced pulse " + prefab);
        return index;
    }

    private static void AssertShatterSpawn(List<PowerHistoryData> rows, string prefab, int source, params int[] targets)
    {
        int pulse = AssertShatterPulse(rows, prefab, source, targets);
        foreach (int id in targets)
        {
            int full = rows.FindIndex(r => r.HasFullEntity && r.FullEntity.Entity == id);
            int hand = rows.FindIndex(r => r.HasTagChange && r.TagChange.Entity == id
                && r.TagChange.Tag == (int)GameTag.ZONE && r.TagChange.Value == (int)EngineZone.HAND);
            Check(full >= 0 && full < pulse && TagValue(rows[full].FullEntity.Tags, GameTag.ZONE) == (int)EngineZone.SETASIDE
                && hand > pulse + 1, "FULL in SETASIDE before override; override before HAND " + id);
        }
    }

    private static void AssertShatterPrivate(List<PowerHistoryData> rows, int[] hidden)
    {
        foreach (var row in rows)
        {
            var snapshot = row.FullEntity ?? row.ShowEntity ?? row.ChangeEntity;
            if (snapshot != null && hidden.Contains(snapshot.Entity))
                Check(snapshot.Name == "" && snapshot.Tags.All(t => t.Name is 49 or 50 or 53 or 263), "anonymous Shatter snapshots contain structural tags only");
            if (row.HasTagChange && hidden.Contains(row.TagChange.Entity))
                Check(row.TagChange.Tag is 49 or 50 or 53 or 263 or 1436, "no identity, modifier, progress or family-tag leak");
        }
    }
}
