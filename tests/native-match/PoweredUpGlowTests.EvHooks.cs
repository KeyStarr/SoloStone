using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-ev-hooks", ProofKind.PoweredUp)]
    private static void EvHooksGlow()
    {
        Scenario("JAIL_EVENT_101", (f,card,met) => {
            if (met) {
                new ReplaceHeroPower(Cards.FromId("JAIL_EVENT_101hp")).Process(f.Game,f.Player,card,null);
                f.Player.Hero.HeroPower[(GameTag)2] = 2;
            }
        }, (f,card) => PlayCardTask.Any(f.Player,card),
            (f,card) => f.Player.Hero.HeroPower[(GameTag)31293] == 1);
    }
    [GroupProof("ev-hooks-native", ProofKind.Native)]
    private static void EvHooksNative()
    {
        foreach (bool enemy in new[] { false,true })
        foreach (string id in new[] { "JAIL_EVENT_100", "TLC_EVENT_400", "CATA_EVENT_110t3" }) {
            var f = new Fixture(); var g = f.Game;
            if (enemy) f.Process(EndTurnTask.Any(f.Player));
            var p = g.CurrentPlayer;
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            p.BaseMana = 10; p.UsedMana = 0;
            if (id == "CATA_EVENT_110t3") {
                foreach (var old in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(old));
                foreach (string spell in new[] { "CS2_029", "CS2_024", "CS2_023" })
                    SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId(spell),zone:p.DeckZone);
            }
            var source = Generic.DrawCard(p,Cards.FromId(id));
            f.Process(PlayCardTask.Any(p,source));
            if (id == "TLC_EVENT_400")
                for (int i=0;i<3;i++) {
                    p.UsedMana=0;
                    f.Process(PlayCardTask.Any(p,Generic.DrawCard(p,Cards.FromId("CS2_172"))));
                }
            if (p.Choice != null) {
                var concede = g.CloneExact();
                Check(concede.Process(ConcedeTask.Any(concede.CurrentPlayer)) && concede.State == State.COMPLETE,
                    "Concede works during EVENT Discover/crafting");
            }
            var offers = new System.Collections.Generic.List<int>();
            while (p.Choice != null) {
                offers.AddRange(p.Choice.Choices);
                int selected = id == "JAIL_EVENT_100" ? p.Choice.Choices.Single(i=>g.IdEntityDic[i].Card.Id=="JAIL_EVENT_100t") : p.Choice.Choices[0];
                f.Process(ChooseTask.Pick(p,selected));
            }
            Check(g.State == State.RUNNING && !g.Resolution.HasSuspendedEffect, "EVENT choice/draw finishes without stall");
            var actors = f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            if (enemy) {
                var hidden = p.HandZone.Select(e=>e.Id).Concat(offers).ToArray();
                Check(actors.Where(e=>hidden.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)), "EVENT opponent hidden cards/choices anonymous");
            }
            Check(actors.All(e=>e.Tags.All(t=>!StartOfGameRules.IsInternalTag((GameTag)t.Name)
                && !BasicHeroPowerRules.IsInternalTag((GameTag)t.Name))), "EVENT internal snapshot tags filtered");
            Check(f.Entries.All(e=>!e.HasTagChange || !StartOfGameRules.IsInternalTag((GameTag)e.TagChange.Tag)
                && !BasicHeroPowerRules.IsInternalTag((GameTag)e.TagChange.Tag)), "EVENT internal deltas filtered");
            if (id == "JAIL_EVENT_100" && !enemy) {
                var custom = p.HandZone.Single();
                var visible = f.Match.SnapshotVisibleTransportTags(custom);
                Check(visible.GetValueOrDefault((int)GameTag.ATK) == custom.Card.ATK
                    && visible.GetValueOrDefault((int)GameTag.HEALTH) == custom.Card.Health, "suspicious minion exposes custom stats");
            }
            f.Process(ConcedeTask.Any(p)); Check(g.State == State.COMPLETE,"Concede remains legal");
        }
        EvHooksNativeLocationInput(); EvHooksNativeStartup(); EvHooksNativeChooseOne(); EvHooksNativeQueuedCraft();
        Console.WriteLine("PASS EVENT native private offers/draws/rewards/startup, dynamic stats and Concede");
    }
    private static void EvHooksNativeChooseOne()
    {
        foreach (bool enemy in new[] { false,true })
        foreach (int seed in new[] { 50,53 })
        {
            var f = new Fixture(); if (enemy) f.Process(EndTurnTask.Any(f.Player));
            var g = f.Game; var p = g.CurrentPlayer; p.Hero[GameTag.CLASS] = (int)CardClass.DRUID;
            // These exact offers were selected under the accepted Black Empire pool.
            const string poolHash = "50606ac01b5dccd8b86be9a55b0a448e391d9716646f300b774a1b7e947b4b64";
            var xml = System.Xml.Linq.XDocument.Load("tests/goldens/pools/" + poolHash + ".xml");
            var policy = (OfflinePoolPolicy)typeof(OfflinePoolPolicy).GetMethod("Parse", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { xml });
            var pools = policy.BuildManifest();
            Check(pools.Hash == poolHash, "native Choose One fixture pool identity");
            typeof(SabberStoneCore.Model.Game).GetProperty("OfflinePools").SetValue(g, pools);
            foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
            p.BaseMana = 10; p.UsedMana = 0; g.SetRandomSeed(seed);
            f.Process(PlayCardTask.Any(p,Generic.DrawCard(p,Cards.FromId("JAIL_EVENT_100"))));
            int selected = p.Choice.Choices.Single(i => g.IdEntityDic[i].Card.Id == (seed==53 ? "JAIL_EVENT_100t" : "END_010"));
            var custom = g.IdEntityDic[selected];
            Check(custom.ChooseOnePlayables.Select(e=>e.Card.Id).SequenceEqual(new[] {"END_010a","END_010b"}), "native suspicious original Choose One options");
            int[] privateIds = p.Choice.Choices.Concat(custom.ChooseOnePlayables.Select(e=>e.Id)).ToArray();
            f.Process(ChooseTask.Pick(p,selected));
            var actors = f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null);
            if (enemy) {
                Check(actors.Where(e=>privateIds.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)), "opponent cannot see generated card or Choose One children");
                using var bytes = new System.IO.MemoryStream();
                foreach (var entry in f.Entries) entry.Serialize(bytes);
                string wire = System.Text.Encoding.UTF8.GetString(bytes.ToArray());
                Check(new[]{"END_010","END_010a","END_010b","JAIL_EVENT_100t"}.All(id=>!wire.Contains(id)),
                    "whole native history contains no hidden definition or suspicious marker");
            }
            else {
                var visible = f.Match.SnapshotVisibleTransportTags(custom);
                Check(visible.GetValueOrDefault((int)GameTag.ATK) == custom.Card.ATK
                    && visible.GetValueOrDefault((int)GameTag.HEALTH) == custom.Card.Health, "native owner sees suspicious Choose One mutation");
                using var bytes = new System.IO.MemoryStream();
                foreach (var entry in f.Entries) entry.Serialize(bytes);
                Check(!System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Contains("JAIL_EVENT_100t")
                    && visible.GetValueOrDefault((int)GameTag.MODULAR_ENTITY_PART_1)==0,
                    "native owner sees visible definition without suspicious token or recipe designation");
            }
            p.UsedMana = 0; f.Process(PlayCardTask.Any(p,custom,chooseOne:1));
            Check(p.BoardZone.First(m=>m.Card.Id=="JAIL_EVENT_100").AttackDamage==1, "native suspicious Choose One applies original Attack option");
        }
    }
    private static void EvHooksNativeQueuedCraft()
    {
        foreach (bool enemy in new[] {false,true})
        {
            var f = new Fixture(); if (enemy) f.Process(EndTurnTask.Any(f.Player));
            var g = f.Game; var p = g.CurrentPlayer;
            p.BaseMana=10; p.UsedMana=0;
            f.Process(PlayCardTask.Any(p,Generic.DrawCard(p,Cards.FromId("TLC_EVENT_400"))));
            for (int i=0;i<3;i++) {
                p.UsedMana=0;
                f.Process(PlayCardTask.Any(p,Generic.DrawCard(p,Cards.FromId("CS2_172"))));
            }
            var queued = p.Choice.NextChoice.Choices.ToArray();
            var actors = f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null);
            Check(actors.Where(e=>queued.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),
                "upcoming crafting offers stay hidden from both native seats");
            f.Process(ChooseTask.Pick(p,p.Choice.Choices[0]));
            if (!enemy) Check(queued.All(id=>actors.Any(e=>e.Entity==id && e.Name==g.IdEntityDic[id].Card.Id)),
                "current crafting offers become visible before the native player answers");
            f.Process(ChooseTask.Pick(p,p.Choice.Choices[0]));
            Check(p.HandZone.Count(card=>card.Card.Id=="ICC_828t")==1,"native queued crafting completes");
        }
    }
    private static void EvHooksNativeStartup()
    {
        var packets=new List<PegasusGame.PowerHistory>();var choices=new List<PegasusGame.EntityChoices>();
        var deck=Enumerable.Repeat(Cards.FromId("CS2_182"),29).Append(Cards.FromId("CATA_EVENT_110")).ToList();
        var match=new NativeMatch((id,message)=>{
            if(id==19) packets.Add((PegasusGame.PowerHistory)message);
            if(id==17) choices.Add((PegasusGame.EntityChoices)message);
        },Cards.FromId("HERO_06"),Enumerable.Repeat(Cards.FromId("CS2_182"),30),23);
        typeof(NativeMatch).GetField("playerCards",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(match,deck);
        match.Start();match.SendChoices(choices.Last().Id,new List<int>());
        var g=match.Game;
        Check(g.State==State.RUNNING && g.Step==Step.MAIN_ACTION,"native post-mulligan startup reaches actionable turn");
        var hidden=g.IdEntityDic.Values.Where(e=>e.Card.Id.StartsWith("CATA_EVENT_110t") && e.Zone==e.Controller.DeckZone).Select(e=>e.Id).ToArray();
        Check(hidden.Length>=5,"native original parent creates six Essences before first draw");
        var actors=packets.SelectMany(p=>p.List).SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
        Check(actors.Where(e=>hidden.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"native generated startup deck identities stay private");
        Check(actors.All(e=>e.Tags.All(t=>!StartOfGameRules.IsInternalTag((GameTag)t.Name))),"native startup phases never projected");
    }

    private static void EvHooksNativeLocationInput()
    {
        var f=new Fixture();var p=f.Player;
        var location=(SabberStoneCore.Model.Entities.BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CORE_REV_990"),zone:p.BoardZone);
        location.IsExhausted=true;location[GameTag.EXHAUSTED]=1;
        var spell=f.Draw("TIME_EVENT_997");f.Flush();
        typeof(NativeMatch).GetMethod("SendHumanOptions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(f.Match,null);
        var options=(SabberStoneCore.Kettle.PowerAllOptions)typeof(NativeMatch).GetField("humanOptions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(f.Match);
        int index=options.PowerOptionList.FindIndex(o=>o.MainOption?.EntityId==spell.Id);
        var task=(PlayCardTask)typeof(NativeMatch).GetMethod("ResolveTask",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .Invoke(f.Match,new object[]{options,index,location.Id,-1,0});
        Check(task.ActionTarget==location && task.ZonePosition==-1,"native location-target spell resolves one task without placement");
        f.Process(task);Check(!location.IsExhausted && location.AppliedEnchantments.Any(e=>e.Card.Id=="TIME_EVENT_997e"),"native location input reopens and grants Deathrattle");
    }

}
