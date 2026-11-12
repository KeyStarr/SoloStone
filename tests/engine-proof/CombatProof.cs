using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class CombatProof
{
    [GroupProof("b14-f4-combat")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (bool history in new[] { false, true })
        foreach (var test in new Action<bool>[] { DamageInstrumentation, AttackingImmunity, MegaWindfury, HeroAura, Outgoing, FireImmunity, Cleave, Unpinned_HeroReplacement, TargetProtection, ReplacementPrevention, DeclaredPools, RedirectedOutgoing })
        {
            try { test(history); Console.WriteLine($"PASS combat {test.Method.Name} history={history}"); }
            catch (Exception e) { failures.Add($"{test.Method.Name} history={history}: {e.Message}"); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    }

    private static void DamageInstrumentation(bool history)
    {
        var method = typeof(Character).GetMethod(nameof(Character.TakeDamage),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        Core2026Proof.Check(method != null && method.IsPublic, "name-only damage instrumentation resolves one public entry, including redirected hits");
    }

    private static void AttackingImmunity(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var attacker = Summon(p, "CS2_171"); attacker[GameTag.IMMUNE_WHILE_ATTACKING] = 1;
        var defender = Summon(p.Opponent, "CS2_182"); defender.BaseHealth = 30;
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p, attacker, defender)) && attacker.Damage == 0 && defender.Damage == 1,
            "attacking immunity prevents retaliation");
        Core2026Proof.Check(!attacker.IsImmune && !attacker.IsAttacking, "attacking immunity does not persist after combat");
        attacker.TakeDamage(defender, 1);
        Core2026Proof.Check(attacker.Damage == 1, "ordinary damage outside attack still applies");
    }

    private static void MegaWindfury(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var attacker = Summon(p, "CS2_171"); attacker.BaseHealth = 100;
        attacker[GameTag.MEGA_WINDFURY] = 1; attacker.CantAttackHeroes = true;
        var defender = Summon(p.Opponent, "CS2_182"); defender.BaseHealth = 100;
        for (int i = 0; i < 4; i++) Core2026Proof.Check(g.Process(MinionAttackTask.Any(p, attacker, defender)), "four attacks accepted: " + i);
        Core2026Proof.Check(attacker.NumAttacksThisTurn == 4 && !attacker.CanAttack && !g.Process(MinionAttackTask.Any(p, attacker, defender)),
            "fifth attack rejected");
        foreach (var copy in new[] { g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) })
            Core2026Proof.Check(!((Minion)copy.IdEntityDic[attacker.Id]).CanAttack, "spent Mega-Windfury survives clone/snapshot");
        attacker.Silence();
        Core2026Proof.Check(attacker[GameTag.MEGA_WINDFURY] == 0 && !attacker.CanAttack, "Silence removes four-attack grant without refreshing attacks");
    }

    private static void HeroAura(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        p.Hero.AttackDamage = 2;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "first hero attack");
        var source = Summon(p, "CS2_182");
        new Aura(AuraType.HERO, new Effect(GameTag.WINDFURY, EffectOperator.ADD, 1)).Activate(source);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.HasWindfury && p.Hero.CanAttack, "hero Windfury aura restores remaining attack");
        foreach (var copy in new[] { g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var copied = (Minion)copy.IdEntityDic[source.Id]; copied.Silence(); copy.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!copy.Player1.Hero.HasWindfury && !copy.Player1.Hero.CanAttack, "aura removal after clone/snapshot restores one-attack budget");
        }
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && !p.Hero.CanAttack, "exactly two aura attacks");
        source.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!p.Hero.HasWindfury && !p.Hero.CanAttack, "Silence removes hero Windfury");
    }

    private static void Outgoing(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var source = Summon(p, "CS2_171");
        source[GameTag.OUTGOING_DAMAGE_ADJUSTMENT] = 1;
        source[GameTag.OUTGOING_DAMAGE_MULTIPLIER] = 2;
        p.Opponent.Hero.Armor = 5;
        p.Opponent.Hero.TakeDamage(source, 3);
        Core2026Proof.Check(p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 3, "additive then multiplier before Armor");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        shield.TakeDamage(source, 0);
        Core2026Proof.Check(shield.HasDivineShield, "zero base event does not become bonus damage");
        shield.TakeDamage(source, 1);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0, "shield prevents modified hit");
    }

    private static void FireImmunity(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var target = Summon(p.Opponent, "FIR_959"); target[(GameTag)4128] = 1;
        // Only Fyrakk is Immune to Fire spells: tag 4128 on any other minion changes nothing.
        var tagged = Summon(p.Opponent, "CS2_182"); tagged[(GameTag)4128] = 1;
        Core2026Proof.Check(Add(p, "CORE_CS2_029").IsValidPlayTarget(tagged), "tag 4128 on a non-Fyrakk minion does not protect it from Fire spells");
        var fire = Add(p, "CORE_CS2_029"); var friendlyFire = Add(p.Opponent, "CORE_CS2_029");
        Core2026Proof.Check(!fire.IsValidPlayTarget(target) && !fire.ValidPlayTargets.Contains(target)
            && friendlyFire.IsValidPlayTarget(target), "enemy Fire target excluded; friendly Fire allowed");
        Core2026Proof.Check(!p.Options().Any(t=>t.Source?.Id==fire.Id && t.Target?.Id==target.Id),"native/player Fire options reject protected target");
        target.TakeDamage(fire, 6);
        Core2026Proof.Check(target.Damage == 0, "direct Fire spell damage prevented");
        target.TakeDamage(Add(p, "CS2_024"), 1);
        Core2026Proof.Check(target.Damage == 1 && !target.IsImmune, "non-Fire damage and attacks remain legal");
        new DestroyTask(EntityType.TARGET).Process(g,p.Opponent,friendlyFire,target); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(target.Zone==p.Opponent.GraveyardZone,"friendly Fire-source destroy bypasses damage immunity");
    }

    private static void Cleave(bool history)
    {
        var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
        var attacker = Summon(p, "CS2_171"); attacker.AttackDamage = 2; attacker.BaseHealth = 20;
        attacker[(GameTag)31366] = 1;
        var left = Summon(p.Opponent, "CS2_182"); var center = Summon(p.Opponent, "CS2_182");
        var right = Summon(p.Opponent, "CS2_182"); var outside = Summon(p.Opponent, "CS2_182");
        foreach (var m in p.Opponent.BoardZone) m.BaseHealth = 20;
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p, attacker, center)) && left.Damage == 2 && center.Damage == 2
            && right.Damage == 2 && outside.Damage == 0 && attacker.Damage == 4, "cleave hits two neighbors; only defender retaliates");
    }

    private static void Unpinned_HeroReplacement(bool history)
    {
        var g=HistoryFactsProof.Fixture(history); var p=g.Player1;
        Play(g,p,"EX1_567");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,p.Opponent.Hero)),"pre-replacement Windfury attack");
        p.Hero.HeroPower.IsExhausted=true;
        Play(g,p,"ICC_830");
        Core2026Proof.Check(p.Hero.NumAttacksThisTurn==1 && !p.Hero.HeroPower.IsExhausted,
            "Unpinned_DuskHeroState: spent attacks transfer independently of fresh Hero Power");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,p.Opponent.Hero)) && !p.Hero.CanAttack,"only second Windfury attack remains");
    }

    private static void TargetProtection(bool history)
    {
        var g=HistoryFactsProof.Fixture(history); var p=g.Player1;
        var attacker=Summon(p,"CS2_171"); var immune=Summon(p.Opponent,"CS2_182");
        immune.HasTaunt=true; immune.IsImmune=true;
        var dormant=Summon(p.Opponent,"CS2_182"); dormant[GameTag.DORMANT]=1;
        Core2026Proof.Check(!attacker.ValidAttackTargets.Contains(immune) && !attacker.ValidAttackTargets.Contains(dormant),
            "protected minions cannot be ordinary attack targets or enforce Taunt");
    }
    private static void ReplacementPrevention(bool history)
    {
        var g=HistoryFactsProof.Fixture(history); var p=g.Player1;
        Play(g,p,"CORE_BT_781"); var weapon=p.Hero.Weapon;
        p.Hero.IsImmune=true; p.Hero.TakeDamage(p.Opponent.Hero,4);
        p.Hero.IsImmune=false; p.Hero.HasDivineShield=true; p.Hero.TakeDamage(p.Opponent.Hero,4);
        p.Hero.TakeDamage(p.Opponent.Hero,0);
        Core2026Proof.Check(weapon.Durability==4 && p.Hero.Damage==0,"Immune, Shield and zero damage do not spend Bulwark");
        p.Hero.Armor=8; p.Hero.TakeDamage(p.Hero,2);
        Core2026Proof.Check(weapon.Durability==3 && p.Hero.Armor==8 && p.Hero.Damage==0,"fatigue replacement precedes Armor");
        foreach(var copy in new[]{g.CloneExact(history:history),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            copy.Player1.Hero.TakeDamage(copy.Player2.Hero,3);
            Core2026Proof.Check(copy.Player1.Hero.Weapon.Durability==2 && copy.Player1.Hero.Armor==8,"replacement persists after clone/snapshot");
        }
    }

    private static void DeclaredPools(bool history)
    {
        var g=HistoryFactsProof.Fixture(history);
        foreach(var id in new[]{"FIR_959","TIME_890t2"})
        {
            var declaration=SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards.SingleOrDefault(c=>c.CardId==id);
            Core2026Proof.Check(declaration!=null,"group pool discovered at runtime: "+id);
            var report=OfflinePoolAdmission.Evaluate(g.OfflinePools,new[]{declaration}); report.ValidateRegistered();
            Console.WriteLine("PASS combat declared pool "+id+": "+string.Join(",",report.Cards.SelectMany(c=>c.PoolRequirements).Select(r=>r.Class+"="+r.Count)));
        }
    }

    private static void RedirectedOutgoing(bool history)
    {
        var g=HistoryFactsProof.Fixture(history); var p=g.Player1;
        var beast=Summon(p,"CS2_171"); Summon(p,"EDR_480");
        var bolf=Summon(p.Opponent,"AT_124");
        p.Opponent.Hero.TakeDamage(beast,2);
        Core2026Proof.Check(bolf.Damage==4 && p.Opponent.Hero.Damage==0,"redirected hit applies outgoing modifiers exactly once");
    }
}
