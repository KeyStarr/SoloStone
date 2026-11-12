using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class GorishiColossusProof
{
    private static int Stacks(Controller p) => QuestRules.PlayerEffectStacks(p, "TLC_631e");
    internal static void Run()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var card = Cards.FromId("TLC_631t");
        Core2026Proof.Check(card.Implemented && card.Cost == 5 && card.ATK == 8 && card.Health == 8 && card.IsRace(Race.BEAST), "reward identity");
        p.Opponent.Hero.Health = 200;
        UnleashTheColossusProof.Hit(p);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && !QuestRules.HasDamageListener(p), "no reward, no extra hit or listener");
        var body = (Minion)Play(g, p, "TLC_631t");
        Core2026Proof.Check(Stacks(p) == 1 && p.AppliedEnchantments.Single(e => e.Card.Id == "TLC_631e").Target == p,
            "battlecry attaches permanent effect to controller");
        UnleashTheColossusProof.Hit(p);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "one stack adds one separate two damage hit");
        Kill(g, body);
        UnleashTheColossusProof.Hit(p, 1); UnleashTheColossusProof.Hit(p, 3);
        p.Hero.TakeDamage(p.Hero, 2); p.Hero.TakeDamage(p.Opponent.Hero, 2);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10 && p.Hero.Damage == 4, "one/three/friendly/enemy-source ignored");
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        shield.TakeDamage(p.Hero, 2);
        p.Opponent.Hero.IsImmune = true; UnleashTheColossusProof.Hit(p); p.Opponent.Hero.IsImmune = false;
        Core2026Proof.Check(shield.Damage == 0 && p.Opponent.Hero.Damage == 10, "shield and immune prevent extra hit");
        End(g); UnleashTheColossusProof.Hit(p);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 14, "effect survives body death and fires on opponent turn");
        End(g); Unpinned_ExtraHitPerStack(g, p);
        Unpinned_SkipDeadTarget(g, p);
        CloneAndSnapshot();
        Console.WriteLine("PASS TLC_631t stacks, prevention, either turn, finite chains, clone/snapshot");
    }

    private static void Unpinned_ExtraHitPerStack(Game g, Controller p)
    {
        Play(g, p, "TLC_631t");
        Core2026Proof.Check(Stacks(p) == 2 && p.AppliedEnchantments.Count(e => e.Card.Id == "TLC_631e") == 1, "second battlecry stacks one effect");
        int damage = p.Opponent.Hero.Damage;
        var hits = new List<int>();
        void Observe(IEntity _) { hits.Add(g.CurrentEventData.EventNumber); }
        p.Opponent.Hero.TakeDamageTrigger += Observe;
        UnleashTheColossusProof.Hit(p);
        p.Opponent.Hero.TakeDamageTrigger -= Observe;
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 6 && hits.SequenceEqual(new[] { 2, 2, 2 }),
            "Unpinned_ExtraHitPerStack: 2+2+2 separate hits terminate");
        p.Opponent.Hero.Armor = 8; damage = p.Opponent.Hero.Damage;
        UnleashTheColossusProof.Hit(p);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 2 && p.Opponent.Hero.Damage == damage, "armor-only original hit triggers both extra hits");
        p.Opponent.Hero.Armor = 0;
        var q = (Spell)Play(g, p, "TLC_631"); UnleashTheColossusProof.Hit(p);
        Core2026Proof.Check(q.QuestProgress == 3, "extra hits still count toward quest, never retrigger stacks");
        End(g); UnleashTheColossusProof.Hit(p);
        Core2026Proof.Check(q.QuestProgress == 3, "opponent turn extra hits leave quest unchanged");
        End(g);
    }

    private static void Unpinned_SkipDeadTarget(Game g, Controller p)
    {
        foreach (int health in new[] { 1, 2, 3, 4 })
        {
            var target = Summon(p.Opponent, "CS2_182"); target.Health = health;
            target.TakeDamage(p.Hero, 2);
            Core2026Proof.Check(target.Damage == (health <= 2 ? 2 : 4), "Unpinned_SkipDeadTarget: stop at lethal, including between stacks");
            g.DeathProcessingAndAuraUpdate();
        }
        var pending = Summon(p.Opponent, "CS2_182"); pending.ToBeDestroyed = true;
        pending.TakeDamage(p.Hero, 2);
        Core2026Proof.Check(pending.Damage == 2, "Unpinned_SkipDeadTarget: positive-health pending destroy ignored");
        g.DeathProcessingAndAuraUpdate();
    }

    private static void CloneAndSnapshot()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = QuestLifecycleProof.Fixture(history); var p = g.Player1;
            Play(g, p, "TLC_631t"); Play(g, p, "TLC_631"); p.Opponent.Hero.Health = 200;
            UnleashTheColossusProof.Hit(p);
            var clone = g.CloneExact(history: history);
            byte[] bytes = g.SaveSnapshot(); var restored = Game.LoadSnapshot(bytes);
            Core2026Proof.Check(bytes.SequenceEqual(restored.SaveSnapshot()), "snapshot byte roundtrip history=" + history);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "clone equal history=" + history);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(restored), "reward clone/snapshot equal mid-quest");
            Play(clone, clone.Player1, "TLC_631t");
            Core2026Proof.Check(Stacks(p) == 1 && Stacks(restored.Player1) == 1 && Stacks(clone.Player1) == 2,
                "cloned stack tags independent");
            Play(g, p, "TLC_631t"); Play(restored, restored.Player1, "TLC_631t");
            for (int i = 0; i < 4; i++) { UnleashTheColossusProof.Hit(p); UnleashTheColossusProof.Hit(clone.Player1); UnleashTheColossusProof.Hit(restored.Player1); }
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored)
                && UnleashTheColossusProof.Rewards(p) == 1 && p.SecretZone.ModernQuests.Count == 0,
                "stacked effect resumes, completes quest once, persists after completion");
            if (history) Core2026Proof.Check(Canonical.History(g) == Canonical.History(restored), "reward resume history equal");
        }
    }
}
