using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-stat-gain-event", ProofKind.PoweredUp)]
    private static void RunB16StatGlow()
    {
        var f=new Fixture();
        foreach(string id in new[]{"BE_209","CATA_139","JAIL_330"})
        {
            var card=f.Draw(id);f.Flush();f.Expected(card,0,id+" unconditional effect has no conditional hand glow");
            if(id=="JAIL_330") Generic.AddEnchantmentBlock(f.Game,Cards.FromId("CS2_092e"),f.Player.Hero,card);
            f.Flush();f.Expected(card,0,id+" remains unconditional after stat gain");
        }
        Console.WriteLine("PASS B16 stat gains PoweredUp: all three N; no G conditions");
    }

    [GroupProof("native-b16-stat-gain-event", ProofKind.Native)]
    private static void RunB16StatNative()
    {
        foreach(bool opponent in new[]{false,true})
        {
            var f=new Fixture();var g=f.Game;var p=opponent?g.Player2:g.Player1;
            foreach(var card in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(card));
            f.Flush();int cursor=f.Entries.Count();
            var hand=Generic.DrawCard(p,Cards.FromId("JAIL_330"));
            var deck=(Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("JAIL_330"));
            Generic.ShuffleIntoDeck(p,p.Hero,deck);
            f.Flush();
            Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_092e"),p.Hero,hand);
            Generic.AddEnchantmentBlock(g,Cards.FromId("CS2_092e"),p.Hero,deck);f.Flush();
            Check(((Minion)hand).AttackDamage==8 && deck.AttackDamage==8,"native hidden cards actually gain one bonus");
            var data=f.Entries.Skip(cursor).ToArray();
            foreach(var host in new[]{hand,(IPlayable)deck})
            {
                bool hidden=opponent || host==deck;
                var actors=host.AppliedEnchantments.Select(e=>e.Id).ToHashSet();
                var snapshots=data.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
                if(hidden)
                {
                    Check(snapshots.Where(e=>e.Entity==host.Id).All(e=>string.IsNullOrEmpty(e.Name)),"hidden Champion identity never reveals");
                    Check(snapshots.Where(e=>actors.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)
                        && e.Tags.All(t=>t.Name!=(int)GameTag.ATTACHED && t.Name!=(int)GameTag.CREATOR && t.Name!=(int)GameTag.CARDTYPE)),"hidden bonus actors reveal no identity/host/creator");
                    Check(!data.Any(e=>e.HasTagChange && (e.TagChange.Entity==host.Id || actors.Contains(e.TagChange.Entity))
                        && new[]{(int)GameTag.ATK,(int)GameTag.HEALTH,(int)GameTag.ATTACHED,(int)GameTag.CREATOR,(int)GameTag.TAG_SCRIPT_DATA_NUM_1,(int)GameTag.TAG_SCRIPT_DATA_NUM_2}.Contains(e.TagChange.Tag)),"hidden stat/script/attachment deltas stay private");
                }
                else Check(snapshots.Any(e=>actors.Contains(e.Entity) && e.Name=="JAIL_330e"),"owner sees exact exported bonus actor");
            }
            // Native play exposes the final realized stats and printed Taunt/Divine Shield.
            if(opponent) Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)),"advance to opponent");
            p.BaseMana=10;p.UsedMana=0;
            Check(g.Process(PlayCardTask.Any(p,hand)),"native Champion play");f.Flush();
            var visible=f.Match.SnapshotVisibleTransportTags(hand);
            Check(visible.GetValueOrDefault((int)GameTag.ATK)==8 && visible.GetValueOrDefault((int)GameTag.HEALTH)==8
                && visible.GetValueOrDefault((int)GameTag.TAUNT)==1 && visible.GetValueOrDefault((int)GameTag.DIVINE_SHIELD)==1,"public stats and printed keywords project");
            using(StatGainRules.Begin(hand)) Check(f.Match.SnapshotVisibleTransportTags(hand).Keys.All(t=>t<30000),"live operation state filtered from visible snapshot");
            var neptulon=Generic.DrawCard(p,Cards.FromId("BE_209"));p.UsedMana=0;
            Check(g.Process(PlayCardTask.Any(p,neptulon)),"native Neptulon play");f.Flush();
            var elemental=(Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CS2_042"));Generic.SummonBlock(g,elemental,-1,p.Hero);f.Flush();
            var actor=elemental.AppliedEnchantments.Single(e=>e.Card.Id=="BE_209e1");
            Check(f.Match.SnapshotVisibleTransportTags(elemental).GetValueOrDefault((int)GameTag.ATK)==5,"entry setter projects 5 Attack");
            Check(f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Any(e=>e!=null && e.Entity==actor.Id && e.Name=="BE_209e1"),"entry setter uses exported actor");
            p.SetasideZone.Add(p.BoardZone.Remove(elemental));f.Flush();
            var wicker=(Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CATA_139"));
            Generic.SummonBlock(g,wicker,-1,p.Hero);f.Flush();
            Check(g.Process(EndTurnTask.Any(p)),"native Wickerfang own-end growth");f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(wicker).GetValueOrDefault((int)GameTag.ATK)==4
                && f.Match.SnapshotVisibleTransportTags(wicker).GetValueOrDefault((int)GameTag.HEALTH)==9,"four leg gains project on body");
            Check(wicker.AppliedEnchantments.Count(e=>e.Card.Id=="CATA_139e")==4,"each mirrored gain has exported body actor");
            Check(f.Entries.All(e=>!e.HasTagChange || e.TagChange.Tag<30000),"no internal tags emitted");
        }
        Console.WriteLine("PASS B16 stat native: hidden hand/deck negative privacy, both seats, retail actors/stats/keywords, internal filtering");
    }
}
