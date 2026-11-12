using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GorishiTunnelerProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_840", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasStealth && source.IsRace(Race.BEAST), "printed Stealth Beast"); var clone = g.CloneExact(history: true);
        Attack(clone, (Minion)clone.IdEntityDic[source.Id], clone.Player2.Hero); Core2026Proof.Check(clone.Player2.Hero.Damage == 4 && p.Opponent.Hero.Damage == 0, "clone attack trigger isolated");
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && !source.HasStealth, "attack deals normal two plus triggered two");
        End(g); var attacker = Summon(p.Opponent, "CS2_171"); Attack(g, attacker, source); Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "being attacked does not trigger"); End(g);
        var victim = Summon(p.Opponent, "CS2_171"); Attack(g, source, victim); Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "minion attack also damages enemy hero");
        Play(g, p, "BE_228", source); End(g); End(g); Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 8, "Silence removes extra hero damage");
        Kill(g, source); Core2026Proof.Check(source.ActivatedTrigger == null, "dead source trigger removed");
    });
}
