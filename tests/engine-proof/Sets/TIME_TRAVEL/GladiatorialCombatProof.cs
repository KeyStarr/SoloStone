using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GladiatorialCombatProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_870", (g, p, id) => {
        var before = p.DeckZone.ToArray(); int count = p.DeckZone.Count; Play(g, p, id);
        var recruited = p.BoardZone.Single(); var tiger = p.Opponent.BoardZone.Single();
        Core2026Proof.Check(before.Any(c => c.Id == recruited.Id) && recruited.Card.Id == "CS2_182" && recruited.AttackDamage == 4 && recruited.Health == 5 && p.DeckZone.Count == count - 1, "random existing deck minion recruited");
        Core2026Proof.Check(tiger.Card.Id == "TIME_870t" && tiger.AttackDamage == 5 && tiger.Health == 5 && tiger.HasStealth && tiger.Card.Implemented, "exact opponent Tiger");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); ClearBoard(g, p); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.DeckZone.Count == 1 && p.Opponent.BoardZone.Count == 2, "no minion still summons Tiger without drawing spell");
        ClearBoard(g, p.Opponent); for (int i = 0; i < 7; i++) Summon(p.Opponent, "CS2_182");
        var candidate = Entity.FromCard(p, Cards.FromId("TIME_006t1")); p.DeckZone.Add(candidate); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Single().Id == candidate.Id && p.Opponent.BoardZone.Count == 7, "enemy full board still recruits, suppresses Tiger");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); for (int i = 0; i < 7; i++) Summon(p, "CS2_182");
        candidate = Entity.FromCard(p, Cards.FromId("TIME_006t1")); p.DeckZone.Add(candidate); Play(g, p, id);
        Core2026Proof.Check(candidate.Zone.Type == Zone.DECK && p.Opponent.BoardZone.Single().Card.Id == "TIME_870t", "own full board preserves deck minion but still gives Tiger");
    });
}
