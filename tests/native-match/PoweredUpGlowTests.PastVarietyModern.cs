using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-pastvar-modern", ProofKind.PoweredUp)]
    private static void ModernGoldwingGlow()
    {
        Scenario("NX2_022",(f,card,met)=> {
            if(met)f.Draw("YOD_010");else f.Summon("YOD_010",f.Player.Opponent);
        },(f,card)=>PlayCardTask.Any(f.Player,card),(f,card)=>((Minion)card).HasWindfury);
    }
    private static Minion ModernSummon(Controller owner,string id)
    {
        var minion=(Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner,Cards.FromId(id));
        Generic.SummonBlock(owner.Game,minion,-1,owner.Hero);owner.Game.DeathProcessingAndAuraUpdate();return minion;
    }
    [GroupProof("native-pastvar-modern", ProofKind.Native)]
    private static void ModernNative()
    {
        foreach(bool second in new[]{false,true}) {
            var f=new Fixture();var g=f.Game;if(second)Check(g.Process(EndTurnTask.Any(f.Player)),"modern opponent turn");
            var p=g.CurrentPlayer;p.BaseMana=10;p.UsedMana=0;
            foreach(var held in p.HandZone.ToArray())p.SetasideZone.Add(p.HandZone.Remove(held));
            foreach(var held in p.DeckZone.ToArray())p.SetasideZone.Add(p.DeckZone.Remove(held));
            // Bind the real Alternate Reality recipe to a one-card historical test bucket.
            var xml=XDocument.Load("engine/SabberStoneCore/resources/Data/OfflinePoolCatalog.xml");var rows=xml.Root.Element("Members");
            rows.Elements("Card").Where(c=>(string)c.Attribute("scope")=="Past").Remove();
            var drum=Cards.FromId("ETC_373");
            rows.Add(new XElement("Card",new XAttribute("cardId",drum.Id),new XAttribute("dbfId",drum.AssetId),new XAttribute("scope","Past"),new XAttribute("set","BATTLE_OF_THE_BANDS"),
                new XAttribute("proofKey","PastVarietyModernProof.Run"),new XAttribute("registrationKind","standard-module"),new XAttribute("type","SPELL"),
                new XAttribute("cost",7),new XAttribute("attack",0),new XAttribute("health",0),new XAttribute("rarity","RARE"),new XAttribute("spellSchool","NATURE"),
                new XElement("Class",new XAttribute("name","DRUID")),drum.Tags.Select(t=>new XElement("Tag",new XAttribute("id",(int)t.Key),new XAttribute("value",t.Value)))));
            var policy=(OfflinePoolPolicy)typeof(OfflinePoolPolicy).GetMethod("Parse",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{xml});
            var manifest=policy.BuildManifest();
            typeof(SabberStoneCore.Model.Game).GetMethod("RestoreOfflinePools",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(g,new object[]{manifest});
            Generic.DrawCard(p,Cards.FromId("CS2_182"));SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("CS2_182"),zone:p.DeckZone);
            var alternate=Generic.DrawCard(p,Cards.FromId("TIME_707"));f.Flush();
            Check(g.Process(PlayCardTask.Any(p,alternate)),"native Alternate play");f.Flush();
            var hand=p.HandZone.Single();var deck=p.DeckZone.Single();
            Check(hand.Card.Id=="ETC_373"&&deck.Card.Id=="ETC_373"&&hand.Cost==6&&deck.Cost==6,"native discounted Drum hand/deck delivery");
            foreach(var hidden in new[]{deck}.Concat(deck.ChooseOnePlayables))AssertB17Hidden(f,hidden.Id,true);
            foreach(var held in new[]{hand}.Concat(hand.ChooseOnePlayables))AssertB17Hidden(f,held.Id,second);
            var drawn=Generic.Draw(p);f.Flush();Check(drawn.Id==deck.Id&&drawn.Cost==6,"native draw retains discount");
            if(second)foreach(var hidden in new[]{drawn}.Concat(drawn.ChooseOnePlayables))AssertB17Hidden(f,hidden.Id,true);
            else Check(f.Match.SnapshotVisibleTransportTags(drawn)[(int)GameTag.COST]==6,"native owner sees six mana");
            var ally=ModernSummon(p,"CS2_182");p.UsedMana=0;
            Check(p.Options().Any(t=>t.Source?.Id==hand.Id&&t.ChooseOne==2),"native Drum buff branch clickable");
            f.Process(PlayCardTask.Any(p,hand,chooseOne:2));
            var tags=f.Match.SnapshotVisibleTransportTags(ally);
            Check(tags[(int)GameTag.ATK]==6&&tags[(int)GameTag.HEALTH]==9&&tags[(int)GameTag.TAUNT]==1,"native +2/+4 Taunt result");
            p.UsedMana=0;Check(p.Options().Any(t=>t.Source?.Id==drawn.Id&&t.ChooseOne==1),"native Drum summon branch clickable");
            f.Process(PlayCardTask.Any(p,drawn,chooseOne:1));
            Check(p.BoardZone.Count(m=>m.Card.Id=="ETC_373t")==5,"native five real Treant actors");
            foreach(var token in p.BoardZone.Where(m=>m.Card.Id=="ETC_373t")) {
                var projected=f.Match.SnapshotVisibleTransportTags(token);Check(token.Card.AssetId==91248&&projected[(int)GameTag.ATK]==2&&projected[(int)GameTag.HEALTH]==2,"native Treant export identity and stats");
            }
        }
        foreach(bool second in new[]{false,true}) {
            var f=new Fixture();var g=f.Game;if(second)Check(g.Process(EndTurnTask.Any(f.Player)),"Survival opponent turn");var p=g.CurrentPlayer;
            var held=Generic.DrawCard(p,Cards.FromId("YOD_010"));var deck=SabberStoneCore.Model.Entities.Entity.FromCard(p,Cards.FromId("YOD_010"),zone:p.DeckZone);var ally=ModernSummon(p,"YOD_010");
            var spell=Generic.DrawCard(p,Cards.FromId("SCH_609"));p.BaseMana=10;p.UsedMana=0;f.Flush();
            Check(g.Process(PlayCardTask.Any(p,spell)),"native Survival play");f.Flush();
            Check(((Minion)held).AttackDamage==6&&((Minion)deck).AttackDamage==6&&ally.AttackDamage==6,"Survival all three zones");
            AssertB17Hidden(f,held.Id,second);AssertB17Hidden(f,deck.Id,true);
            var enchants=g.IdEntityDic.Values.OfType<Enchantment>().Where(e=>e.Card.Id=="SCH_609e"&&new[]{held.Id,deck.Id}.Contains(e[GameTag.ATTACHED])).ToArray();
            foreach(var enchant in enchants)AssertB17Hidden(f,enchant.Id,enchant[GameTag.ATTACHED]==deck.Id||second);
        }
        Console.WriteLine("PASS modern native branch privacy, discounted hand/deck delivery, clickable branches and hidden Survival buffs on both seats");
    }
}
