using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Diagnostic characterization; retail failed-condition presentation is not yet pinned.
    private static void ChillmawHistory(bool dump = false, bool requireNoEmptyEffect = false)
    {
        var unresolved = new List<string>();
        foreach (string id in new[] { "CORE_AT_123", "AT_123" })
        foreach (bool bot in new[] { false, true })
        foreach (string scenario in new[] { "death-no-dragon", "death-dragon", "play", "summon" })
        {
            var (match, capture) = AnimationMatch(botTurn: scenario.StartsWith("death") ? !bot : bot);
            var game = match.Game;
            var owner = bot ? game.Player2 : game.Player1;
            foreach (var player in new[] { game.Player1, game.Player2 })
                foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            var friendly = (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_200"), zone: owner.BoardZone);
            var enemy = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone);
            bool death = scenario.StartsWith("death");
            var source = (Minion)EngineEntity.FromCard(owner, Cards.FromId(id), zone: death ? owner.BoardZone : owner.HandZone);
            if (scenario == "death-dragon") EngineEntity.FromCard(owner, Cards.FromId("EX1_043"), zone: owner.HandZone);
            // Opponent's Dragon must never satisfy Chillmaw's condition.
            if (death) EngineEntity.FromCard(owner.Opponent, Cards.FromId("EX1_043"), zone: owner.Opponent.HandZone);
            var action = death
                ? EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_029"), zone: owner.Opponent.HandZone)
                : scenario == "summon" ? EngineEntity.FromCard(owner, Cards.FromId("GVG_029"), zone: owner.HandZone) : source;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, game.CurrentPlayer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD
                && t.Source?.Id == action.Id && (!death || t.Target?.Id == source.Id)));
            FlushFieldHistory(match);
            var packets = capture.HistorySnapshots(cursor).ToArray();
            var rows = packets.SelectMany(p => p.List).ToArray();
            int expectedDamage = scenario == "death-dragon" ? 3 : 0;
            Check(friendly.Damage == expectedDamage && enemy.Damage == expectedDamage, id + " " + scenario + " damage");
            Check(death ? owner.GraveyardZone.Contains(source) : owner.BoardZone.Contains(source), id + " " + scenario + " zone");
            var effects = rows.Where(r => r.HasPowerStart && r.PowerStart.Source == source.Id
                && ((int)r.PowerStart.Type == 3 || (int)r.PowerStart.Type == 5)).ToArray();
            string label = $"{id} owner={(bot ? 2 : 1)} {scenario}";
            if (!death) Check(effects.Length == 0, label + " has no Chillmaw power/trigger spell route");
            if (expectedDamage != 0)
            {
                Check(effects.Length == 1 && effects[0].PowerStart.Type == HistoryBlock.Type.TRIGGER
                    && effects[0].PowerStart.EffectIndex == 0 && effects[0].PowerStart.TriggerKeyword == 217
                    && string.IsNullOrEmpty(effects[0].PowerStart.EffectCardId), label + " retains authored trigger effect");
                int start = Array.IndexOf(rows, effects[0]);
                var body = rows.Skip(start + 1).TakeWhile(r => !r.HasPowerEnd).ToArray();
                var hits = body.Where(r => r.HasMetaData && (int)r.MetaData.Type == 1).ToArray();
                Check(hits.Length == 2 && hits.All(r => r.MetaData.Data == 3)
                    && hits.SelectMany(r => r.MetaData.Info).SequenceEqual(new[] { friendly.Id, enemy.Id }),
                    label + " carries both damage tasks inside trigger");
            }
            if (scenario == "death-no-dragon" && effects.Any(r => r.PowerStart.EffectIndex >= 0
                || !string.IsNullOrEmpty(r.PowerStart.EffectCardId))) unresolved.Add(label);
            Check(!rows.Any(r => (r.HasShowEntity || r.HasFullEntity || r.HasChangeEntity)
                && game.Player2.HandZone.Concat(game.Player2.DeckZone).Any(c => c.Id == SnapshotEntity(r))
                && !string.IsNullOrEmpty(SnapshotName(r))), label + " keeps NPC hand/deck private");
            Check(NativeAdmission.ValidatePackets(capture.HistorySnapshots()
                .Select(p => (IReadOnlyList<PowerHistoryData>)p.List)).Count == 0, label + " native admission");
            if (dump)
            {
            Console.WriteLine($"CHILLMAW {label} source={source.Id} effectBlocks={effects.Length} damage={friendly.Damage},{enemy.Damage}");
            foreach (var packet in packets)
            {
                Console.WriteLine("PACKET");
                foreach (var row in packet.List)
                {
                    if (row.HasPowerStart) Console.WriteLine($" START {row.PowerStart.Type} source={row.PowerStart.Source} index={row.PowerStart.EffectIndex} effect={row.PowerStart.EffectCardId} keyword={(row.PowerStart.HasTriggerKeyword ? row.PowerStart.TriggerKeyword.ToString() : "absent")}");
                    else if (row.HasPowerEnd) Console.WriteLine(" END");
                    else if (row.HasMetaData) Console.WriteLine($" META {row.MetaData.Type} data={row.MetaData.Data} info={string.Join(",", row.MetaData.Info)}");
                    else if (row.HasTagChange) Console.WriteLine($" TAG {row.TagChange.Entity} {(GameTag)row.TagChange.Tag}={row.TagChange.Value}");
                    else if (row.HasShowEntity) Console.WriteLine($" SHOW {row.ShowEntity.Entity} {row.ShowEntity.Name}");
                    else if (row.HasFullEntity) Console.WriteLine($" FULL {row.FullEntity.Entity} {row.FullEntity.Name}");
                    else Console.WriteLine(" OTHER " + row.GetType().Name);
                }
            }
            }
            capture.SerializeAll();
        }
        // Opt-in red acceptance probe, not an assertion that unobserved retail behavior is correct.
        if (requireNoEmptyEffect) Check(unresolved.Count == 0,
            "UNPINNED retail expectation: failed condition must not select effect spell: " + string.Join("; ", unresolved));
        Console.WriteLine("PASS Chillmaw rules, successful trigger routing, summon routes, privacy and admission; "
            + unresolved.Count + " failed-condition effect routes observed (retail expectation unpinned)");
    }
}
