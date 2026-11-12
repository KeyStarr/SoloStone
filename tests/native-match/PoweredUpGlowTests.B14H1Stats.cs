using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-h1-stats", ProofKind.PoweredUp)]
    private static void RunB14H1Glow()
    {
        Scenario("TIME_021", (f, card, met) => {
            f.Draw("CS2_182");
            if (!met) { f.Player.HandZone.Remove(card); f.Player.HandZone.Add(card); f.Draw("CS2_182"); }
        }, (f, card) => PlayCardTask.Any(f.Player,card), (f, card) => f.Player.Hero.IsImmune);
        Scenario("TLC_482", (f, card, met) => {
            if (met)
            {
                f.Process(PlayCardTask.Any(f.Player,f.Draw("GIL_681")));
                f.Process(EndTurnTask.Any(f.Player)); f.Process(EndTurnTask.Any(f.Game.Player2));
            }
        }, (f,card)=>PlayCardTask.Any(f.Player,card), (f,card)=>f.Game.Player2.Hero.Damage>0);
    }
    [GroupProof("native-b14-h1-stats", ProofKind.Native)]
    private static void RunB14H1Native()
    {
        foreach (bool opponent in new[] {false,true})
        {
            var f=new Fixture(); var g=f.Game;
            if(opponent) Check(g.Process(EndTurnTask.Any(f.Player)),"H1 opponent turn");
            var owner=opponent?g.Player2:g.Player1;
            foreach(var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var hidden=Generic.DrawCard(owner,Cards.FromId("CATA_487"));
            var secret=Generic.DrawCard(owner,Cards.FromId("END_024"));
            f.Flush(); owner.BaseMana=10; owner.UsedMana=0;
            Check(g.Process(PlayCardTask.Any(owner,hidden)),"H1 Raincaller native play"); f.Flush();
            owner.UsedMana=0; Check(g.Process(PlayCardTask.Any(owner,secret)),"H1 Secret native play"); f.Flush();
            var spell=Generic.DrawCard(owner,Cards.FromId("CS2_024"));
            owner.Opponent.Hero.Armor=20; owner.UsedMana=0;
            Check(g.Process(PlayCardTask.Any(owner,spell,owner.Opponent.Hero)),"H1 Armor spell native play");
            f.Flush();
            Check(((Minion)hidden).AttackDamage==3,"H1 native Attack layer");
            var snapshots=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null).ToArray();
            if(opponent) Check(snapshots.Where(e=>e.Entity==secret.Id).All(e=>string.IsNullOrEmpty(e.Name)),"H1 opponent Secret identity stays private");
            else Check(snapshots.Any(e=>e.Entity==secret.Id && e.Name=="END_024"),"H1 owner sees Secret");
            foreach(var actor in hidden.AppliedEnchantments)
                Check(snapshots.Any(e=>e.Entity==actor.Id && e.Name==actor.Card.Id),"H1 public board buff actor identity "+actor.Card.Id+" seat="+opponent+" snapshots="+string.Join(",",snapshots.Where(e=>e.Entity==actor.Id).Select(e=>e.Name)));
            Check(f.Entries.All(e=>!e.HasTagChange || e.TagChange.Tag<30000),"H1 no internal stat tags on wire");
            var venom=f.Summon("JAIL_459",owner); g.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(hidden).GetValueOrDefault((int)GameTag.POISONOUS)>0,"H1 effective Poisonous projects for both seats");
            venom.Silence(); g.AuraUpdate(); f.Flush();
            Check(!((Minion)hidden).Poisonous,"H1 poison aura removed");
            foreach(var m in owner.BoardZone.ToArray()) owner.SetasideZone.Add(owner.BoardZone.Remove(m));
            g.DeathProcessingAndAuraUpdate();
            var alone=f.Summon("CATA_613",owner); g.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(alone).GetValueOrDefault((int)GameTag.IMMUNE)==1,"H1 conditional Immune projects for both seats");
            f.Summon("CS2_182",owner); g.DeathProcessingAndAuraUpdate(); f.Flush();
            Check(!alone.IsImmune,"H1 conditional immunity drops with a companion");
        }
        Console.WriteLine("PASS H1 native: Armor reaction, buff actors, Secret privacy both seats");
    }
}
