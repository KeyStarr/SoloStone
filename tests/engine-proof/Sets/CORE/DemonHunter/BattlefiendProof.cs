using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BattlefiendProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_351", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var hand = (Minion)Add(p, id);
        Core2026Proof.Check(source.IsRace(Race.DEMON), "printed Demon");
        Play(g, p, "CS2_106");
        var clone = g.Clone();
        Core2026Proof.Check(clone.Process(HeroAttackTask.Any(clone.Player1, clone.Player2.Hero))
            && ((Minion)clone.IdEntityDic[source.Id]).AttackDamage == 2 && source.AttackDamage == 1, "clone hero attack buff isolated");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Core2026Proof.Check(source.AttackDamage == 2 && hand.AttackDamage == 1, "hero attack gains one Attack on board only");
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(source.AttackDamage == 2, "minion attack does not trigger");
        End(g); Play(g, p.Opponent, "CS2_106");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)) && source.AttackDamage == 2, "opponent hero does not trigger");
        End(g); Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && source.AttackDamage == 3, "permanent buff stacks across turns");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 1, "Silence removes earned Attack");
        End(g); End(g); Play(g, p, "CS2_106");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && source.AttackDamage == 1, "Silence removes listener");
        Kill(g, source); var dead = Summon(p, id); Kill(g, dead);
        End(g); End(g);
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && dead.AttackDamage == 1, "source death removes listener");
    });
}
