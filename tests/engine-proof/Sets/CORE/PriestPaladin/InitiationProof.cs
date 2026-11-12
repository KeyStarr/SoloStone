using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class InitiationProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SCH_512", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182");
        Play(g, p, id, target);
        Core2026Proof.Check(target.Health == 1 && p.BoardZone.Count == 0, "surviving target gives no copy");
        target.HasDivineShield = true; Play(g, p, id, target);
        Core2026Proof.Check(target.Health == 1 && !target.HasDivineShield && p.BoardZone.Count == 0, "shield prevents lethal damage and copy");
        Generic.AddEnchantmentBlock(g, Cards.FromId("BT_292e"), p.Hero, target); target.Damage = 4;
        var left = Summon(p, "CS2_182"); Play(g, p, id, target);
        var copy = p.BoardZone.Last();
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && copy.Card.Id == "CS2_182" && copy.AttackDamage == 4 && copy.Health == 5 && copy.Damage == 0, "new base copy excludes damage and buffs");
        Core2026Proof.Check(p.BoardZone[0] == left && copy.ZonePosition == 1, "spell copy appends");
        Summon(p, "EX1_012"); var spellTarget = Summon(p.Opponent, "CS2_182"); Play(g, p, id, spellTarget);
        Core2026Proof.Check(spellTarget.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Last().Card.Id == "CS2_182", "Spell Damage changes lethal threshold");
        TargetRules(g, p, id, copy, left, false);
        ClearBoard(g, p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        var friendly = Summon(p, "CS2_182"); friendly.Damage = 1; Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 6, "mortally wounded friendly still occupies full-board slot during spell");
        Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "EX1_506a"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 7, "full friendly board blocks enemy copy");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        var golem = Summon(p, "EX1_556"); Play(g, p, id, golem);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Id == "skele21" && !p.BoardZone.Any(m => m.Card.Id == "EX1_556"), "full board blocks copy before Deathrattle later fills freed slot");
        ClearBoard(g, p);
        golem = Summon(p.Opponent, "EX1_556");
        bool beforeDeath = false;
        void Observe(IEntity summoned) {
            if (summoned.Controller == p && summoned.Card.Id == "EX1_556")
                beforeDeath = golem.Zone == p.Opponent.BoardZone && golem.IsDead
                    && !p.Opponent.BoardZone.Any(m => m.Card.Id == "skele21");
        }
        g.TriggerManager.AfterSummonTrigger += Observe;
        Play(g, p, id, golem);
        g.TriggerManager.AfterSummonTrigger -= Observe;
        Core2026Proof.Check(beforeDeath && p.BoardZone.Single().Card.Id == "EX1_556"
            && p.Opponent.BoardZone.Any(m => m.Card.Id == "skele21"), "base copy summons before target death and Deathrattle");
    });
}
