using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Kettle;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B17TransformAttackProof
{
    [GroupProof("b17-transform-attack")]
    internal static void Run()
    {
        Unpinned_PodlingDestination();
        Unpinned_TributeStateAndLegality();
        Unpinned_TributeSoulrestTriggers();
        ArbitrarySourcesAndForcedAttacks();
        Unpinned_StaffSnapshotAndCapacity();
        Unpinned_TinyPalReload();
        Unpinned_FrostSeededOrder();
        Unpinned_TributeLayers();
        Unpinned_AmmoPoolsAndCapacity();
        RewindDecisionPlays();
        Console.WriteLine("PASS b17 transform/attack facility, lifecycle, RNG, clone/snapshot");
    }

    private static void Unpinned_PodlingDestination()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool opponent in new[] { false, true })
        {
            var g = CthunRulesProof.Fixture().CloneExact(history: history);
            var p = opponent ? g.Player2 : g.Player1;
            foreach (var intended in new[] { "hexfrog", "CS2_182", "CS2_200", "EX1_561", "EX1_562", "EX1_620", "EDR_529" })
            {
                var podling = Summon(p, "EDR_529");
                var destination = Cards.FromId(intended);
                int tier = destination.Cost >= 10 ? destination.Cost : Math.Min(10, destination.Cost + 2);
                var candidates = OfflineCardPools.Candidates(g, p.HeroClass, OfflinePoolFilter.Random(type: CardType.MINION).WithCost(tier), 115295).ToArray();
                Console.WriteLine("Podling bucket " + tier + "=" + candidates.Length);
                var rng = g.CloneExact().Random;
                var expected = candidates.Length == 0 ? "EDR_529" : candidates[rng.Next(candidates.Length)].Id;
                int start = g.PowerHistory?.Last.Count ?? 0;
                Generic.ChangeEntityBlock(p, podling, destination, true);
                g.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(podling.Card.Id == expected && g.Random.CaptureState().Equals(rng.CaptureState()), "Podling exact seeded destination and one RNG pick");
                if (history && candidates.Length != 0)
                    Core2026Proof.Check(g.PowerHistory.Last.Skip(start).OfType<PowerHistoryChangeEntity>().Count() == 1, "only final transform is emitted");
                p.SetasideZone.Add(p.BoardZone.Remove(podling));
            }
            var silent = Summon(p, "EDR_529"); silent.Silence(); var random = g.Random.CaptureState();
            Generic.ChangeEntityBlock(p, silent, Cards.FromId("hexfrog"), true);
            Core2026Proof.Check(silent.Card.Id == "hexfrog" && random.Equals(g.Random.CaptureState()), "Silence disables interception");
            var held = Add(p, "EDR_529"); Generic.ChangeEntityBlock(p, held, Cards.FromId("CS2_182"), true);
            Core2026Proof.Check(held.Card.Id == "CS2_182", "offboard Podling has no interception");
            var old = Summon(p, "EDR_529"); Generic.TransformBlock(p, Cards.FromId("hexfrog"), old);
            Core2026Proof.Check(old.Zone == p.SetasideZone && p.BoardZone.Last().Card.Cost == 2, "legacy replacement intercepts");
            var copy = Summon(p, "EDR_529"); var target = Summon(p.Opponent, "CS2_182"); target.Silence();
            new Enchant(Effects.Attack_N(6)).ActivateTo(target);
            new TransformCopyTask().Process(g, p, copy, target);
            Core2026Proof.Check(copy.Card.Cost == 6 && !copy.IsSilenced && copy.AttackDamage == copy.Card.ATK, "copy transform intercepts before Silence/buffs transfer");
            foreach (var clone in new[] { g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) })
                Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "transform state roundtrips");
        }
    }

    private static void Unpinned_TributeStateAndLegality()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        var first = Summon(p, "CS2_231"); var spell = Add(p, "DINO_414");
        var hidden = Summon(p.Opponent, "EX1_028");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "enemy Stealth cannot provide second target");
        int mana = p.RemainingMana;
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, spell, first)) && p.RemainingMana == mana, "incomplete pair rejected before payment");
        var second = Summon(p.Opponent, "CS2_182");
        new Enchant(Effects.Attack_N(3), Effects.Health_N(3)).ActivateTo(second); second.Damage = 2;
        first.NumAttacksThisTurn = 1; second.NumAttacksThisTurn = 2;
        while (p.BoardZone.Count < 7) Summon(p, "CS2_231");
        PlayEntity(g, p, spell, first);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { second.Id }.Concat(p.BoardZone.Where(m => m.Id != first.Id).Select(m => m.Id)).OrderBy(id => id)), "fresh second offer excludes first and illegal enemy");
        CthunRulesProof.PickTarget(g, second.Id);
        Core2026Proof.Check(first.Card.Id == second.Card.Id && first.AttackDamage == 7 && first.Health == 6 && first.Damage == 2 && first.NumAttacksThisTurn == 1 && p.BoardZone.Count == 7,
            $"full-board copy keeps non-aura stats, damage, own attack count: {first.Card.Id} {first.AttackDamage}/{first.Health} damage={first.Damage} attacks={first.NumAttacksThisTurn} board={p.BoardZone.Count}");
        first.Silence();
        Core2026Proof.Check(first.AttackDamage == 4 && second.AttackDamage == 7, "copy buffs independently removable");
        var copySpell = Add(p, "DINO_414"); second.Silence();
        PlayEntity(g, p, copySpell, first); CthunRulesProof.PickTarget(g, second.Id);
        Core2026Proof.Check(first.IsSilenced, "Tribute retains Silence without enabling powers");
        new ReturnHandTask(EntityType.TARGET).Process(g, p, first, first);
        Core2026Proof.Check(first.Card.Id == "CS2_182" && first.Damage == 0, "bounce resets copied damage");
    }

    private static void Unpinned_TributeSoulrestTriggers()
    {
        foreach (bool enemyCopy in new[] { false, true })
        foreach (bool enemyOriginal in new[] { false, true })
        foreach (string cleanup in new[] { "none", "silence", "bounce" })
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1;
            var first = Summon(enemyCopy ? p.Opponent : p, "CS2_231");
            var original = Summon(enemyOriginal ? p.Opponent : p, "CS2_182");
            for (int i = 0; i < 2; i++) Generic.AddEnchantmentBlock(g, Cards.FromId("DINO_417e"), original, original);
            Play(g, p, "DINO_414", first); CthunRulesProof.PickTarget(g, original.Id);
            Core2026Proof.Check(first.AttackDamage == 6 && first.IsRush && first.AppliedEnchantments.Count(e => e.Card.Id == "DINO_417e") == 2,
                "Tribute copies stacked Soulrest stats once, with independent marks");
            PoolPilotProof.Resume(g, next => {
                var copy = (Minion)next.IdEntityDic[first.Id];
                var target = next.IdEntityDic[original.Id];
                if (cleanup == "silence") copy.Silence();
                if (cleanup == "bounce") new ReturnHandTask(EntityType.TARGET).Process(next, copy.Controller, copy, copy);
                End(next);
                Core2026Proof.Check(target.Zone.Type == (enemyOriginal ? Zone.PLAY : Zone.GRAVEYARD), "original Soulrest follows its own controller's turn end");
                Core2026Proof.Check(copy.Zone.Type == (cleanup == "bounce" ? Zone.HAND : cleanup == "silence" || enemyCopy ? Zone.PLAY : Zone.GRAVEYARD),
                    "Tribute Soulrest follows copying controller; Silence/bounce cancels only copied death");
                End(next);
                Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD && copy.Zone.Type == (cleanup == "bounce" ? Zone.HAND : cleanup == "silence" ? Zone.PLAY : Zone.GRAVEYARD),
                    "opponent copy dies on its own turn end; canceled copy survives both turns");
                Core2026Proof.Check(next.Player1.GraveyardZone.OfType<Minion>().Count() + next.Player2.GraveyardZone.OfType<Minion>().Count() == (cleanup == "none" ? 2 : 1),
                    "stacked Soulrest triggers kill each bearer once");
            });
        }
    }

    private static readonly SuspendableTask Forced = StateTaskList.Suspendable("proof/b17/forced/v1",
        new AttackTask(EntityType.HERO, EntityType.OP_MINIONS), new AttackTask(EntityType.HERO, EntityType.OP_MINIONS));

    private static void ArbitrarySourcesAndForcedAttacks()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        var a = Summon(p, "CS2_231"); var b = Summon(p, "CS2_182");
        var queue = Cards.FromId("JAIL_875").Power.Trigger.SingleTask;
        queue.Process(g, p, a, null); queue.Process(g, p, b, null); queue.Process(g, p, a, null);
        Kill(g, a); AfterAttackRules.Flush(g);
        for (int i = 0; i < 3; i++)
        {
            Core2026Proof.Check(p.Choice != null, "each arbitrary-source activation gets its own saved decision");
            CthunRulesProof.PickTarget(g, p.Choice.Choices[0]);
        }
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 3, "dead source and duplicate activations finish once");
        g = CthunRulesProof.Fixture(); p = g.Player1;
        Play(g, p, "JAIL_875"); p.Hero.Weapon.Durability = 2;
        var victim = Summon(p.Opponent, "CS2_200"); victim.AttackDamage = 0; victim[GameTag.HEALTH] = 30;
        Forced.Process(g, p, p.Hero, null, new TaskStack());
        Core2026Proof.Check(victim.Damage == 1 && p.Choice != null, "first forced attack waits for decision before second combat");
        CthunRulesProof.PickTarget(g, p.Choice.Choices[0]);
        Core2026Proof.Check(victim.Damage == 2 && p.Hero.Weapon == null && p.Choice != null, "final forced swing still creates Staff decision");
        CthunRulesProof.PickTarget(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.Hero.NumAttacksThisTurn == 0, "two forced attacks preserve normal attack allowance");
    }

    private static void Unpinned_StaffSnapshotAndCapacity()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        Play(g, p, "JAIL_875"); new Enchant(Effects.Attack_N(4)).ActivateTo(p.Hero);
        Console.WriteLine("Staff Druid bucket=" + OfflineCardPools.Candidates(g, p.HeroClass,
            new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID), 126243).Count());
        Core2026Proof.Check(p.Hero.AttackDamage == 5, $"Staff setup Attack={p.Hero.AttackDamage}");
        p.Hero.Weapon.Durability = 1;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Staff final attack");
        Core2026Proof.Check(p.Choice?.Choices.Count == 3 && p.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id).Distinct().Count() == 3, "Staff offers three distinct Druid cards");
        int pick = p.Choice.Choices[0], printed = g.IdEntityDic[pick].Card.Cost;
        Console.WriteLine("Staff receipts=" + string.Join(",", g.IdEntityDic.Values.Where(PayloadRules.IsStored).Select(e => e[PayloadRules.Value])));
        Core2026Proof.Check(p.Hero.Weapon == null, "weapon breaks before saved choice");
        CthunRulesProof.PickTarget(g, pick);
        Core2026Proof.Check(g.IdEntityDic[pick].Cost == Math.Max(0, printed - 5),
            $"discount uses trigger-entry Attack including final weapon: printed={printed} actual={g.IdEntityDic[pick].Cost} hero={p.Hero.AttackDamage} script={g.IdEntityDic[pick].AppliedEnchantments?.LastOrDefault()?[GameTag.TAG_SCRIPT_DATA_NUM_1]}");
        Play(g, p, "JAIL_875"); p.Hero.IsExhausted = false; p.Hero.NumAttacksThisTurn = 0;
        PoolPilotProof.FillHand(p);
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "full hand still opens Discover");
        CthunRulesProof.PickTarget(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Choice == null, "full hand discards chosen card without a stalled choice");
        p.Hero.IsExhausted = false; p.Hero.NumAttacksThisTurn = 0;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Staff decision before concession");
        var rng = g.Random.CaptureState();
        Core2026Proof.Check(g.Process(ConcedeTask.Any(p)) && p.Choice == null && !g.Resolution.HasSuspendedEffect && rng.Equals(g.Random.CaptureState()),
            "concession cancels saved after-attack work without another roll");
    }

    private static void Unpinned_TinyPalReload()
    {
        var equipped = CthunRulesProof.Fixture(); var owner = equipped.Player1;
        new WeaponTask("JAIL_458").Process(equipped, owner, owner.Hero, null);
        Core2026Proof.Check(owner.Choice == null && owner.Hero.Weapon.Card.Id == "JAIL_458", "random equip skips initial ammunition Battlecry");
        Core2026Proof.Check(equipped.Process(HeroAttackTask.Any(owner, owner.Opponent.Hero)) && owner.Choice?.Choices.Count == 4
            && owner.HandZone.Count == 0 && owner.BoardZone.Count == 0 && owner.Opponent.Hero.Damage == 2,
            "unloaded weapon has no bonus and its first attack offers all ammunition");
        CthunRulesProof.Pick(equipped, "JAIL_458t1");
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        Play(g, p, "JAIL_458"); CthunRulesProof.Pick(g, "JAIL_458t1");
        var weapon = p.Hero.Weapon; new Enchant(GameTag.ATK, EffectOperator.ADD, 1).ActivateTo(weapon);
        var a = Summon(p.Opponent, "CS2_200"); var b = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Tiny Pal first swing");
        Core2026Proof.Check(a.IsFrozen && b.IsFrozen && !p.Opponent.Hero.IsFrozen, "Frost freezes two others, excluding defender");
        Core2026Proof.Check(p.Choice.Choices.All(i => g.IdEntityDic[i].Card.Id != "JAIL_458t1"), "reload excludes only current ammo");
        CthunRulesProof.Pick(g, "JAIL_458t2");
        Core2026Proof.Check(p.Hero.Weapon == weapon && weapon.Durability == 2 && weapon.AttackDamage == 3, "reload preserves entity/durability/weapon buffs");
        p.Hero.IsExhausted = false; p.Hero.NumAttacksThisTurn = 0;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Tiny Pal second swing");
        Core2026Proof.Check(a.Damage == 1 && b.Damage == 1, "old Fire ammo resolves before reload");
        CthunRulesProof.Pick(g, "JAIL_458t1");
        p.Hero.IsExhausted = false; p.Hero.NumAttacksThisTurn = 0; a.IsFrozen = false; b.IsFrozen = false;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Tiny Pal final swing");
        Core2026Proof.Check(p.Hero.Weapon == null && p.Choice == null && a.IsFrozen && b.IsFrozen, "final ammo resolves without reload");
        g = CthunRulesProof.Fixture(); p = g.Player1; Play(g, p, "JAIL_458"); CthunRulesProof.Pick(g, "JAIL_458t1");
        p.Opponent.Hero.Damage = 28;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && g.State == State.COMPLETE && p.Choice == null,
            "lethal attack offers no reload");
    }

    private static void Unpinned_FrostSeededOrder()
    {
        foreach (bool reverse in new[] { false, true })
        foreach (var sample in new[] { (Seed: 3L, First: 3), (Seed: 11L, First: 2), (Seed: 29L, First: 1) })
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1;
            Play(g, p, "JAIL_458"); CthunRulesProof.Pick(g, "JAIL_458t1");
            p.Hero.Weapon.Durability = 1;
            var cards = new[] { "CS2_182", "CS2_200", "CS2_231", "CS2_186" };
            var enemies = (reverse ? cards.Reverse() : cards).Select(id => Summon(p.Opponent, id)).ToArray();
            // Fixed first two draws select [3,0], [2,0], [1,0] from four ordered enemies.
            var expected = new[] { enemies[sample.First].Id, enemies[0].Id };
            g.SetRandomSeed(sample.Seed);
            var random = g.CloneExact().Random; random.NextLong(); random.NextLong();
            PoolPilotProof.Resume(g, next => {
                Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1, next.Player2.Hero)), "seeded Frost final attack accepted");
                var freezes = next.PowerHistory.Last.OfType<PowerHistoryTagChange>()
                    .Where(t => t.Tag == GameTag.FROZEN && t.Value == 1).Select(t => t.EntityId);
                Core2026Proof.Check(freezes.SequenceEqual(expected), "Frost seeded identities and freeze order follow board order without replacement");
                Core2026Proof.Check(enemies.All(e => ((Minion)next.IdEntityDic[e.Id]).IsFrozen == expected.Contains(e.Id))
                    && !next.Player2.Hero.IsFrozen && !next.Player1.Hero.IsFrozen, "Frost freezes exactly two distinct other enemies");
                Core2026Proof.Check(next.Random.CaptureState().Equals(random.CaptureState()), "Frost consumes exactly two RNG draws, including after clone/load");
                Core2026Proof.Check(next.Player1.Choice == null && next.Player1.Hero.Weapon == null, "Frost final swing completes without a decision");
            });
        }
    }

    private static void Unpinned_TributeLayers()
    {
        var g = CthunRulesProof.Fixture(); var p = g.Player1;
        Summon(p, "CS2_122"); var first = Summon(p, "CS2_231");
        var champion = Summon(p.Opponent, "CS2_222"); var target = Summon(p.Opponent, "CS2_182");
        new Enchant(Effects.Attack_N(3)) { IsOneTurnEffect = true }.ActivateTo(target); target.Cost = 1;
        Play(g, p, "DINO_414", first); CthunRulesProof.PickTarget(g, target.Id);
        Core2026Proof.Check(first.AttackDamage == 8 && first.BaseHealth == 5 && first.Cost == 1,
            $"copy uses recipient auras and copied cost: {first.AttackDamage}/{first.BaseHealth} cost={first.Cost}");
        Kill(g, champion); End(g);
        Core2026Proof.Check(first.AttackDamage == 5 && target.AttackDamage == 4, "copied temporary buffs expire independently");
        g = CthunRulesProof.Fixture(); p = g.Player1;
        first = Summon(p, "CS2_231"); target = Summon(p.Opponent, "EX1_393"); target.Damage = 1; g.AuraUpdate();
        Play(g, p, "DINO_414", first); CthunRulesProof.PickTarget(g, target.Id);
        Core2026Proof.Check(first.AttackDamage == target.AttackDamage, "copied Enrage is realized once");
        first.Damage = 0; g.AuraUpdate();
        Core2026Proof.Check(first.AttackDamage == first.Card.ATK && target.Damage == 1, "copied Enrage heals independently");
        g = CthunRulesProof.Fixture(); p = g.Player1;
        first = Summon(p, "CS2_231"); target = Summon(p.Opponent, "CS2_182");
        Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_158e"), target, target);
        Play(g, p, "DINO_414", first); CthunRulesProof.PickTarget(g, target.Id); Kill(g, first);
        Core2026Proof.Check(p.BoardZone.Any(m => m.Card.Id == "EX1_158t") && target.Zone == p.Opponent.BoardZone,
            "granted Deathrattle belongs to independent copy");
    }

    private static void Unpinned_AmmoPoolsAndCapacity()
    {
        foreach (bool full in new[] { false, true })
        foreach (string form in new[] { "JAIL_458t3", "JAIL_458t4" })
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1; Play(g, p, "JAIL_458"); CthunRulesProof.Pick(g, form);
            if (full) { while (!p.BoardZone.IsFull) Summon(p, "CS2_231"); PoolPilotProof.FillHand(p); }
            var filter = form == "JAIL_458t3" ? OfflinePoolFilter.Random(type: CardType.MINION, minCost: 3, maxCost: 3)
                : OfflinePoolFilter.Random(type: CardType.MINION, keyword: GameTag.BATTLECRY);
            var pool = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(form).AssetId).ToArray();
            Console.WriteLine(form + " bucket=" + pool.Length);
            var expected = pool[g.CloneExact().Random.Next(pool.Length)]; int hand = p.HandZone.Count, board = p.BoardZone.Count;
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "ammo attack accepted");
            if (!full && form == "JAIL_458t3")
                Core2026Proof.Check(p.BoardZone.Last().Card.Id == expected.Id && p.BoardZone.Last().HasTaunt, "Earth seeded exact-cost summon with Taunt");
            if (!full && form == "JAIL_458t4")
                Core2026Proof.Check(p.HandZone.Last().Card.Id == expected.Id && p.HandZone.Last().Cost == Math.Max(0, expected.Cost - 2), "Air seeded Battlecry and actual discount");
            if (full) Core2026Proof.Check(p.HandZone.Count == hand && p.BoardZone.Count == board, "full capacities burn/block ammo output");
            CthunRulesProof.Pick(g, "JAIL_458t1");
            End(g); var enemy = Summon(p.Opponent, "CS2_231"); enemy.IsExhausted = false;
            var defender = (ICharacter)p.BoardZone.FirstOrDefault(m => m.HasTaunt) ?? p.Hero;
            Core2026Proof.Check(g.Process(MinionAttackTask.Any(p.Opponent, enemy, defender)) && p.Choice == null && p.Opponent.Choice == null,
                "opponent attack cannot trigger friendly weapon");
        }
    }

    private static void RewindDecisionPlays()
    {
        foreach (string id in new[] { "DINO_414", "JAIL_458" })
        {
            var g = CthunRulesProof.Fixture(); var p = g.Player1;
            var first = Summon(p, "EDR_529"); var target = Summon(p.Opponent, "CS2_182");
            var card = Add(p, id); card[GameTag.REWIND] = 1;
            PlayEntity(g, p, card, id == "DINO_414" ? first : null);
            void FinishChoice(Game game)
            {
                int selected = id == "DINO_414" ? target.Id : game.Player1.Choice.Choices[0];
                CthunRulesProof.PickTarget(game, selected);
            }
            FinishChoice(g);
            Core2026Proof.Check(g.Rewind != null, "decision play completes into Rewind offer");
            foreach (var copy in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var transaction = copy.Rewind;
                Core2026Proof.Check(copy.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(transaction.OfferChoiceId,
                    transaction.ControllerId, new[] { transaction.RewindEntityId }), out _), "Rewind restarts decision play");
                FinishChoice(copy);
                Core2026Proof.Check(copy.Rewind == null && copy.Player1.Choice == null, "replayed decision finishes without duplicate tail");
            }
        }
    }
}
