using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Hookfist3000Proof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NX2_028", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.MECHANICAL) && source.IsRace(Race.PIRATE) && source.AttackDamage == 4 && source.Health == 3, "4/3 both races");
        Play(g, p, "CS2_106");
        var clone = g.Clone();
        Core2026Proof.Check(clone.Process(HeroAttackTask.Any(clone.Player1, clone.Player2.Hero)), "clone hero attack accepted");
        Core2026Proof.Check(clone.Player1.Hero.Armor == 4 && clone.Player1.HandZone.Count == 1 && p.Hero.Armor == 0 && p.HandZone.Count == 0, "clone armor/draw isolated");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Core2026Proof.Check(p.Hero.Armor == 4 && p.HandZone.Count == 1, "hero attack gains four armor and draws once");
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(p.Hero.Armor == 4 && p.HandZone.Count == 1, "minion attack does not trigger");
        End(g); Play(g, p.Opponent, "CS2_106");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)), "opponent attack accepted");
        Core2026Proof.Check(p.HandZone.Count == 1 && p.Hero.Armor == 1, "opponent attack only consumes armor; no trigger");
        End(g); int hand = p.HandZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "next turn attack accepted");
        Core2026Proof.Check(p.HandZone.Count == hand + 1 && p.Hero.Armor == 5, "trigger persists beyond end of turn");
        source.Silence(); End(g); End(g); Play(g, p, "CS2_106"); hand = p.HandZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.HandZone.Count == hand && p.Hero.Armor == 5, "Silence removes hero listener");
        Kill(g, source); source = Summon(p, id); Kill(g, source);
        End(g); End(g); hand = p.HandZone.Count;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.HandZone.Count == hand && p.Hero.Armor == 5, "dead source removes listener");
    });
}
