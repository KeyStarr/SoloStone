using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class ShowOfForceProof
{
    internal static void Run() => StandardSetsProof.Case("BE_559", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "Show of Force declares history consumer");
        DamageAndTargets(g.Clone(), id);
        LostPlayerTarget(g.Clone(), id);
        TurnWindows(g.Clone(), id);
        AutomaticCasts(g.Clone(), id);
        LegacyProducers(g.Clone(), id);
        DrawnProducer(g.Clone(), id);

        var victim = Summon(p.Opponent, "CS2_200");
        var held = Add(p, id);
        var clone = g.Clone();
        clone.Player1.Hero.GainArmor(clone.Player1.Hero, 1);
        PlayEntity(clone, clone.Player1, clone.IdEntityDic[held.Id], (Minion)clone.IdEntityDic[victim.Id]);
        Core2026Proof.Check(clone.IdEntityDic[victim.Id].Zone.Type == Zone.GRAVEYARD && victim.Damage == 0
            && !g.HistoryFacts.Query.ArmorGains().Any(), "clone armor gain/destruction leaves original history and board untouched");
        PlayEntity(g, p, held, victim);
        Core2026Proof.Check(victim.Damage == 3 && victim.Zone.Type == Zone.PLAY, "original clone branch still deals damage without armor gain");

        p.Hero.GainArmor(p.Hero, 1);
        p.Hero.TakeDamage(p.Opponent.Hero, 1);
        var createdLate = Play(g, p, id, victim);
        Core2026Proof.Check(p.Hero.Armor == 0 && victim.Zone.Type == Zone.GRAVEYARD && victim.Damage == 3
            && !g.HistoryFacts.Query.Damage().Any(f => f.SourceId == createdLate.Id),
            "gain before creation, then lost armor, destroys instead of dealing damage");
        var drawn = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(drawn);
        Generic.Draw(p, drawn);
        var shield = Summon(p, "CS2_200"); shield.HasDivineShield = true;
        PlayEntity(g, p, drawn, shield);
        Core2026Proof.Check(shield.Zone.Type == Zone.GRAVEYARD && shield.Damage == 0 && shield.HasDivineShield,
            "gain before draw remains visible; destruction bypasses Divine Shield without a damage event");
    });

    private static void DamageAndTargets(Game g, string id)
    {
        var p = g.Player1;
        var friendly = Summon(p, "CS2_200");
        var enemy = Summon(p.Opponent, "CS2_200");
        var elusive = Summon(p.Opponent, "EDR_598");
        var stealth = Summon(p.Opponent, "CS2_200"); stealth.HasStealth = true;
        var immune = Summon(p.Opponent, "CS2_200"); immune.IsImmune = true;
        var dormant = Summon(p.Opponent, "CS2_200"); dormant[GameTag.DORMANT] = 1;
        var spell = Add(p, id);
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(targets.Length == 2 && targets.All(t => t.Target is Minion)
            && targets.Any(t => t.Target.Id == friendly.Id) && targets.Any(t => t.Target.Id == enemy.Id),
            "required friendly/enemy minion; heroes, Elusive, hostile Stealth/Immune and Dormant excluded");
        PlayEntity(g, p, spell, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && friendly.Damage == 0, "no armor gain deals exactly three to selected minion");
        Summon(p, "EX1_012");
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Damage == 4, "damage branch scales with Spell Damage");
        var shield = Summon(p, "CS2_200"); shield.HasDivineShield = true;
        Play(g, p, id, shield);
        Core2026Proof.Check(shield.Damage == 0 && !shield.HasDivineShield, "damage branch respects Divine Shield");
        friendly.IsImmune = true;
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Damage == 4, "friendly Immune target prevents damage");
        p.Hero.GainArmor(p.Hero, 1);
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Zone.Type == Zone.GRAVEYARD && friendly.Damage == 4,
            "destroy branch bypasses friendly immunity without damage");
        StandardSetsProof.ClearBoard(g, p); StandardSetsProof.ClearBoard(g, p.Opponent);
        spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD),
            "no minion means required-target spell cannot be played");
    }

    private static void LostPlayerTarget(Game fixture, string id)
    {
        foreach (bool armor in new[] { false, true })
        {
            var g = fixture.Clone(); var p = g.Player1;
            Summon(p, "NEW1_026");
            var juggler = Summon(p, "NEW1_019");
            var target = Summon(p.Opponent, "CS2_231");
            if (armor) p.Hero.GainArmor(p.Hero, 1);
            var spell = Add(p, id);
            g.SetRandomSeed(3);
            var expectedRng = g.Random.Clone();
            Core2026Proof.Check(expectedRng.Next(2) == 1, "fixture knife targets the minion instead of the hero");
            PlayEntity(g, p, spell, target);
            Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD
                && g.HistoryFacts.Query.Damage().Any(f => f.SourceId == juggler.Id && f.VictimId == target.Id && f.Amount == 1),
                "Violet Teacher summon triggers Knife Juggler to kill selected target before spell text");
            Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.All(m => m.Damage == 0)
                && p.BoardZone.Count(m => m.Card.Id == "NEW1_026t") == 1
                && !g.HistoryFacts.Query.Damage().Any(f => f.SourceId == spell.Id),
                "lost player-selected target causes no replacement damage or destruction in either armor branch");
            Core2026Proof.Check(g.Random.Next() == expectedRng.Next(), "lost player-selected target consumes no fallback RNG");
        }
    }

    private static void TurnWindows(Game g, string id)
    {
        var p = g.Player1;
        var enemy = Summon(p.Opponent, "CS2_200");
        p.Opponent.Hero.GainArmor(p.Opponent.Hero, 5);
        p.Hero.GainArmor(p.Hero, 0);
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && enemy.Zone.Type == Zone.PLAY, "opponent gain and own zero gain do not qualify");
        p.Hero.GainArmor(p.Hero, 5);
        End(g);
        var auto = (Spell)Entity.FromCard(p, Cards.FromId(id));
        new EffectContext(g, p.Id, auto.Id).CastSpell(auto.Id, HistoryCastKind.Effect, enemy.Id);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.Damage == 6 && enemy.Zone.Type == Zone.PLAY, "immediately previous global turn gain does not qualify");
        End(g);
        enemy = Summon(p.Opponent, "CS2_200");
        p.Hero.Armor = 4;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && enemy.Zone.Type == Zone.PLAY, "retained armor and lowering SET do not qualify on later turn");
        p.Hero[GameTag.ARMOR] = 5;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && enemy.Damage == 3, "positive committed armor SET counts under binding R5 lean");
        End(g);
        enemy = Summon(p.Opponent, "CS2_200");
        p.Hero.GainArmor(p.Opponent.Hero, 1);
        auto = (Spell)Entity.FromCard(p, Cards.FromId(id));
        new EffectContext(g, p.Id, auto.Id).CastSpell(auto.Id, HistoryCastKind.Effect, enemy.Id);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "own gain during opponent turn counts for same-global-turn automatic cast");
    }

    private static void AutomaticCasts(Game fixture, string id)
    {
        foreach (bool armor in new[] { false, true })
        foreach (var kind in new[] { HistoryCastKind.Effect, HistoryCastKind.Repeat, HistoryCastKind.Drawn })
        {
            var g = fixture.Clone(); var p = g.Player1;
            var elusive = Summon(p.Opponent, "EDR_598");
            var stealth = Summon(p.Opponent, "CS2_200"); stealth.HasStealth = true;
            var immune = Summon(p.Opponent, "CS2_200"); immune.IsImmune = true;
            var first = Summon(p.Opponent, "CS2_200");
            var second = Summon(p.Opponent, "CS2_200");
            if (armor) p.Hero.GainArmor(p.Hero, 1);
            var spell = (Spell)Entity.FromCard(p, Cards.FromId(id));
            var expectedRng = g.Random.Clone();
            int expected = expectedRng.Next(2) == 0 ? first.Id : second.Id;
            var other = expected == first.Id ? second : first;
            // Null and invalid legacy producer targets must both use legal, game-seeded selection.
            new EffectContext(g, p.Id, spell.Id).CastSpell(spell.Id, kind, kind == HistoryCastKind.Repeat ? elusive.Id : null);
            g.DeathProcessingAndAuraUpdate();
            var selected = (Minion)g.IdEntityDic[expected];
            Core2026Proof.Check((armor ? selected.Zone.Type == Zone.GRAVEYARD : selected.Damage == 3)
                && other.Damage == 0 && other.Zone.Type == Zone.PLAY
                && elusive.Damage == 0 && stealth.Damage == 0 && immune.Damage == 0
                && !g.HistoryFacts.Query.Plays().Any()
                && g.HistoryFacts.Query.Casts().Count(f => f.EntityId == spell.Id && f.CasterKind == kind) == 2,
                "automatic casts use legal minion targets and game RNG, with correct armor branch/cast kind and no play");
            Core2026Proof.Check(g.Random.Next() == expectedRng.Next(), "automatic target uses exactly one game RNG selection");
            Kill(g, other); if (selected.Zone.Type == Zone.PLAY) Kill(g, selected);
            var rng = g.Random.Clone();
            spell = (Spell)Entity.FromCard(p, Cards.FromId(id));
            new EffectContext(g, p.Id, spell.Id).CastSpell(spell.Id, kind, stealth.Id);
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(g.Random.Next() == rng.Next() && elusive.Damage == 0 && stealth.Damage == 0 && immune.Damage == 0,
                "no legal automatic target fizzles without RNG or hitting protected targets");
        }
    }

    private static void LegacyProducers(Game fixture, string id)
    {
        foreach (bool archivist in new[] { true, false })
        {
            var g = fixture.Clone(); var p = g.Player1;
            var elusive = Summon(p.Opponent, "EDR_598");
            var target = Summon(p.Opponent, "CS2_200");
            p.Hero.GainArmor(p.Hero, 1);
            IPlayable spell;
            if (archivist)
            {
                spell = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(spell);
                var source = Summon(p, "LOOT_414"); source.CantBeTargetedBySpells = true;
                End(g);
            }
            else
            {
                spell = Add(p, id); End(g);
                p.Hero.GainArmor(p.Hero, 1);
                var source = (Minion)Add(p.Opponent, "DAL_538"); source.CantBeTargetedBySpells = true;
                PlayEntity(g, p.Opponent, source);
            }
            Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && elusive.Zone.Type == Zone.PLAY && elusive.Damage == 0
                && spell.Zone.Type == Zone.GRAVEYARD
                && g.HistoryFacts.Query.Casts().Count(f => f.EntityId == spell.Id && f.CasterKind == HistoryCastKind.Effect) == 2,
                "actual Archivist/Saboteur casts respect Elusive and spell controller's armor history");
        }
    }

    private static void DrawnProducer(Game g, string id)
    {
        var p = g.Player1;
        var elusive = Summon(p.Opponent, "EDR_598");
        var target = Summon(p.Opponent, "CS2_200");
        p.Hero.GainArmor(p.Hero, 1);
        var card = Cards.FromId(id).Clone(); card.Tags[GameTag.CASTSWHENDRAWN] = 1;
        card.Power = new Power {
            PowerTask = card.Power.PowerTask,
            TopdeckTask = new CustomTask((game, controller, source, t, stack) =>
                Generic.CastSpell(controller, game, (Spell)source, null, 0))
        };
        var spell = Entity.FromCard(p, card); p.DeckZone.Add(spell);
        Generic.Draw(p, spell); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && elusive.Damage == 0
            && g.HistoryFacts.Query.Casts().Count(f => f.EntityId == spell.Id && f.CasterKind == HistoryCastKind.Drawn) == 2
            && !g.HistoryFacts.Query.Plays().Any(), "actual when-drawn dispatch selects legal target and keeps one Drawn invocation");
    }
}
