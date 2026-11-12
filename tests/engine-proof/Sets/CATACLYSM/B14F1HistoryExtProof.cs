using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F1CataclysmProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_130", CrystalspineCub);
        StandardSetsProof.Case("CATA_493", Unpinned_DukeSilenceHealth);
    }
    private static void CrystalspineCub(Game g, Controller p, string id)
    {
        var cub=(Minion)Play(g,p,id); p.BaseMana=2;p.UsedMana=0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,Add(p,"CS2_120"))),"last-Mana play accepted");
        Core2026Proof.Check(cub.AttackDamage==2 && cub.Health==2,"spending last positive Mana buffs Cub");
        Core2026Proof.Check(g.HistoryFacts.Query.Payments().Last().SpentLastMana,"payment records event-time last-Mana flag");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p,Add(p,"CS2_231"))),"zero play accepted at zero Mana");
        Core2026Proof.Check(cub.AttackDamage==2,"zero payment does not trigger");
        foreach(var copy in new[]{g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            var next=(Minion)copy.IdEntityDic[cub.Id];copy.Player1.BaseMana=1;copy.Player1.UsedMana=0;
            PrepareRules.Grant(Add(copy.Player1,"CS2_168"));
            Core2026Proof.Check(copy.Process(PrepareCardTask.Any(copy.Player1,copy.Player1.HandZone.Last())),"last Mana Prepare accepted");
            Core2026Proof.Check(next.AttackDamage==3 && cub.AttackDamage==2,"cloned trigger sees Prepare payment independently");
        }
        cub.Silence();p.BaseMana=2;p.UsedMana=0;g.Process(PlayCardTask.Any(p,Add(p,"CS2_120")));
        Core2026Proof.Check(cub.AttackDamage==1,"Silence removes trigger and buffs");
        End(g);p.Opponent.BaseMana=2;p.Opponent.UsedMana=0;g.Process(PlayCardTask.Any(p.Opponent,Add(p.Opponent,"CS2_120")));
        Core2026Proof.Check(cub.AttackDamage==1,"opponent payment never triggers");
    }
    private static void DukeExternalAura(Game g, Controller p, string id)
    {
        for(int i=0;i<3;i++)Generic.DiscardBlock(p,Add(p,"CS2_231"));
        var champion=Summon(p,"CS2_222");
        var duke=(Minion)Play(g,p,id);
        Core2026Proof.Check(duke.AttackDamage==9 && duke.Health==9,"Champion adds one external stat to scaled Duke");
        foreach(var copy in new[]{g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot()),g})
        {
            var next=(Minion)copy.IdEntityDic[duke.Id];
            Generic.DiscardBlock(copy.Player1,Add(copy.Player1,"CS2_231"));copy.AuraUpdate();
            Core2026Proof.Check(next.AttackDamage==11 && next.Health==11,"fourth discard with Champion changes 9/9 to 11/11");
            Kill(copy,(Minion)copy.IdEntityDic[champion.Id]);
            Core2026Proof.Check(next.AttackDamage==10 && next.Health==10,"Champion departure removes exactly its own stat layer");
        }
        Generic.ReturnToHandBlock(p,duke);g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(duke.AttackDamage==2 && duke.BaseHealth==2,"leave play removes local scaling layer");
        PlayEntity(g,p,duke);
        Core2026Proof.Check(duke.AttackDamage==10 && duke.Health==10,"re-entry reapplies discard count once");
    }
    private static void Unpinned_DukeSilenceHealth(Game g, Controller p, string id)
    {
        for(int i=0;i<3;i++)Generic.DiscardBlock(p,Add(p,"CS2_231"));
        var duke=(Minion)Play(g,p,id);
        Core2026Proof.Check(duke.AttackDamage==8 && duke.Health==8,"three discards add six stats");
        foreach(var copy in new[]{g.CloneExact(),Game.LoadSnapshot(g.SaveSnapshot())})
        {
            var next=(Minion)copy.IdEntityDic[duke.Id];
            Generic.DiscardBlock(copy.Player1,Add(copy.Player1,"CS2_231"));copy.AuraUpdate();
            Core2026Proof.Check(next.AttackDamage==10 && next.Health==10 && duke.Health==8,"active discard aura survives clone/snapshot independently");
        }
        duke.TakeDamage(p.Opponent.Hero,7);duke.Silence();g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(duke.AttackDamage==2 && duke.BaseHealth==2 && duke.Health==1 && duke.Zone==p.BoardZone,"Silence leaves living 2/1");
        Generic.DiscardBlock(p,Add(p,"CS2_231"));g.AuraUpdate();
        Core2026Proof.Check(duke.BaseHealth==2,"Silence prevents later scaling");
        var extra=HistoryFactsProof.Fixture();DukeExternalAura(extra,extra.Player1,id);
    }
}
