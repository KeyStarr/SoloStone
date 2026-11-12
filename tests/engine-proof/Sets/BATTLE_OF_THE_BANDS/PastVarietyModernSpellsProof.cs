using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PastVarietyModernProof
{
    private static void SpellCase(Game g,string id,bool pupil,int branch=0,int mana=10,int boardSize=1,int handSize=2)
    {
        var p=g.CurrentPlayer;
        StandardSetsProof.ParkBoard(p);StandardSetsProof.ParkBoard(p.Opponent);
        foreach(var e in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(e));
        var friend=Core2026Proof.Summon(p,"CS2_182");
        for(int i=1;i<boardSize;i++)Core2026Proof.Summon(p,"CS2_182");
        var enemy=Core2026Proof.Summon(p.Opponent,"CS2_182");
        var hand=Generic.DrawCard(p, Cards.FromId("CS2_182"));
        for(int i=1;i<handSize;i++)Generic.DrawCard(p,Cards.FromId("CS2_029"));
        var deck=p.DeckZone.OfType<Minion>().Select(m=>m.Id).ToArray();
        IPlayable source;
        if(pupil) {
            // Exercise actual Mentor delivery/capture, including a catalog not yet carrying the new member.
            var mentor=Generic.DrawCard(p,Cards.FromId("TIME_704"));Play(g,p,mentor);
            Core2026Proof.Check(p.Choice!=null && p.Choice.Choices.Count==1,"singleton exact Mentor bucket");
            var offered=g.IdEntityDic[p.Choice.Choices[0]];
            Core2026Proof.Check(offered.Card.Id==id,"Mentor offers actual modern candidate");
            Core2026Proof.Check(Answer(g,offered.Id),"Mentor teaching answer");
            source=p.HandZone.Single(e=>e.Card.Id=="TIME_704t");
            Core2026Proof.Check(PayloadRules.Entries(source,1).Single().Card.Id==id,"Pupil stores exact chosen definition");
        } else source=Generic.DrawCard(p,Definition(g,p,id));
        int beforeBoard=p.BoardZone.Count, beforeHand=p.HandZone.Count-1, beforeDeck=p.DeckZone.Count;
        int predictedBranch=branch;
        if(pupil && id=="ETC_373") predictedBranch=g.Random.Clone().Next(1,3);
        if(pupil && id=="ETC_373") Core2026Proof.Check(predictedBranch==branch,"seed selects requested Pupil Drum branch");
        Resume(g,next=> {
            var owner=next.CurrentPlayer;var actual=next.IdEntityDic[source.Id];owner.BaseMana=mana;owner.UsedMana=0;
            Core2026Proof.Check(next.Process(PlayCardTask.Any(owner,actual,chooseOne:pupil?0:branch)),id+" direct/Pupil real cast");
            int existing=beforeBoard+(pupil?1:0);var ally=(Minion)next.IdEntityDic[friend.Id];var opponent=(Minion)next.IdEntityDic[enemy.Id];
            Core2026Proof.Check(next.HistoryFacts.Query.Casts().Any(c=>c.DbfId==Cards.FromId(id).AssetId&&c.SpellSchool==Cards.FromId(id)[GameTag.SPELL_SCHOOL]),"actual spell cast receipt "+id);
            switch(id) {
                case "ONY_021":
                    var whelps=owner.BoardZone.Where(m=>m.Card.Id=="ONY_001t").ToArray();
                    Core2026Proof.Check(owner.BoardZone.Count==7 && whelps.Length==7-existing && whelps.All(m=>m.AttackDamage==2&&m.Health==1&&m[GameTag.RUSH]==1),"Scale fills only empty slots with real 2/1 Rush Whelps");
                    foreach(var w in whelps) Core2026Proof.Check(owner.Options().Any(a=>a.Source?.Id==w.Id&&a.Target?.Id==opponent.Id)
                        && !owner.Options().Any(a=>a.Source?.Id==w.Id&&a.Target?.Id==owner.Opponent.Hero.Id),"new Whelp can Rush minion, never hero");
                    break;
                case "ETC_373":
                    if(predictedBranch==1) {
                        var treants=owner.BoardZone.Where(m=>m.Card.Id=="ETC_373t").ToArray();
                        Core2026Proof.Check(treants.Length==Math.Min(5,7-existing) && treants.All(m=>m.AttackDamage==2&&m.Health==2&&!m.HasTaunt),"Drum summon branch actual 2/2 Treants");
                        Core2026Proof.Check(ally.AttackDamage==4,"summon branch never buffs old minions");
                    } else {
                        Core2026Proof.Check(owner.BoardZone.Count==existing&&ally.AttackDamage==6&&ally.Health==9&&ally.HasTaunt,"Drum buff branch +2/+4 Taunt, no Treants");
                        if(pupil)Core2026Proof.Check(((Minion)actual).AttackDamage==4&&((Minion)actual).Health==6&&((Minion)actual).HasTaunt,"Pupil participates in its own Drum buff");
                        ally.Silence();next.DeathProcessingAndAuraUpdate();Core2026Proof.Check(ally.AttackDamage==4&&ally.Health==5&&!ally.HasTaunt,"Drum Silence removes stats/keyword");
                    }
                    break;
                case "TSC_656":
                    int size=Math.Min(10,beforeHand+3);
                    Core2026Proof.Check(owner.HandZone.Count==size&&owner.DeckZone.Count==beforeDeck-3,"Miracle draws three, including burns");
                    var plants=owner.BoardZone.Where(m=>m.Card.Id=="TSC_656t").ToArray();
                    Core2026Proof.Check(plants.Length==(existing==7?0:1),"Miracle respects full board");
                    if(plants.Length>0) {
                        var plant=plants.Single();Core2026Proof.Check(plant.HasTaunt&&plant.AttackDamage==size&&plant.Health==size,"Miracle uses post-draw hand size");
                        Generic.DrawCard(owner,Cards.FromId("CS2_029"));Core2026Proof.Check(plant.AttackDamage==size,"Plant stats do not track future hand changes");
                        plant.Silence();next.DeathProcessingAndAuraUpdate();Core2026Proof.Check(plant.AttackDamage==1&&plant.Health==1&&!plant.HasTaunt,"Plant Silence restores printed 1/1");
                    }
                    break;
                case "SCH_609":
                    Core2026Proof.Check(ally.AttackDamage==8&&ally.Health==9,"Survival buffs battlefield +4/+4");
                    Core2026Proof.Check(((Minion)next.IdEntityDic[hand.Id]).AttackDamage==8&&((Minion)next.IdEntityDic[hand.Id]).Health==9,"Survival buffs hand minion");
                    Core2026Proof.Check(deck.All(n=>((Minion)next.IdEntityDic[n]).AttackDamage==8&&((Minion)next.IdEntityDic[n]).Health==9),"Survival buffs every deck minion");
                    Core2026Proof.Check(owner.HandZone.Where(c=>c.Card.Type==CardType.SPELL).All(c=>c.Cost==4),"Survival ignores spells");
                    Resume(next,fork=> {
                        var survivor=(Minion)fork.IdEntityDic[friend.Id];Core2026Proof.End(fork);
                        Core2026Proof.Check(survivor.AttackDamage==8&&survivor.Health==9,"Survival buff persists across turn");
                        survivor.Silence();fork.DeathProcessingAndAuraUpdate();Core2026Proof.Check(survivor.AttackDamage==4&&survivor.Health==5,"Survival Silence removes layered buff");
                    });
                    break;
                case "DEEP_028":
                    int gained=Math.Min(3,owner[GameTag.MAXRESOURCES]-mana),overflow=3-gained;
                    Core2026Proof.Check(owner.BaseMana==mana+gained&&owner.UsedMana==(pupil?2:7)+gained,"Cluster gains empty, not full crystals");
                    var elementals=owner.BoardZone.Where(m=>m.Card.Id=="DEEP_028t").ToArray();
                    Core2026Proof.Check(elementals.Length==Math.Min(overflow,7-existing)&&elementals.All(m=>m.HasTaunt&&m.AttackDamage==3&&m.Health==7&&m.IsRace(Race.ELEMENTAL)),"Cluster excess crystals summon real 3/7 Taunt Elementals");
                    break;
            }
            Core2026Proof.Check(opponent.AttackDamage==4&&opponent.Health==5&&!opponent.HasTaunt,"modern spells never buff/summon for opponent");
            Core2026Proof.Check(owner.Choice==null&&!next.Resolution.HasSuspendedEffect,"modern cast never stalls");
            Core2026Proof.Check(Canonical.State(next)==Canonical.State(next.CloneExact(history:true)),"effect result exact clone "+id);
            Core2026Proof.Check(Canonical.State(next)==Canonical.State(Game.LoadSnapshotWithPools(next.SaveSnapshot(),next.OfflinePools)),"effect result snapshot "+id);
        });
    }
    private static void Unpinned_MentorCasts()
    {
        foreach(string id in Ids.Skip(3)) {
            var pools=CandidatePools(id);
            if(id=="ETC_373") {
                var seen=new HashSet<int>();
                for(int seed=1;seen.Count<2&&seed<40;seed++) {
                    // Probe the Pupil branch without assuming that singleton Discover consumes RNG.
                    var probe=Fresh(seed,pools);var mentor=Core2026Proof.Play(probe,probe.Player1,"TIME_704");
                    Core2026Proof.Check(Answer(probe,probe.Player1.Choice.Choices[0]),"probe Mentor answer");
                    int branch=probe.Random.Clone().Next(1,3);if(!seen.Add(branch))continue;
                    SpellCase(Fresh(seed,pools),id,true,branch);
                }
                Core2026Proof.Check(seen.Count==2,"Pupil seeded automatic casts cover both Drum branches");
            } else SpellCase(Fresh(23,pools),id,true);
            Console.WriteLine("PASS Unpinned_MentorCasts "+id);
        }
        foreach(int mana in new[]{7,8,9,10}) foreach(bool pupil in new[]{false,true})
            SpellCase(Fresh(31,pupil?CandidatePools("DEEP_028"):null),"DEEP_028",pupil,mana:mana);
        foreach(int mana in new[]{17,18,19,20}) {
            var expanded=Fresh();expanded.Player1[GameTag.MAXRESOURCES]=20;
            SpellCase(expanded,"DEEP_028",false,mana:mana);
        }
        var fatigue=Fresh();var caster=fatigue.Player1;
        foreach(var e in caster.DeckZone.ToArray())caster.SetasideZone.Add(caster.DeckZone.Remove(e));
        var growth=Core2026Proof.Add(caster,"TSC_656");Play(fatigue,caster,growth);
        Core2026Proof.Check(caster.Hero.Damage==6&&caster.HandZone.Count==0&&caster.BoardZone.Count==0
            &&caster.GraveyardZone.Any(e=>e.Card.Id=="TSC_656t"),"Miracle fatigue draws then zero-hand 0/0 Plant dies");
        foreach(string id in new[]{"TSC_656","DEEP_028"})
            SpellCase(Fresh(),id,false,id=="ETC_373"?1:0,boardSize:7);
        SpellCase(Fresh(),"TSC_656",false,handSize:9);
        var pending=Fresh(1,CandidatePools());Core2026Proof.Play(pending,pending.Player1,"TIME_704");
        Core2026Proof.Check(pending.GetPendingDecisions().Any()&&pending.Process(ConcedeTask.Any(pending.Player1))&&pending.State==State.COMPLETE,"Concede works during Mentor decision");
    }
}
