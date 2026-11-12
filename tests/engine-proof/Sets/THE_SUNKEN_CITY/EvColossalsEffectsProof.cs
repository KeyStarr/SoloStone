using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static partial class EvColossalsProof
{
    private static void Answer(Game g,int index=0)
    {
        var p=g.Player1.Choice!=null?g.Player1:g.Player2;
        var d=g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[]{p.Choice.Choices[index]}),out var error),"choice accepted: "+error);
    }
    private static void Unpinned_Colaque(Game g,Controller p,Minion body)
    {
        var shell=ColossalRules.AppendagesOf(body).Single();
        Core2026Proof.Check(shell.ZonePosition==body.ZonePosition+1 && body.IsImmune,"Shell right of immune body");
        body.TakeDamage(p.Hero,3);Core2026Proof.Check(body.Damage==0,"Shell prevents damage");
        Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[shell.Id]));
        Core2026Proof.Check(!body.IsImmune && p.Hero.Armor==8,"Shell death grants controller Armor and removes immunity");
        shell=Summon(p,"TSC_026t");shell.Silence();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(body.IsImmune,"unlinked silenced Shell still enables immunity");
        body.Silence();g.DeathProcessingAndAuraUpdate();Core2026Proof.Check(!body.IsImmune,"body Silence removes conditional immunity");
    }
    private static void Unpinned_Gaia(Game g,Controller p,Minion body)
    {
        var enemy=Summon(p.Opponent,"CS2_200");var other=Summon(p.Opponent,"CS2_182");
        var drill=ColossalRules.AppendagesOf(body).First();int face=p.Opponent.Hero.Damage;
        Resume(g,next=>Attack(next,(Minion)next.IdEntityDic[drill.Id],(ICharacter)next.IdEntityDic[enemy.Id]));
        Core2026Proof.Check(other.Damage==1 && p.Opponent.Hero.Damage==face+1,"friendly Mech attack sweeps all enemies even if drill dies");
        var vanilla=Summon(p,"CS2_182");Attack(g,vanilla,other);
        Core2026Proof.Check(p.Opponent.Hero.Damage==face+1,"non-Mech does not trigger Gaia");
        body.Silence();var drill2=p.BoardZone.First(m=>m.Card.Id.StartsWith("TSC_029t"));
        Attack(g,drill2,enemy);Core2026Proof.Check(p.Opponent.Hero.Damage==face+1,"silenced Gaia no sweep");
    }
    private static void Unpinned_Leviathan(Game g,Controller p,Minion body)
    {
        ClassDeck(p);
        foreach(var id in new[]{"CS2_182","CS2_200","EX1_556","CS2_029"})p.DeckZone.Add(Entity.FromCard(p,Cards.FromId(id)));
        var bottom=p.DeckZone.Take(3).Select(c=>c.Id).ToArray();var order=p.DeckZone.Select(c=>c.Id).ToArray();
        var enemy=Summon(p.Opponent,"CS2_200");
        Resume(g,next=>Attack(next,(Minion)next.IdEntityDic[body.Id],(ICharacter)next.IdEntityDic[enemy.Id]));
        Core2026Proof.Check(p.Choice!=null && p.Choice.Choices.Select(n=>g.IdEntityDic[n][DiscoverRealRules.BoundEntity]).SequenceEqual(bottom),"Dredge bottom-first actual entities");
        Resume(g,next=>Answer(next,1));
        Core2026Proof.Check(p.DeckZone.Select(c=>c.Id).SequenceEqual(order.Where(n=>n!=bottom[1]).Append(bottom[1])),"Dredge moves only selected original to top without shuffle");
        var claw=ColossalRules.AppendagesOf(body).Single();int hand=p.HandZone.Count;
        Attack(g,claw,enemy);
        Core2026Proof.Check(p.HandZone.Count==hand+1 && p.HandZone.Last().Id==bottom[1],"Claw draws Dredged entity");
        ClassDeck(p);body.IsExhausted=false;body.NumAttacksThisTurn=0;body.HasDivineShield=true;
        enemy=Summon(p.Opponent,"CS2_200");Attack(g,body,enemy);
        Core2026Proof.Check(p.Choice==null,"empty-deck Dredge cannot stall");
    }
    private static void Unpinned_Behemoth(Game g,Controller p,Minion body)
    {
        p.Hero.Damage=15;
        var enemies=Enumerable.Range(0,3).Select(i=>Summon(p.Opponent,"CS2_182")).ToArray();
        int chosen=g.Random.Clone().Next(enemies.Length);
        enemies[chosen].IsFrozen=true;g.ProcessTasks();enemies[chosen].IsExhausted=true;
        Resume(g,End);
        Core2026Proof.Check(enemies[chosen].Zone==p.Opponent.GraveyardZone && enemies.Where((m,i)=>i!=chosen).All(m=>m.Zone==p.Opponent.BoardZone),"seeded Lure chooses ordered enemy even Frozen/exhausted");
        Core2026Proof.Check(body.Damage==4 && p.Hero.Damage==7,"Behemoth retaliates with Lifesteal");
        Kill(g,body); int damage=p.Hero.Damage;End(g);End(g);
        Core2026Proof.Check(p.Hero.Damage==damage,"orphan Lure no forced combat");End(g);
    }
    private static void Unpinned_Crabatoa(Game g,Controller p,Minion body)
    {
        var claws=ColossalRules.AppendagesOf(body).ToArray();
        Core2026Proof.Check(claws.All(m=>m.AttackDamage==4 && m.IsRush),"both Claws get aura Attack and printed Rush");
        Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[claws[0].Id]));
        Core2026Proof.Check(p.Hero.Weapon.Card.Id=="TSC_937t2" && p.Hero.Weapon.AttackDamage==4 && p.Hero.Weapon.Durability==1,"Claw equips buffed 2/1 weapon");
        p.Hero.Weapon.AttackDamage=1;g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage==3,"Claw stat SET preserves ongoing +2 aura");
        body.Silence();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(claws[1].AttackDamage==2 && p.Hero.Weapon.AttackDamage==1,"aura removal reaches minions and weapon");
    }
    private static void Unpinned_Glugg(Game g,Controller p,Minion body)
    {
        var tail=ColossalRules.AppendagesOf(body).First();tail.AttackDamage=30;tail.BaseHealth=20;tail.Damage=10;
        Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[tail.Id]));
        Core2026Proof.Check(body.AttackDamage==5 && body.BaseHealth==7,"Glugg gains printed 2/2, excludes buffs/damage");
        Kill(g,Summon(p.Opponent,"CS2_200"));Core2026Proof.Check(body.AttackDamage==5,"opponent death excluded");
        body.Silence();g.DeathProcessingAndAuraUpdate();Core2026Proof.Check(body.AttackDamage==3 && body.BaseHealth==5,"Silence clears gained stats");
    }
    private static void Unpinned_Hydralodon(Game g,Controller p,Minion body)
    {
        var head=ColossalRules.AppendagesOf(body).First();
        Core2026Proof.Check(p.BoardZone.Where(Hydralodon.IsHead).All(m=>m.IsRush),"Battlecry grants existing Heads Rush");
        Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[head.Id]));
        Core2026Proof.Check(p.BoardZone.Count(Hydralodon.IsHead)==3 && p.BoardZone.Count(m=>Hydralodon.IsHead(m)&&m.IsRush)==1,"two new Heads have no inherited Rush");
        body.Silence();g.DeathProcessingAndAuraUpdate();head=p.BoardZone.First(Hydralodon.IsHead);Kill(g,head);
        Core2026Proof.Check(p.BoardZone.Count(Hydralodon.IsHead)==4,"silenced Hydralodon still satisfies Head name condition");
        Kill(g,body);int before=p.BoardZone.Count;Kill(g,p.BoardZone.First(Hydralodon.IsHead));
        Core2026Proof.Check(p.BoardZone.Count==before-1,"no body means no regrowth");
    }
    private static void Unpinned_Xhilag(Game g,Controller p,Minion body)
    {
        Core2026Proof.Check(p.BoardZone.Where(XhilagOfTheAbyss.IsStalk).All(m=>m[GameTag.TAG_SCRIPT_DATA_NUM_1]==2),"pinned current Stalk base damage is two");
        var enemies=new[]{Summon(p.Opponent,"CS2_200"),Summon(p.Opponent,"CS2_182")};foreach(var enemy in enemies)enemy.BaseHealth=100;
        int damage=p.Opponent.Hero.Damage;var expected=new int[3];var rng=g.Random.Clone();for(int i=0;i<4;i++)expected[rng.Next(3)]+=2;
        Resume(g,End);
        Core2026Proof.Check(enemies[0].Damage==expected[0] && enemies[1].Damage==expected[1] && p.Opponent.Hero.Damage==damage+expected[2],"four seeded Stalk picks use ordered three-enemy candidates");
        Resume(g,End);
        var stalks=p.BoardZone.Where(XhilagOfTheAbyss.IsStalk).ToArray();
        Core2026Proof.Check(stalks.All(m=>m[GameTag.TAG_SCRIPT_DATA_NUM_1]==3),"owner turn start grows all current Stalks");
        Kill(g,body);stalks[0].Silence();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(stalks[1][GameTag.TAG_SCRIPT_DATA_NUM_1]==3,"growth persists after body leaves");
        damage=p.Opponent.Hero.Damage;int total=enemies.Sum(m=>m.Damage)+damage;
        Resume(g,End);Core2026Proof.Check(enemies.Sum(m=>m.Damage)+p.Opponent.Hero.Damage==total+9,"silenced Stalk stops; surviving Stalks keep growth");End(g);
    }
    private static void Unpinned_Gigafin(Game g,Controller p,Minion body)
    {
        StandardSetsProof.ParkBoard(p);
        var victim=Summon(p.Opponent,"EX1_556");victim.AttackDamage=20;victim.BaseHealth=25;victim.Cost=0;victim.HasTaunt=true;
        body=(Minion)Play(g,p,"TSC_962");
        Core2026Proof.Check(victim.Zone==p.Opponent.GraveyardZone && p.Opponent.BoardZone.Single().Card.Id=="skele21","Devour is destruction and triggers Deathrattle");
        Core2026Proof.Check(PayloadRules.Entries(body,Gigafin.Food).Single().Card.Id=="EX1_556","food stores base definition");
        Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[body.Id]));
        var returned=p.Opponent.BoardZone.Single(m=>m.Card.Id=="EX1_556");Core2026Proof.Check(returned.AttackDamage==returned.Card.ATK && returned.BaseHealth==returned.Card.Health && returned.Damage==0
            && returned.Cost==returned.Card.Cost && !returned.HasTaunt,"spit returns fresh printed stats/cost/keywords");
        StandardSetsProof.ParkBoard(p);StandardSetsProof.ParkBoard(p.Opponent);
        victim=Summon(p.Opponent,"CS2_182");body=(Minion)Play(g,p,"TSC_962");
        var maw=ColossalRules.AppendagesOf(body).Single();Kill(g,maw);Kill(g,body);
        Core2026Proof.Check(p.Opponent.BoardZone.Count==0,"Maw permanently removes stored food before body dies");
        victim=Summon(p.Opponent,"CS2_182");body=(Minion)Play(g,p,"TSC_962");body.Silence();Kill(g,body);
        Core2026Proof.Check(p.Opponent.BoardZone.Count==0,"Silence clears food and Deathrattle");
    }
    private static void Unpinned_Nellie(Game g,Controller p,Minion body)
    {
        var ship=ColossalRules.AppendagesOf(body).Single();
        var selected=new List<string>();
        int count=SabberStoneCore.Model.OfflineCardPools.Candidates(g,p.HeroClass,NellieTheGreatThresher.Pirates(),74894).Count;
        Console.WriteLine("ev-colossals nellie class="+p.HeroClass+" bucket="+count);
        while(p.Choice!=null) {
            var offers=p.Choice.Choices.Select(n=>g.IdEntityDic[n]).ToArray();
            Core2026Proof.Check(offers.Select(c=>c.Card.Id).Distinct().Count()==offers.Length && offers.All(c=>c.Card.IsRace(Race.PIRATE)),"distinct Pirate Discover offers");
            var picked=(Minion)offers[offers.Length-1];selected.Add(picked.Card.Id);
            picked.AttackDamage=30;picked.BaseHealth=40;picked.Cost=0;
            Resume(g,next=>Answer(next,next.Player1.Choice.Choices.Count-1));
        }
        Core2026Proof.Check(selected.Count==(count==0?0:3) && PayloadRules.Entries(ship,NellieTheGreatThresher.Crew).Select(c=>c.Card.Id).SequenceEqual(selected),"three picks saved in Ship order");
        EmptyHand(p);FillHand(p,8);Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[ship.Id]));
        Core2026Proof.Check(p.HandZone.Count==8+Math.Min(2,selected.Count) && p.HandZone.Skip(8).All(c=>c.Cost==1
            && c is Minion m && m.AttackDamage==c.Card.ATK && m.BaseHealth==c.Card.Health),"Ship grants fresh crew definitions at one, full hand burns overflow: hand="+p.HandZone.Count+" costs="+string.Join(",",p.HandZone.Skip(8).Select(c=>c.Cost)));
        // An empty exact class bucket yields no choice and never stalls.
        StandardSetsProof.ParkBoard(p);EmptyHand(p);Pool(g,"TSC_660",0,()=>{body=(Minion)Play(g,p,"TSC_660");Core2026Proof.Check(p.Choice==null,"empty Pirate pool no decision");});
        StandardSetsProof.ParkBoard(p);EmptyHand(p);for(int i=0;i<6;i++)Summon(p,"CS2_182");
        body=(Minion)Play(g,p,"TSC_660");Core2026Proof.Check(p.Choice==null && p.BoardZone.Count==7,"no room for Ship means no crew choice stall");
    }
    private static void Unpinned_Ozumat(Game g,Controller p,Minion body)
    {
        var tentacles=ColossalRules.AppendagesOf(body).ToArray();
        foreach(var tentacle in tentacles.Skip(2))p.SetasideZone.Add(p.BoardZone.Remove(tentacle));
        var enemies=Enumerable.Range(0,4).Select(i=>Summon(p.Opponent,"CS2_182")).ToArray();
        var expected=enemies.ToList();var rng=g.Random.Clone();for(int i=0;i<2;i++)expected.RemoveAt(rng.Next(expected.Count));
        Resume(g,next=>Kill(next,(Minion)next.IdEntityDic[body.Id]));
        Core2026Proof.Check(p.Opponent.BoardZone.Select(m=>m.Id).SequenceEqual(expected.Select(m=>m.Id)),"seeded Ozumat chooses two distinct ordered victims");
    }
    private static void Unpinned_Neptulon(Game g,Controller p,Minion body)
    {
        var enemy=Summon(p.Opponent,"CS2_200");enemy.BaseHealth=40;enemy.AttackDamage=20;
        var hands=ColossalRules.AppendagesOf(body).ToArray();hands[0].IsFrozen=true;g.ProcessTasks();
        Resume(g,next=>Attack(next,(Minion)next.IdEntityDic[body.Id],(ICharacter)next.IdEntityDic[enemy.Id]));
        Core2026Proof.Check(body.Damage==0 && body.NumAttacksThisTurn==1 && enemy.Damage==8 && hands.All(m=>m.Damage==0 && m.NumAttacksThisTurn==0),"Hands replace body with two immune forced attacks; independent budgets unchanged");
        Attack(g,body,enemy);Core2026Proof.Check(body.NumAttacksThisTurn==2 && enemy.Damage==16,"Windfury spends second body attack for two more Hands");
        body.Silence();g.DeathProcessingAndAuraUpdate();hands[0].IsFrozen=false;
        body.NumAttacksThisTurn=0;body.IsExhausted=false;body.AttackDamage=1;enemy.AttackDamage=1;
        Attack(g,body,enemy);Core2026Proof.Check(enemy.Damage==17 && body.Damage==1,"silenced lone body attacks normally");
    }
}
