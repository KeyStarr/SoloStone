using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FaeTricksterProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_571", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (var cardId in new[] { "CS2_029", "CS2_226" }) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(cardId)));
        int deck = p.DeckZone.Count; var source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.DeckZone.Count == deck && p.HandZone.Count == 0 && p.Hero.Damage == 0, "nonmatching type/cost/school: no draw or fatigue");
        var match = Entity.FromCard(p, Cards.FromId("EX1_407")); p.DeckZone.Add(match);
        source = (Minion)Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "draw waits for death"); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Single().Id == match.Id && p.HandZone.Single().Card.Id == "EX1_407" && p.DeckZone.Count == deck, "only exact matching card drawn at threshold");
        var next = Entity.FromCard(p, Cards.FromId("EX1_407")); p.DeckZone.Add(next);
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(next.Zone.Type == Zone.DECK && p.HandZone.Count == 1, "silence removes filtered draw");
        source = (Minion)Play(g, p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(next.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == 10 && p.DeckZone.Count == deck, "full hand burns matching card only");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        source = Summon(p, id); int damage = p.Hero.Damage; Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage == damage && p.HandZone.Count == 10, "empty deck causes no fatigue");
    });
}
