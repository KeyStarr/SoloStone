using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class AmberWardenProof
{
    private static string[] Past => OfflinePoolExpectations.Ids(PoolScope.Past)
        .Where(id => Cards.FromId(id).Type == CardType.MINION && Cards.FromId(id)[(GameTag)2247] == 0).ToArray();

    internal static void Run() => StandardSetsProof.Case("TIME_052", (g, p, id) => {
        var filter = PoolPilotProof.Filter(id);
        Core2026Proof.Check(filter == OfflinePoolFilter.Random(type: CardType.MINION, scope: PoolScope.Past), "Past any-class minion pool");
        SourceExcluded(g, id);
        foreach (CardClass hero in new[] { CardClass.DEATHKNIGHT, CardClass.DEMONHUNTER, CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE,
            CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR })
            Core2026Proof.Check(OfflineCardPools.Candidates(g, hero, filter, Cards.FromId(id).AssetId, PoolDestination.Summon).Select(c => c.Id).SequenceEqual(Past),
                "exact eligible Past minions for " + hero);

        EmptyHand(p);
        var played = (Minion)Play(g, p, id);
        Core2026Proof.Check(played.HasTaunt && played.AttackDamage == 4 && played.Health == 12 && p.BoardZone.Count == 1 && p.HandZone.Count == 0,
            "4/12 Taunt; nothing on play");

        // Deathrattle summons into the dying minion's physical slot: [location, left, OUTPUT, right].
        Minion source = null;
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id));
        var random = g.Random.Clone();
        string expected = Past[random.Next(Past.Length)];
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var generated = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        Generated(g, source, generated, 1);
        Core2026Proof.Check(generated.Single().Card.Id == expected && g.Random.CaptureState().Equals(random.CaptureState()),
            "one RNG draw picks the summoned Past minion");
        CheckDeathrattleSlot(p, board, generated, "Amber Warden summon takes the dying slot");

        // Empty pool: no RNG, no entity. One-member pool: summons it.
        ParkBoard(p);
        source = Summon(p, id);
        Pool(g, id, 0, () => {
            var state = g.Random.CaptureState(); int entities = g.IdEntityDic.Count; Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 0 && g.Random.CaptureState().Equals(state) && g.IdEntityDic.Count == entities, "empty pool: nothing summoned, no RNG");
        });
        Pool(g, id, 1, () => {
            source = Summon(p, id); Kill(g, source);
            Generated(g, source, p.BoardZone, 1);
        });

        // Full board: the pick is still drawn, nothing summoned.
        ParkBoard(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = Summon(p, id);
        long draws = g.Random.DrawCount;
        Resolve(g, source, death: true);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.All(m => !Past.Contains(m.Card.Id)) &&
            g.Random.DrawCount == draws + 1, "full board: one draw, no eighth slot");

        // Silence removes the Deathrattle; an enemy Warden summons for its owner.
        ParkBoard(p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes Deathrattle");
        var enemyWarden = Summon(p.Opponent, id); Kill(g, enemyWarden);
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count(m => m[GameTag.DISPLAYED_CREATOR] == enemyWarden.Id) == 1,
            "summon goes to the dying minion's owner");
        ParkBoard(p.Opponent);
    });
}
