using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PetalPeddlerProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_889", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        var dragon = Summon(p, "TIME_056"); var ordinary = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "TIME_056");
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[dragon.Id]).AttackDamage == 5 && dragon.AttackDamage == 4, "clone end-turn buff isolated");
        End(g);
        Core2026Proof.Check(dragon.AttackDamage == 5 && dragon.Health == 2 && source.AttackDamage == 1 && ordinary.AttackDamage == 4 && enemy.AttackDamage == 4, "only another friendly Dragon gains exact +1/+1");
        End(g); Core2026Proof.Check(dragon.AttackDamage == 5, "opponent turn excluded");
        Play(g, p, "BE_228", source); End(g); End(g);
        Core2026Proof.Check(dragon.AttackDamage == 5 && source.ActivatedTrigger == null, "Silence removes own trigger; buff persists across turns");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(dragon.AttackDamage == 5, "dead source no buff");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); End(g); End(g);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 4, "no other Dragon means no target");
        var a = Summon(p, "TIME_056"); var b = Summon(p, "TIME_056"); End(g); End(g);
        Core2026Proof.Check(a.AttackDamage + b.AttackDamage == 9 && a.Health + b.Health == 3, "multiple candidates select exactly one Dragon");
    });
}
