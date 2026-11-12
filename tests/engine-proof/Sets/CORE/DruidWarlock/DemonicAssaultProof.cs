using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DemonicAssaultProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_088", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id);
        var targets = p.Options().Where(t => t is PlayCardTask && t.Source == spell).ToArray();
        Core2026Proof.Check(targets.Any(t => t.Target == p.Hero) && targets.Any(t => t.Target == p.Opponent.Hero)
            && targets.Any(t => t.Target == left) && targets.Any(t => t.Target == enemy)
            && targets.All(t => t.Target != null), "any character target required");
        PlayEntity(g, p, spell, enemy);
        var walkers = p.BoardZone.Skip(1).ToArray();
        Core2026Proof.Check(enemy.Damage == 3 && walkers.Length == 2 && walkers.All(m => m.Card.Id == "CS2_065" && m.Card.Implemented
            && m.AttackDamage == 1 && m.Health == 3 && m.HasTaunt && m.IsRace(Race.DEMON)), "three damage and exact two Voidwalkers");
        Core2026Proof.Check(p.BoardZone.First() == left && walkers[0].Id < walkers[1].Id, "summons append in creation order");
        ClearBoard(g, p); Summon(p, "CS2_052");
        Play(g, p, id, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "spell damage scales damage only");
        Core2026Proof.Check(p.BoardZone.Skip(1).All(m => m.AttackDamage == 1 && m.Health == 3), "spell damage does not scale tokens");
        p.Hero.HasDivineShield = true; Play(g, p, id, p.Hero);
        Core2026Proof.Check(!p.Hero.HasDivineShield && p.Hero.Damage == 0, "hero shield absorbs damage while summons resolve");
        ClearBoard(g, p);
        var sheep = Summon(p.Opponent, "GVG_076"); Play(g, p, id, sheep);
        Core2026Proof.Check(sheep.Zone == p.Opponent.GraveyardZone && p.BoardZone.Count == 2
            && p.BoardZone.All(m => m.Card.Id == "CS2_065" && m.Damage == 2 && m.Health == 1),
            "Voidwalkers summon before lethal target's Sheep Deathrattle resolves");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Id == "CS2_065", "one space summons one Voidwalker");
        int damage = p.Opponent.Hero.Damage; Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.Opponent.Hero.Damage == damage + 3, "full board still deals damage");
        var victim = p.BoardZone.Last(); Play(g, p, id, victim);
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && p.BoardZone.Count == 6
            && p.BoardZone.All(m => m.Card.Id == "CS2_182"),
            "lethal friendly target still occupies its slot during summons");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        var murmy = Summon(p, "ULD_723"); Play(g, p, id, murmy);
        var walker = p.BoardZone.Single(m => m.Card.Id == "CS2_065");
        var reborn = p.BoardZone.Single(m => m.Card.Id == "ULD_723");
        Core2026Proof.Check(murmy.Zone == p.GraveyardZone && p.BoardZone.Count == 7
            && walker.Id < reborn.Id && reborn.Health == 1 && reborn[GameTag.HAS_BEEN_REBORN] == 1,
            "only pre-death space summons a Voidwalker, before target's Reborn resolves");
    });
}
