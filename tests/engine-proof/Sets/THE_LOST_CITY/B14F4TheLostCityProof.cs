using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B14F4TheLostCityProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("DINO_136", Scenario);
        StandardSetsProof.Case("TLC_107", Unpinned_Stormbrewer);
        StandardSetsProof.Case("TLC_228", Unpinned_Bralma);
    }
    private static void Scenario(Game g, Controller p, string id)
    {
        var spell=Add(p,id); PlayEntity(g,p,spell);
        var raptors=p.BoardZone.Where(m=>m.Card.Id=="DINO_136t").ToArray();
        Core2026Proof.Check(raptors.Length==3 && raptors.All(m=>m.IsRush && m[GameTag.IMMUNE_WHILE_ATTACKING]==1),"Outcast Raptors granted attacking immunity");
        End(g); Core2026Proof.Check(raptors.All(m=>m[GameTag.IMMUNE_WHILE_ATTACKING]==0),"Outcast immunity expires");
    }

    private static void Unpinned_Stormbrewer(Game g, Controller p, string id)
    {
        var body=(Minion)Play(g,p,id); End(g); End(g);
        var target=Summon(p.Opponent,"CS2_182"); target.BaseHealth=3;
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p,body,target)) && target.Zone==p.Opponent.GraveyardZone
            && body.NumAttacksThisTurn==0 && !body.IsExhausted && body.Damage==0,"direct prehit lethal preserves ordinary attack");
        var survivor=Summon(p.Opponent,"CS2_182"); survivor.BaseHealth=30;
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p,body,survivor)) && survivor.Damage==6 && body.NumAttacksThisTurn==1,"nonlethal prehit plus combat spends one attack");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        Unpinned_StormbrewerChainedDeaths(g,p);
    }
    private static void Unpinned_StormbrewerChainedDeaths(Game g, Controller p)
    {
        var body=Summon(p,"TLC_107"); body.IsExhausted=false;
        var ghoul=Summon(p.Opponent,"FP1_024"); var gnome=Summon(p.Opponent,"EX1_029");
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p,body,ghoul)) && gnome.Zone==p.Opponent.GraveyardZone
            && body.NumAttacksThisTurn==1 && body.IsExhausted,"two pre-combat death passes consume canceled attack");
    }
    private static void Unpinned_Bralma(Game g, Controller p, string id)
    {
        p.Opponent.Hero.BaseHealth=200;
        var elemental=Summon(p,"CS2_033"); var body=(Minion)Play(g,p,id);
        p.Opponent.Hero.TakeDamage(elemental,1); p.Opponent.Hero.TakeDamage(elemental,1);
        Core2026Proof.Check(p.Opponent.Hero.Damage==4,"two one-damage hits become two two-damage hits");
        Kill(g,elemental); p.Opponent.Hero.TakeDamage(elemental,1);
        Core2026Proof.Check(p.Opponent.Hero.Damage==6,"departed Elemental keeps original source bonus");
        var cinder=Summon(p,"TLC_249");
        int damage=p.Opponent.Hero.Damage;
        PoolPilotProof.Resolve(g,cinder,death:true);
        Core2026Proof.Check(p.Opponent.Hero.Damage==damage+4 && g.HistoryFacts.Query.Damage().Where(f=>f.SourceId==cinder.Id)
            .Select(f=>f.HealthDamage).SequenceEqual(new[]{2,2}),"living Cinder retrigger remains two hits of two");
        damage=p.Opponent.Hero.Damage; Kill(g,cinder);
        Core2026Proof.Check(p.Opponent.Hero.Damage==damage+4,"natural departed Cinder Deathrattle gets active Bralma");
        int finalBefore=p.Opponent.Hero.Damage;
        body.Silence(); p.Opponent.Hero.TakeDamage(elemental,1);
        Core2026Proof.Check(p.Opponent.Hero.Damage==finalBefore+1,"Bralma must remain active");
    }
}
