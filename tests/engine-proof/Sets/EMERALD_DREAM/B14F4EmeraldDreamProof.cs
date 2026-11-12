using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14F4EmeraldDreamProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_480", Scenario);
        StandardSetsProof.Case("FIR_959", Unpinned_Fyrakk);
    }
    private static void Scenario(Game g, Controller p, string id)
    {
        p.Opponent.Hero.BaseHealth=200;
        var beast=Summon(p,"CS2_171"); var body=(Minion)Play(g,p,id);
        p.Opponent.Hero.TakeDamage(beast,2); Core2026Proof.Check(p.Opponent.Hero.Damage==4,"Beast damage doubled");
        Kill(g,beast); p.Opponent.Hero.TakeDamage(beast,2);
        Core2026Proof.Check(p.Opponent.Hero.Damage==8,"departed Beast source still doubles");
        body.Silence(); p.Opponent.Hero.TakeDamage(beast,2);
        Core2026Proof.Check(p.Opponent.Hero.Damage==10,"removed provider no longer doubles");
    }

    private static void Unpinned_Fyrakk(Game g, Controller p, string id)
    {
        p.Hero.BaseHealth=200; p.Opponent.Hero.BaseHealth=200;
        TimeTravelS2Proof.WithCards(g,new[]{"CORE_CS2_029"},()=> {
            var body=(Minion)Play(g,p,id);
            Core2026Proof.Check(p.Opponent.Hero.Damage==18 && p.Hero.Damage==0 && p.UsedMana==10,
                "Unpinned_Fyrakk: printed budget casts three Fireballs at legal enemies without paying cast Mana");
            body.TakeDamage(Add(p,"CORE_CS2_029"),6);
            Core2026Proof.Check(body.Damage==0,"Fire immunity also prevents friendly direct damage");
            body.Silence(); body.TakeDamage(Add(p,"CORE_CS2_029"),1);
            Core2026Proof.Check(body.Damage==1,"Silence removes Fire protection");
        });
        foreach(bool reversed in new[]{false,true})
        {
            StandardSetsProof.ParkBoard(p.Opponent);
            var left=Summon(p.Opponent,"CS2_182"); var right=Summon(p.Opponent,"CS2_182");
            left.BaseHealth=200; right.BaseHealth=200;
            if(reversed) { p.Opponent.SetasideZone.Add(p.Opponent.BoardZone.Remove(left)); p.Opponent.BoardZone.Add((Minion)p.Opponent.SetasideZone.Remove(left)); }
            TimeTravelS2Proof.WithCards(g,new[]{"CORE_CS2_029","CORE_CS2_032"},()=> {
                var body=Summon(p,id); var expected=g.CloneExact().Random; int remaining=15;
                var targets=p.Opponent.BoardZone.Cast<ICharacter>().Append(p.Opponent.Hero).ToArray();
                var damage=targets.Select(t=>t.Damage).ToArray();
                while(remaining>0)
                {
                    var candidates=OfflineCardPools.Candidates(g,p.HeroClass,OfflinePoolFilter.Random(type:CardType.SPELL,spellSchool:2,maxCost:remaining),115756);
                    if(!candidates.Any(c=>c.Cost>0)) break;
                    var chosen=candidates[expected.Next(candidates.Count)]; remaining-=chosen.Cost;
                    if(chosen.Id=="CORE_CS2_029") damage[expected.Next(targets.Length)]+=6;
                    else for(int i=0;i<targets.Length-1;i++) damage[i]+=5;
                }
                PoolPilotProof.Resolve(g,body);
                Core2026Proof.Check(targets.Select(t=>t.Damage).SequenceEqual(damage) && g.Random.DrawCount==expected.DrawCount,
                    "Unpinned_Fyrakk: exact seeded card and target picks after reordered board; printed budget and RNG consumption");
            });
        }
        TimeTravelS2Proof.WithCards(g,System.Array.Empty<string>(),()=> {
            long draws=g.Random.DrawCount; var body=Summon(p,id);
            PoolPilotProof.Resolve(g,body);
            Core2026Proof.Check(g.Random.DrawCount==draws,"empty Fire pool terminates without RNG");
        });
        var lethal=g.CloneExact(history:true); var owner=lethal.Player1;
        StandardSetsProof.ParkBoard(owner);
        TimeTravelS2Proof.WithCards(lethal,new[]{"CORE_CS2_029"},()=> {
            StandardSetsProof.ParkBoard(owner.Opponent); owner.Opponent.Hero.Health=6;
            int casts=lethal.HistoryFacts.Query.Casts().Count(f=>f.Outcome==HistoryCastOutcome.Completed);
            Play(lethal,owner,id);
            Core2026Proof.Check(lethal.HistoryFacts.Query.Casts().Count(f=>f.Outcome==HistoryCastOutcome.Completed)==casts+1,
                "Unpinned_Fyrakk stops after one lethal nested cast");
        });
    }
}
