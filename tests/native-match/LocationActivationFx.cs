using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Owner playtest 2026-11-04 (Erupting Volcano): a location activation whose native CardDef has no authored play spell
    // showed damage splats but no missile or impact. Class: every location effect that deals damage as the location itself
    // (damage inside a block whose source is the location, nested spell casts excluded) must carry one damage sub-spell per
    // resolved hit unless the native card authors its own effect.
    // A new damaging location, or one that stops being activatable on the fixture, fails here until it is classified.
    private static readonly HashSet<string> LocationFxAuthored = new HashSet<string>
        { "CORE_REV_990", "CATA_527", "TLC_433t2" };           // CardDef plays its own missile (census play_status authored)
    private static readonly HashSet<string> LocationFxSubstituted = new HashSet<string>
        { "CATA_584", "TIME_810", "TIME_810t1", "TIME_810t2" }; // census-empty: per-hit substitute recipe on the activation
    private static readonly HashSet<string> LocationFxNoOption = new HashSet<string>
        { "EDR_454" };                                          // Clutch of Corruption offers no activation on the fixture board

    private static List<PegasusGame.PowerHistoryData> LocationOwnDamage(List<PegasusGame.PowerHistoryData> entries, int source)
    {
        var own = new List<PegasusGame.PowerHistoryData>(); var stack = new Stack<int>();
        foreach (var e in entries)
        {
            if (e.HasPowerStart) stack.Push(e.PowerStart.Source);
            else if (e.HasPowerEnd) { if (stack.Count > 0) stack.Pop(); }
            else if (e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.DAMAGE && stack.Count > 0 && stack.Peek() == source) own.Add(e);
        }
        return own;
    }

    private static void LocationActivationFx()
    {
        var census = File.ReadAllLines("data/fx/empty-playeffect-37.0.0.254477.tsv").Skip(1).Select(l => l.Split('\t'))
            .ToDictionary(c => c[0], c => c[2]);
        var damaging = new HashSet<string>(); var noOption = new HashSet<string>();
        foreach (bool bot in new[] { false, true })
        foreach (int enemyHealth in new[] { 30, 1 })   // 1: hits kill minions mid-activation
        foreach (bool finalCharge in new[] { false, true })
        foreach (var card in Cards.All.Where(c => c.Type == CardType.LOCATION && c.Id != null && c.Implemented)
            .OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            for (int i = 0; i < 2; i++)
            {
                var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_182"), zone: owner.Opponent.BoardZone);
                enemy.BaseHealth = enemyHealth;
            }
            var friend = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
            friend.BaseHealth = 30; friend.Damage = 3;
            var source = (BoardObject)EngineEntity.FromCard(owner, Cards.FromId(card.Id), zone: owner.BoardZone);
            source[GameTag.EXHAUSTED] = 0; source[GameTag.LOCATION_ACTION_COOLDOWN] = 0;
            if (finalCharge) source[GameTag.DAMAGE] = source[GameTag.HEALTH] - 1;
            FlushFieldHistory(match);
            string label = $"{card.Id} bot={bot} enemyHealth={enemyHealth} final={finalCharge}";
            var task = owner.Options().OfType<ActivateLocationTask>().FirstOrDefault(t => t.Source?.Id == source.Id);
            if (task == null) { noOption.Add(card.Id); continue; }
            int cursor = capture.Packets.Count;
            InvokeProcess(match, task);
            for (int picks = 0; owner.Choice != null && picks < 4; picks++)   // follow Discover/choice stages to the end
                InvokeProcess(match, ChooseTask.Pick(owner, owner.Choice.Choices[0]));
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            var damage = LocationOwnDamage(entries, source.Id).Select(e => e.MetaData.Info.Single()).ToList();
            var shots = entries.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToList();
            Check(shots.Count == entries.Count(e => e.HasSubSpellEnd), label + ": unbalanced sub-spell scopes");
            if (damage.Count == 0) continue;
            damaging.Add(card.Id);
            if (census[card.Id] != "empty")
            {
                Check(LocationFxAuthored.Contains(card.Id), label + ": authored damaging location not classified");
                continue;
            }
            Check(LocationFxSubstituted.Contains(card.Id), label + ": census-empty damaging location not classified");
            var damageShots = shots.Where(s => s.SpellPrefabGuid.Contains("Missile")).ToList();
            Check(damageShots.Count == damage.Count, $"{label}: {damage.Count} hits but {damageShots.Count} damage sub-spells");
            Check(damageShots.All(s => s.SourceEntityId == source.Id), label + ": missile must originate at the location");
            Check(damageShots.SelectMany(s => s.TargetEntityIds).SequenceEqual(damage), label + ": missile targets must be the hit targets in order");
            if (finalCharge) Check(source.Zone == owner.GraveyardZone, label + ": final charge must retire the location after its missiles");
            if (enemyHealth == 1 && card.Id == "CATA_584")
                Check(owner.Opponent.GraveyardZone.Count(m => m.Card.Id == "CS2_182") == 2, label + ": hits must kill both minions mid-activation");
            capture.SerializeAll();
        }
        Check(damaging.SetEquals(LocationFxAuthored.Concat(LocationFxSubstituted)),
            "damaging location set changed: " + string.Join(",", damaging.OrderBy(x => x, StringComparer.Ordinal)));
        Check(noOption.SetEquals(LocationFxNoOption), "locations without an activation option changed: " + string.Join(",", noOption.OrderBy(x => x, StringComparer.Ordinal)));
        LocationDeathrattleFx();
        Console.WriteLine("PASS location damage FX class: activation and Deathrattle damage, final charge, killing hits, both seats");
    }

    // Location-granted Deathrattle damage with the same splat-only symptom: Elise's composite locations (1/3/5 to all enemies)
    // and Past/Present/Future Gnomeregan's granted Deathrattle (2 to the enemy hero).
    private static void LocationDeathrattleFx()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string mode in new[] { "TLC_100t1", "TLC_100t2", "TLC_100t3", "TIME_044t1", "TIME_044t2" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var owner = match.Game.CurrentPlayer;
            for (int i = 0; i < 2; i++)
            {
                var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_182"), zone: owner.Opponent.BoardZone);
                enemy.BaseHealth = i == 0 ? 1 : 30;   // one hit kills
            }
            IPlayable dying; string label = $"{mode} deathrattle bot={bot}";
            if (mode.StartsWith("TLC_100"))
            {
                char tier = mode[mode.Length - 1];
                var card = CompositeLocationRules.Compose(Cards.FromId(mode), Cards.FromId($"TLC_100t{tier}1"), Cards.FromId($"TLC_100t{tier}2"));
                dying = (IPlayable)EngineEntity.FromCard(owner, card, zone: owner.BoardZone);
            }
            else
            {
                var minion = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.BoardZone);
                minion.BaseHealth = 3;
                var location = (BoardObject)EngineEntity.FromCard(owner, Cards.FromId(mode), zone: owner.BoardZone);
                location[GameTag.EXHAUSTED] = 0;
                FlushFieldHistory(match);
                InvokeProcess(match, owner.Options().OfType<ActivateLocationTask>().First(t => t.Source?.Id == location.Id && t.ActionTarget?.Id == minion.Id));
                dying = minion;
            }
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            dying.Destroy();
            match.Game.DeathProcessingAndAuraUpdate();
            FlushFieldHistory(match);
            var entries = History(capture, cursor);
            int begin = entries.FindIndex(e => e.HasPowerStart && e.PowerStart.Source == dying.Id && (int)e.PowerStart.Type == (int)BlockType.TRIGGER);
            Check(begin >= 0, label + ": Deathrattle trigger block missing");
            int depth = 1, end = begin + 1;
            for (; end < entries.Count && depth > 0; end++) { if (entries[end].HasPowerStart) depth++; if (entries[end].HasPowerEnd) depth--; }
            var body = entries.Skip(begin + 1).Take(end - begin - 2).ToList();
            var damage = body.Where(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.DAMAGE).Select(e => e.MetaData.Info.Single()).ToList();
            var shots = body.Where(e => e.HasSubSpellStart).Select(e => e.SubSpellStart).ToList();
            Check(damage.Count == (mode.StartsWith("TLC_100") ? 3 : 1), $"{label}: expected hits, got {damage.Count}");
            Check(shots.Count == damage.Count && shots.Count == body.Count(e => e.HasSubSpellEnd), $"{label}: {damage.Count} hits but {shots.Count} sub-spells");
            Check(shots.All(s => s.SourceEntityId == owner.Hero.Id), label + ": hero origin");
            Check(shots.SelectMany(s => s.TargetEntityIds).SequenceEqual(damage), label + ": missile targets must be the hit targets in order");
            capture.SerializeAll();
        }
    }
}
