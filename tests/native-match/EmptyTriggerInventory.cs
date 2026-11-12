using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PegasusGame;

internal static partial class Program
{
    // Optional census, off unless HS_EMPTY_TRIGGER_REPORT names an output file: lists every empty TRIGGER block in the
    // admission sweep so a before/after count can be taken. It produced the 26,544 -> 157 figure from the fix that
    // dropped empty trigger blocks; the remaining 157 are protected cases.
    private static void InventoryEmptyTriggers(string family, string fixture, int seat, PowerHistory[] packets, SabberStoneCore.Model.Game game)
    {
        string output = Environment.GetEnvironmentVariable("HS_EMPTY_TRIGGER_REPORT");
        if (string.IsNullOrEmpty(output)) return;
        // Diagnostic identities never enter the wire. They also identify concealed sources in a before/after census.
        var sources = game.IdEntityDic.ToDictionary(pair => pair.Key, pair => pair.Value.Card.Id);
        File.AppendAllText(output + ".identities", JsonSerializer.Serialize(new { family, fixture, seat, sources }) + "\n");
        var names = new Dictionary<int, string>();
        foreach (var packet in packets)
        for (int i = 0; i < packet.List.Count; i++)
        {
            var row = packet.List[i];
            var entity = row.HasFullEntity ? row.FullEntity : row.HasShowEntity ? row.ShowEntity : row.HasChangeEntity ? row.ChangeEntity : null;
            if (entity != null) names[entity.Entity] = entity.Name;
            if (!row.HasPowerStart || row.PowerStart.Type != HistoryBlock.Type.TRIGGER || i + 1 >= packet.List.Count || !packet.List[i + 1].HasPowerEnd) continue;
            File.AppendAllText(output, JsonSerializer.Serialize(new { family, fixture, seat, entityId = row.PowerStart.Source, source = names.GetValueOrDefault(row.PowerStart.Source, "?"), keyword = row.PowerStart.TriggerKeyword }) + "\n");
        }
    }
}
