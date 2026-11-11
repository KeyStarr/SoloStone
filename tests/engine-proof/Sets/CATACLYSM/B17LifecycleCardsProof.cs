using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B17LifecycleCardsProof
{
    internal static readonly Dictionary<string, Action<Game,Controller,string>> Scenarios = new() {
        ["CATA_307"] = Unpinned_GuardianArmAndPinnedFullWithoutHeal,
        ["CAP_800"] = B17LifecycleSteedProof.Unpinned_SteedFullEnchantedRebirth,
        ["TIME_618"] = B17LifecycleTimeProof.Unpinned_HuskResurrectionAndPinnedNoStack,
        ["MEND_801"] = Unpinned_SaviorRecruitSilence,
        ["MEND_900"] = Unpinned_TeamworkIndependentCapacity,
        ["TIME_042"] = B17LifecycleTimeProof.Unpinned_InfiniteBananaCounterConsumes,
        ["BE_143"] = B17LifecycleAmitutProof.Unpinned_AmitutSnapshotAndDrawGrant,
        ["TIME_620"] = B17LifecycleTimeProof.Unpinned_UntimelyPlayedTurn
    };
    [GroupProof("b17-lifecycle-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_307", Unpinned_GuardianArmAndPinnedFullWithoutHeal);
        StandardSetsProof.Case("MEND_801", Unpinned_SaviorRecruitSilence);
        StandardSetsProof.Case("MEND_900", Unpinned_TeamworkIndependentCapacity);
        B17LifecycleAmitutProof.Run(); B17LifecycleSteedProof.Run(); B17LifecycleTimeProof.Run();
    }
    private static void Unpinned_GuardianArmAndPinnedFullWithoutHeal(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g,p,id);
        Core2026Proof.Check(p.Hero.Health == 15 && p.Hero.BaseHealth == 30 && p.Opponent.Hero.Health == 30, "Guardian sets remaining health only and arms");
        body.Silence(); Kill(g,body);
        PoolPilotProof.Resume(g.CloneExact(history: true), next => {
            var owner = next.Player1;
            owner.Hero.Health = 15;
            Core2026Proof.Check(owner.Opponent.Hero.Health == 15, "Pinned full without healing consumes persistent grant after source gone");
            owner.Hero.Damage = 1; owner.Hero.Damage = 0;
            Core2026Proof.Check(owner.Opponent.Hero.Health == 15, "grant consumes once");
        });
        p.Hero.Health = 15;
        p.Opponent.Hero.Health = 60;
        Play(g,p,id);
        Core2026Proof.Check(p.Opponent.Hero.Health == 45, "Unpinned already-full on arm triggers immediately");
    }


    private static void Unpinned_SaviorRecruitSilence(Game g, Controller p, string id)
    {
        var old=Summon(p,"CS2_101t"); var savior=(Minion)Play(g,p,id);
        savior.TakeDamage(p.Opponent.Hero,1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(old.BaseHealth==2,"shield loss grants existing Recruit health");
        old.Silence(); Core2026Proof.Check(old.BaseHealth==1,"Silence removes realized grant");
        PoolPilotProof.Resume(g.CloneExact(history: true),next => {
            var owner=next.Player1; next.AuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[old.Id]).BaseHealth==1,"old ledger never reapplied on refresh/load");
            var fresh=Summon(owner,"CS2_101t"); Core2026Proof.Check(fresh.Health==2,"fresh recruit receives persistent ledger");
            var s=(Minion)next.IdEntityDic[savior.Id]; s.HasDivineShield=true; s.TakeDamage(owner.Opponent.Hero,1);
            next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[old.Id]).Health==2 && fresh.Health==3,"new increment only after Silence");
        });
        savior.Silence(); var later=Summon(p,"CS2_101t");
        Core2026Proof.Check(later.Health==2,"Savior Silence preserves player grant without extra trigger");
        Core2026Proof.Check(Summon(p.Opponent,"CS2_101t").Health==1,"opponent not buffed");
    }
    private static void Unpinned_TeamworkIndependentCapacity(Game g, Controller p, string id)
    {
        Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count==4 && p.HandZone.Count==4 && p.BoardZone.Concat(p.HandZone).Select(x=>x.Id).Distinct().Count()==8,
            "Teamwork four board plus four separate hand entities");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_101t");
        PoolPilotProof.EmptyHand(p); Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.Count==7 && p.HandZone.Count==4,"Pinned full-board play retains hand component");
        PoolPilotProof.FillHand(p,9); Play(g,p,id);
        Core2026Proof.Check(p.HandZone.Count==10,"independent hand cap includes freed spell slot");
    }



}
