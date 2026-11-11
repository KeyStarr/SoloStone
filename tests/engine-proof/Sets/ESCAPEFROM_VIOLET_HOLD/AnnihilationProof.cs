using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AnnihilationProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_510", (g, p, id) => {

        foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        var first = (Minion)Entity.FromCard(p, Cards.FromId("EX1_319"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        var third = (Minion)Entity.FromCard(p, Cards.FromId("CS2_065"), zone: p.DeckZone);
        var fourth = (Minion)Entity.FromCard(p, Cards.FromId("EX1_319"), zone: p.DeckZone);
        Summon(p, "CS2_182"); Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { first.Id, third.Id }) && p.Opponent.BoardZone.Count == 0 && fourth.Zone == p.DeckZone && p.DeckZone.Count == 2, "destroy both boards; summon existing Demons only from bottom three in deck order");
        Core2026Proof.Check(first.AttackDamage == 3 && first.Health == 2 && third.AttackDamage == 1 && third.Health == 3 && p.Hero.Damage == 0, "Demon identity/stats preserved; no summoned Battlecry");
        var clone = g.Clone(); Play(clone, clone.Player1, id); Core2026Proof.Check(p.BoardZone.Count == 2 && first.Zone == p.BoardZone, "deck summon and destruction isolated in clone");
        ClearBoard(g, p); foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        Summon(p, "EX1_556"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "skele21", "deathrattles resolve before empty-deck summon phase");
        ClearBoard(g, p); Summon(p, "JAIL_007"); p.Opponent.Hero.Damage = 0; Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.Hero.Damage == 2, "deathrattle damage still resolves");
        Summon(p, "JAIL_399"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "JAIL_399t1" && m.AttackDamage == 8 && m.Health == 8 && m.HasTaunt && m.HasLifeSteal)
            && p.DeckZone.Count == 0, "bottom-three inspected after deathrattle inserts exact Demons");
        ClearBoard(g, p);
        for (int i = 0; i < 6; i++) Summon(p, "EX1_556");
        for (int i = 0; i < 3; i++) Entity.FromCard(p, Cards.FromId("EX1_319"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "EX1_319") == 1 && p.DeckZone.Count == 2, "deathrattle-filled board limits deck summons without deleting unsummoned Demons");
    });
}
