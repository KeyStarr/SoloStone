using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TrackingProof
{
    internal static void Run() => Case("CORE_DS1_184", (g, p, id) => {
        Summon(p, "BRM_031"); // Chromaggus proves a real draw, not a generated copy.
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
        int deck = p.DeckZone.Count; int drawn = p.NumCardsDrawnThisTurn;
        Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3 && p.Choice.Choices.Select(key => g.IdEntityDic[key].Card.Id).Distinct().Count() == 3 && p.DeckZone.Count == deck, "Tracking offers deck cards without discarding");
        var selected = p.Choice.Choices[0];
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, selected)), "Tracking pick accepted");
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.Any(c => c.Id == selected) && p.NumCardsDrawnThisTurn == drawn + 1 && p.DeckZone.Count == deck - 1 && p.GraveyardZone.Count == 1, "only selected card leaves deck");
        foreach (var minion in p.BoardZone.ToArray()) Kill(g, minion);
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        var shred = Entity.FromCard(p, Cards.FromId("TIME_025t"), zone: p.DeckZone);
        var replacement = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        int damage = p.Hero.Damage;
        Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.Contains(shred.Id), "Tracking offers cast-when-drawn Shred");
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, shred.Id)), "Tracking Shred selection accepted");
        Core2026Proof.Check(p.Choice == null && shred.Zone == p.GraveyardZone && p.DeckZone.Count == 0
            && p.HandZone.Single().Id == replacement.Id && p.Hero.Damage == damage + 3,
            "automatic Shred cast preserves outer Tracking choice and draws replacement once");
    });
}
