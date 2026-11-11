using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class MuradinsLastStandProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_568", (g, p, id) => {
        var untouched = Add(p, id);
        Core2026Proof.Check(untouched.Cost == 9 && untouched[GameTag.COST] == 9, "no attacks keeps base visible cost");
        var attacker = Summon(p, "CS2_171");
        Attack(g, attacker, p.Opponent.Hero);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(untouched.Cost == 8 && untouched[GameTag.COST] == 8, "friendly minion attack updates visible cost");
        Play(g, p, "CS2_082"); p.Hero.IsExhausted = false;
        var heroAttack = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(heroAttack), "hero attack accepted");
        var late = Add(p, id);
        Core2026Proof.Check(late.Cost == 7, "hero attack and history before card creation count");
        End(g); var enemy = Summon(p.Opponent, "CS2_171"); Attack(g, enemy, p.Hero); End(g);
        Core2026Proof.Check(late.Cost == 7, "own lifetime attacks persist; opponent attack excluded");
        var clone = g.Clone();
        new AttackTask(EntityType.SOURCE, EntityType.OP_HERO).Process(clone, clone.Player1, clone.IdEntityDic[attacker.Id], null);
        clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[late.Id].Cost == 6 && late.Cost == 7, "forced attack counted only in divergent clone");
        var doomed = Summon(p, "CS2_171");
        void Cancel(IEntity _) => doomed.ToBeDestroyed = true;
        g.TriggerManager.AttackTrigger += Cancel;
        new AttackTask(EntityType.SOURCE, EntityType.OP_HERO).Process(g, p, doomed, null);
        g.TriggerManager.AttackTrigger -= Cancel;
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(late.Cost == 7, "cancelled attack never reached combat and gives no discount");
        for (int i = 0; i < 9; i++) new AttackTask(EntityType.SOURCE, EntityType.OP_HERO).Process(g, p, attacker, null);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(late.Cost == 0 && late[GameTag.COST] == 0, "discount floors at zero");
        int deck = p.DeckZone.Count, hand = p.HandZone.Count;
        PlayEntity(g, p, late);
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && p.HandZone.Count == hand + 1, "draw exactly two at discounted cost");
        while (p.HandZone.Count < 9) Add(p, "GAME_005");
        deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 2, "full hand burns overflow while drawing two");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
    });
}
