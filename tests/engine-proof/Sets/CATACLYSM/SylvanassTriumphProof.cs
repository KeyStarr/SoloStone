using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class SylvanassTriumphProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_557", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "Triumph declares cast-history consumer");
        var enemy = Summon(p.Opponent, "CS2_200"); var friendly = Summon(p, "CS2_200");
        var first = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == first.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD)
            .All(t => t.Target != null), "first player play requires a character target");
        var isolated = g.Clone();
        PlayEntity(isolated, isolated.Player1, isolated.IdEntityDic[first.Id], isolated.Player2.Hero);
        Core2026Proof.Check(isolated.Player2.Hero.Damage == 3 && ((Minion)isolated.IdEntityDic[enemy.Id]).Damage == 0
            && p.Opponent.Hero.Damage == 0 && p.PlayHistory.Count == 0, "clone first play single-target, original history unaffected");

        // Automatic invocations have no committed hand-play, even when repeatedly using the same physical spell.
        var auto = (Spell)Entity.FromCard(p, Cards.FromId(id));
        var ctx = new EffectContext(g, p.Id, p.Hero.Id);
        foreach (var kind in new[] { HistoryCastKind.Effect, HistoryCastKind.Repeat, HistoryCastKind.Drawn })
        {
            ctx.CastSpell(auto.Id, kind, enemy.Id); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(enemy.Damage == 3 && p.Opponent.Hero.Damage == 0 && p.PlayHistory.Count == 0,
                "effect/repeat/drawn cast does not unlock all-enemy mode");
            enemy.Damage = 0;
        }
        End(g);
        Play(g, p.Opponent, id, friendly);
        Core2026Proof.Check(friendly.Damage == 3 && p.Hero.Damage == 0, "opponent first copy single-target"); End(g);
        PlayEntity(g, p, first, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && p.Opponent.Hero.Damage == 0, "own first play excludes current occurrence and opponent prior copy");
        End(g); End(g);
        var late = Add(p, id); var options = p.Options().Where(t => t.Source?.Id == late.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Length == 1 && options[0].Target == null, "earlier-turn play before creation unlocks untargeted mode");
        var elusive = Summon(p.Opponent, "EDR_598");
        var stealth = Summon(p.Opponent, "CS2_200"); stealth.HasStealth = true;
        var immune = Summon(p.Opponent, "CS2_200"); immune.IsImmune = true;
        var shield = Summon(p.Opponent, "CS2_200"); shield.HasDivineShield = true;
        Summon(p, "CS2_052");
        PlayEntity(g, p, late);
        Core2026Proof.Check(enemy.Damage == 7 && p.Opponent.Hero.Damage == 4 && friendly.Damage == 3
            && elusive.Zone.Type == Zone.GRAVEYARD && stealth.Damage == 4 && immune.Damage == 0
            && shield.Damage == 0 && !shield.HasDivineShield, "second copy hits all enemies with spell damage, bypasses targeting protections but respects damage prevention");
        // The same entity's earlier player play also qualifies its later effect-only recast.
        enemy = Summon(p.Opponent, "CS2_200"); stealth.Damage = 0;
        ctx.CastSpell(late.Id, HistoryCastKind.Repeat); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.Damage == 4 && stealth.Damage == 4 && p.Opponent.Hero.Damage == 8
            && p.PlayHistory.Count(h => h.SourceCard.Id == id) == 2, $"automatic recast uses earlier hand-plays without adding a play: {enemy.Damage}/{stealth.Damage}/{p.Opponent.Hero.Damage}; plays={p.PlayHistory.Count(h => h.SourceCard.Id == id)}");

        // Independent branch: a countered committed play qualifies, an automatic cast does not.
        var counter = isolated.Clone(); var owner = counter.Player1;
        // This clone already has one player play; switch sides so the other controller starts at zero.
        var counterspell = Play(counter, owner, "EX1_287");
        End(counter); owner = counter.Player2;
        if (counterspell.Zone != owner.Opponent.SecretZone) throw new Exception("Counterspell fixture not installed");
        var victim = Summon(owner.Opponent, "CS2_200");
        Play(counter, owner, id, victim);
        Core2026Proof.Check(victim.Damage == 0 && owner.PlayHistory.Count(h => h.SourceCard.Id == id) == 1,
            "countered copy is a committed play without damage");
        Play(counter, owner, id);
        Core2026Proof.Check(victim.Damage == 3 && owner.Opponent.Hero.Damage == 3, "countered earlier copy unlocks all-enemy mode");

        // Fresh branch has no prior own plays and only one legal automatic target.
        var random = isolated.Clone(); owner = random.Player2;
        StandardSetsProof.ClearBoard(random, random.Player1); StandardSetsProof.ClearBoard(random, random.Player2);
        random.Player1.Hero.CantBeTargetedBySpells = true; random.Player2.Hero.CantBeTargetedBySpells = true;
        var hidden = Summon(owner.Opponent, "CS2_200"); hidden.HasStealth = true;
        var protectedMinion = Summon(owner.Opponent, "EDR_598");
        var target = Summon(owner.Opponent, "CS2_200");
        auto = (Spell)Entity.FromCard(owner, Cards.FromId(id));
        ctx = new EffectContext(random, owner.Id, owner.Hero.Id);
        ctx.CastSpell(auto.Id, HistoryCastKind.Effect, protectedMinion.Id); random.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(target.Damage == 3 && hidden.Damage == 0 && protectedMinion.Damage == 0,
            "illegal automatic Elusive target replaced by sole legal target using game RNG");
        target.Damage = 0;
        auto = (Spell)Entity.FromCard(owner, Cards.FromId(id));
        new PlayTask(PlayType.SPELL, EntityType.TARGET).Process(random, owner, owner.Hero, hidden,
            new TaskStack { Playables = new System.Collections.Generic.List<IPlayable> { auto } });
        random.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(target.Damage == 3 && hidden.Damage == 0 && protectedMinion.Damage == 0,
            "automatic PlayTask producer's hostile Stealth target replaced by legal target");
        Kill(random, target); var rng = random.Random.Clone();
        auto = (Spell)Entity.FromCard(owner, Cards.FromId(id));
        ctx.CastSpell(auto.Id, HistoryCastKind.Repeat); random.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(random.Random.Next() == rng.Next() && hidden.Damage == 0 && protectedMinion.Damage == 0,
            "automatic cast without legal targets fizzles without target RNG");
    });
}
