using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BarricadeBasherProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_400", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        p.Hero.GainArmor(p.Hero, 0); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 4 && source.BaseHealth == 3, "zero Armor does not trigger");
        Play(g, p, "EX1_606");
        Core2026Proof.Check(source.AttackDamage == 6 && source.BaseHealth == 5 && source.Damage == 0 && p.Opponent.Hero.Damage == 0, "Armor buffs even without enemy minions; never attacks hero");
        var enemy = Summon(p.Opponent, "CS2_182");
        var clone = g.Clone(); Play(clone, clone.Player1, "EX1_606");
        var copied = (Minion)clone.IdEntityDic[source.Id];
        Core2026Proof.Check(copied.AttackDamage == 8 && copied.BaseHealth == 7 && copied.Damage == 4
            && clone.IdEntityDic[enemy.Id].Zone == clone.Player2.GraveyardZone && source.AttackDamage == 6 && enemy.Damage == 0, "clone buffs then forces attack, original unaffected");
        Play(g, p, "EX1_606"); Core2026Proof.Check(source.AttackDamage == 8 && source.Damage == 4 && enemy.Zone == p.Opponent.GraveyardZone, "summoning sickness does not prevent forced attack");
        End(g); Play(g, p.Opponent, "EX1_606");
        Core2026Proof.Check(source.AttackDamage == 8 && source.BaseHealth == 7, "opponent Armor does not trigger");
        End(g); Core2026Proof.Check(source.AttackDamage == 8, "buff permanent across turns");
        Play(g, p, "BE_228", source); Play(g, p, "EX1_606");
        Core2026Proof.Check(source.AttackDamage == 4 && source.BaseHealth == 3, "Silence removes buffs and trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Kill(g, source); Play(g, p, "EX1_606");
        Core2026Proof.Check(source.AttackDamage == 4, "dead source never triggers");
        var hand = (Minion)Add(p, id); Play(g, p, "EX1_606");
        Core2026Proof.Check(hand.AttackDamage == 4, "hand copy never triggers");
    });
}
