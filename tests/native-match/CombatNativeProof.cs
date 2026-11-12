using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
internal static class CombatNativeProof
{
    private static void Check(bool condition,string message)
    { if(!condition) throw new Exception("combat projection: "+message); }
    [GroupProof("b14-f4-combat-native",ProofKind.Native)]
    internal static void Run()
    {
        var f=new PoweredUpGlowTests.Fixture();
        var source=f.Draw("BE_230");
        f.Process(PlayCardTask.Minion(f.Player,source));
        var right=f.Summon("CS2_182",f.Player); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        var tags=f.Match.SnapshotVisibleTransportTags(right);
        Check(tags.TryGetValue((int)GameTag.IMMUNE_WHILE_ATTACKING,out int immune) && immune==1,"positional attacking immunity projects as public keyword");
        Check(!tags.Any(t=>CombatRules.IsInternalTag((GameTag)t.Key)),"combat implementation tags filtered from snapshot");
        var support=f.Draw("CATA_564"); f.Mana(10); f.Process(PlayCardTask.MinionTarget(f.Player,support,right));
        tags=f.Match.SnapshotVisibleTransportTags(right);
        Check(tags.TryGetValue((int)GameTag.MEGA_WINDFURY,out int mega) && mega==1,"four-attack grant projects Mega-Windfury");
        var goldrinn=f.Summon("EDR_480",f.Player); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        var beast=f.Summon("CS2_171",f.Player); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(beast.AppliedEnchantments.Any(e=>e.Card.Id=="EDR_480e"),"double-damage aura uses exported enchantment actor");
        foreach(var entry in f.Entries.Where(e=>e.HasTagChange))
            Check(!CombatRules.IsInternalTag((GameTag)entry.TagChange.Tag),"combat private tag never emitted");
        right.Silence(); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==right.Id
            && e.TagChange.Tag==(int)GameTag.MEGA_WINDFURY && e.TagChange.Value==0),"Silence publishes removed Mega-Windfury keyword");
        var fire=f.Summon("FIR_959",f.Player); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        fire.Silence(); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        tags=f.Match.SnapshotVisibleTransportTags(fire);
        Check(!tags.TryGetValue(4128,out int protection) || protection==0,"Silence clears public Fire-immunity tag");
        var hidden=Generic.DrawCard(f.Game.Player2,Cards.FromId("CAP_104")); f.Flush();
        var snapshots=f.Entries.Where(e=>e.HasFullEntity && e.FullEntity.Entity==hidden.Id).Select(e=>e.FullEntity).ToArray();
        Check(snapshots.Length>0 && snapshots.All(e=>string.IsNullOrEmpty(e.Name)
            && e.Tags.All(t=>t.Name==(int)GameTag.ENTITY_ID || t.Name==(int)GameTag.CONTROLLER
                || t.Name==(int)GameTag.ZONE || t.Name==(int)GameTag.ZONE_POSITION)),"opponent hidden card never exposes identity, stats or combat markers");
        Unpinned_FyrakkSecretPrivacy(f);
        StackedRushProjection();
        Console.WriteLine("PASS combat native keyword/enchantment/internal-tag projection");
    }

    private static void StackedRushProjection()
    {
        var f=new PoweredUpGlowTests.Fixture();
        var target=f.Summon("CS2_182",f.Player);
        var first=f.Summon("BE_230",f.Player); var second=f.Summon("BE_230",f.Player);
        f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(f.Match.SnapshotVisibleTransportTags(target).GetValueOrDefault((int)GameTag.RUSH)==1,
            "stacked Rush counts project one public keyword");
        int cursor=f.Entries.Count();
        first.Silence(); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(f.Match.SnapshotVisibleTransportTags(target).GetValueOrDefault((int)GameTag.RUSH)==1
            && !f.Entries.Skip(cursor).Any(e=>e.HasTagChange && e.TagChange.Entity==target.Id
                && e.TagChange.Tag==(int)GameTag.RUSH && e.TagChange.Value==0),
            "removing one Rush provider keeps projected keyword");
        second.ToBeDestroyed=true; f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(f.Match.SnapshotVisibleTransportTags(target).GetValueOrDefault((int)GameTag.RUSH)==0
            && f.Entries.Skip(cursor).Any(e=>e.HasTagChange && e.TagChange.Entity==target.Id
                && e.TagChange.Tag==(int)GameTag.RUSH && e.TagChange.Value==0),
            "last Rush provider departure clears projected keyword");
    }

    private static void Unpinned_FyrakkSecretPrivacy(PoweredUpGlowTests.Fixture f)
    {
        var original=f.Game.OfflinePools;
        var xml=XDocument.Load("engine/SabberStoneCore/resources/Data/OfflinePoolCatalog.xml");
        var rows=xml.Root.Element("Members");
        rows.ReplaceNodes(rows.Elements("Card").Where(e=>(string)e.Attribute("cardId")=="CORE_EX1_610").Select(e=>new XElement(e)).ToArray());
        var policy=(OfflinePoolPolicy)typeof(OfflinePoolPolicy).GetMethod("Parse",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .Invoke(null,new object[]{xml});
        var property=typeof(SabberStoneCore.Model.Game).GetProperty(nameof(SabberStoneCore.Model.Game.OfflinePools));
        try
        {
            property.SetValue(f.Game,policy.BuildManifest());
            var source=f.Summon("FIR_959",f.Game.Player2);
            source.Card.Power.PowerTask.Process(f.Game,f.Game.Player2,source,null,new TaskStack());
            f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
            var secret=f.Game.Player2.SecretZone.Single();
            Check(f.Entries.Where(e=>e.HasFullEntity && e.FullEntity.Entity==secret.Id).All(e=>string.IsNullOrEmpty(e.FullEntity.Name))
                && !f.Entries.Any(e=>e.HasShowEntity && e.ShowEntity.Entity==secret.Id && !string.IsNullOrEmpty(e.ShowEntity.Name)),
                "generated Fyrakk opponent Secret identity remains private from creation through installation");
        }
        finally { property.SetValue(f.Game,original); }
    }
}
