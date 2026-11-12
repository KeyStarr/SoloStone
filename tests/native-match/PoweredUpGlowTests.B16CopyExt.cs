using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-copy-ext", ProofKind.PoweredUp)]
    private static void B16CopyGlow()
    {
        foreach(var id in new[]{"JAIL_432","JAIL_433"})
            Scenario(id,(f,held,met)=> {
                f.Game[PayloadRules.Tracking] = 2;
                f.Summon("CS2_182",f.Game.Player2);
                if(!met) return;
                var original=Generic.DrawCard(f.Game.Player2,Cards.FromId("CS2_231"));
                var copy=Generic.Copy(f.Player,f.Player.Hero,original,SabberStoneCore.Enums.Zone.HAND);
                f.Process(PlayCardTask.Any(f.Player,copy));
            },(f,held)=>PlayCardTask.Any(f.Player,held,held.Card.Id=="JAIL_433"?f.Game.Player2.BoardZone.First():null),
                (f,held)=>held.Card.Id=="JAIL_433"?f.Player.UsedMana==1:f.Game.Player2.BoardZone.First().Damage==2);
        foreach(var id in new[]{"BE_852","BE_731","EDR_491","EDR_818","JAIL_434","JAIL_430"})
        {var f=new Fixture();var card=f.Draw(id);f.Flush();f.Expected(card,0,id);}
    }
    [GroupProof("native-b16-copy-ext", ProofKind.Native)]
    private static void B16CopyPrivacy()
    {
        NightmareCopyPrivacy();
        var f=new Fixture();var g=f.Game;var p=g.Player2;
        g[PayloadRules.Tracking] = 3;
        var original=Generic.DrawCard(f.Player,Cards.FromId("CS2_029"));
        var hand=Generic.Copy(p,p.Hero,original,SabberStoneCore.Enums.Zone.HAND);
        var deck=Generic.Copy(p,p.Hero,original,SabberStoneCore.Enums.Zone.DECK);
        var donor=f.Summon("CATA_210",f.Player);donor[GameTag.TAG_SCRIPT_DATA_NUM_1]=4;
        var recipient=f.Summon("BE_731",f.Player);
        PayloadRules.GainDeathrattles(recipient,donor,"BE_731e");
        var actor=recipient.AppliedEnchantments.Single(a=>a.Card.Id=="BE_731e");
        f.Flush();
        Check(f.Entries.Any(e=>e.HasShowEntity && e.ShowEntity.Entity==actor.Id && e.ShowEntity.Name=="BE_731e"),"retail grant enchantment identity");
        Check(f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==actor.Id && e.TagChange.Tag==(int)GameTag.TAG_SCRIPT_DATA_NUM_1)
            && f.Entries.Where(e=>e.HasTagChange && e.TagChange.Entity==actor.Id && e.TagChange.Tag==(int)GameTag.TAG_SCRIPT_DATA_NUM_1)
                .All(e=>e.TagChange.Value==donor.Id && e.TagChange.Value!=donor.Card.AssetId),
            "grant never sends the donor dbfId: the client reads this tag as an entity id (builder 19), so it names the donor minion's entity (see entity-reference-grants-native)");
        var effect=f.Draw("BE_852");f.Mana(10);f.Process(PlayCardTask.Any(f.Player,effect));
        var played=f.Draw("EX1_096");f.Mana(10);f.Process(PlayCardTask.Any(f.Player,played));
        Check(f.Player.BoardZone.Count(m=>m.Card.Id=="EX1_096")==2,"native after-play copy completes without a decision stall");
        var ids=new[]{hand.Id,deck.Id};
        var entities=f.Entries.SelectMany(e=>new[]{e.FullEntity,e.ShowEntity,e.ChangeEntity}).Where(e=>e!=null);
        Check(ids.All(id=>entities.Any(e=>e.Entity==id)) && entities.Where(e=>ids.Contains(e.Entity)).All(e=>string.IsNullOrEmpty(e.Name)),"opponent copied hand/deck identities hidden");
        Check(entities.All(e=>e.Tags.All(t=>!PayloadRules.IsInternalTag((GameTag)t.Name))) && f.Entries.Where(e=>e.HasTagChange).All(e=>!PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)),"lineage/holding/eligibility tags filtered");
    }
    private static void NightmareCopyPrivacy()
    {
        foreach (bool combo in new[] { false, true })
        {
            var f = new Fixture(); var g = f.Game; g[PayloadRules.Tracking] = 2;
            Check(g.Process(EndTurnTask.Any(f.Player)), "enemy turn for private Discover");
            var enemy = g.Player2; enemy.BaseMana = 10; enemy.UsedMana = 0;
            foreach (var card in enemy.HandZone.ToArray()) enemy.SetasideZone.Add(enemy.HandZone.Remove(card));
            enemy.IsComboActive = combo;
            var spell = Generic.DrawCard(enemy, Cards.FromId("EDR_528"));
            Check(g.Process(PlayCardTask.Any(enemy, spell)), "enemy Nightmare Fuel accepted");
            var offers = enemy.Choice.Choices.ToArray();
            Check(offers.Length > 0 && offers.All(id => PayloadRules.CopiedFromOpponent(g.IdEntityDic[id])), "private offers retain source provenance");
            f.Flush();
            var decision = g.GetPendingDecisions().Single();
            Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { offers[0] }), out _), "enemy copy selected");
            f.Flush();
            var entities = f.Entries.SelectMany(e => new[] { e.FullEntity, e.ShowEntity, e.ChangeEntity }).Where(e => e != null);
            Check(entities.Where(e => offers.Contains(e.Entity)).All(e => string.IsNullOrEmpty(e.Name)), "enemy ordinary/Combo copy offers and selected hidden card never reveal identity");
            Check(f.Entries.Where(e => e.HasTagChange).All(e => !PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)), "Discover provenance remains internal");
        }
    }
}
