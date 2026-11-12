using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B15CombatContextCardScenarios
{
    [GroupProof("b15-combat-context-cards", ProofKind.Cards)]
    internal static void All()
    {
        B15CombatContextBECardsProof.Run();
        B15CombatContextCATACLYSMCardsProof.Run();
        B15CombatContextCORECardsProof.Run();
        B15CombatContextTHELOSTCITYCardsProof.Run();
        B15CombatContextEMERALDDREAMCardsProof.Run();
        B15CombatContextESCAPEFROMVIOLETHOLDCardsProof.Run();
    }
    internal static void Run(Game g, Controller p, string id)
    {
        switch (id)
        {
            case "BE_151": Unpinned_Vaultkeeper(g,p,id); break;
            case "CATA_185": Unpinned_Faceless(g,p,id); break;
            case "CATA_464": Blackwing(g,p,id); break;
            case "CORE_CATA_004": Unpinned_Rehgar(g,p,id); break;
            case "CORE_RLK_086": Unpinned_Frostmourne(g,p,id); break;
            case "CORE_YOD_026": Fiendish(g,p,id); break;
            case "DINO_401": Unpinned_Dracorex(g,p,id); break;
            case "EDR_258": Unpinned_TorethShieldEdges(g,p,id); break;
            case "EDR_453": Unpinned_Briarspawn(g,p,id); break;
            case "EDR_484": Flytrap(g,p,id); break;
            case "EDR_819": Unpinned_Ursoc(g,p,id); break;
            case "JAIL_315": Unpinned_Mystic(g,p,id); break;
            default: throw new Exception(id);
        }
        g.DeathProcessingAndAuraUpdate();
    }
    private static void Buff(Minion m, int attack) => new SabberStoneCore.Enchants.Enchant(
        SabberStoneCore.Enchants.Effects.Attack_N(attack)).ActivateTo(m);
    private static IEnumerable<Game> Futures(Game g) => new[] { g, g.CloneExact(history:true), Game.LoadSnapshot(g.SaveSnapshot()) };
    private static void Unpinned_Vaultkeeper(Game g, Controller p, string id)
    {
        var a=Summon(p,"CS2_231"); var left=Summon(p,"CS2_231");
        var body=(Minion)Add(p,id);
        p.BaseMana=10; p.UsedMana=0;
        Core2026Proof.Check(g.Process(new PlayCardTask(p,body,zonePosition:2)),"Vaultkeeper middle play");
        var right=Summon(p,"CS2_231"); var b=Summon(p,"CS2_231");
        Core2026Proof.Check(left.AttackDamage==3 && left.BaseHealth==3 && a.AttackDamage==1,"Vaultkeeper Battlecry immediate neighbour");
        foreach(var next in Futures(g))
        {
            foreach(int dead in new[]{left.Id,body.Id,right.Id}) next.IdEntityDic[dead].ToBeDestroyed=true;
            next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[a.Id]).AttackDamage==3 && ((Minion)next.IdEntityDic[b.Id]).AttackDamage==3,
                "Unpinned_Vaultkeeper: simultaneous deaths expose A/B at virtual position");
            Core2026Proof.Check(next.IdEntityDic[body.Id][DeathContextRules.Anchor]==0,"resolved anchor expires");
        }
    }
    private static void Unpinned_Faceless(Game g, Controller p, string id)
    {
        var victim=Summon(p,id); var killer=Summon(p.Opponent,"CS2_182");
        foreach(var next in Futures(g))
        {
            var v=(Minion)next.IdEntityDic[victim.Id]; var k=(Minion)next.IdEntityDic[killer.Id];
            v.TakeDamage(k,3); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(k.Card.Id==id && k.AttackDamage==3 && k.Health==3 && k.CantBeTargetedBySpells,
                "Unpinned_Faceless: direct minion kill transforms living killer, fresh stats/Elusive");
        }
        var simultaneous=Summon(p,id); var enemy=Summon(p.Opponent,"CS2_182"); enemy.Health=1;
        Attack(g,simultaneous,enemy);
        Core2026Proof.Check(enemy.Card.Id=="CS2_182" && simultaneous.Zone==p.GraveyardZone,"simultaneous killer is gone, never transformed");
        var fromSpell=Summon(p,id); fromSpell.TakeDamage(Add(p.Opponent,"CS2_029"),6); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Opponent.BoardZone.Count(m=>m.Card.Id==id)==1,"spell receives no minion creator credit");
    }
    private static void Blackwing(Game g, Controller p, string id)
    {
        var body=(Minion)Play(g,p,id); Buff(body,4);
        foreach(var next in Futures(g))
        {
            var owner=next.Player1; Kill(next,(Minion)next.IdEntityDic[body.Id]);
            var spell=owner.HandZone.Single(m=>m.Card.Id=="CATA_464t");
            Core2026Proof.Check(spell.Cost==2 && spell[GameTag.TAG_SCRIPT_DATA_NUM_1]==7,"Dragon Breath retains last-known buffed Attack");
            int damage=owner.Opponent.Hero.Damage;
            PlayEntity(next,owner,spell,owner.Opponent.Hero);
            Core2026Proof.Check(owner.Opponent.Hero.Damage==damage+7,"Dragon Breath legal targeted spell deals captured damage");
        }
    }
    private static void Unpinned_Rehgar(Game g, Controller p, string id)
    {
        var attacker=Summon(p,"CS2_231"); var body=(Minion)Play(g,p,id);
        var enemy=Summon(p.Opponent,"CS2_182");
        foreach(var next in Futures(g))
        {
            var owner=next.Player1; Attack(next,(Minion)next.IdEntityDic[attacker.Id],(Minion)next.IdEntityDic[enemy.Id]);
            Core2026Proof.Check(owner.HandZone.Count(c=>c.Card.Id=="CORE_EX1_238")==1,"Unpinned_Rehgar: dead adjacent attacker still grants Bolt");
            ((Minion)next.IdEntityDic[body.Id]).Silence();
            var later=Summon(owner,"CS2_231"); Attack(next,later,owner.Opponent.Hero);
            Core2026Proof.Check(owner.HandZone.Count(c=>c.Card.Id=="CORE_EX1_238")==1,"Silence disables observer");
        }
    }
    private static void Unpinned_Frostmourne(Game g, Controller p, string id)
    {
        Play(g,p,id); var weapon=p.Hero.Weapon; weapon.Damage=2;
        var victim=Summon(p.Opponent,"CS2_231"); Buff(victim,2);
        foreach(var next in Futures(g))
        {
            Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1,(Minion)next.IdEntityDic[victim.Id])),"Frostmourne final swing");
            Core2026Proof.Check(next.Player1.Hero.Weapon==null && next.Player1.BoardZone.Single().Card.Id=="CS2_231"
                && next.Player1.BoardZone.Single().AttackDamage==1,"Unpinned_Frostmourne: final durability kill captured and resurrected fresh");
        }
    }
    private static void Fiendish(Game g, Controller p, string id)
    {
        var source=(Minion)Play(g,p,id); Buff(source,5);
        var first=Summon(p,"CS2_231"); var second=Summon(p,"CS2_231");
        foreach(var next in Futures(g))
        {
            Kill(next,(Minion)next.IdEntityDic[source.Id]);
            var attacks=next.Player1.BoardZone.Select(m=>m.AttackDamage).OrderBy(x=>x).ToArray();
            Core2026Proof.Check(attacks.SequenceEqual(new[]{1,8}),"Fiendish grants last-known 7 Attack exactly once to a friendly minion");
            foreach(var m in next.Player1.BoardZone) m.Silence();
            Core2026Proof.Check(next.Player1.BoardZone.All(m=>m.AttackDamage==1),"transferred Attack is removable buff");
        }
    }
    private static void Unpinned_Dracorex(Game g, Controller p, string id)
    {
        var source=(Minion)Play(g,p,id); Buff(source,3); source.Health=1;
        var defender=Summon(p.Opponent,"CS2_182"); defender.BaseHealth=30;
        var other=Summon(p.Opponent,"CS2_182"); other.BaseHealth=30;
        foreach(var next in Futures(g))
        {
            Attack(next,(Minion)next.IdEntityDic[source.Id],(Minion)next.IdEntityDic[defender.Id]);
            Core2026Proof.Check(next.IdEntityDic[source.Id].Zone==next.Player1.GraveyardZone
                && ((Minion)next.IdEntityDic[defender.Id]).Damage==8 && ((Minion)next.IdEntityDic[other.Id]).Damage==5,
                "Unpinned_Dracorex: lethal retaliation settles first; dead source splashes printed 5, excludes defender");
        }
    }
    private static void Unpinned_TorethShieldEdges(Game g, Controller p, string id)
    {
        var aura=(Minion)Play(g,p,id); var target=Summon(p,"CS2_182"); target.HasDivineShield=true;
        target.TakeDamage(p.Opponent.Hero,1); target.TakeDamage(p.Opponent.Hero,1);
        Core2026Proof.Check(target.HasDivineShield && target.Damage==0,"PinnedRule_Toreth: shield survives first two hits");
        foreach(var next in Futures(g))
        {
            var copy=(Minion)next.IdEntityDic[target.Id]; copy.TakeDamage(next.Player2.Hero,1);
            Core2026Proof.Check(!copy.HasDivineShield && copy.Damage==0,"PinnedRule_Toreth: third hit breaks shield in clone/load");
        }
        target.HasDivineShield=true; target.TakeDamage(p.Opponent.Hero,1); target.TakeDamage(p.Opponent.Hero,1);
        target.HasDivineShield=true; target.TakeDamage(p.Opponent.Hero,1); target.TakeDamage(p.Opponent.Hero,1);
        Core2026Proof.Check(target.HasDivineShield,"Unpinned_Toreth: regrant resets blocked hits");
        aura.Silence(); Core2026Proof.Check(target.HasDivineShield,"Unpinned_Toreth: aura removal preserves shield");
        target.TakeDamage(p.Opponent.Hero,1); Core2026Proof.Check(!target.HasDivineShield,"next ordinary hit consumes shield after removal");
    }
    private static void Unpinned_Briarspawn(Game g, Controller p, string id)
    {
        var body=(Minion)Play(g,p,id); var defender=Summon(p.Opponent,"CS2_182"); defender.Health=3;
        defender.HasDivineShield=true; p.Opponent.Hero.Armor=5;
        foreach(var next in Futures(g))
        {
            End(next);
            Core2026Proof.Check(((Minion)next.IdEntityDic[defender.Id]).Damage==0
                && !((Minion)next.IdEntityDic[defender.Id]).HasDivineShield && next.Player2.Hero.Armor==0 && next.Player2.Hero.Damage==4,
                "Unpinned_Briarspawn: 12 versus shielded 3 spills 9, Armor first");
        }
    }
    private static void Flytrap(Game g, Controller p, string id)
    {
        var body=(Minion)Play(g,p,id); var victim=Summon(p.Opponent,"CS2_231"); Buff(victim,6);
        foreach(var next in Futures(g))
        {
            Kill(next,(Minion)next.IdEntityDic[victim.Id]);
            Core2026Proof.Check(((Minion)next.IdEntityDic[body.Id]).AttackDamage==9,"Flytrap gains enemy last-known 7 Attack");
            ((Minion)next.IdEntityDic[body.Id]).Silence();
            Core2026Proof.Check(((Minion)next.IdEntityDic[body.Id]).AttackDamage==2,"Silence removes realized Attack and listener");
        }
    }
    private static void Unpinned_Ursoc(Game g, Controller p, string id)
    {
        var friendly=Summon(p,"CS2_231"); var enemy=Summon(p.Opponent,"CS2_171");
        var source=(Minion)Play(g,p,id);
        Core2026Proof.Check(friendly.Zone==p.GraveyardZone && enemy.Zone==p.Opponent.GraveyardZone && source.Damage==2,
            "Unpinned_Ursoc: Battlecry attacks friendly and enemy snapshot once");
        var ledger=PayloadRules.Entries(source,DeathContextRules.KillKey).Select(e=>e.Card.Id).ToArray();
        Core2026Proof.Check(ledger.Length==2 && ledger.Contains("CS2_231") && ledger.Contains("CS2_171"),"Ursoc kill set excludes retaliation and unrelated kills");
        foreach(var next in Futures(g))
        {
            Kill(next,(Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(next.Player1.BoardZone.Select(m=>m.Card.Id).SequenceEqual(ledger),"Unpinned_Ursoc: clean resurrections follow recorded kill order");
        }
    }
    private static void Unpinned_Mystic(Game g, Controller p, string id)
    {
        Play(g,p,id); End(g);
        var attacker=Summon(p.Opponent,"CS2_182");
        foreach(var next in Futures(g))
        {
            Attack(next,(Minion)next.IdEntityDic[attacker.Id],next.Player1.Hero);
            var sheep=next.Player2.BoardZone.Single();
            Core2026Proof.Check(next.Player1.Hero.Damage==0 && sheep.Card.Id=="CS2_tk1" && sheep.AttackDamage==1 && sheep.Health==1
                && sheep.IsExhausted && next.Player1.SecretZone.Count==0,"Unpinned_Mystic: replace attacker, cancel pending combat, ordinary Sheep readiness");
        }
    }
}
