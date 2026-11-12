using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

namespace TransportInvariants;

internal static partial class HistoryShapeProof
{
    private static void ConditionalDeathrattle()
    {
        foreach (string id in new[] { "AT_123", "CORE_AT_123" })
        foreach (bool opponent in new[] { false, true })
        foreach (bool dragon in new[] { false, true })
        {
            var f = new Fixture();
            var owner = opponent ? f.Player.Opponent : f.Player;
            var source = f.Summon(owner, id);
            var victims = new[] { f.Summon(f.Player, "CS2_200"), f.Summon(f.Player.Opponent, "CS2_200") };
            if (dragon) EngineEntity.FromCard(owner, Cards.FromId("NEW1_023"), zone: owner.HandZone);
            var spell = EngineEntity.FromCard(f.Player, Cards.FromId("CS2_029"), zone: f.Player.HandZone);
            f.Ready($"conditional-deathrattle-{id}-{opponent}-{dragon}");
            int cursor = f.Session.Packets.Count;
            f.Act(PlayCardTask.SpellTarget(f.Player, spell, source));
            var rows = f.Session.Packets.Skip(cursor).Where(p => p.Id == 19).SelectMany(p => {
                var packet = new PowerHistory();
                using var stream = new MemoryStream(Convert.FromBase64String(p.Base64));
                packet.Deserialize(stream); return packet.List;
            }).ToArray();
            var blocks = rows.Where(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                && e.PowerStart.Type is HistoryBlock.Type.TRIGGER or HistoryBlock.Type.POWER).ToArray();
            Check(victims.All(v => v.Damage == (dragon ? 3 : 0)), "conditional Deathrattle damage agrees with condition");
            Check(blocks.Any(e => e.PowerStart.EffectIndex >= 0 || !string.IsNullOrEmpty(e.PowerStart.EffectCardId)) == dragon,
                "serialized failed Deathrattle has no effect spell; successful Deathrattle retains slot 0");
            f.Session.History.Finish();
            Check(f.Session.History.Findings.Count == 0, "conditional Deathrattle has no HistoryShape findings: "
                + string.Join(";", f.Session.History.Findings.Select(v => v.Rule + ": " + v.Detail)));
            HistoryShapeReport.Add(f.Session.History);
        }
        Console.WriteLine("PASS history-shape conditional Deathrattle: both Chillmaw IDs/seats, failed/successful conditions and serialized effect selectors");
    }
}
