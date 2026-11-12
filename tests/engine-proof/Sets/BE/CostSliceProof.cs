using System.IO.Compression;
using System.Text.Json;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CostSliceProof
{
    internal static void Visible(IPlayable card, int cost, CostPaymentKind kind = CostPaymentKind.Mana)
    {
        var tags = PowerHistoryBuilder.FullEntity(card).Entity.Tags.ToDictionary(t => t.Key, t => t.Value);
        Core2026Proof.Check(card.Cost == cost && tags.GetValueOrDefault(GameTag.COST) == cost
            && tags.GetValueOrDefault(GameTag.CARD_ALTERNATE_COST) == (int)kind,
            card.Card.Id + " visible COST/2837");
    }
    internal static void Definition(string id)
    {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card.Implemented, "implemented dependency " + id);
        if (ClientExport.TryCards() is not { } export) { ClientExport.Skip("Cost slice dependencies against the client export"); return; }
        var row = export[id];
        Core2026Proof.Check(card.AssetId == row.GetProperty("dbfId").GetInt32()
            && card.Cost == row.GetProperty("cost").GetInt32() && card.ATK == row.GetProperty("atk").GetInt32()
            && card.Health == row.GetProperty("health").GetInt32() && card.Text == row.GetProperty("text").GetString(), "pinned dependency " + id);
        foreach (var tag in row.GetProperty("tags").EnumerateObject())
            Core2026Proof.Check(card[(GameTag)int.Parse(tag.Name)] == tag.Value.GetInt32(), "pinned dependency tag " + id + "/" + tag.Name);
    }
}
