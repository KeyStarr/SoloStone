using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SpecterSpecialistProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_804", (g, p, id) => {
        var target = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var held = Add(p, id); var options = p.Options().Where(t => t.Source?.Id == held.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == target.Id) && options.All(t => t.Target is Minion && t.Target.Controller == p && t.Target.Id != enemy.Id), "friendly minion targeting only");
        PlayEntity(g, p, held, target); Core2026Proof.Check(target[GameTag.REBORN] == 1 && p.BoardZone.Count == 2, "fresh Reborn granted without copy");
        Play(g, p, "CAP_801", target); target.Damage = 2;
        var right = Summon(p, "CS2_182"); held = Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, held, target, zonePosition: 1)), "middle specialist play");
        var copy = p.BoardZone[2];
        Core2026Proof.Check(copy.Id != target.Id && copy.Card.Id == target.Card.Id && copy.AttackDamage == 6 && copy.BaseHealth == 8 && copy.Damage == 2 && copy.HasTaunt && copy[GameTag.REBORN] == 1 && p.BoardZone.Last() == right, "enchanted damaged copy beside source");
        var clone = g.Clone(); ((Minion)clone.IdEntityDic[copy.Id]).Silence();
        Core2026Proof.Check(copy.AttackDamage == 6 && ((Minion)clone.IdEntityDic[copy.Id]).AttackDamage == 4, "copy buffs clone isolated");
        Kill(g, target); var revived = p.BoardZone.First(m => m.Card.Id == "CS2_182" && m[GameTag.HAS_BEEN_REBORN] == 1);
        Play(g, p, id, revived); Core2026Proof.Check(revived[GameTag.HAS_BEEN_REBORN] == 0 && revived[GameTag.REBORN] == 1, "consumed Reborn reset");
        while (p.BoardZone.Count < 6) Summon(p, "CS2_182");
        Play(g, p, id, copy); Core2026Proof.Check(p.BoardZone.Count == 7, "full board suppresses copy");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p); Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "no target Battlecry safe");
        ClearBoard(g, p); target = Summon(p, "CS2_182"); Play(g, p, "CAP_801", target); target.Damage = 2;
        Play(g, p, "EX1_366"); Play(g, p, id, target); copy = p.BoardZone.Single(m => m.Card.Id == target.Card.Id && m.Id != target.Id);
        Core2026Proof.Check(copy.Card.Id == target.Card.Id && copy.AttackDamage == 7 && copy.BaseHealth == 9 && copy.Damage == 2
            && copy.HasTaunt && copy[GameTag.REBORN] == 1 && target.AttackDamage == 6 && target.BaseHealth == 8
            && p.Hero.Weapon.Durability == 3, $"copy initialized before Sword summon buff; source unchanged: copy={copy.Card.Id} {copy.AttackDamage}/{copy.BaseHealth} damage={copy.Damage} taunt={copy.HasTaunt} reborn={copy[GameTag.REBORN]}; target={target.AttackDamage}/{target.BaseHealth}; durability={p.Hero.Weapon.Durability}");
        Play(g, p, "CS2_082"); var removed = p.BoardZone.Single(m => m.Card.Id == id); Kill(g, removed);
        right = Summon(p, "CS2_182");
        removed.Card.Power.PowerTask.Process(g, p, removed, target, new TaskStack()); g.DeathProcessingAndAuraUpdate();
        copy = p.BoardZone.Last();
        Core2026Proof.Check(copy.Card.Id == target.Card.Id && copy.Id != target.Id && copy.AttackDamage == 6
            && copy.BaseHealth == 8 && copy.Damage == 2 && p.BoardZone[copy.ZonePosition - 1] == right, "removed Battlecry source appends copy");
    });
}
