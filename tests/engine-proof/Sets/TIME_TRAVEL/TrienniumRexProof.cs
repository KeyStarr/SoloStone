using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
using static TimeTravelPoolProof;

internal static class TrienniumRexProof
{
    internal static void Run() => StandardSetsProof.Case("END_015", (g, p, id) => {
        var filter = TimeTravelPoolProof.Filter(id);
        Core2026Proof.Check(filter == OfflinePoolFilter.Random(type: CardType.MINION, keyword: GameTag.DEATHRATTLE) &&
            filter.ClassMode == PoolClassMode.Any, "random Deathrattle minion pool is any class (offline pool policy)");
        int dbf = Cards.FromId(id).AssetId;
        var hunter = OfflineCardPools.Candidates(g, CardClass.HUNTER, filter, dbf);
        var mage = OfflineCardPools.Candidates(g, CardClass.MAGE, filter, dbf);
        Core2026Proof.Check(hunter.Count >= 3 && hunter.Select(c => c.Id).SequenceEqual(mage.Select(c => c.Id)) &&
            hunter.Any(c => c.Class != CardClass.HUNTER && c.Class != CardClass.NEUTRAL) &&
            hunter.All(c => c.Type == CardType.MINION && c[GameTag.DEATHRATTLE] > 0 && c.AssetId != dbf),
            "class-independent Deathrattle minions with off-class members; source excluded");

        // No Kindred: nothing was played on the previous turn.
        EmptyHand(p);
        var first = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.IsRace(Race.BEAST) && first.HasDeathrattle && p.HandZone.Count == 0, "no Kindred: no Battlecry card");
        // Own Beast this turn and the opponent's Beast on its turn never activate Kindred.
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "current-turn Beast does not activate Kindred");
        StandardSetsProof.ClearBoard(g, p); EmptyHand(p);
        End(g); Play(g, p.Opponent, "CS2_171"); End(g); End(g); Play(g, p.Opponent, "CS2_171"); End(g);
        EmptyHand(p); Play(g, p, id);
        // The Beast played two own turns ago (Rex above) is stale; the opponent's Beast does not count.
        Core2026Proof.Check(p.HandZone.Count == 0, "stale own Beast and opponent previous-turn Beast do not activate Kindred");

        // Kindred: own Beast (Rex) played on the previous own turn.
        StandardSetsProof.ClearBoard(g, p); EmptyHand(p);
        End(g); End(g); EmptyHand(p);
        var source = (Minion)Add(p, id);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        RandomGenerated(g, source, p.HandZone, 1, discount: 2);
        var kindredCard = p.HandZone.Single();
        Core2026Proof.Check(kindredCard.Card.Cost >= 0 && kindredCard.Cost == Math.Max(0, kindredCard.Card.Cost - 2), "Kindred card costs (2) less");

        // Deathrattle adds a second, independent discounted card (Kindred and Deathrattle both resolve).
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        RandomGenerated(g, source, p.HandZone, 2, discount: 2);
        End(g); End(g);
        Core2026Proof.Check(p.HandZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).Count() == 2 &&
            p.HandZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).All(c => c.Cost == Math.Max(0, c.Card.Cost - 2) && c[GameTag.COST] == c.Cost), "discount is permanent across turns");

        // Summoned (not played) Rex has no Kindred Battlecry; Silence removes its Deathrattle.
        EmptyHand(p);
        var summoned = Summon(p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "summon skips Kindred Battlecry");
        summoned.Silence(); Kill(g, summoned);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes Deathrattle");

        // Opponent's Rex gives its owner the card.
        var enemyRex = Summon(p.Opponent, id); EmptyHand(p.Opponent);
        Kill(g, enemyRex);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Opponent.HandZone.Count == 1 &&
            p.Opponent.HandZone.Single()[GameTag.DISPLAYED_CREATOR] == enemyRex.Id, "Deathrattle card goes to the dying minion's owner");
        EmptyHand(p.Opponent);

        // Empty/one-member pools, full hand (pick still consumed), duplicates with replacement.
        RandomBoundaries(g, p, id, rex => Resolve(g, rex, death: true), discount: 2);
        StandardSetsProof.ClearBoard(g, p);
    });
}
