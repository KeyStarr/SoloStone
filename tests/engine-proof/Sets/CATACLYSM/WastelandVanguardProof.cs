using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WastelandVanguardProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_302", (g, p, id) => {
        int health = p.Opponent.Hero.Health; Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Health == health - 3, "no kills only three missiles");
        ClearBoard(g, p); p.Opponent.Hero.Damage = 0; p.Opponent.Hero.IsImmune = false;
        for (int i = 0; i < 7; i++) Summon(p.Opponent, "EX1_506a");
        int before = p.Opponent.GraveyardZone.Count; Play(g, p, id);
        int deaths = p.Opponent.GraveyardZone.Count - before;
        Core2026Proof.Check(deaths >= 1 && deaths + p.Opponent.Hero.Damage == 6 && p.Opponent.BoardZone.Count == 7 - deaths, "first-salvo kill repeats once, never loops");
        ClearBoard(g, p.Opponent); p.Opponent.Hero.IsImmune = false; ClearBoard(g, p);
        var tough = Summon(p.Opponent, "CS2_182"); tough.BaseHealth = 20; health = p.Opponent.Hero.Health;
        Play(g, p, id); Core2026Proof.Check(tough.Damage + health - p.Opponent.Hero.Health == 3, "surviving damage never repeats");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); p.Opponent.Hero.Damage = 0;
        var boss = Summon(p.Opponent, "BRM_006"); boss.BaseHealth = 20;
        var source = Add(p, id);
        // Boss, freshly summoned Imp, then hero: no initial-board minion dies.
        g.Random.SetSeed(32);
        before = p.Opponent.GraveyardZone.Count(m => m.Card.Id == "BRM_006t");
        PlayEntity(g, p, source);
        deaths = p.Opponent.GraveyardZone.Count(m => m.Card.Id == "BRM_006t") - before;
        Core2026Proof.Check(boss.Zone.Type == Zone.PLAY && deaths >= 1 && boss.Damage + p.Opponent.Hero.Damage + deaths == 6, "first missile spawns Imp, later missile kills it and triggers second salvo");
    });
}
