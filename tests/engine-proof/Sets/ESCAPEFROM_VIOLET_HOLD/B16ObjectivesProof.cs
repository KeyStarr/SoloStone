using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
internal static class B16ObjectivesTrialProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_405",Unpinned_DraftAndLengths);
    private static void Pick(Game g,int id) => CthunRulesProof.PickTarget(g,id);
    private static void Unpinned_DraftAndLengths(Game g,Controller p,string id)
    {
        var candidates=Enumerable.Range(1,9).Select(i=>Cards.FromId("CAP_405t"+i).AssetId).ToList();
        var rng=g.Random.Clone();var expected=new List<int>();
        for(int i=0;i<3;i++){int pick=rng.Next(candidates.Count);expected.Add(candidates[pick]);candidates.RemoveAt(pick);}
        Play(g,p,id);
        Core2026Proof.Check(p.Choice.Choices.Select(e=>g.IdEntityDic[e].Card.AssetId).SequenceEqual(expected),"first draft exact seeded order of three distinct components");
        int first=g.IdEntityDic[p.Choice.Choices[1]].Card.AssetId;
        Pick(g,p.Choice.Choices[1]);
        Core2026Proof.Check(p.Choice.Choices.Select(e=>g.IdEntityDic[e].Card.AssetId).Distinct().Count()==3 && p.Choice.Choices.All(e=>g.IdEntityDic[e].Card.AssetId!=first),"second offer excludes only chosen component");
        int second=g.IdEntityDic[p.Choice.Choices[0]].Card.AssetId;Pick(g,p.Choice.Choices[0]);
        Core2026Proof.Check(p.Choice.Choices.Select(e=>g.IdEntityDic[e].Card.Id).SequenceEqual(new[]{"CAP_405tb1b","CAP_405tb2b","CAP_405tb3b"}),"three exact length choices");
        CthunRulesProof.Pick(g,"CAP_405tb3b");var made=p.HandZone.Single();
        Core2026Proof.Check(made[GameTag.MODULAR_ENTITY_PART_1]==first && made[GameTag.MODULAR_ENTITY_PART_2]==second && made.Cost==0,"saved ordered pair, zero-cost Unending");
        p.BoardZone.Single().Silence();B16ObjectivesProof.Cycle(g);
        var copy=Generic.Copy(p,made,made,Zone.HAND);
        Core2026Proof.Check(copy[GameTag.MODULAR_ENTITY_PART_1]==first && copy[GameTag.MODULAR_ENTITY_PART_2]==second,"hand copy retains composition");
        foreach(var branch in new[]{g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            var owner=branch.Player1;var trial=branch.IdEntityDic[made.Id];
            Core2026Proof.Check(trial[GameTag.QUEST_PROGRESS]==0,"held trial never ticks");
            PlayEntity(branch,owner,trial);
            for(int i=0;i<3;i++)B16ObjectivesProof.Cycle(branch);
            Core2026Proof.Check(ObjectiveRules.Remaining(trial)==1 && trial.Zone==owner.SecretZone,"four subsequent owner starts");
            B16ObjectivesProof.Cycle(branch);Core2026Proof.Check(trial.Zone==owner.GraveyardZone && ObjectiveRules.CompletedRituals(owner)==0,"Trial expires once, no Ritual completion");
        }
        Unpinned_ComponentsAndOrder();Unpinned_ForcedPairingSnapshot();Unpinned_ShivDormantExclusions();
    }
    internal static IPlayable Trial(Controller p,string id,string first,string second)
    {
        var trial=Add(p,id);trial[GameTag.MODULAR_ENTITY_PART_1]=Cards.FromId(first).AssetId;
        trial[GameTag.MODULAR_ENTITY_PART_2]=Cards.FromId(second).AssetId;return trial;
    }
    private static void Unpinned_ShivDormantExclusions()
    {
        var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
        var sleeper=Summon(p,"TIME_063");var active=Summon(p,"CS2_182");
        var enemy=Summon(p.Opponent,"CS2_182");var held=Add(p,"TIME_063");var deck=p.DeckZone[0];
        var trial=Trial(p,"CAP_405tb2","CAP_405t5","CAP_405t8");PlayEntity(g,p,trial);
        foreach(var branch in new[] {g.CloneExact(true),Game.LoadSnapshot(g.SaveSnapshot()),g}) {
            B16ObjectivesProof.Cycle(branch);
            var sleeping=(Minion)branch.IdEntityDic[sleeper.Id];
            Core2026Proof.Check(sleeping.IsDormant && sleeping.AttackDamage==8 && sleeping.Health==8,
                "Unpinned_ShivDormantExclusions skips sleeping board minion");
            Core2026Proof.Check(((Minion)branch.IdEntityDic[active.Id]).AttackDamage==7
                && ((Minion)branch.IdEntityDic[held.Id]).AttackDamage==11
                && ((Minion)branch.IdEntityDic[deck.Id]).AttackDamage==1
                && ((Minion)branch.IdEntityDic[enemy.Id]).AttackDamage==4,
                "Shiv retains friendly active/hand buffs, excludes deck and opponent");
            DormantRules.Awaken(sleeping);
            Core2026Proof.Check(sleeping.AttackDamage==8 && sleeping.Health==8,"Shiv leaves no hidden buff after awakening");
        }
    }
    private static void Unpinned_ComponentsAndOrder()
    {
        var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
        foreach(var held in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(held));
        var minion=Add(p,"CS2_182");p.Hero.Damage=20;
        var immediate=Trial(p,"CAP_405tb1","CAP_405t4","CAP_405t7");PlayEntity(g,p,immediate);
        Core2026Proof.Check(p.HandZone.Count==4 && minion.Cost==2 && p.HandZone.Where(x=>x!=minion).All(x=>x.Cost==0),"draw-three before hand reduction");
        var delayed=Trial(p,"CAP_405tb2","CAP_405t5","CAP_405t8");PlayEntity(g,p,delayed);B16ObjectivesProof.Cycle(g);
        Core2026Proof.Check(((Minion)minion).AttackDamage==7 && p.Hero.Damage==8,"Grueling next-start hand buff then heal");
        foreach(var held in p.Opponent.HandZone.ToArray())p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(held));
        var enemy=Summon(p.Opponent,"CS2_200");var stolen=Add(p.Opponent,"CS2_029");stolen.Cost=1;
        PlayEntity(g,p,Trial(p,"CAP_405tb1","CAP_405t2","CAP_405t3"));
        Core2026Proof.Check(enemy.Controller==p && stolen.Controller==p && stolen.Cost==1,"control minion and physical hand theft preserves Cost");
        StandardSetsProof.ParkBoard(p);
        PlayEntity(g,p,Trial(p,"CAP_405tb1","CAP_405t9","CAP_405t6"));
        var pool=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.MINION,minCost:3,maxCost:3),127067);
        Console.WriteLine("Trial 3-Cost bucket="+pool.Count);
        Core2026Proof.Check(p.BoardZone.Any(m=>m.Card.Id=="CORE_LOOT_368") && p.BoardZone.Count==4,"Voidlord and three exact-cost summons");
        var voidlord=p.BoardZone.Single(m=>m.Card.Id=="CORE_LOOT_368");Kill(g,voidlord);
        Core2026Proof.Check(p.BoardZone.Count(m=>m.Card.Id=="CS2_065")==3,"Voidlord full deathrattle closure");
    }
    private static void Unpinned_ForcedPairingSnapshot()
    {
        var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
        var bodies=new[]{Summon(p,"CS2_231"),Summon(p,"CS2_231"),Summon(p.Opponent,"CS2_231"),Summon(p.Opponent,"CS2_231")};
        foreach(var m in bodies){m[GameTag.HEALTH]=100;m.IsFrozen=true;}
        var rng=g.Random.Clone();var order=bodies.ToArray();
        for(int i=order.Length-1;i>0;i--){int j=rng.Next(i+1);(order[i],order[j])=(order[j],order[i]);}
        var defenders=order.Select(a=>bodies.Where(m=>m.Id!=a.Id).ToArray()[rng.Next(3)].Id).ToArray();
        PlayEntity(g,p,Trial(p,"CAP_405tb1","CAP_405t1","CAP_405t8"));
        var attacks=g.HistoryFacts.Query.AttacksStarted().ToArray();
        Core2026Proof.Check(attacks.Select(a=>a.AttackerId).SequenceEqual(order.Select(m=>m.Id)) && attacks.Select(a=>a.TargetId).SequenceEqual(defenders),"uniform attacker shuffle and fresh defender order");
        Core2026Proof.Check(bodies.All(m=>m.NumAttacksThisTurn==0 && m.IsFrozen),"forced attacks preserve allowance and Frozen");
    }
}
