using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;
using static Core2026Proof;

// Regression proofs for the targeting rules: random damage and destroy skip mortally wounded characters (#120), mass effects skip
// Untouchable minions (#121), a damage wound lifted above 0 Health before the death check is rescued (#123), a transform resets
// Attack and Health (#138), and Slime 'em! gives both players its spell (#139).
internal static class TargetingRulesProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, "TargetingRules: " + message);

    internal static void Run()
    {
        var failures = new List<string>();
        var cases = new (string, Action)[] {
            ("#120 random damage skips mortally wounded", RandomDamageSkipsMortallyWounded),
            ("#120 nothing left", RandomDamageWithNothingElseLeft),
            ("#120 inline random picks", InlineRandomPicks),
            ("#120 Deathwing and Flames", DeathwingAndFlames),
            ("#121 mass transform skips Untouchable", MassTransformSkipsUntouchable),
            ("#138 transform resets stats", TransformResetsStats),
            ("#123 heal before death check", HealBeforeDeathCheck),
            ("#123 rescued death attribution", RescuedDeathAttribution),
            ("#123 rescued combat kill tracking", RescuedCombatKillTracking),
            ("#123 buff, aura, clone", BuffAndAuraRescue),
            ("#123 heal after death check", HealAfterDeathCheck),
            ("#123 real damage path, Poisonous", RealDamageAndPoison),
            ("#123 Silence, copy, transform", SilenceCopyTransform),
            ("#123 snapshot at a decision", SnapshotWithMark),
            ("#121 Untouchable sweep", UntouchableSweep),
            ("#120 mortally wounded sweep", MortallyWoundedSweep),
            ("#139 Slime 'em! empty boards", SlimeEmEmptyBoards) };
        foreach (var (name, proof) in cases)
        {
            try { proof(); }
            catch (Exception e) { failures.Add(name + ": " + e.Message); }
        }
        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        Console.WriteLine("PASS targeting rules: " + cases.Length + " proof groups");
    }

    // #120: two Sol'etos Deathrattles die together; the first 5-damage ping may pick the lone enemy minion, the second must not
    // pick it again while it is still pending death.
    private static void RandomDamageSkipsMortallyWounded()
    {
        int minionThenHero = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
            var target = Summon(p.Opponent, "CS2_182");
            var a = Summon(p, "TLC_817t4"); var b = Summon(p, "TLC_817t4");
            a.ToBeDestroyed = true; b.ToBeDestroyed = true;
            g.DeathProcessingAndAuraUpdate();
            Verify(target.Damage <= 5, $"seed {seed}: the pending-death minion took a second random hit (damage {target.Damage})");
            int hero = p.Opponent.Hero.Damage;
            Verify(target.Damage == 5 ? hero == 5 : hero == 10, $"seed {seed}: both pings land (minion {target.Damage}, hero {hero})");
            if (target.Damage == 5) minionThenHero++;
        }
        Verify(minionThenHero > 0, "the minion-first ordering was exercised");
    }

    // The effect does nothing when every candidate is mortally wounded or pending destroy.
    private static void RandomDamageWithNothingElseLeft()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
            var enemyMinion = Summon(p.Opponent, "CS2_182"); enemyMinion.Damage = 9;
            var hero = p.Opponent.Hero; hero.Damage = 29; hero.ToBeDestroyed = true;
            var death = Summon(p, "TLC_817t4");
            death.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Verify(enemyMinion.Damage == 9 && hero.Damage == 29, $"seed {seed}: no candidate left, nothing is hit (minion {enemyMinion.Damage}, hero {hero.Damage})");
        }
    }

    // #120: Deathrattles and spells that build and run their own RandomTask: a Wisp (1 Health) that the first ping kills must not be
    // picked again by the second Deathrattle while it is still pending death.
    private static void InlineRandomPicks()
    {
        var failures = new List<string>();
        foreach (string id in new[] { "EDR_110", "CATA_580t" })
        {
            bool wispFirst = false;
            for (int seed = 1; seed <= 40; seed++)
                Parts(failures, id + " seed " + seed, () => {
                    var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
                    var wisp = Summon(p.Opponent, "CS2_231"); var big = Summon(p.Opponent, "CS2_182");
                    var a = Summon(p, id); var b = Summon(p, id);
                    a[GameTag.TAG_SCRIPT_DATA_NUM_1] = 5; b[GameTag.TAG_SCRIPT_DATA_NUM_1] = 5; // Soldier damage; Sporegnasher ignores it
                    a.ToBeDestroyed = true; b.ToBeDestroyed = true;
                    g.DeathProcessingAndAuraUpdate();
                    Verify(wisp.Damage <= (id == "EDR_110" ? 1 : 5), $"{id} seed {seed}: the dead Wisp was hit again (damage {wisp.Damage})");
                    if (wisp.Zone.Type == Zone.GRAVEYARD && big.Damage > 0) wispFirst = true;
                });
            if (!failures.Any(f => f.StartsWith(id))) Verify(wispFirst, id + ": the Wisp-first ordering was exercised");
        }
        // Nothing left to hit (the only enemy character is already dying): the Deathrattle does nothing instead of failing.
        foreach (string id in new[] { "EDR_110", "CATA_580t" })
            Parts(failures, id + " no candidate", () => {
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
                p.Opponent.Hero.ToBeDestroyed = true;
                var a = Summon(p, id); a[GameTag.TAG_SCRIPT_DATA_NUM_1] = 5; a.ToBeDestroyed = true;
                g.DeathProcessingAndAuraUpdate();
                Verify(a.Zone.Type == Zone.GRAVEYARD, id + " died with no candidate and no error");
            });
        // Spells: Aeon Rend (two random enemies), Cease to Exist (random enemy minion destroyed).
        for (int seed = 1; seed <= 25; seed++)
        {
            int sd = seed;
            Parts(failures, "Aeon Rend seed " + sd, () => {
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(sd);
                var live = Summon(p.Opponent, "CS2_182"); var dead = Wounded(p.Opponent); int damage = dead.Damage;
                Cast(g, p, "TIME_441");
                Verify(dead.Damage == damage && live.Damage == 4, $"Aeon Rend seed {sd}: both hits land on live targets (dead {dead.Damage}, live {live.Damage})");
            });
            Parts(failures, "Cease to Exist seed " + sd, () => {
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(sd);
                var live = Summon(p.Opponent, "CS2_182"); var dead = Wounded(p.Opponent);
                Cast(g, p, "TIME_433"); g.DeathProcessingAndAuraUpdate();
                Verify(live.Zone.Type == Zone.GRAVEYARD, $"Cease to Exist seed {sd}: the live minion is the pick");
            });
        }
        if (failures.Count != 0) throw new Exception(string.Join("; ", failures.Take(12)));
    }

    // #120: Deathwing option 1 (Topple, destroys the highest-Health enemy minion) and Flames of Infinity skip a mortally wounded minion.
    private static void DeathwingAndFlames()
    {
        var failures = new List<string>();
        Parts(failures, "Deathwing", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            p[HeraldRules.Progress] = 0;
            var live = Summon(p.Opponent, "CS2_231");
            var hero = Add(p, "CATA_190h"); PlayEntity(g, p, hero);
            Verify(p.Choice != null, "Deathwing offers its Cataclysms");
            var tallest = Summon(p.Opponent, "CS2_200"); tallest.Destroy();
            Verify(tallest.Health > live.Health && tallest.IsDead, "Topple pending candidate is tallest");
            B14H3LifecycleProof.Answer(g, "CATA_190t11");
            Verify(live.Zone.Type == Zone.GRAVEYARD, "Topple destroys the live minion, not the pending one");
        });
        Parts(failures, "Flames", () => {
            var g2 = QuestLifecycleProof.Fixture(); var p2 = g2.Player1;
            var flames = Add(p2.Opponent, "END_024");
            var victim = Summon(p2, "CS2_182");
            var tall = Summon(p2, "CS2_200"); tall.Destroy();
            Verify(tall.Health > victim.Health && tall.IsDead, "Flames pending candidate is tallest");
            Cards.FromId("END_024").Power.Trigger.SingleTask.Process(g2, p2.Opponent, flames, null, null);
            g2.DeathProcessingAndAuraUpdate();
            Verify(victim.Zone.Type == Zone.GRAVEYARD, "Flames of Infinity hits the live minion, not the pending one");
        });
        Verify(failures.Count == 0, string.Join("; ", failures));
    }


    // #121: Archmage Rafaam transforms every non-Rafaam minion except an Untouchable one.
    private static void MassTransformSkipsUntouchable()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var portal = Summon(p, "UNG_829t2"); var mine = Summon(p, "CS2_182"); var theirs = Summon(p.Opponent, "CS2_231");
        var theirPortal = Summon(p.Opponent, "UNG_829t2");
        Verify(portal.Untouchable && theirPortal.Untouchable, "the Nether Portal is Untouchable");
        Play(g, p, "TIME_005t9");
        Verify(mine.Card.Id == "TIME_005t9t" && theirs.Card.Id == "TIME_005t9t", "ordinary minions become sheep");
        Verify(portal.Card.Id == "UNG_829t2" && theirPortal.Card.Id == "UNG_829t2", "Untouchable portals are not transformed");
        Verify(p.BoardZone.Contains(portal) && p.Opponent.BoardZone.Contains(theirPortal), "portals stay on their boards");
    }

    // #138: a transformed minion shows only the new card's stats, whatever buffed or printed value it had.
    private static void TransformResetsStats()
    {
        string[] cards = { "CORE_EX1_383", "CS2_182", "CS2_200" };
        for (int kind = 0; kind < 4; kind++)
        {
            foreach (var id in cards)
            {
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
                var m = Summon(p.Opponent, id);
                switch (kind)
                {
                    case 1: Play(g, p, "CS2_092", m); break;           // Blessing of Kings enchantment
                    case 2: m.AttackDamage = 11; m.Health = 12; break; // set stats
                    case 3: m.Damage = 2; break;                       // damaged
                }
                Play(g, p, "TIME_005t9");
                Verify(m.Card.Id == "TIME_005t9t" && m.AttackDamage == 1 && m.Health == 1 && m.BaseHealth == 1 && m.Damage == 0,
                    $"{id} buff kind {kind} -> sheep {m.AttackDamage}/{m.Health} (damage {m.Damage})");
            }
        }
        // With aura sources on the board, in either board order, and with the player's own side too.
        var auraFailures = new List<string>();
        foreach (var aura in new[] { "CS2_222", "CS2_122", "EX1_162", "EX1_584", "CS2_117" })
            foreach (bool auraFirst in new[] { true, false })
                foreach (bool own in new[] { true, false })
                {
                    var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
                    var side = own ? p : p.Opponent;
                    Minion src = null, m;
                    if (auraFirst) src = Summon(side, aura);
                    m = Summon(side, "CORE_EX1_383");
                    if (!auraFirst) src = Summon(side, aura);
                    Play(g, p, "TIME_005t9");
                    // The aura source becomes a sheep as well, so no aura remains: the sheep is a plain 1/1.
                    if (!(m.Card.Id == "TIME_005t9t" && m.AttackDamage == 1 && m.Health == 1))
                        auraFailures.Add($"aura {aura} first={auraFirst} own={own}: sheep {m.AttackDamage}/{m.Health}");
                }
        Verify(auraFailures.Count == 0, string.Join("; ", auraFailures));
        // Same-type transform through the generic path as well (Polymorph on a printed 8/8).
        var g2 = QuestLifecycleProof.Fixture(); var p2 = g2.Player1;
        var tirion = Summon(p2.Opponent, "CORE_EX1_383");
        Verify(tirion.AttackDamage == 8 && tirion.Health == 8, "Tirion is printed 8/8");
        Play(g2, p2, "CS2_022", tirion);
        Verify(tirion.AttackDamage == 1 && tirion.Health == 1, $"Polymorph on Tirion gives {tirion.AttackDamage}/{tirion.Health}");
    }

    // #123: a minion damaged to 0 or less and lifted above 0 Health before the death check lives; damage is the only wound that rescue
    // covers, and the mark goes with the entity (clone) and out of the death check.
    private static void HealBeforeDeathCheck()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var m = Summon(p.Opponent, "CS2_182"); // 4/5
        m.Damage = 6; Verify(m.ToBeDestroyed && m.IsDead && m.Health == -1, "damage wound is pending");
        m.TakeHeal(p.Hero, 3);
        Verify(!m.IsDead && !m.ToBeDestroyed, "healed above 0: no longer dead");
        g.DeathProcessingAndAuraUpdate();
        Verify(m.Zone.Type == Zone.PLAY && m.Health == 2 && !m.ToBeDestroyed && !m.NativeTags.ContainsKey((GameTag)31420), "healed above 0 before the death check: survives, mark cleared");
        m.Damage = 5; g.DeathProcessingAndAuraUpdate();
        Verify(m.Zone.Type == Zone.GRAVEYARD, "a rescued minion wounded again still dies");

        var d = Summon(p.Opponent, "CS2_182");
        d.Damage = 6; d.TakeHeal(p.Hero, 1);
        Verify(d.IsDead, "still at 0 or less after the heal: dead");
        g.DeathProcessingAndAuraUpdate();
        Verify(d.Zone.Type == Zone.GRAVEYARD, "still at 0 or less after the heal: dies");

        var e = Summon(p.Opponent, "CS2_182");
        e.Damage = 1; e.Destroy(); e.TakeHeal(p.Hero, 1);
        g.DeathProcessingAndAuraUpdate();
        Verify(e.Zone.Type == Zone.GRAVEYARD, "explicit Destroy is not undone by healing");

        var f = Summon(p.Opponent, "CS2_182");
        f.Damage = 6; f.Destroy(); f.TakeHeal(p.Hero, 3);
        Verify(f.IsDead, "damage wound plus explicit Destroy: dead");
        g.DeathProcessingAndAuraUpdate();
        Verify(f.Zone.Type == Zone.GRAVEYARD, "damage wound plus explicit Destroy: dies");

        // Destroy first, damage after: still an explicit destroy.
        var h = Summon(p.Opponent, "CS2_182");
        h.Destroy(); h.Damage = 6; h.TakeHeal(p.Hero, 3);
        g.DeathProcessingAndAuraUpdate();
        Verify(h.Zone.Type == Zone.GRAVEYARD, "Destroy then damage then heal: dies");
    }

    // A rescued fatal hit must not own a later death, even when the next hit or destroy precedes the death check.
    private static void RescuedDeathAttribution()
    {
        var failures = new List<string>();
        foreach (bool settle in new[] { true, false })
            foreach (string finish in new[] { "damage", "DestroyTask", "Destroy" })
                Parts(failures, $"settle={settle} finish={finish}", () => {
                    var g = HistoryFactsProof.Fixture(); var p = g.Player1;
                    var first = Add(p, "CS2_029"); var second = Add(p.Opponent, "CS2_029");
                    var victim = Summon(p.Opponent, "CS2_182");
                    victim.TakeDamage(first, 6); victim.TakeHeal(p.Hero, 3);
                    if (settle) { g.DeathProcessingAndAuraUpdate(); End(g); }
                    if (finish == "damage") victim.TakeDamage(second, victim.Health);
                    else if (finish == "DestroyTask")
                        using (g.Resolution.Enter(SabberStoneCore.Model.Events.ResolutionKind.TaskResolution, second.Id))
                            new SabberStoneCore.Tasks.SimpleTasks.DestroyTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, p.Opponent, second, victim);
                    else
                        using (g.Resolution.Enter(SabberStoneCore.Model.Events.ResolutionKind.EffectResolution, second.Id)) victim.Destroy();
                    g.DeathProcessingAndAuraUpdate();
                    var death = g.HistoryFacts.Query.Deaths().Single(f => f.EntityId == victim.Id);
                    long fatalSequence = finish == "damage"
                        ? g.HistoryFacts.Query.Damage().Single(f => f.VictimId == victim.Id && f.SourceId == second.Id).Sequence : 0;
                    Verify(death.SourceId == second.Id && death.SourceControllerId == p.Opponent.Id
                        && death.Cause == (finish == "damage" ? HistoryDeathCause.SpellOrEffect : HistoryDeathCause.Destroy)
                        && death.FatalDamageSequence == fatalSequence,
                        $"rescued death credits new source {second.Id}, got {death.SourceId} (settle={settle} finish={finish})");
                });
        Verify(failures.Count == 0, string.Join("; ", failures));
    }

    private static void RescuedCombatKillTracking()
    {
        var failures = new List<string>();
        foreach (bool weapon in new[] { true, false })
            foreach (bool settle in new[] { true, false })
                foreach (string finish in new[] { "damage", "DestroyTask", "Destroy" })
                    Parts(failures, $"weapon={weapon} settle={settle} finish={finish}", () => {
                        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
                        IPlayable tracker; ICharacter attacker;
                        if (weapon) { Play(g, p, "CORE_RLK_086"); tracker = p.Hero.Weapon; attacker = p.Hero; }
                        else { var ursoc = Summon(p, "EDR_819"); ursoc.IsAttacking = true; tracker = ursoc; attacker = ursoc; }
                        var victim = Summon(p.Opponent, "CS2_182");
                        victim[DeathContextRules.CaptureDeath] = 1;
                        victim.TakeDamage(attacker, 6, combat: true);
                        Verify(victim[DeathContextRules.PendingTracker] == tracker.Id && victim[DeathContextRules.FatalSource] == attacker.Id,
                            "fatal combat hit records tracker and source");
                        victim.TakeHeal(p.Hero, 3);
                        if (settle)
                        {
                            g.DeathProcessingAndAuraUpdate();
                            Verify(new[] { DeathContextRules.PendingTracker, DeathContextRules.FatalOrdinal, DeathContextRules.FatalSource }
                                .All(tag => !victim.NativeTags.ContainsKey(tag)), "rescue clears all fatal tracker tags");
                        }
                        if (finish == "damage") victim.TakeDamage(p.Opponent.Hero, victim.Health);
                        else if (finish == "DestroyTask")
                            using (g.Resolution.Enter(SabberStoneCore.Model.Events.ResolutionKind.TaskResolution, p.Opponent.Hero.Id))
                                new SabberStoneCore.Tasks.SimpleTasks.DestroyTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, victim);
                        else
                            using (g.Resolution.Enter(SabberStoneCore.Model.Events.ResolutionKind.EffectResolution, p.Opponent.Hero.Id)) victim.Destroy();
                        g.DeathProcessingAndAuraUpdate();
                        Verify(PayloadRules.Entries(tracker, DeathContextRules.KillKey).Length == 0,
                            "rescued victim must not enter combat kill payload");
                        Verify(victim[DeathContextRules.FatalSource] == p.Opponent.Hero.Id, "later fatal source replaces rescued combat source");
                        if (weapon) p.Hero.Weapon.Durability = 0;
                        else ((Minion)tracker).Destroy();
                        g.DeathProcessingAndAuraUpdate();
                        Verify(!p.BoardZone.Any(m => m.Card.Id == victim.Card.Id), "tracker Deathrattle does not summon rescued victim");
                    });
        Verify(failures.Count == 0, string.Join("; ", failures));
    }

    // #123: other ways to lift Health above 0 before the death check.
    private static void BuffAndAuraRescue()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var m = Summon(p.Opponent, "CS2_182"); m.Damage = 6;                 // 4/5, Health -1
        new SabberStoneCore.Tasks.SimpleTasks.AddEnchantmentTask("CS2_092e", SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, p, p.Hero, m); // Blessing of Kings: +4/+4 -> Health 3
        Verify(!m.IsDead && m.Health == 3, $"Health buff lifts it above 0 (Health {m.Health})");
        g.DeathProcessingAndAuraUpdate();
        Verify(m.Zone.Type == Zone.PLAY, "Health buff before the death check: survives");

        var n = Summon(p.Opponent, "CS2_182"); n.Damage = 5;                 // Health 0, pending
        Verify(n.IsDead && n.ToBeDestroyed, "wounded to 0");
        var aura = Summon(p.Opponent, "CS2_222");                            // Stormwind Champion: others +1/+1
        g.DeathProcessingAndAuraUpdate();
        Verify(n.Zone.Type == Zone.PLAY && n.Health == 1, $"aura lifts Health above 0 before the death check: survives (zone {n.Zone.Type}, Health {n.Health})");
        Verify(aura.Zone.Type == Zone.PLAY, "aura source untouched");

        var o = Summon(p.Opponent, "CS2_182"); o.Damage = 6;
        g.DeathProcessingAndAuraUpdate();
        Verify(o.Zone.Type == Zone.GRAVEYARD || o.Health > 0 && o.Zone.Type == Zone.PLAY, "state is consistent");
        // The aura makes Health 0 here, so the minion with 1 more damage than the aura covers dies.
        Verify(o.Zone.Type == Zone.GRAVEYARD, $"aura does not cover the whole wound: dies (Health {o.Health})");

        // A mark survives CloneExact: the copy rescues and kills like the original.
        var g2 = QuestLifecycleProof.Fixture(); var p2 = g2.Player1;
        var w = Summon(p2.Opponent, "CS2_182"); w.Damage = 6;
        var clone = g2.CloneExact();
        var cw = (Minion)clone.IdEntityDic[w.Id];
        Verify(cw.NativeTags.ContainsKey((GameTag)31420) && cw.IsDead, "the clone keeps the damage mark");
        cw.TakeHeal(clone.Player1.Hero, 3); clone.DeathProcessingAndAuraUpdate();
        Verify(cw.Zone.Type == Zone.PLAY, "clone: healed before the check, survives");
        g2.DeathProcessingAndAuraUpdate();
        Verify(w.Zone.Type == Zone.GRAVEYARD, "original: untouched by the clone's heal, dies");
    }

    // #123 through the real damage path (TakeDamage), not by setting Damage: heal rescue, and a Poisonous hit makes the death final.
    private static void RealDamageAndPoison()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var m = Summon(p.Opponent, "CS2_182");
        m.TakeDamage(p.Hero, 6);
        Verify(m.IsDead && m.NativeTags.ContainsKey((GameTag)31420), "TakeDamage to lethal marks the wound");
        m.TakeHeal(p.Hero, 3); g.DeathProcessingAndAuraUpdate();
        Verify(m.Zone.Type == Zone.PLAY && m.Health == 2, "healed before the death check: survives");

        var n = Summon(p.Opponent, "CS2_182"); var source = Summon(p, "CS2_182");
        new SabberStoneCore.Tasks.SimpleTasks.AddEnchantmentTask("CORE_EDR_002e", SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, p, source, source);
        Verify(source.Poisonous, "Poisonous source");
        n.TakeDamage(p.Hero, 6);
        Verify(n.NativeTags.ContainsKey((GameTag)31420), "ordinary damage marks it");
        n.TakeDamage(source, 1);
        Verify(!n.NativeTags.ContainsKey((GameTag)31420) && n.IsDead, "a Poisonous hit on a marked minion makes the death final");
        n.TakeHeal(p.Hero, 10); g.DeathProcessingAndAuraUpdate();
        Verify(n.Zone.Type == Zone.GRAVEYARD, "healing cannot undo a Poisonous kill");
    }

    // #123 edges: Silence keeps a damage mark; a copy never carries it; a same-type transform keeps main's result (the minion dies).
    private static void SilenceCopyTransform()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var m = Summon(p.Opponent, "CS2_182"); Summon(p.Opponent, "CS2_222"); // Stormwind Champion aura +1/+1
        m.TakeDamage(p.Hero, 6);                                              // Health 6 with the aura: wounded to 0
        Verify(m.IsDead && m.NativeTags.ContainsKey((GameTag)31420), "wounded under an aura");
        m.Silence();
        Verify(m.NativeTags.ContainsKey((GameTag)31420), "Silence keeps the damage-only mark");
        m.TakeHeal(p.Hero, 3); g.DeathProcessingAndAuraUpdate();
        Verify(m.Zone.Type == Zone.PLAY, "silenced minion healed before the death check still survives");

        var g2 = QuestLifecycleProof.Fixture(); var p2 = g2.Player1;
        var w = Summon(p2.Opponent, "CS2_182"); w.TakeDamage(p2.Hero, 6);
        var copy = (Minion)SabberStoneCore.Actions.Generic.Copy(p2.Opponent, p2.Hero, w, Zone.PLAY);
        Verify(copy != null && !copy.NativeTags.ContainsKey((GameTag)31420), "a copy carries no wound mark");
        var kept = (Minion)SabberStoneCore.Actions.Generic.Copy(p2.Opponent, p2.Hero, w, Zone.PLAY, preserveBoardMinionState: true);
        Verify(kept != null && !kept.NativeTags.ContainsKey((GameTag)31420), $"a state-preserving copy carries no wound mark (damage {kept?.Damage}, dead {kept?.IsDead})");

        var g3 = QuestLifecycleProof.Fixture(); var p3 = g3.Player1;
        var t = Summon(p3.Opponent, "CS2_182"); t.TakeDamage(p3.Hero, 6);
        new SabberStoneCore.EffectSystem.EffectContext(g3, p3.Id, p3.Hero.Id).Transform(t.Id, "CS2_tk1");
        g3.DeathProcessingAndAuraUpdate();
        Verify(t.Zone.Type == Zone.GRAVEYARD, "same-type transform of a mortally wounded minion still dies, as on main (retail rule unsettled)");
    }

    // #123 snapshot at a decision boundary (Deathwing prompt) with a marked minion: the mark and the rescue survive save/load.
    private static void SnapshotWithMark()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var hero = Add(p, "CATA_190h"); PlayEntity(g, p, hero);
        Verify(p.Choice != null, "decision open");
        var m = Summon(p.Opponent, "CS2_182"); m.TakeDamage(p.Hero, 6);
        Verify(m.NativeTags.ContainsKey((GameTag)31420), "marked while the prompt is open");
        var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        var lm = (Minion)loaded.IdEntityDic[m.Id];
        Verify(lm.NativeTags.ContainsKey((GameTag)31420) && lm.IsDead, "loaded game keeps the mark");
        lm.TakeHeal(loaded.Player1.Hero, 3);
        B14H3LifecycleProof.Answer(loaded, "CATA_190t10");
        Verify(lm.Zone.Type == Zone.PLAY && lm.Health == 2, "loaded game: rescued by the heal");
        B14H3LifecycleProof.Answer(g, "CATA_190t10");
        Verify(m.Zone.Type == Zone.GRAVEYARD, "original game without a heal: the minion dies");
    }

    // #123 as reported: death creation removes every mortally wounded minion before any Deathrattle; a later heal cannot revive.
    private static void HealAfterDeathCheck()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        var m = Summon(p.Opponent, "CS2_182"); m.Damage = 6;
        g.DeathProcessingAndAuraUpdate();
        Verify(m.Zone.Type == Zone.GRAVEYARD, "mortally wounded minion dies at the death check");
        m.TakeHeal(p.Hero, 10);
        g.DeathProcessingAndAuraUpdate();
        Verify(m.Zone.Type == Zone.GRAVEYARD && !p.Opponent.BoardZone.Contains(m), "a heal after the death check cannot revive it");
    }

    // Runs each part of a sweep on its own so one failing card does not hide the others.
    private static void Parts(List<string> failures, string name, Action part)
    {
        try { part(); } catch (Exception e) { failures.Add(name + ": " + e.Message); }
    }

    private static Minion Wounded(Controller side)
    {
        var m = Summon(side, "CS2_182"); m.Damage = m.BaseHealth + 1;
        Verify(m.IsDead && m.ToBeDestroyed, "wounded fixture is pending death");
        return m;
    }

    private static void Cast(Game g, Controller p, string id, IPlayable target = null)
    {
        var source = Add(p, id);
        var card = Cards.FromId(id);
        card.Power.PowerTask.Process(g, p, source, target, null);
    }

    // #121 sweep: every effect that takes minions from the raw board leaves an Untouchable minion (Nether Portal) alone.
    private static void UntouchableSweep()
    {
        var failures = new List<string>();
        Minion Portal(Controller side) { var m = Summon(side, "UNG_829t2"); Verify(m.Untouchable, "portal is Untouchable"); return m; }
        void Stays(Minion portal, string what) => Verify(portal.Zone.Type == Zone.PLAY && portal.Card.Id == "UNG_829t2" && !portal.IsDead && portal.Damage == 0, what + ": the portal is untouched");

        Parts(failures, "Medivh the Hallowed", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var mine = Portal(p); var theirs = Portal(p.Opponent); var a = Summon(p, "CS2_182"); var b = Summon(p.Opponent, "CS2_182");
            Play(g, p, "TIME_890");
            Verify(a.Zone.Type == Zone.GRAVEYARD && b.Zone.Type == Zone.GRAVEYARD, "Medivh destroys ordinary minions");
            Stays(mine, "Medivh own"); Stays(theirs, "Medivh enemy");
        });
        Parts(failures, "Disciple of Demise", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var portal = Portal(p.Opponent);
            Cast(g, p, "TIME_EVENT_301");
            Stays(portal, "Disciple of Demise");
        });
        Parts(failures, "Frame Job", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var portal = Portal(p.Opponent); var a = Summon(p.Opponent, "CS2_182"); var b = Summon(p.Opponent, "CS2_182");
            Play(g, p, "CAP_403");
            Verify(a.Zone.Type == Zone.GRAVEYARD && b.Zone.Type == Zone.GRAVEYARD, "Frame Job destroys the two ordinary minions");
            Stays(portal, "Frame Job");
        });
        Parts(failures, "Tome Translator missiles", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var portal = Portal(p.Opponent);
            Cast(g, p, "BE_327t2");
            Stays(portal, "Tome Translator missiles");
        });
        for (int seed = 1; seed <= 20; seed++)
        Parts(failures, "Titanographer Osk seed " + seed, () => { // ability 22: 4 damage to a random enemy
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
            var portal = Portal(p.Opponent);
            Cast(g, p, "TLC_452t22");
            Stays(portal, "Osk random damage seed " + seed);
            Verify(p.Opponent.Hero.Damage == 4, "Osk random damage lands on the hero");
        });
        Parts(failures, "Desperate Bribe", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var portal = Portal(p);
            Play(g, p, "JAIL_EVENT_102");
            Verify(portal.Card.Id == "UNG_829t2", "Desperate Bribe leaves the portal as it is");
        });
        if (failures.Count != 0) throw new Exception(string.Join("; ", failures));
    }

    // #120 sweep: a random harmful effect never picks a minion that is already mortally wounded, so the live ones take it all.
    private static void MortallyWoundedSweep()
    {
        var failures = new List<string>();
        for (int seed = 1; seed <= 25; seed++)
        {
            Parts(failures, "Disciple seed " + seed, () => { // Disciple of Demise: one destroy
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
                var b = Summon(p.Opponent, "CS2_182"); var a = Wounded(p.Opponent);
                Cast(g, p, "TIME_EVENT_301");
                Verify(b.Zone.Type == Zone.GRAVEYARD, $"Disciple seed {seed}: the live minion is the pick");
            });
            Parts(failures, "Frame Job seed " + seed, () => { // Frame Job: two destroys
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
                var b = Summon(p.Opponent, "CS2_182"); var c = Summon(p.Opponent, "CS2_182"); var a = Wounded(p.Opponent);
                Cast(g, p, "CAP_403");
                Verify(b.Zone.Type == Zone.GRAVEYARD && c.Zone.Type == Zone.GRAVEYARD, $"Frame Job seed {seed}: both live minions die");
            });
            Parts(failures, "Tome Translator seed " + seed, () => { // Tome Translator missiles: the wounded minion takes none
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
                var b = Summon(p.Opponent, "CS2_182"); var a = Wounded(p.Opponent); var damage = a.Damage;
                Cast(g, p, "BE_327t2");
                Verify(a.Damage == damage, $"Tome Translator seed {seed}: the wounded minion was not hit again");
            });
            Parts(failures, "Osk and Follow the Fuse seed " + seed, () => { // Osk ability 22 and Follow the Fuse: the wounded enemy minion is never the pick, the hero or the live minion is
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1; g.SetRandomSeed(seed);
                var b = Summon(p.Opponent, "CS2_182"); var a = Wounded(p.Opponent); var damage = a.Damage;
                Cast(g, p, "TLC_452t22"); Cast(g, p, "CAP_101");
                Verify(a.Damage == damage, $"Osk / Follow the Fuse seed {seed}: the wounded minion was not hit again");
                Verify(b.Damage + p.Opponent.Hero.Damage == 6, $"Osk / Follow the Fuse seed {seed}: both hits land on live targets");
            });
        }
        if (failures.Count != 0) throw new Exception(string.Join("; ", failures));
    }

    // #139: Slime 'em! text has no condition; both players get the spell even with no minions anywhere.
    private static void SlimeEmEmptyBoards()
    {
        var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
        Verify(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0, "empty boards");
        Play(g, p, "CAP_805");
        Verify(p.HandZone.Count(c => c.Card.Id == "CAP_805t") == 1 && p.Opponent.HandZone.Count(c => c.Card.Id == "CAP_805t") == 1,
            "both players get an Ectoplasm with no minions (text has no condition)");
    }
}
