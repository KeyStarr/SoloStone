using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class OasisAllyProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BAR_812", (g, p, id) => {
        var defender = Summon(p, "CS2_182");
        Play(g, p, id);
        var ownAttacker = Summon(p, "CS2_182"); Attack(g, ownAttacker, p.Opponent.Hero);
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 2, "own turn cannot reveal secret");
        End(g);
        var attacker = Summon(p.Opponent, "CS2_182"); Attack(g, attacker, p.Hero);
        Core2026Proof.Check(p.SecretZone.Count == 1, "hero attack target does not trigger");
        attacker = Summon(p.Opponent, "CS2_182");
        var clone = g.Clone();
        Attack(clone, (Minion)clone.IdEntityDic[attacker.Id], (Minion)clone.IdEntityDic[defender.Id]);
        Core2026Proof.Check(clone.Player1.SecretZone.Count == 0 && clone.Player1.BoardZone.Last().Card.Id == "CS2_033" && p.SecretZone.Count == 1, "clone reveals independently");
        Attack(g, attacker, defender);
        var water = p.BoardZone.Last();
        Core2026Proof.Check(p.SecretZone.Count == 0 && water.Card.Id == "CS2_033" && water.AttackDamage == 3 && water.Health == 6, "minion attack reveals once and appends exact 3/6");
        End(g); Attack(g, water, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.IsFrozen, "summoned Water Elemental freezes damaged hero");
        ClearBoard(g, p); for (int i = 0; i < 7; i++) Summon(p, "CS2_182");
        Play(g, p, id); End(g);
        attacker = Summon(p.Opponent, "CS2_182"); Attack(g, attacker, p.BoardZone[0]);
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 7, "full board retains secret without summon");
        Kill(g, p.BoardZone.Last());
        attacker = Summon(p.Opponent, "CS2_182"); Attack(g, attacker, p.BoardZone[1]);
        Core2026Proof.Check(p.SecretZone.Count == 0 && p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Id == "CS2_033", "secret waits for board space");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); End(g);
        defender = Summon(p, "CS2_182"); ownAttacker = Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(Generic.AttackBlock.Invoke(p, ownAttacker, defender, true, false), "forced friendly attack on own turn accepted");
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 2, "forced friendly attack on own turn does not reveal secret");
        ownAttacker.Damage = 0; defender.Damage = 0;
        End(g);
        var enemyDefender = Summon(p.Opponent, "CS2_182");
        attacker = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(Generic.AttackBlock.Invoke(p.Opponent, attacker, enemyDefender, true, false), "forced opponent friendly attack accepted");
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 2, "opponent-owned defender does not reveal secret");
        clone = g.Clone();
        Core2026Proof.Check(Generic.AttackBlock.Invoke(clone.Player1, (Minion)clone.IdEntityDic[ownAttacker.Id],
            (Minion)clone.IdEntityDic[defender.Id], true, false), "forced friendly attack in clone accepted");
        Core2026Proof.Check(clone.Player1.SecretZone.Count == 0 && clone.Player1.BoardZone.Last().Card.Id == "CS2_033"
            && p.SecretZone.Count == 1 && p.BoardZone.Count == 2, "forced friendly attack reveals secret independently in clone");
        Core2026Proof.Check(Generic.AttackBlock.Invoke(p, ownAttacker, defender, true, false), "forced friendly attack on opponent turn accepted");
        water = p.BoardZone.Last();
        Core2026Proof.Check(p.SecretZone.Count == 0 && p.BoardZone.Count == 3 && water.Card.Id == "CS2_033"
            && water.AttackDamage == 3 && water.Health == 6, "friendly attacker on opponent turn reveals once and summons exact Water Elemental");
    });
}
