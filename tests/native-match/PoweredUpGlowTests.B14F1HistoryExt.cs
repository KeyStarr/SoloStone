using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-f1-history-ext", ProofKind.PoweredUp)]
    private static void B14F1HistoryConditions()
    {
        Scenario("CORE_BAR_313",(f,card,met)=>{
            var body=f.Summon("CS2_182",f.Player);body.TakeDamage(f.Player.Hero,1);
            if(met)body.TakeHeal(f.Player.Hero,1);
        },(f,card)=>PlayCardTask.Minion(f.Player,card),(f,card)=>((Minion)card).AttackDamage==8);
        Scenario("CORE_RLK_116",(f,card,met)=>{
            var body=f.Summon("RLK_503",met?f.Player:f.Game.Player2);body.ToBeDestroyed=true;f.Game.DeathProcessingAndAuraUpdate();
        },(f,card)=>PlayCardTask.Minion(f.Player,card),(f,card)=>f.Player.Choice!=null);
        Scenario("TIME_600",(f,card,met)=>{if(!met)f.Draw("CS2_231");},
            (f,card)=>PlayCardTask.SpellTarget(f.Player,card,f.Game.Player2.Hero),
            (f,card)=>f.Game.Player2.Hero.Damage==5);
    }

    [GroupProof("native-b14-f1-history-ext", ProofKind.Native)]
    private static void B14F1HistoryPrivacy()
    {
        var f=new Fixture();var proto=f.Draw("BE_997");f.Process(PlayCardTask.Minion(f.Player,proto));
        f.Draw("CS2_231");f.Flush();
        var hidden=f.Game.Player2.HandZone.Last();
        Check(hidden.Card.Type==CardType.SPELL,"Proto gave opponent spell");
        Check(!f.Entries.Any(e=>e.HasFullEntity && e.FullEntity.Entity==hidden.Id && e.FullEntity.Name==hidden.Card.Id
            || e.HasShowEntity && e.ShowEntity.Entity==hidden.Id && e.ShowEntity.Name==hidden.Card.Id
            || e.HasChangeEntity && e.ChangeEntity.Entity==hidden.Id && e.ChangeEntity.Name==hidden.Card.Id),
            "opponent generated hand identity never reaches owner viewer");
        proto.ToBeDestroyed=true;f.Game.DeathProcessingAndAuraUpdate();
        var body=f.Summon("CS2_182",f.Player);body.TakeDamage(f.Player.Hero,1);body.TakeHeal(f.Player.Hero,1);
        f.Flush();
        Check(!f.Entries.Any(e=>e.HasTagChange && e.TagChange.Tag>=31283 && e.TagChange.Tag<=31286),
            "healing counter tags filtered from native packets");
        var knight=f.Draw("CORE_ETC_523");f.Flush();
        Check(!f.Match.SnapshotVisibleTransportTags(knight).TryGetValue(2837,out int normal) || normal==0,"minion healing does not switch hero-healing payment");
        f.Player.Hero.TakeDamage(f.Game.Player2.Hero,2);f.Player.Hero.TakeHeal(f.Player.Hero,1);f.Flush();
        Check(f.Match.SnapshotVisibleTransportTags(knight)[2837]==1,"hero healing projects alternate Health cost");
        B14F1DukeExternalAura();
    }

    private static void B14F1DukeExternalAura()
    {
        var f=new Fixture();f.Summon("CS2_222",f.Player);
        for(int i=0;i<3;i++)Generic.DiscardBlock(f.Player,f.Draw("CS2_231"));
        var duke=f.Summon("CATA_493",f.Player);f.Flush();
        Generic.DiscardBlock(f.Player,f.Draw("CS2_231"));f.Game.AuraUpdate();f.Flush();
        var tags=f.Match.SnapshotVisibleTransportTags(duke);
        Check(tags[(int)GameTag.ATK]==11 && tags[(int)GameTag.HEALTH]==11,"Duke external aura projects 11/11");
        Check(f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==duke.Id && e.TagChange.Tag==(int)GameTag.ATK && e.TagChange.Value==11)
            && f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==duke.Id && e.TagChange.Tag==(int)GameTag.HEALTH && e.TagChange.Value==11),
            "Duke stat-layer change emits retail stat tags");
    }
}
