using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;

internal static class B17InfinityProof
{
    [GroupProof("b17-infinity")]
    internal static void Run()
    {
        var failures = new List<string>();
        void Test(string name, Action<Game> test)
        {
            foreach (bool history in new[] { false, true })
                try { test(B14H1StatsProof.NewGame(history)); Console.WriteLine("PASS " + name + " history=" + history); }
                catch (Exception e) { failures.Add(name + ": " + e.Message); }
        }
        Test("Unpinned_InfinityAttackArithmetic", g => {
            var m = Summon(g.Player1, "CS2_182");
            Effects.SetAttack(int.MaxValue).ApplyTo(m);
            Effects.Attack_N(1).ApplyTo(m);
            Core2026Proof.Check(m.AttackDamage == int.MaxValue, "positive Attack overflow must saturate");
            Effects.Attack_N(-1).ApplyTo(m);
            Core2026Proof.Check(m.AttackDamage == int.MaxValue - 1, "infinity remains subtractable");
            new Effect(GameTag.ATK, EffectOperator.ADD, 2).ApplyAuraTo(m);
            Core2026Proof.Check(m.AttackDamage == int.MaxValue, "effective aura sum must saturate");
            Effects.SetAttack(1).ApplyTo(m);
            Core2026Proof.Check(m.AttackDamage == 3, "finite setter preserves external aura");
            Effects.SetAttack(int.MaxValue).ApplyTo(m);
            ATK.Effect(EffectOperator.MUL, 0).ApplyTo(m);
            Core2026Proof.Check(m.AttackDamage == 2, "zero multiplication preserves separate aura");
        });
        Test("Unpinned_InfinityHeroArithmetic", g => {
            Play(g, g.Player1, "CS2_106");
            var weapon = g.Player1.Hero.Weapon;
            new Effect(GameTag.ATK, EffectOperator.SET, int.MaxValue).ApplyTo(weapon);
            new Effect(GameTag.ATK, EffectOperator.ADD, 1).ApplyTo(weapon);
            Core2026Proof.Check(weapon.AttackDamage == int.MaxValue, "weapon arithmetic must saturate");
            ((Character)g.Player1.Hero).AttackDamage = 1;
            Core2026Proof.Check(g.Player1.Hero.AttackDamage == int.MaxValue, "hero plus weapon must saturate");
        });
        Test("Unpinned_InfinityTransferredHealth", g => {
            var m = Summon(g.Player1, "CS2_182");
            Effects.SetAttack(int.MaxValue).ApplyTo(m);
            new SwapAttackHealthTask(EntityType.TARGET, "EX1_059e").Process(g, g.Player1, g.Player1.Hero, m);
            Health.Effect(EffectOperator.ADD, 1).ApplyTo(m);
            Core2026Proof.Check(m.Health == int.MaxValue, "transferred infinity Health cannot wrap on buff");
            Health.Effect(EffectOperator.SUB, 1).ApplyTo(m);
            Core2026Proof.Check(m.Health == int.MaxValue - 1, "transferred Health remains numeric");
            Health.Effect(EffectOperator.ADD, 2).ApplyAuraTo(m);
            Core2026Proof.Check(m.Health == int.MaxValue, "Health plus external aura caps independently");
        });
        Test("Unpinned_InfinityCostArithmetic", g => {
            var card = (Playable)Add(g.Player1, "CS2_029");
            Effects.SetCost(int.MaxValue).ApplyTo(card);
            Effects.AddCost(1).ApplyTo(card);
            Core2026Proof.Check(card.Cost == int.MaxValue && !card.IsPlayable, "Cost overflow cannot make card free");
            Effects.ReduceCost(1).ApplyTo(card);
            Core2026Proof.Check(card.Cost == int.MaxValue - 1, "numeric Cost remains reducible");
            Effects.SetCost(1).ApplyTo(card);
            Core2026Proof.Check(card.Cost == 1, "later finite Cost setter");
            foreach (var future in new[] { g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
                Core2026Proof.Check(((IPlayable)future.IdEntityDic[card.Id]).Cost == 1, "Cost clone/load");
        });
        Test("Unpinned_PendingDeparture", g => {
            foreach (string departure in new[] { "silence", "bounce", "transform", "death" })
            {
                var p = g.CurrentPlayer;
                var m = (Minion)Play(g, p, "TIME_024");
                if (departure == "silence") m.Silence();
                else if (departure == "bounce") new ReturnHandTask(EntityType.TARGET).Process(g, p, p.Hero, m);
                else if (departure == "transform") Generic.ChangeEntityBlock(p, m, Cards.FromId("CS2_182"), true);
                else Kill(g, m);
                End(g); End(g);
                Core2026Proof.Check(m.AttackDamage != int.MaxValue, "departed/silenced original cannot receive delayed grant: " + departure);
                Core2026Proof.Check(m.AppliedEnchantments?.All(e => e.Card.Id != "TIME_024e2") != false, "pending actor retired: " + departure);
            }
        });
        Test("Unpinned_WeaponFiniteSetter", g => {
            var p = g.Player1;
            Play(g, p, "END_012");
            var weapon = p.Hero.Weapon;
            new Effect(GameTag.ATK, EffectOperator.SUB, 1).ApplyTo(weapon);
            Core2026Proof.Check(weapon.AttackDamage == int.MaxValue - 1, "weapon minus one");
            new Effect(GameTag.ATK, EffectOperator.SET, 1).ApplyTo(weapon);
            End(g);
            Core2026Proof.Check(weapon.AttackDamage == 1, "later finite weapon setter survives expiry");
        });
        Test("Unpinned_PendingCardCopies", g => {
            var p = g.Player1;
            var original = (Minion)Play(g, p, "TIME_024");
            var copy = (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY);
            new SummonCopyTask(EntityType.TARGET).Process(g, p, p.Hero, original);
            var summonedCopy = p.BoardZone.Last();
            original.Silence();
            End(g);
            Core2026Proof.Check(copy.AttackDamage == 8 && summonedCopy.AttackDamage == 8, "copied pending effects await owner start");
            End(g);
            Core2026Proof.Check(copy.AttackDamage == int.MaxValue && summonedCopy.AttackDamage == int.MaxValue,
                "both exact-copy paths activate independent pending attachments");
            Core2026Proof.Check(original.AttackDamage == 8, "original Silence does not cancel copied timers");
        });
        Test("Unpinned_WeaponNonPlayEquip", g => {
            var p = g.Player1;
            var weapon = (Weapon)Entity.FromCard(p, Cards.FromId("END_012"));
            p.Hero.AddWeapon(weapon); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(weapon.AttackDamage == 4, "non-play equip never runs Battlecry");
        });
        Test("Unpinned_WeaponExactCopyAndCapacity", g => {
            var p = g.Player1;
            Play(g, p, "END_012");
            var original = p.Hero.Weapon;
            var copy = (Weapon)Generic.Copy(p.Opponent, p.Hero, original, Zone.PLAY);
            Core2026Proof.Check(p.Opponent.Hero.Weapon == copy && copy.AttackDamage == int.MaxValue, "exact weapon copy equips realized Attack");
            Core2026Proof.Check(copy.AppliedEnchantments.Any(e => e.Card.Id == "END_012e" && e.Target == copy), "independent copied setter");
            while (!p.HandZone.IsFull) Add(p, "CS2_029");
            var burned = Generic.Copy(p, p.Hero, original, Zone.HAND);
            Core2026Proof.Check(!p.HandZone.Contains(burned) && p.HandZone.Count == p.HandZone.MaxSize, "full hand cannot receive generated weapon copy");
            End(g);
            Core2026Proof.Check(original.AttackDamage == 4 && copy.AttackDamage == 4, "both exact setters expire at same global end");
        });
        Test("Unpinned_CombatPrevention", g => {
            var p = g.Player1;
            Play(g, p, "END_012");
            p.Hero.Weapon.HasLifeSteal = true;
            p.Hero.Damage = 10;
            var victim = Summon(p.Opponent, "CS2_182"); victim.HasDivineShield = true;
            Core2026Proof.Check(!p.Options().Any(o => o.PlayerTaskType == PlayerTaskType.HERO_ATTACK && o.Target is Hero), "no hero attack option");
            p.Hero.IsExhausted = false;
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, victim)), "Shield combat accepted");
            Core2026Proof.Check(!victim.HasDivineShield && victim.Damage == 0 && p.Hero.Damage > 0, "Shield prevents full hit and Lifesteal");
            p.Hero.NumAttacksThisTurn = 0; p.Hero.IsExhausted = false;
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, victim)), "ordinary unshielded combat accepted");
            Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == victim.AttackDamage, "ordinary lethal hit and Lifesteal before retaliation: damage=" + p.Hero.Damage);
            Core2026Proof.Check(p.Hero.Weapon == null && !g.OneTurnEffectEnchantments.Any(e => e.Card.Id == "END_012e"), "destruction removes temporary actor");
        });
        Test("Unpinned_RewindInfinityState", g => {
            var p = g.Player1;
            var m = Play(g, p, "TIME_024"); Play(g, p, "END_012");
            Play(g, p, "TIME_001");
            var transaction = g.Rewind;
            Core2026Proof.Check(transaction != null, "Rewind offer exists");
            Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(transaction.OfferChoiceId, p.Id,
                new[] { transaction.RewindEntityId }), out var error), "Rewind accepted: " + error);
            Core2026Proof.Check(g.Player1.Hero.Weapon.AttackDamage == int.MaxValue
                && ((Minion)g.IdEntityDic[m.Id]).AttackDamage == 8, "Rewind preserves active setter and pending trigger");
            End(g); End(g);
            Core2026Proof.Check(g.Player1.Hero.Weapon.AttackDamage == 4
                && ((Minion)g.IdEntityDic[m.Id]).AttackDamage == int.MaxValue, "Rewind restored expiry and trigger future");
        });
        Test("Unpinned_InfinityTemporaryCost", g => {
            var card = (Playable)Add(g.Player1, "CS2_029");
            CostRules.AddModifier(card, EffectOperator.SET, int.MaxValue, g.Turn);
            CostRules.AddModifier(card, EffectOperator.ADD, 1, g.Turn);
            Core2026Proof.Check(card.Cost == int.MaxValue, "temporary Cost chain cannot overflow free");
            CostRules.AddModifier(card, EffectOperator.SUB, 1, g.Turn);
            Core2026Proof.Check(card.Cost == int.MaxValue, "wide temporary intermediates clamp only at chain end");
        });
        Test("Unpinned_PendingFacelessCopy", g => {
            var p = g.Player1;
            var original = (Minion)Play(g, p, "TIME_024");
            var copy = (Minion)Play(g, p, "EX1_564", original);
            original.Silence();
            foreach (var future in new[] { g, g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                End(future);
                Core2026Proof.Check(((Minion)future.IdEntityDic[copy.Id]).AttackDamage == 8, "Faceless waits for owner start");
                End(future);
                Core2026Proof.Check(((Minion)future.IdEntityDic[copy.Id]).AttackDamage == int.MaxValue,
                    "Faceless pending copy must fire independently");
                Core2026Proof.Check(((Minion)future.IdEntityDic[original.Id]).AttackDamage == 8, "original remains silenced");
            }
        });
        Test("Unpinned_TemporaryAttackSaturationExpiry", g => {
            var p = g.Player1;
            var original = (Minion)Play(g, p, "TIME_024");
            End(g); End(g);
            Play(g, p, "CORE_CS2_188", original);
            Core2026Proof.Check(original.AttackDamage == int.MaxValue, "temporary buff saturates");
            var copy = (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY);
            g.DeathProcessingAndAuraUpdate();
            foreach (var future in new[] { g, g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                End(future);
                Core2026Proof.Check(((Minion)future.IdEntityDic[original.Id]).AttackDamage == int.MaxValue,
                    "Abusive expiry preserves capped permanent Attack");
                Core2026Proof.Check(((Minion)future.IdEntityDic[copy.Id]).AttackDamage == int.MaxValue,
                    "exact copy preserves capped modifier expiry");
            }
        });
        Test("Unpinned_TemporaryAttackPartialSaturation", g => {
            var m = Summon(g.Player1, "CS2_182");
            Effects.SetAttack(int.MaxValue - 1).ApplyTo(m);
            Play(g, g.Player1, "CORE_CS2_188", m);
            Effects.Attack_N(-1).ApplyTo(m);
            End(g);
            Core2026Proof.Check(m.AttackDamage == int.MaxValue - 2, "expiry removes only realized growth, retaining later reduction");
        });
        Test("Unpinned_CostAuraSaturationRemoval", g => {
            var card = (Playable)Add(g.Player1, "CS2_029");
            Effects.SetCost(int.MaxValue).ApplyTo(card);
            Core2026Proof.Check(card.Cost == int.MaxValue, "warm original Cost");
            var tax = new Effect(GameTag.COST, EffectOperator.ADD, 1);
            tax.ApplyAuraTo(card);
            Core2026Proof.Check(card.Cost == int.MaxValue, "tax caps");
            foreach (var future in new[] { g, g.CloneExact(), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var target = (Playable)future.IdEntityDic[card.Id];
                tax.RemoveAuraFrom(target);
                Core2026Proof.Check(target.Cost == int.MaxValue, "removing tax restores same Cost");
                tax.ApplyAuraTo(target);
                var discount = new Effect(GameTag.COST, EffectOperator.SUB, 2);
                discount.ApplyAuraTo(target);
                Core2026Proof.Check(target.Cost == int.MaxValue - 1, "surviving aura operations use wide intermediates");
                tax.RemoveAuraFrom(target);
                Core2026Proof.Check(target.Cost == int.MaxValue - 2, "remaining discount retained");
                discount.RemoveAuraFrom(target);
                Core2026Proof.Check(target.Cost == int.MaxValue, "all aura operations removed");
                tax.ApplyAuraTo(target);
                Core2026Proof.Check(target.Cost == int.MaxValue, "rewarm saturated aura Cost");
                Effects.ReduceCost(1).ApplyTo(target);
                Core2026Proof.Check(target.Cost == int.MaxValue, "permanent reduction preserves surviving tax");
                tax.RemoveAuraFrom(target);
                Core2026Proof.Check(target.Cost == int.MaxValue - 1, "tax removal preserves permanent reduction");
            }
        });
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }
}
