using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CorpseCannonProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_450", (g, p, id) => {

        var left = Summon(p, "CS2_182"); var weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 1 && weapon.Durability == 3 && g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "printed weapon and first attack");
        var ghoul = p.BoardZone.Last(); Core2026Proof.Check(ghoul.Card.Id == "HERO_11bpt" && ghoul.AttackDamage == 1 && ghoul.Health == 1 && ghoul.HasCharge && p.BoardZone.First() == left, "hero attack appends exact 1/1 Charge Ghoul");
        Attack(g, left, p.Opponent.Hero); Core2026Proof.Check(p.BoardZone.Count == 2, "minion attack does not summon");
        var clone = g.Clone(); End(clone); End(clone); Core2026Proof.Check(clone.Process(HeroAttackTask.Any(clone.Player1, clone.Player2.Hero)) && clone.Player1.BoardZone.Last().Card.Id == "HERO_11bpt" && ghoul.Zone == p.BoardZone, "trigger persists and clone isolated");
        End(g); Core2026Proof.Check(ghoul.Zone == p.GraveyardZone, "summoned Ghoul dies at owner end turn");
        Play(g, p.Opponent, "CS2_082"); Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)) && p.BoardZone.Count == 1, "opponent attack does not summon"); End(g);
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182"); Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.BoardZone.Count == 7, "full board suppresses summon");
        ClearBoard(g, p); End(g); End(g);
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.Hero.Weapon == null && p.BoardZone.Single().Card.Id == "HERO_11bpt", "breaking swing still summons");
        Play(g, p, id); Play(g, p, "CS2_082"); End(g); End(g); int count = p.BoardZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.BoardZone.Count == count, "replacement removes weapon trigger");
    });
}
