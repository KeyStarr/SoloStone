using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class DarkPeddlerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_WON_096", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_07")); CoreCostProof.Count(g, id);
        foreach (bool burn in new[] { false, true })
        {
            EmptyHand(p); Play(g, p, id);
            var offers = p.Choice.Choices.ToArray();
            Core2026Proof.Check(offers.Length == 3 && offers.All(x => g.IdEntityDic[x].Card.Cost == 1), "three one-cost offers");
            int selectedId = offers[2]; string selectedCardId = g.IdEntityDic[selectedId].Card.Id;
            if (burn) FillHand(p);
            var hand = p.HandZone.Select(c => c.Id).ToArray(); var grave = p.GraveyardZone.Select(c => c.Id).ToArray();
            var board = p.BoardZone.Slots.Select(c => c.Id).ToArray();
            Resume(g, other => {
                CoreCostProof.Answer(other, selectedId); var owner = other.Player1; var chosen = other.IdEntityDic[selectedId];
                Core2026Proof.Check(chosen.Card.Id == selectedCardId && (burn ? chosen.Zone == owner.GraveyardZone : chosen.Zone == owner.HandZone),
                    "exact selected Peddler entity/card reaches hand or burn destination");
                Core2026Proof.Check(owner.HandZone.Select(c => c.Id).SequenceEqual(burn ? hand : hand.Append(selectedId))
                    && owner.GraveyardZone.Select(c => c.Id).SequenceEqual(burn ? grave.Append(selectedId) : grave)
                    && owner.BoardZone.Slots.Select(c => c.Id).SequenceEqual(board)
                    && offers.Where(x => x != selectedId).All(x => other.IdEntityDic[x].Zone == owner.SetasideZone),
                    "only selected offer changes destination across original, clone and snapshot");
                Visible(chosen, 1);
            });
        }
    });
}
