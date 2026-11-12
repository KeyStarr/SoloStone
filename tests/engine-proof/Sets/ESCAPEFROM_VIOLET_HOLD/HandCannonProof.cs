using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HandCannonProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_103", (g, p, id) => {
        var cannon = Summon(p, "CAP_107t"); var ordinary = Summon(p, "CS2_182");
        var weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 3 && weapon.Durability == 2, "printed weapon stats");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "hero attack fires friendly Cannoneer");
        Attack(g, ordinary, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 8, "minion attack does not fire Cannoneer");
        var clone = g.CloneExact(history: true); End(clone); End(clone);
        Core2026Proof.Check(clone.Process(HeroAttackTask.Any(clone.Player1, clone.Player2.Hero)), "clone hero attack accepted");
        Core2026Proof.Check(clone.Player2.Hero.Damage == 13 && p.Opponent.Hero.Damage == 8, "last durability still fires, clone isolated");
        End(g); Play(g, p.Opponent, "CS2_082");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)), "opponent hero attack accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 9, "opponent hero does not fire our Cannoneer"); End(g);
        cannon.Silence(); int damage = p.Opponent.Hero.Damage;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "second hero attack accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 3, "silenced Cannoneer does not fire");
        var replacement = Summon(p, "CAP_107t"); Play(g, p, id); Play(g, p, "CS2_082");
        End(g); End(g); damage = p.Opponent.Hero.Damage;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "replacement weapon attack accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 1, "replaced Hand Cannon loses trigger");
    });
}
