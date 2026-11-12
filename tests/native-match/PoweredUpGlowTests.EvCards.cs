using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-ev-cards", ProofKind.PoweredUp)]
    private static void EvCardsGlow()
    {
        Scenario("CATA_EVENT_002", (f,card,met) => {
            f.Summon("CS2_182", f.Game.Player2);
            if(met) f.Process(PlayCardTask.SpellTarget(f.Player,f.Draw("CORE_CS2_029"),f.Game.Player2.Hero));
        }, (f,card) => f.Player.CardsPlayedThisTurn.Count>0
            ? PlayCardTask.MinionTarget(f.Player,card,f.Game.Player2.BoardZone.Single())
            : PlayCardTask.Minion(f.Player,card), (f,card) => f.Game.Player2.BoardZone.Count==0);

    }
    [GroupProof("native-ev-cards", ProofKind.Native)]
    private static void EvCardsNative()
    {
        foreach(bool combo in new[]{false,true})
        {
            var f=new Fixture(); var bribe=f.Draw("CATA_EVENT_402");
            var victim=f.Summon("CS2_182",f.Game.Player2);
            if(combo) f.Process(PlayCardTask.Spell(f.Player,f.Draw("GAME_005")));
            f.Flush();
            Check(f.Match.SnapshotVisibleTransportTags(bribe)[(int)GameTag.COMBO]==1 && f.Player.IsComboActive==combo,
                "native Combo derives from printed COMBO and controller COMBO_ACTIVE");
            f.Process(PlayCardTask.SpellTarget(f.Player,bribe,victim));
            Check(f.Game.Player2.HandZone.Count>0 && f.Player.HandZone.Any(c=>c.Card.Id=="GAME_005")==combo,
                "native Combo/noncombo recipients match condition");
        }


        foreach (bool copyToEnemy in new[] { false, true })
        {
            var f = new Fixture();
            var recipient = copyToEnemy ? f.Game.Player2 : f.Player;
            var foreign = recipient.Opponent;
            foreach (var card in foreign.HandZone.ToArray()) foreign.SetasideZone.Add(foreign.HandZone.Remove(card));
            foreign.DeckZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(foreign, Cards.FromId("CS2_029")));
            recipient.DeckZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(recipient, Cards.FromId("CS2_182")));
            var lookout = Generic.DrawCard(foreign, Cards.FromId("EDR_950"));
            f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
            int secret = lookout[GameTag.TAG_SCRIPT_DATA_NUM_6];
            Check(secret != 0 && secret != recipient.DeckZone.Last().Card.AssetId, "distinct nonzero deck tops exercise private preview copy");
            if (copyToEnemy) f.Process(EndTurnTask.Any(f.Player));
            recipient.BaseMana = 10; recipient.UsedMana = 0;
            var vision = Generic.DrawCard(recipient, Cards.FromId("CS2_003"));
            f.Process(PlayCardTask.Spell(recipient, vision));
            var copy = recipient.HandZone.Single(c => c.Card.Id == "EDR_950");
            Check(!f.Entries.Any(e =>
                e.HasTagChange && e.TagChange.Entity == copy.Id && e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_6 && e.TagChange.Value == secret
                || e.HasFullEntity && e.FullEntity.Entity == copy.Id && e.FullEntity.Tags.Any(t => t.Name == (int)GameTag.TAG_SCRIPT_DATA_NUM_6 && t.Value == secret)
                || e.HasShowEntity && e.ShowEntity.Entity == copy.Id && e.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.TAG_SCRIPT_DATA_NUM_6 && t.Value == secret)
                || e.HasChangeEntity && e.ChangeEntity.Entity == copy.Id && e.ChangeEntity.Tags.Any(t => t.Name == (int)GameTag.TAG_SCRIPT_DATA_NUM_6 && t.Value == secret)),
                "Mind Vision never projects source controller deck preview, including transient TAG_CHANGE");
            Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_6] == recipient.DeckZone.Last().Card.AssetId, "copied Lookout previews recipient deck instead");
            var before = f.Game.SaveSnapshot();
            var fair = FairBotState.Create(f.Game, foreign.PlayerId, 42, DeckStyle.Control);
            var simulation = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fair);
            FairViewHiddenOfferProof.AssertHiddenActors(f.Game, simulation, recipient.PlayerId, copy.Id);
            Check(before.SequenceEqual(f.Game.SaveSnapshot()), "copy privacy simulation leaves authoritative state unchanged");
        }
        foreach(bool enemy in new[]{false,true})
        {
            var f=new Fixture(); var owner=enemy?f.Game.Player2:f.Player;
            var top=owner.DeckZone.Last();
            var lookout=Generic.DrawCard(owner,Cards.FromId("EDR_950"));
            f.Game.DeathProcessingAndAuraUpdate();f.Flush();
            Check(lookout[GameTag.TAG_SCRIPT_DATA_NUM_6]==top.Card.AssetId,"Lookout engine preview matches top");
            Check(!f.Entries.Any(e=>e.HasTagChange && e.TagChange.Entity==lookout.Id && e.TagChange.Tag==(int)GameTag.TAG_SCRIPT_DATA_NUM_6 && e.TagChange.Value==top.Card.AssetId) || !enemy,"opponent preview remains private");
            if(enemy) {f.Process(EndTurnTask.Any(f.Player)); owner.BaseMana=10;owner.UsedMana=0;}
            int drawnId=owner.DeckZone.Last().Id;
            f.Process(PlayCardTask.Minion(owner,lookout));
            Check(lookout[GameTag.TAG_SCRIPT_DATA_NUM_6]==0,"public Lookout has no hidden preview");
            Check(!f.Entries.Any(e=>e.HasFullEntity && e.FullEntity.Entity==drawnId && e.FullEntity.Name==f.Game.IdEntityDic[drawnId].Card.Id
                || e.HasShowEntity && e.ShowEntity.Entity==drawnId && e.ShowEntity.Name==f.Game.IdEntityDic[drawnId].Card.Id) || !enemy,"opponent draw identity stays private");
            foreach(var id in new[]{"CATA_EVENT_401","CATA_EVENT_400"})
            {
                var held=Generic.DrawCard(owner,Cards.FromId(id));owner.BaseMana=10;owner.UsedMana=0;
                f.Process(PrepareCardTask.Any(owner,held));
                Check(held[GameTag.PREPARING]==1 && held.Cost==0,"Prepare locks and discounts actual card");
                Check(!PoweredUp.HasCondition(held.Card),"Prepare alone does not invent powered-up condition");
            }
            var before=f.Game.SaveSnapshot();
            var fair=FairBotState.Create(f.Game,owner.Opponent.PlayerId,42,DeckStyle.Control);
            var game=(SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(fair);
            FairViewHiddenOfferProof.AssertHiddenActors(f.Game, game, owner.PlayerId, drawnId);
            Check(before.SequenceEqual(f.Game.SaveSnapshot()),"bot projection leaves authoritative state unchanged");
            f.Process(ConcedeTask.Any(f.Game.CurrentPlayer));
            Check(f.Game.State==State.COMPLETE,"Concede remains available after Prepare");
        }
    }
}
