using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SizzlingSwarmProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_221", (g, p, id) => {
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        Play(g, p, id, shield); Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && p.BoardZone.Count == 0, "prevented damage summons none");
        p.Opponent.Hero.HasDivineShield = true; Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && !p.Opponent.Hero.HasDivineShield && p.BoardZone.Count == 0, "hero shield absorbs hit without summons");
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.BoardZone.Count == 3 && p.BoardZone.All(m => m.Card.Id == "TLC_249" && m.AttackDamage == 2 && m.Health == 1), "three actual damage creates pinned 2/1 Cinders");
        var cinders = p.BoardZone.ToArray(); Kill(g, cinders[0]);
        Core2026Proof.Check(p.BoardZone.Count == 2 && shield.Damage + p.Opponent.Hero.Damage == 5, "generated Cinder retains two-damage Deathrattle");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var power = Summon(p, "EX1_012"); var victim = Summon(p.Opponent, "CS2_231");
        Play(g, p, id, victim); Core2026Proof.Check(p.BoardZone.Count == 5 && victim.Zone == p.Opponent.GraveyardZone, "four spell damage including overkill summons four");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 4 && p.BoardZone.Count == 7, "friendly hero target; full board caps summons");
        ClearBoard(g, p); p.Opponent.Hero.Damage = 0; p.Opponent.Hero.Armor = 5;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 2 && p.Opponent.Hero.Damage == 0
            && p.BoardZone.Count == 3, "damage absorbed by Armor still summons three Cinders");
        ClearBoard(g, p); int damage = p.Hero.Damage; p.Hero.IsImmune = true;
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == damage && p.BoardZone.Count == 0, "Immune prevents damage and summons");
        p.Hero.IsImmune = false;
    });
}
