using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
internal static class B18ODeckCardsProof
{
    [GroupProof("b18-o-deck-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_397",(g,p,id)=> {
            int count=p.DeckZone.Count;
            var card=(Minion)Play(g,p,id);
            Core2026Proof.Check(card.HasTaunt && card.AttackDamage==5 && card.Health==6,"Beatrix printed body/Taunt");
            Core2026Proof.Check(p.DeckZone.Count==count,"generated Beatrix does not expand deck");
            card.Silence();Core2026Proof.Check(!card.HasTaunt,"Taunt removed by Silence");
        });
        StandardSetsProof.Case("JAIL_831",(g,p,id)=> {
            Play(g,p,id);Core2026Proof.Check(p.Choice==null,"Unpinned_generated King has no sideboard/choice");
            var beasts=B18ODeckProof.Beasts();
            foreach(var beast in beasts) PayloadRules.StoreCard(p,Cards.FromId(id).AssetId,Cards.FromId(beast),0);
            Play(g,p,id);
            Core2026Proof.Check(p.Choice.Choices.Select(e=>g.IdEntityDic[e].Card.Id).SequenceEqual(beasts),"Unpinned_configured offers");
            Core2026Proof.Check(B18ODeckProof.Answer(g,p.Choice.Choices[2]),"choice accepted");
            var gained=p.HandZone.Last();Core2026Proof.Check(gained.Card.Id==beasts[2] && gained.Cost==System.Math.Max(0,gained.Card.Cost-3),"fresh discounted chosen Beast");
        });
    }
}
