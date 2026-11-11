using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static B14H1StatsProof;

internal static class B14H1StatsCardsProof
{
    [GroupProof("b14-h1-stats-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CORE_EX1_414", Enrage);
        StandardSetsProof.Case("CORE_OG_218", Enrage);
        StandardSetsProof.Case("CATA_613", Survivalist);
        StandardSetsProof.Case("JAIL_459", Arachnathid);
        StandardSetsProof.Case("END_022", Seer);
        StandardSetsProof.Case("CATA_487", Unpinned_EntryWindow);
        StandardSetsProof.Case("END_024", Unpinned_InfiniteNumericDamage);
        StandardSetsProof.Case("TLC_482", Unpinned_SplitPerHit);
        StandardSetsProof.Case("CORE_OG_044", Fandral);
        StandardSetsProof.Case("TIME_021", Prepper);
    }
    private static void Future(Game game, Action<Game> action)
    {
        string before=Canonical.State(game), history=Canonical.History(game);
        var bytes=game.SaveSnapshot(); var clone=game.CloneExact(history:true);
        var loaded=Game.LoadSnapshotWithPools(bytes,game.OfflinePools);
        action(clone); action(loaded);
        Core2026Proof.Check(before==Canonical.State(game) && history==Canonical.History(game),"branches leave original state/history unchanged");
        action(game);
        Core2026Proof.Check(Canonical.State(game)==Canonical.State(clone) && Canonical.State(game)==Canonical.State(loaded),"clone/snapshot futures identical");
    }
    private static void Enrage(Game g, Controller p, string id)
    {
        var m=(Minion)Play(g,p,id); int atk=id=="CORE_EX1_414"?4:2, bonus=id=="CORE_EX1_414"?6:3;
        Core2026Proof.Check(m.AttackDamage==atk,"undamaged base Attack"); m.Damage=1; g.AuraUpdate();
        Core2026Proof.Check(m.AttackDamage==atk+bonus,"damaged Attack"); m.TakeFullHeal(p.Hero); g.AuraUpdate();
        Core2026Proof.Check(m.AttackDamage==atk,"full heal removes bonus"); m.Damage=1; g.AuraUpdate();
        Future(g, next => {
            var copy=(Minion)next.IdEntityDic[m.Id]; copy.Silence(); next.AuraUpdate();
            Core2026Proof.Check(copy.AttackDamage==atk,"Silence removes bonus exactly once");
        });
        Generic.ReturnToHandBlock(p,m); PlayEntity(g,p,m); m.Damage=1; g.AuraUpdate();
        Core2026Proof.Check(m.AttackDamage==atk+bonus,"bounce restores printed Enrage");
        Effects.Attack_N(2).ApplyTo(m); m.TakeFullHeal(p.Hero); g.AuraUpdate();
        Core2026Proof.Check(m.AttackDamage==atk+2,"healing preserves independent Attack buff");
    }
    private static void Survivalist(Game g, Controller p, string id)
    {
        var m=(Minion)Play(g,p,id); Core2026Proof.Check(m.IsImmune,"alone Immune");
        var other=Summon(p,"CS2_182"); Core2026Proof.Check(!m.IsImmune,"other minion disables Immune");
        Kill(g,other); Core2026Proof.Check(m.IsImmune,"last other departure restores Immune");
        m.IsImmune=true; var blocker=Summon(p,"CS2_182");
        Core2026Proof.Check(m.IsImmune,"independent Immune survives condition loss"); m.IsImmune=false;
        Core2026Proof.Check(!m.IsImmune,"independent source removal"); Kill(g,blocker);
        Future(g,next=> {var survivor=(Minion)next.IdEntityDic[m.Id]; survivor.Silence(); next.AuraUpdate();
            Core2026Proof.Check(!survivor.IsImmune,"Silence clears condition after clone/snapshot");});
    }
    private static void Arachnathid(Game g, Controller p, string id)
    {
        var friend=Summon(p,"CS2_182"); var source=(Minion)Play(g,p,id);
        Core2026Proof.Check(source.Poisonous && friend.Poisonous && !Summon(p.Opponent,"CS2_182").Poisonous,"friendly aura includes source only own seat");
        var enemy=Summon(p.Opponent,"CS2_182"); enemy.BaseHealth=20;
        enemy.TakeDamage(friend,1); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(enemy.Zone==p.Opponent.GraveyardZone,"aura poison lethal");
        var second=Summon(p,id); friend.Silence(); g.AuraUpdate();
        Core2026Proof.Check(friend.Poisonous,"recipient Silence retains external Poisonous");
        source.Silence(); g.AuraUpdate(); Core2026Proof.Check(friend.Poisonous,"second aura survives first removal");
        Future(g,next=> {var last=(Minion)next.IdEntityDic[second.Id]; Kill(next,last);
            Core2026Proof.Check(!((Minion)next.IdEntityDic[friend.Id]).Poisonous,"last aura departure clears Poisonous");});
        Generic.ReturnToHandBlock(p,friend); Core2026Proof.Check(!friend.Poisonous,"leave-play drops aura contribution");
    }
    private static void Seer(Game g, Controller p, string id)
    {
        var m=(Minion)Play(g,p,id); Core2026Proof.Check(p.CurrentSpellPower==0,"undamaged no spell damage");
        m.Damage=1; g.AuraUpdate(); Core2026Proof.Check(p.CurrentSpellPower==2,"damaged spell damage two");
        m.TakeFullHeal(p.Hero); g.AuraUpdate(); Core2026Proof.Check(p.CurrentSpellPower==0,"heal clears spell damage");
        m.Damage=1; g.AuraUpdate();
        Future(g,next=> {var seer=(Minion)next.IdEntityDic[m.Id]; seer.Silence(); next.AuraUpdate();
            Core2026Proof.Check(next.Player1.CurrentSpellPower==0 && seer.SpellPower==0,"Silence cannot leave negative spell damage");});
        Generic.ReturnToHandBlock(p,m); PlayEntity(g,p,m); m.Damage=1; g.AuraUpdate(); Kill(g,m);
        Core2026Proof.Check(p.CurrentSpellPower==0,"departed Seer contributes nothing");
    }
    // combat/mage-raincaller-entry-window.yaml: approved medium-confidence entry-local observation.
    private static void Unpinned_EntryWindow(Game g, Controller p, string id)
    {
        Play(g,p,"CS2_024",p.Opponent.Hero);
        var m=(Minion)Play(g,p,id); p.Opponent.Hero.Armor=20;
        var spell=Add(p,"CS2_024"); var shield=Summon(p.Opponent,"CS2_182"); shield.HasDivineShield=true;
        shield.TakeDamage(spell,1); p.Opponent.Hero.IsImmune=true;
        p.Opponent.Hero.TakeDamage(spell,1); p.Opponent.Hero.IsImmune=false;
        shield.TakeDamage(spell,0); shield.TakeDamage(p.Hero,1);
        Core2026Proof.Check(m.AttackDamage==1,"Shield, Immune, zero and non-spell damage leave trigger unused");
        Play(g,p,"CS2_024",p.Opponent.Hero); Core2026Proof.Check(m.AttackDamage==3,"Armor-only positive spell damage qualifies after entry");
        var fresh=Summon(p,id); Play(g,p,"CS2_024",p.Opponent.Hero);
        Core2026Proof.Check(m.AttackDamage==3 && fresh.AttackDamage==3,"independent entry-local first event");
        Future(g,next=> {
            var caller=(Minion)next.IdEntityDic[m.Id]; var other=(Minion)next.IdEntityDic[fresh.Id];
            var copied=(Minion)Generic.Copy(next.Player1,caller,caller,Zone.PLAY,true);
            var copiedAgain=(Minion)Generic.Copy(next.Player1,caller,copied,Zone.PLAY,true);
            next.Player2.Hero.TakeDamage((IPlayable)next.IdEntityDic[spell.Id],1);
            Core2026Proof.Check(caller.AttackDamage==3 && other.AttackDamage==3 && copied.AttackDamage==3 && copiedAgain.AttackDamage==3,"battlefield copy retains used stamp and Attack buff");
            End(next); next.Player2.Hero.TakeDamage((IPlayable)next.IdEntityDic[spell.Id],1);
            Core2026Proof.Check(caller.AttackDamage==5 && other.AttackDamage==5 && copied.AttackDamage==5 && copiedAgain.AttackDamage==5,"all instances reset on opponent turn, including a copy of a copied lock");
            caller.Silence(); next.AuraUpdate(); next.Player2.Hero.TakeDamage((IPlayable)next.IdEntityDic[spell.Id],1);
            Core2026Proof.Check(caller.AttackDamage==1,"Silence removes buffs and observer");
        });
        End(g); p.Opponent.Hero.TakeDamage(spell,1);
        Generic.ReturnToHandBlock(p,fresh); PlayEntity(g,p,fresh); p.Opponent.Hero.TakeDamage(spell,1);
        Core2026Proof.Check(fresh.AttackDamage==3,"same-turn bounce clears lock and previous Attack buffs");
    }
    // combat/mage-flames-infinity-number.yaml: approved finite max-int assumption; retail counter wrap deliberately saturated.
    private static void Unpinned_InfiniteNumericDamage(Game g, Controller p, string id)
    {
        Play(g,p,id); End(g); Core2026Proof.Check(p.SecretZone.Count==1,"own turn end does not fire");
        End(g); Core2026Proof.Check(p.SecretZone.Count==1,"empty board keeps Secret armed"); End(g);
        var low=Summon(p.Opponent,"CS2_182"); low.BaseHealth=1;
        var victim=Summon(p.Opponent,"CS2_182"); victim.BaseHealth=10; victim.Damage=1;
        var tie=Summon(p.Opponent,"CS2_182"); tie.BaseHealth=9;
        var secret=(Spell)p.SecretZone.Single(); secret.HasLifeSteal=true; p.Hero.Damage=10;
        Summon(p,"CS2_052"); // Spell Damage cannot overflow a saturated proposal.
        int[] candidates={victim.Id,tie.Id}; var oracle=g.CloneExact(history:true);
        int expected=candidates[oracle.Random.Next(2)];
        Future(g,next=> {
            End(next); var killed=(Minion)next.IdEntityDic[expected];
            Core2026Proof.Check(killed.Damage==int.MaxValue && killed.Zone==next.Player2.GraveyardZone && next.Player1.SecretZone.Count==0,"highest Health random tie takes full finite damage");
            Core2026Proof.Check(next.Player1.Hero.Damage==0 && next.HistoryFacts.Query.Damage().Any(f=>f.SourceId==secret.Id && f.HealthDamage==int.MaxValue),"overkill reaches observers and gained Lifesteal heals normally");
            Core2026Proof.Check(((Minion)next.IdEntityDic[low.Id]).Damage==0,"lower Health excluded");
        });
        foreach(string modifier in new[]{"TIME_060","CATA_208"})
        {
            var branch=NewGame(); var owner=branch.Player1; Play(branch,owner,id); End(branch);
            var amplified=Summon(owner.Opponent,modifier); amplified.Damage=1;
            End(branch); Core2026Proof.Check(amplified.Zone==owner.Opponent.GraveyardZone && amplified.Damage==int.MaxValue,
                "incoming multiplier/addition saturates instead of wrapping: "+modifier);
        }
        foreach(bool immune in new[]{false,true})
        {
            var branch=NewGame(); var owner=branch.Player1; Play(branch,owner,id); End(branch);
            var protectedMinion=Summon(owner.Opponent,"CS2_182"); protectedMinion.HasDivineShield=true; protectedMinion.IsImmune=immune;
            End(branch); Core2026Proof.Check(owner.SecretZone.Count==0 && protectedMinion.Damage==0 && protectedMinion.HasDivineShield==immune,"ordinary Immune-before-Shield prevention; Secret still consumed");
        }
    }
    // combat/bralma-split-damage-allocation.yaml: approved assumption; second Bralma is an explicit additive extension.
    private static void Unpinned_SplitPerHit(Game g, Controller p, string id)
    {
        Summon(p,"TLC_228"); Play(g,p,"TLC_249"); End(g); End(g);
        int before=p.Opponent.Hero.Damage; int cursor=g.HistoryFacts.Count;
        Play(g,p,id); Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id=="TLC_249")==3,"Battlecry summons two Cinders");
        var hits=g.HistoryFacts.Query.Damage().Where(f=>f.Sequence>cursor).ToArray();
        Core2026Proof.Check(p.Opponent.Hero.Damage-before==12 && hits.Length==6 && hits.All(f=>f.Amount==2 && g.IdEntityDic[f.SourceId].Card.Id=="TLC_249"),"three living Cinders: exactly two hits of two each, actual Cinder attribution");
        foreach(int bralmas in new[]{0,1,2})
        {
            var branch=NewGame(); var owner=branch.Player1;
            for(int i=0;i<bralmas;i++) Summon(owner,"TLC_228");
            var cinder=Summon(owner,"TLC_249");
            cinder.ActivateTask(PowerActivation.DEATHRATTLE); branch.ProcessTasks();
            var damage=branch.HistoryFacts.Query.Damage().ToArray();
            Core2026Proof.Check(damage.Length==2 && damage.All(f=>f.Amount==1+bralmas),"Unpinned additive Bralma extension keeps two-hit budget");
        }
        foreach(int seed in new[]{1,2,3})
        {
            var branch=NewGame(); var owner=branch.Player1; Summon(owner,"TLC_228"); var cinder=Summon(owner,"TLC_249");
            var left=Summon(owner.Opponent,"CS2_182"); left.BaseHealth=100;
            var right=Summon(owner.Opponent,"CS2_200"); right.BaseHealth=100;
            int[] candidates={owner.Opponent.Hero.Id,left.Id,right.Id}; branch.SetRandomSeed(seed);
            var oracle=branch.CloneExact(history:true); int[] expected={candidates[oracle.Random.Next(3)],candidates[oracle.Random.Next(3)]};
            Future(branch,next=> {
                ((Minion)next.IdEntityDic[cinder.Id]).ActivateTask(PowerActivation.DEATHRATTLE); next.ProcessTasks();
                var actual=next.HistoryFacts.Query.Damage().ToArray();
                Core2026Proof.Check(actual.Select(f=>f.VictimId).SequenceEqual(expected) && actual.All(f=>f.Amount==2),"seeded independent per-hit Cinder allocation across enemy hero/minions");
            });
        }
        var noKindred=NewGame(); Play(noKindred,noKindred.Player1,id);
        Core2026Proof.Check(noKindred.Player1.BoardZone.Count==3 && noKindred.Player2.Hero.Damage==0,"unmet Kindred summons without activation");
        var full=NewGame(); var f=full.Player1; Play(full,f,"TLC_249"); End(full); End(full);
        for(int i=0;i<5;i++) Summon(f,"CS2_182"); Play(full,f,id);
        Core2026Proof.Check(f.BoardZone.Count==7 && f.BoardZone.Count(m=>m.Card.Id=="TLC_249")==1 && f.Opponent.Hero.Damage==2,"full board retriggers existing Cinder despite no summon space");
    }
    private static void Fandral(Game g, Controller p, string id)
    {
        var a=(Minion)Play(g,p,id); var b=Summon(p,id);
        Future(g,next=> {
            var owner=next.Player1; var first=(Minion)next.IdEntityDic[a.Id]; var remaining=(Minion)next.IdEntityDic[b.Id];
            first.Silence(); next.AuraUpdate();
            Core2026Proof.Check(owner.ChooseBoth && !owner.Opponent.ChooseBoth,"second source retained only for its controller");
            Play(next,owner,"EX1_160");
            Core2026Proof.Check(owner.BoardZone.Any(m=>m.Card.Id=="EX1_160t") && remaining.AttackDamage==4,"both Choose One effects");
            Kill(next,remaining); Core2026Proof.Check(!owner.ChooseBoth,"last source departure");
        });
    }
    private static void Prepper(Game g, Controller p, string id)
    {
        PoolPilotProof.EmptyHand(p); Play(g,p,id); Core2026Proof.Check(p.Hero.IsImmune,"Outcast grants Immune");
        Future(g,next=> {
            var owner=next.Player1; var provider=Summon(owner,"CORE_CATA_001");
            End(next); Core2026Proof.Check(owner.Hero.IsImmune,"persists opponent turn");
            End(next); Core2026Proof.Check(owner.Hero.IsImmune,"independent ongoing source survives timed expiry");
            Kill(next,provider); Core2026Proof.Check(!owner.Hero.IsImmune,"last immunity source removed");
        });
        PoolPilotProof.EmptyHand(p); Add(p,"CS2_182"); var middle=Add(p,id); Add(p,"CS2_182"); PlayEntity(g,p,middle);
        Core2026Proof.Check(!p.Hero.IsImmune,"middle hand does not grant Outcast");
    }
}
