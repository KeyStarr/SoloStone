using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class DormantRulesProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);
    private static void End(Game g) => Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "end accepted");
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            var m = HeroDivineShieldProof.Summon(g, "TIME_046");
            Check(m.IsDormant && m.AppliedEnchantments.Single().Card.Id == "TIME_046e" && DormantRules.TurnsRemaining(m) == 3,
                "Unpinned_DormantClock natural three-start sleep");
            End(g); Check(DormantRules.TurnsRemaining(m) == 3, "Unpinned_DormantClock opponent start ignored");
            End(g); Check(DormantRules.TurnsRemaining(m) == 2, "Unpinned_DormantClock first future owner start");
            foreach (var resumed in new[] { g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var sleeper = (Minion)resumed.IdEntityDic[m.Id];
                End(resumed); End(resumed); End(resumed); End(resumed);
                Check(!sleeper.IsDormant && sleeper.HasTaunt && sleeper.IsExhausted && sleeper[GameTag.DORMANT_AWAKENED_THIS_TURN] == 1,
                    "Unpinned_DormantLifecycle clone/restore wakes at zero with Taunt and sickness");
                Check(sleeper.AppliedEnchantments.Count == 0, "Unpinned_DormantCounterProjection helper retired");
            }
            Check(DormantRules.TurnsRemaining(m) == 2, "clone isolation");
            var copy = (Minion)Generic.Copy(p, p.Hero, m, Zone.PLAY);
            Check(copy.IsDormant && DormantRules.TurnsRemaining(copy) == 2
                && copy.AppliedEnchantments.Single().Id != m.AppliedEnchantments.Single().Id, "Unpinned_DormantLifecycle independent board-copy helpers");
            DormantRules.Awaken(m);
            var awake = (Minion)Generic.Copy(p, p.Hero, m, Zone.PLAY);
            Check(!awake.IsDormant && awake.AppliedEnchantments?.Count is null or 0, "Unpinned_DormantLifecycle awake board copy stays awake");
            var rng = g.Random.CaptureState(); int count = g.PowerHistory?.Last.Count ?? 0;
            Check(!DormantRules.Awaken(m) && !DormantRules.Accelerate(m, 0) && rng.Equals(g.Random.CaptureState())
                && (g.PowerHistory?.Last.Count ?? 0) == count, "repeated wake is no-op");
            var held = (Minion)Generic.DrawCard(p, Cards.FromId("TIME_046"));
            Check(!held.IsDormant && held[GameTag.DORMANT] == 0 && !DormantRules.Make(held, 1, "TIME_046e", p.Hero), "off-board rejected");
            External(history); PendingAndTransform(history); Auras(history); RetiredAdjacency(history); LegacyTakeover(history); Control(history); AwakenRecipe(history); AsleepListener(history); SilencedSleepCopy(history);
        }
        FreshProcess();
        Console.WriteLine("PASS DormantRules: Unpinned_DormantClock/Exclusions/Lifecycle/CounterProjection/Replace; clone/restore/copy/pending/transform/auras");
    }
    private static void SilencedSleepCopy(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var wolf = HeroDivineShieldProof.Summon(g, "EX1_162");
        var neighbor = HeroDivineShieldProof.Summon(g, "CS2_182");
        Check(neighbor.AttackDamage == 5, "Dire Wolf aura initially active");
        wolf.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(neighbor.AttackDamage == 4 && wolf.IsSilenced, "Silence removes original aura");
        Check(DormantRules.Make(wolf, 2, "TIME_046e", p.Hero), "silenced wolf externally sleeps");
        var copy = (Minion)Generic.Copy(p, p.Hero, wolf, Zone.PLAY);
        g.DeathProcessingAndAuraUpdate();
        foreach (var resumed in new[] { g, g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var sleeper = (Minion)resumed.IdEntityDic[copy.Id];
            Check(DormantRules.Awaken(sleeper), "sleeping copy awakens");
            resumed.DeathProcessingAndAuraUpdate();
            Check(sleeper.IsSilenced && sleeper.OngoingEffect == null
                && ((Minion)resumed.IdEntityDic[neighbor.Id]).AttackDamage == 4,
                "SilencedSleepCopy Unpinned_DormantLifecycle copy/wake preserves Silence and cannot restore aura");
            var awakeCopy = (Minion)Generic.Copy(resumed.Player1, resumed.Player1.Hero, sleeper, Zone.PLAY);
            resumed.DeathProcessingAndAuraUpdate();
            Check(awakeCopy.IsSilenced && awakeCopy.OngoingEffect == null && sleeper.AttackDamage == 2,
                "SilencedSleepCopy Unpinned_DormantLifecycle awake copy cannot restore silenced aura");
        }
        Console.WriteLine("PASS SilencedSleepCopy: silenced EX1_162 sleeping/awake copies; neighbor Attack=4; clone/restore");
    }
    private static void External(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var m = HeroDivineShieldProof.Summon(g, "CS2_182");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_087e"), p.Hero, m);
        m.Damage = 2; m.IsFrozen = true; m.IsRush = true; m.NumAttacksThisTurn = 1;
        m[GameTag.TAG_SCRIPT_DATA_NUM_1] = 77;
        int atk = m.AttackDamage, health = m.BaseHealth;
        Check(DormantRules.Make(m, 2, "TIME_046e", p.Hero), "external sleep");
        int helper = DormantRules.SleepGeneration(m), count = g.PowerHistory?.Last.Count ?? 0;
        Check(!DormantRules.Make(m, 2, "TIME_046e", p.Hero) && (g.PowerHistory?.Last.Count ?? 0) == count, "same Make no allocation/history");
        Check(m[GameTag.TAG_SCRIPT_DATA_NUM_1] == 77 && m.AppliedEnchantments.Single(e => e.Id == helper)[(GameTag)451] == 2,
            "Unpinned_DormantCounterProjection external helper owns counter; script2 retained");
        m.Silence(); m.TakeDamage(p.Opponent.Hero, 99); m.Destroy();
        Check(m.IsDormant && !m.IsSilenced && !m.ToBeDestroyed && m.Damage == 2
            && !p.BoardZone.GetAll().Contains(m), "Unpinned_DormantExclusions silence/damage/destroy/active selection");
        Check(DormantRules.Make(m, 1, "TIME_046e", p.Hero) && DormantRules.SleepGeneration(m) != helper,
            "Unpinned_DormantReplace application can replace sleeping target");
        DormantRules.Accelerate(m, 50);
        Check(!m.IsDormant && m.Damage == 2 && m.AttackDamage == atk && m.BaseHealth == health && m.IsFrozen
            && m.NumAttacksThisTurn == 1 && m[GameTag.TAG_SCRIPT_DATA_NUM_1] == 77, "Unpinned_DormantLifecycle state retained, no attack refund");
        Check(DormantRules.Make(m, null, "TIME_046e", p.Hero), "external release mode");
        End(g); End(g); Check(m.IsDormant && !DormantRules.Accelerate(m, 100), "external release never naturally ticks");
        var old = DormantRules.SleepGeneration(m);
        p.SetasideZone.Add(p.BoardZone.Remove(m));
        Check(!m.IsDormant && !DormantRules.HasState(m) && DormantRules.SleepGeneration(m) == 0
            && m.AppliedEnchantments.All(e => e.Id != old), "Unpinned_DormantLifecycle exit invalidates release generation");
    }
    private static void PendingAndTransform(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var m = (Minion)Entity.FromCard(p, Cards.FromId("TIME_046"));
        DormantRules.MarkPending(m, 1, "TIME_046e");
        Generic.SummonBlock(g, m, -1, p.Hero);
        Check(DormantRules.TurnsRemaining(m) == 1 && !DormantRules.HasPending(m), "pending external duration overrides natural before activation");
        var transformed = (Minion)Generic.ChangeEntityBlock(p, m, Cards.FromId("CS2_182"), true);
        Check(!transformed.IsDormant && !DormantRules.HasState(transformed) && transformed.AppliedEnchantments.Count == 0, "transform away clears helper without awakening");
        transformed = (Minion)Generic.ChangeEntityBlock(p, transformed, Cards.FromId("TIME_046"), true);
        Check(transformed.IsDormant && DormantRules.TurnsRemaining(transformed) == 3, "board transform initializes new printed Dormant");
        var fresh = (Minion)Entity.FromCard(p, Cards.FromId("TIME_046")); p.BoardZone.Replace(transformed, fresh);
        Check(fresh.IsDormant && !DormantRules.HasState(transformed), "BoardZone.Replace lifecycle");
        var pending = Entity.FromCard(p, Cards.FromId("TIME_046")); DormantRules.MarkPending(pending, 2, "TIME_046e");
        Generic.AddHandPhase(p, pending); Check(!DormantRules.HasPending(pending), "other zone admission clears pending sleep");
    }
    private static void Auras(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var champion = HeroDivineShieldProof.Summon(g, "CS2_222");
        var m = HeroDivineShieldProof.Summon(g, "CS2_182");
        Check(m.AttackDamage == 5, "received aura fixture");
        DormantRules.Make(m, 1, "TIME_046e", champion); g.DeathProcessingAndAuraUpdate();
        Check(m.AttackDamage == 4, "Unpinned_DormantExclusions received aura removed");
        DormantRules.Awaken(m); g.DeathProcessingAndAuraUpdate(); Check(m.AttackDamage == 5, "received aura restored");
        DormantRules.Make(champion, 1, "TIME_046e", m); g.DeathProcessingAndAuraUpdate();
        Check(m.AttackDamage == 4, "emitted aura removed");
        DormantRules.Awaken(champion); g.DeathProcessingAndAuraUpdate(); Check(m.AttackDamage == 5, "emitted aura restored");
        while (!p.BoardZone.IsFull) HeroDivineShieldProof.Summon(g, "CS2_231");
        Check(p.BoardZone.SlotCount == 7 && p.BoardZone.FreeSpace == 0, "Unpinned_DormantExclusions sleepers occupy real slots");
    }
    private static void RetiredAdjacency(bool history)
    {
        foreach (string retirement in new[] { "silence", "death", "bounce", "sleep" })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            var left = HeroDivineShieldProof.Summon(g, "CS2_182");
            var wolf = HeroDivineShieldProof.Summon(g, "EX1_162");
            var right = HeroDivineShieldProof.Summon(g, "CS2_182");
            Check(left.AttackDamage == 5 && right.AttackDamage == 5, "retirement adjacency fixture");
            if (retirement == "silence") wolf.Silence();
            else if (retirement == "death") wolf.Destroy();
            else if (retirement == "bounce") Generic.ReturnToHandBlock(p, wolf);
            else DormantRules.Make(wolf, 1, "TIME_046e", p.Hero);
            g.DeathProcessingAndAuraUpdate();
            Check(left.AttackDamage == 4 && right.AttackDamage == 4, "retired aura removed once: " + retirement);
            DormantRules.Make(left, 1, "TIME_046e", p.Hero);
            DormantRules.Make(right, 1, "TIME_046e", p.Hero);
            g.DeathProcessingAndAuraUpdate();
            Check(left.AttackDamage == 4 && right.AttackDamage == 4 && p.BoardZone.AdjacentAuras.Count == 0,
                "RetiredAdjacency Unpinned_DormantExclusions retirement-then-sleep never subtracts retired buff: " + retirement);
            DormantRules.Awaken(left); DormantRules.Awaken(right); g.DeathProcessingAndAuraUpdate();
            Check(left.AttackDamage == 4 && right.AttackDamage == 4, "retired adjacency stays absent after wake");
        }
        Console.WriteLine("PASS RetiredAdjacency: retirement-then-sleep; silence/death/bounce/sleep; both neighbors");
    }
    private static void LegacyTakeover(bool history)
    {
        foreach (string id in new[] { "EDR_469", "CORE_BT_156" })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            var m = HeroDivineShieldProof.Summon(g, id);
            var legacy = m.AppliedEnchantments?.SingleOrDefault(e => e.Card.Id == "EDR_469e");
            Check(DormantRules.Make(m, 3, "TIME_046e", p.Hero), "legacy external takeover accepted");
            Check(g.Process(HeroPowerTask.Any(p, p.Opponent.Hero)), "takeover hero power accepted");
            Check(m.IsDormant && DormantRules.TurnsRemaining(m) == 3
                && m.AppliedEnchantments.All(e => e.Card.Id != "EDR_469e")
                && (legacy == null || legacy[GameTag.ATTACHED] == 0 && (!history || legacy[GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME)),
                "LegacyTakeover Unpinned_DormantReplace retires old condition; hero power cannot bypass duration: " + id);
            var copy = (Minion)Generic.Copy(p, p.Hero, m, Zone.PLAY);
            Check(copy.IsDormant && DormantRules.TurnsRemaining(copy) == 3
                && copy.AppliedEnchantments.Single().Card.Id == "TIME_046e", "LegacyTakeover board copy cannot recreate legacy helper");
            new ControlTask(EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, m);
            End(g); p.Opponent.BaseMana = 10; p.Opponent.UsedMana = 0;
            foreach (var resumed in new[] { g, g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var controlled = (Minion)resumed.IdEntityDic[m.Id];
                Check(resumed.Process(HeroPowerTask.Any(resumed.Player2, resumed.Player1.Hero)), "transferred takeover power accepted");
                Check(controlled.IsDormant && DormantRules.TurnsRemaining(controlled) == 2
                    && controlled.AppliedEnchantments.Single().Card.Id == "TIME_046e"
                    && controlled.AppliedEnchantments.Single().Controller == resumed.Player2,
                    "LegacyTakeover control entry/clone/restore keeps only modern helper");
                DormantRules.Accelerate(controlled, 2);
                Check(!controlled.IsDormant, "modern duration still awakens legacy minion");
            }
            var awakeCopy = (Minion)Generic.Copy(p.Opponent, p.Opponent.Hero, m, Zone.PLAY);
            Check(!awakeCopy.IsDormant, "awake modern legacy copy stays awake");
            Generic.ReturnToHandBlock(p.Opponent, awakeCopy);
            Core2026Proof.PlayEntity(g, p.Opponent, awakeCopy);
            Check(awakeCopy.IsDormant && !DormantRules.HasState(awakeCopy)
                && (id != "EDR_469" || awakeCopy.AppliedEnchantments.Single().Card.Id == "EDR_469e"),
                "LegacyTakeover Unpinned_DormantLifecycle bounced awake copy starts fresh legacy sleep");
        }
        Console.WriteLine("PASS LegacyTakeover: legacy condition retirement; hero power; board copy/control entry; clone/restore");
    }
    private static void Control(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var m = HeroDivineShieldProof.Summon(g, "TIME_046");
        int generation = DormantRules.SleepGeneration(m);
        new SabberStoneCore.Tasks.SimpleTasks.ControlTask(EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, m);
        Check(m.Controller == p.Opponent && m.IsDormant && DormantRules.TurnsRemaining(m) == 3
            && DormantRules.SleepGeneration(m) == generation && m.AppliedEnchantments.Single().Controller == p.Opponent,
            "Unpinned_DormantLifecycle control transfer retains condition/generation and rebinds helper");
        End(g); Check(DormantRules.TurnsRemaining(m) == 2, "new controller next eligible start ticks");
    }

    private static void AwakenRecipe(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var m = HeroDivineShieldProof.Summon(g, "TIME_046");
        m.Card = m.Card.Clone();
        m.Card.Dormant = new DormantDefinition(1, "TIME_046e", onAwaken: new CustomTask((game, c, s, t, stack) => c.Hero.Armor += 3));
        DormantRules.Awaken(m); Check(p.Hero.Armor == 3, "Unpinned_DormantReawaken static printed awaken task once");
        DormantRules.Make(m, 1, "TIME_046e", p.Hero); DormantRules.Awaken(m); DormantRules.Awaken(m);
        Check(p.Hero.Armor == 6, "Unpinned_DormantReawaken repeated real awakening, idempotent wake");
        m.Silence(); DormantRules.Make(m, 1, "TIME_046e", p.Hero); DormantRules.Awaken(m);
        Check(p.Hero.Armor == 6, "awake Silence prevents restoration of printed awaken text");
    }

    private static void FreshProcess()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        HeroDivineShieldProof.Summon(g, "TIME_046"); CostRules.AddNextTemporaryRule(p, 2);
        TemporaryRulesProof.Grant(p);
        string path = Path.Combine(Path.GetTempPath(), "cs-p0a-" + Guid.NewGuid() + ".snapshot");
        try
        {
            File.WriteAllBytes(path, g.SaveSnapshot());
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(typeof(DormantRulesProof).Assembly.Location);
            start.ArgumentList.Add("--card-state-restore-child"); start.ArgumentList.Add(path);
            using var child = System.Diagnostics.Process.Start(start);
            var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
            if (!child.WaitForExit(30000)) { child.Kill(); throw new Exception("card-state restore child timeout"); }
            Check(child.ExitCode == 0, "fresh-process restore: " + output.Result + error.Result);
            Console.Write(output.Result);
        }
        finally { File.Delete(path); }
    }
    internal static void RestoreChild(string path)
    {
        var g = Game.LoadSnapshot(File.ReadAllBytes(path)); var p = g.Player1;
        var m = p.BoardZone.Single(c => c.Card.Id == "TIME_046");
        var temporary = p.HandZone.Single(TemporaryRules.IsTemporary);
        Check(m.IsDormant && DormantRules.TurnsRemaining(m) == 3 && temporary.Cost == 2, "fresh process restored timer/mark/discount");
        End(g); Check(p.DiscardedEntities.Contains(temporary.Id), "restored real expiry"); End(g);
        Check(DormantRules.TurnsRemaining(m) == 2, "restored future start");
        Check(TemporaryRulesProof.Grant(p).Cost == 2, "restored discount not consumed by expiry");
        Console.WriteLine("PASS card-state fresh-process restore: Dormant timer/helper, Temporary mark/expiry and persistent discount");
    }

    private static void AsleepListener(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var m = HeroDivineShieldProof.Summon(g, "TIME_046"); DormantRules.Awaken(m);
        m.Card = m.Card.Clone();
        m.Card.Dormant = new DormantDefinition(1, "TIME_046e", "TIME_046e");
        m.Card.Power = new Power { Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_END) {
            TriggerSource = TriggerSource.FRIENDLY,
            SingleTask = new CustomTask((game, c, source, target, stack) => c.Hero.Armor += 100)
        } };
        m.Card.Power.Trigger.Activate(m);
        DormantRules.Make(m, 1, "TIME_046e", p.Hero);
        var helper = m.AppliedEnchantments.Last(); helper.Card = helper.Card.Clone();
        helper.Card.Power = new Power { Trigger = new SabberStoneCore.Triggers.Trigger(TriggerType.TURN_END) {
            TriggerSource = TriggerSource.FRIENDLY,
            SingleTask = new CustomTask((game, c, source, target, stack) => {
                if (source is Enchantment e && e.Target is Minion parent && parent.IsDormant && parent.AppliedEnchantments.Contains(e)) c.Hero.Armor += 3;
            })
        } };
        helper.Power.Trigger.Activate(helper);
        End(g); Check(p.Hero.Armor == 3, "Unpinned_DormantWhile explicit helper runs asleep; ordinary minion listener suppressed");
        End(g); End(g); Check(p.Hero.Armor == 103 && !m.AppliedEnchantments.Contains(helper), "wake retires explicit helper and restores ordinary listener once");
    }

}
