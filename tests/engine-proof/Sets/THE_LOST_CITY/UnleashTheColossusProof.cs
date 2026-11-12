using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class UnleashTheColossusProof
{
    internal static void Hit(Controller p, int amount = 2) => p.Opponent.Hero.TakeDamage(p.Hero, amount);
    internal static Spell Quest(Controller p) => p.SecretZone.ModernQuests.Single();
    internal static int Rewards(Controller p) => p.HandZone.Count(c => c.Card.Id == "TLC_631t")
        + p.GraveyardZone.Count(c => c.Card.Id == "TLC_631t");

    internal static void Run()
    {
        StandardSetsProof.Case("TLC_631", (g, p, id) => {
            var q = (Spell)Play(g, p, id);
            p.Opponent.Hero.Health = 200;
            Core2026Proof.Check(q.QuestProgress == 0 && q.QuestTotalProgress == 12, "quest starts zero/12");
            var minion = Summon(p.Opponent, "CS2_182");
            minion.TakeDamage(p.Hero, 2); Hit(p);
            Core2026Proof.Check(q.QuestProgress == 2, "two damage to enemy minion and hero count separately");
            Hit(p, 1); Hit(p, 3); p.Hero.TakeDamage(p.Hero, 2);
            p.Hero.TakeDamage(p.Opponent.Hero, 2);
            Core2026Proof.Check(q.QuestProgress == 2, "one/three, friendly target, opponent source ignored");
            minion.HasDivineShield = true; minion.TakeDamage(p.Hero, 2);
            p.Opponent.Hero.IsImmune = true; Hit(p); p.Opponent.Hero.IsImmune = false;
            Core2026Proof.Check(!minion.HasDivineShield && q.QuestProgress == 2, "shield and immune prevent progress");
            End(g); Hit(p);
            Core2026Proof.Check(q.QuestProgress == 2, "owner damage on opponent turn ignored");
            End(g);
            PinnedRule_ArmorCountsAsDealt(p, q);
            PinnedRule_OverkillCounts(g, p, q);
            while (q.QuestProgress < 11) Hit(p);
            Core2026Proof.Check(Rewards(p) == 0 && q.Zone == p.SecretZone, "11 hits below threshold");
            Hit(p);
            Core2026Proof.Check(q.QuestProgress == 12 && q.Zone == p.GraveyardZone && Rewards(p) == 1, "12th hit grants one reward");
            Hit(p);
            Core2026Proof.Check(Rewards(p) == 1 && !QuestRules.HasDamageListener(p), "completed listener gone; no duplicate");
        });
        SourcesAndModifiers();
        AreaDamage();
        PinnedRule_RewardWaitsForHandSpace();
        CloneAndSnapshot();
    }

    private static void PinnedRule_ArmorCountsAsDealt(Controller p, Spell q)
    {
        int progress = q.QuestProgress;
        p.Opponent.Hero.Armor = 5; int damage = p.Opponent.Hero.Damage;
        Hit(p);
        Core2026Proof.Check(q.QuestProgress == progress + 1 && p.Opponent.Hero.Armor == 3 && p.Opponent.Hero.Damage == damage,
            "PinnedRule_ArmorCountsAsDealt: armor-only two counts");
        p.Opponent.Hero.Armor = 1; Hit(p);
        Core2026Proof.Check(q.QuestProgress == progress + 2 && p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == damage + 1,
            "PinnedRule_ArmorCountsAsDealt: split armor/health counts");
        p.Opponent.Hero.Armor = 1; Hit(p, 3);
        Core2026Proof.Check(q.QuestProgress == progress + 2, "three dealt, two post-armor does not count");
    }

    private static void PinnedRule_OverkillCounts(Game g, Controller p, Spell q)
    {
        var target = Summon(p.Opponent, "CS2_168"); int progress = q.QuestProgress;
        target.TakeDamage(p.Hero, 2);
        Core2026Proof.Check(target.Health == -1 && q.QuestProgress == progress + 1, "PinnedRule_OverkillCounts");
        g.DeathProcessingAndAuraUpdate();
    }

    private static void SourcesAndModifiers()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var q = (Spell)Play(g, p, "TLC_631"); p.Opponent.Hero.Health = 200;
        var attacker = Summon(p, "CS2_168"); Attack(g, attacker, p.Opponent.Hero);
        Play(g, p, "DS1_185", p.Opponent.Hero);
        Play(g, p, "LOE_118"); p.Hero.IsExhausted = false;
        var attack = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id);
        Core2026Proof.Check(g.Process(attack) && q.QuestProgress == 3, "minion attack, spell, weapon hero attack count");
        p.Hero.TakeDamage(p.Opponent.Hero.HeroPower, 2);
        p.Opponent.Hero.TakeDamage(p.Hero.HeroPower, 2);
        var weapon = p.Hero.Weapon;
        p.Opponent.Hero.TakeDamage(weapon, 2);
        Core2026Proof.Check(q.QuestProgress == 5, "friendly hero power and weapon source count, enemy source ignored");
        p.Hero.HeroPowerDamage = 1;
        Generic.DamageCharFunc(p.Hero.HeroPower, p.Opponent.Hero, 1, false);
        Core2026Proof.Check(q.QuestProgress == 6, "hero power modifier applied before fact");
        var target = Summon(p.Opponent, "CS2_182");
        void Reduce(IEntity _) { g.CurrentEventData.EventNumber = 2; }
        target.PreDamageTrigger += Reduce;
        target.TakeDamage(p.Hero, 3);
        target.PreDamageTrigger -= Reduce;
        Core2026Proof.Check(target.Damage == 2 && q.QuestProgress == 7, "predamage modifier commits actual two");
        void Prevent(IEntity _) { g.CurrentEventData.EventNumber = 0; }
        target.PreDamageTrigger += Prevent;
        target.TakeDamage(p.Hero, 2);
        target.PreDamageTrigger -= Prevent;
        Core2026Proof.Check(q.QuestProgress == 7, "fully prevented predamage ignored");
    }

    private static void AreaDamage()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var q = (Spell)Play(g, p, "TLC_631");
        Summon(p.Opponent, "CS2_182"); Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_093");
        Core2026Proof.Check(q.QuestProgress == 3, "AoE counts each of two enemy minions and hero");
    }

    private static void PinnedRule_RewardWaitsForHandSpace()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var q = (Spell)Play(g, p, "TLC_631"); p.Opponent.Hero.Health = 200;
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        for (int i = 0; i < 13; i++) Hit(p);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.Id != "TLC_631t")
            && Rewards(p) == 0 && q.Zone == p.GraveyardZone
            && !g.IdEntityDic.Values.Any(c => c.Card.Id == "TLC_631t"), "full hand waits; consumes quest without burn");
        PlayEntity(g, p, p.HandZone[0]);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "TLC_631t") == 1
            && !p.GraveyardZone.Any(c => c.Card.Id == "TLC_631t"), "play frees slot and delivers once");
    }

    private static void CloneAndSnapshot()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = QuestLifecycleProof.Fixture(history); var p = g.Player1;
            var q = (Spell)Play(g, p, "TLC_631"); p.Opponent.Hero.Health = 200;
            for (int i = 0; i < 5; i++) Hit(p);
            var clone = g.CloneExact(history: history);
            byte[] bytes = g.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes);
            Core2026Proof.Check(bytes.SequenceEqual(restored.SaveSnapshot()), "snapshot byte roundtrip history=" + history);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "clone equal history=" + history);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(restored), "mid-progress clone/snapshot equal");
            Core2026Proof.Check(!ReferenceEquals(q.QuestState, Quest(clone.Player1).QuestState), "quest clone state independent");
            for (int i = 0; i < 8; i++) Hit(clone.Player1);
            Core2026Proof.Check(q.QuestProgress == 5 && Rewards(p) == 0 && Quest(restored.Player1).QuestProgress == 5,
                "clone cannot advance original or restored quest");
            for (int i = 0; i < 8; i++) { Hit(p); Hit(restored.Player1); }
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored)
                && Rewards(p) == 1 && Rewards(clone.Player1) == 1 && Rewards(restored.Player1) == 1,
                "resume same progress, exactly one reward");
            if (history) Core2026Proof.Check(Canonical.History(g) == Canonical.History(restored), "resume history equal");
        }
        Console.WriteLine("PASS TLC_631 damage qualification, sources, threshold, pending reward, clone/snapshot");
    }
}
