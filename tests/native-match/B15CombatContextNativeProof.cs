using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
internal static class B15CombatContextNativeProof
{
    private static void Check(bool ok,string message) { if(!ok) throw new Exception("b15 native: "+message); }
    [GroupProof("b15-combat-context-native", ProofKind.Native)]
    internal static void Run()
    {
        foreach(bool opponent in new[]{false,true})
        {
            var f=new PoweredUpGlowTests.Fixture(); var owner=opponent?f.Game.Player2:f.Player;
            var source=f.Summon("CATA_464",owner); source.AttackDamage=7;
            source.ToBeDestroyed=true; f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
            var spell=owner.HandZone.Single(c=>c.Card.Id=="CATA_464t");
            Check(spell[GameTag.TAG_SCRIPT_DATA_NUM_1]==7,"generated damage scalar retained");
            var shapes=f.Entries.Select(e=>e.HasFullEntity?e.FullEntity:e.HasShowEntity?e.ShowEntity:e.HasChangeEntity?e.ChangeEntity:null)
                .Where(e=>e!=null && e.Entity==spell.Id).ToArray();
            Check(shapes.Length>0,"generated hand has creation shape");
            if(opponent)
                Check(shapes.All(e=>string.IsNullOrEmpty(e.Name) && e.Tags.All(t=>t.Name==(int)GameTag.ENTITY_ID || t.Name==(int)GameTag.CONTROLLER
                    || t.Name==(int)GameTag.ZONE || t.Name==(int)GameTag.ZONE_POSITION)),"opponent Breath never exposes identity or payload");
            else Check(shapes.Any(e=>e.Name=="CATA_464t"),"own generated spell has exported identity");
            Check(!f.Match.SnapshotVisibleTransportTags(source).Any(t=>DeathContextRules.IsInternalTag((GameTag)t.Key)),"death tags filtered from snapshots");
            Check(!f.Entries.Any(e=>e.HasTagChange && DeathContextRules.IsInternalTag((GameTag)e.TagChange.Tag)),"death tags filtered from deltas");
            var toreth=f.Summon("EDR_258",owner); var shield=f.Summon("CS2_182",owner); shield.HasDivineShield=true;
            shield.TakeDamage(owner.Opponent.Hero,1); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(shield[DeathContextRules.ShieldHits]==1 && !f.Match.SnapshotVisibleTransportTags(shield).Any(t=>DeathContextRules.IsInternalTag((GameTag)t.Key)),
                "live partially spent Shield never exposes private hit counter");
            var ursoc=f.Summon("EDR_819",owner); var victim=f.Summon("CS2_231",owner.Opponent);
            new AttackTask(EntityType.SOURCE,EntityType.TARGET).Process(f.Game,owner,ursoc,victim,new TaskStack());
            f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
            var stored=PayloadRules.Entries(ursoc,DeathContextRules.KillKey).Single();
            Check(f.Entries.Where(e=>e.HasFullEntity && e.FullEntity.Entity==stored.Id).All(e=>string.IsNullOrEmpty(e.FullEntity.Name))
                && !f.Entries.Any(e=>e.HasShowEntity && e.ShowEntity.Entity==stored.Id && !string.IsNullOrEmpty(e.ShowEntity.Name)),
                "private ordered kill payload never creates a visible extra minion");
        }
        Unpinned_MysticProjection();
        Console.WriteLine("PASS b15 native context tags, generated hand privacy, Secret transform/cancel");
    }
    private static void Unpinned_MysticProjection()
    {
        var f=new PoweredUpGlowTests.Fixture(); var p=f.Player;
        var secret=f.Draw("JAIL_315"); f.Process(PlayCardTask.Spell(p,secret));
        f.Process(EndTurnTask.Any(p)); var attacker=f.Summon("CS2_182",f.Game.Player2); attacker.IsExhausted=false;
        f.Process(MinionAttackTask.Any(f.Game.Player2,attacker,p.Hero));
        Check(f.Entries.Any(e=>e.HasChangeEntity && e.ChangeEntity.Entity==attacker.Id && e.ChangeEntity.Name=="CS2_tk1"),"Sheep uses exported CHANGE_ENTITY");
        Check(p.Hero.Damage==0 && p.SecretZone.Count==0 && attacker.Card.Id=="CS2_tk1","transform cancellation leaves settled decision");
        Check(!f.Entries.Any(e=>e.HasTagChange && DeathContextRules.IsInternalTag((GameTag)e.TagChange.Tag)),"cancellation marker stays private");
        var hidden=Generic.DrawCard(f.Game.Player2,Cards.FromId("JAIL_315"));
        f.Game.Player2.BaseMana=10; f.Game.Player2.UsedMana=0;
        f.Process(PlayCardTask.Spell(f.Game.Player2,hidden));
        Check(f.Entries.Where(e=>e.HasFullEntity && e.FullEntity.Entity==hidden.Id).All(e=>string.IsNullOrEmpty(e.FullEntity.Name))
            && !f.Entries.Any(e=>e.HasShowEntity && e.ShowEntity.Entity==hidden.Id && !string.IsNullOrEmpty(e.ShowEntity.Name)),"opponent Secret stays private from hand through installation");
    }
}
