using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B17LifecycleTimeProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("TIME_618", Unpinned_HuskResurrectionAndPinnedNoStack);
        StandardSetsProof.Case("TIME_042", Unpinned_InfiniteBananaCounterConsumes);
        StandardSetsProof.Case("TIME_620", Unpinned_UntimelyPlayedTurn);
    }
    internal static void Unpinned_HuskResurrectionAndPinnedNoStack(Game g, Controller p, string id)
    {
        g = DeathKnightC1Proof.NewGame(g.Seed); p=g.Player1; PoolPilotProof.EmptyHand(p);

        Play(g,p,id); Play(g,p,id); CorpseRules.Gain(p,45);
        PoolPilotProof.Resume(g.CloneExact(history: true),next => {
            var owner=next.Player1; owner.Hero.TakeDamage(owner.Opponent.Hero,100);
            next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(owner.Hero.Health==20 && owner.Hero.BaseHealth==20 && !owner.Hero.ToBeDestroyed
                && CorpseRules.Available(owner)==25 && !owner.Hero.IsImmune,"Husk pays once and restores full max/current health");
            owner.Hero.Destroy(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(owner.Hero.ToBeDestroyed && CorpseRules.Available(owner)==25,"Pinned repeated grants never bank lives");
        });
        // Keep card scenario nonterminal for its snapshot contract.
        p.Hero.Destroy(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Health==20 && !p.Hero.ToBeDestroyed,"destroy also resurrects");
    }
    internal static void Unpinned_InfiniteBananaCounterConsumes(Game g, Controller p, string id)
    {
        Add(p,"CS2_182"); Add(p,"CS2_029");
        Play(g,p,id); Core2026Proof.Check(p.HandZone.Count==1 && p.HandZone[0].Card.Id=="TIME_042t","Maluk discards whole hand then adds Banana");
        var banana=p.HandZone[0]; banana.Cost=0; var body=p.BoardZone.Single();
        int atk=body.AttackDamage; PlayEntity(g,p,banana,body);
        Core2026Proof.Check(body.AttackDamage==atk+1 && p.HandZone.Single().Cost==1 && p.HandZone.Single().Id!=banana.Id,"success returns fresh printed-cost Banana");
        body.Silence(); Core2026Proof.Check(body.AttackDamage==5,"Banana buff silences normally");
        End(g); Play(g,p.Opponent,"EX1_287"); End(g);
        var held=p.HandZone.Single(c=>c.Card.Id=="TIME_042t");
        PlayEntity(g,p,held,body);
        Core2026Proof.Check(p.HandZone.All(c=>c.Card.Id!="TIME_042t") && body.AttackDamage==5 && p.UsedMana==1,"counter consumes Banana and payment without buff/return");
    }
    internal static void Unpinned_UntimelyPlayedTurn(Game g, Controller p, string id)
    {
        Play(g,p,id); var played=(Minion)Play(g,p,"CS2_182");
        var summoned=Summon(p,"CS2_182"); End(g);
        Kill(g,summoned); Core2026Proof.Check(p.SecretZone.Count==1,"summon is not a hand play");
        PoolPilotProof.Resume(g.CloneExact(history: true),next=>{
            var owner=next.Player1; Kill(next,(Minion)next.IdEntityDic[played.Id]);
            Core2026Proof.Check(owner.SecretZone.Count==0 && owner.BoardZone.Count==1 && owner.BoardZone[0].Card.Id=="CS2_182"
                && owner.BoardZone[0].Id!=played.Id && owner.BoardZone[0].Health==5,"next global turn resummons fresh after death clears slot");
        });
        Kill(g,played);
        Core2026Proof.Check(p.SecretZone.Count==0 && p.BoardZone.Count==1,"secret resolves once");
    }
}
