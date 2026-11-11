using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class ConfluxCrasherProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_004", (g, p, id) => {
        var friendly = Summon(p, "CS2_182");
        var source = Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 7 && p.Hero.Damage == 0 && friendly.Damage == 0,
            "lone enemy hero takes seven; own side excluded");
        p.Opponent.Hero.BaseHealth = 1000;
        foreach (string protection in new[] { "Stealth", "Immune", "Elusive" })
        {
            var recipient = Summon(p.Opponent, "CS2_186"); recipient.BaseHealth = 1000;
            recipient.HasStealth = protection == "Stealth";
            recipient.IsImmune = protection == "Immune";
            recipient.CantBeTargetedBySpells = protection == "Elusive";
            recipient[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = protection == "Elusive" ? 1 : 0;
            bool hitHero = false, pickedMinion = false;
            for (int i = 0; i < 12; i++)
            {
                int heroDamage = p.Opponent.Hero.Damage, minionDamage = recipient.Damage;
                long draws = g.Random.DrawCount;
                PoolPilotProof.Resolve(g, source);
                int heroDelta = p.Opponent.Hero.Damage - heroDamage, minionDelta = recipient.Damage - minionDamage;
                Core2026Proof.Check(g.Random.DrawCount == draws + 1 &&
                    (heroDelta == 7 && minionDelta == 0 || heroDelta == 0 && minionDelta == (recipient.IsImmune ? 0 : 7)),
                    protection + ": one random enemy selected; Immune prevents damage after selection");
                hitHero |= heroDelta == 7; pickedMinion |= heroDelta == 0;
            }
            Core2026Proof.Check(hitHero && pickedMinion && friendly.Damage == 0 && p.Hero.Damage == 0,
                protection + ": both enemy recipients sampled, friendly characters excluded");
            Kill(g, recipient);
        }
        var protectedEnemy = Summon(p.Opponent, "CS2_186");
        protectedEnemy.IsImmune = protectedEnemy.HasStealth = p.Opponent.Hero.IsImmune = true;
        var rng = g.Random.CaptureState();
        PoolPilotProof.Resolve(g, source);
        Core2026Proof.Check(g.Random.DrawCount == rng.DrawCount + 1 && protectedEnemy.Damage == 0,
            "all enemies protected: selection still consumes RNG");
        Kill(g, protectedEnemy); p.Opponent.Hero.IsImmune = false;
        foreach (bool destroyed in new[] { false, true })
        {
            var dead = Summon(p.Opponent, "CS2_186");
            if (destroyed) dead.ToBeDestroyed = true; else dead.Damage = dead.Health;
            int before = p.Opponent.Hero.Damage;
            rng = g.Random.CaptureState();
            source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
            Core2026Proof.Check(p.Opponent.Hero.Damage == before + 7 && rng.Equals(g.Random.CaptureState()),
                "dead/destroyed enemy excluded before cleanup; sole living enemy needs no RNG");
            g.DeathProcessingAndAuraUpdate();
        }
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int damage = p.Opponent.Hero.Damage;
        PoolPilotProof.Resolve(g, source);
        Core2026Proof.Check(p.BoardZone.IsFull && p.Opponent.Hero.Damage == damage + 7,
            "full friendly board still resolves damage");
    });
}
