using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VigilantSentryProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_035", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "Neutral in deck prevents summons"); ClearBoard(g, p);
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var copies = p.BoardZone.Where(m => m.Card.Id == id && m.Id != source.Id).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.AttackDamage == 2 && m.Health == 3 && m.HasTaunt), "two exact 2/3 Taunt Sentries without recursive Battlecry");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(copies.Select(m => m.Id)).Append(right.Id)), "copies immediately right of source in creation order");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182"); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 2, "only one summon when one slot remains");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "full board prevents summons");
        ClearBoard(g, p); foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 3, "empty deck has no Neutral cards");
        ClearBoard(g, p); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("END_031")));
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 3, "multi-class cards are not Neutral");
        ClearBoard(g, p); p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CFM_321")));
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 3, "tri-class cards are not Neutral");
    });
}
