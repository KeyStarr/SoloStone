using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class BlindHarrowerProof
{
    private static IPlayable[] Generated(Controller p, IPlayable source) =>
        p.HandZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();

    internal static void Run() => StandardSetsProof.Case("BE_264", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_10"));
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var report = OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }); report.ValidateRegistered();
        var requirement = report.Cards.Single().PoolRequirements.Single();
        Core2026Proof.Check(requirement.Class == "DEMONHUNTER" && requirement.Count >= OfflinePoolAdmission.MinimumCandidates,
            "Outcast pool >= 3 for the only playable class (Demon Hunter): " + requirement.Count);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.All(c => c[GameTag.OUTCAST] == 1 && c.AssetId != Cards.FromId(id).AssetId), "pool: Outcast cards, source excluded");
        SourceExcluded(g, id);

        // Outcast: a single card is at the edge; Brann does not double it (no BATTLECRY tag).
        EmptyHand(p);
        var source = (Minion)Add(p, id);
        PlayEntity(g, p, source);
        var outputs = Generated(p, source);
        PoolPilotProof.Generated(g, source, outputs, 1);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 2 && source.HasDeathrattle && !source.HasBattleCry, "3/2 Deathrattle Beast");

        // Tier C: outof.games/realms/hearthstone/keywords/battlecry, pseudo-Battlecries (R4 in outcast.md).
        // Right edge with another card held: still once with Brann present (Outcast is not a Battlecry).
        EmptyHand(p);
        var brann = Summon(p, "LOE_077");
        Add(p, "CS2_182"); var right = (Minion)Add(p, id);
        Resume(g, other => {
            var copy = other.IdEntityDic[right.Id]; PlayEntity(other, other.Player1, copy);
            PoolPilotProof.Generated(other, copy, Generated(other.Player1, copy), 1);
        });
        Kill(g, brann);

        // Middle of hand: no Outcast card and no RNG.
        EmptyHand(p);
        Add(p, "CS2_182"); var middle = (Minion)Add(p, id); Add(p, "CS2_182");
        long draws = g.Random.DrawCount;
        PlayEntity(g, p, middle);
        Core2026Proof.Check(p.HandZone.Count == 2 && Generated(p, middle).Length == 0 && g.Random.DrawCount == draws, "middle play gets nothing");

        // Left edge also qualifies.
        EmptyHand(p);
        var left = (Minion)Add(p, id); Add(p, "CS2_182"); Add(p, "CS2_182");
        p.HandZone.Remove(left); p.HandZone.Add(left, 0);
        Core2026Proof.Check(left.ZonePosition == 0, "left-edge fixture");
        PlayEntity(g, p, left);
        PoolPilotProof.Generated(g, left, Generated(p, left), 1);

        // Deathrattle: killing the minion gives one more card (played from the middle: Deathrattle only).
        EmptyHand(p);
        Kill(g, middle);
        PoolPilotProof.Generated(g, middle, Generated(p, middle), 1);

        // Summoned copy: no Outcast, still a Deathrattle.
        EmptyHand(p); draws = g.Random.DrawCount;
        var summoned = Summon(p, id);
        Core2026Proof.Check(p.HandZone.Count == 0 && g.Random.DrawCount == draws, "summon has no Outcast");
        Resume(g, other => {
            var copy = (Minion)other.IdEntityDic[summoned.Id]; Kill(other, copy);
            PoolPilotProof.Generated(other, copy, other.Player1.HandZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == copy.Id), 1);
        });

        // Silence removes the Deathrattle.
        EmptyHand(p);
        source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "silenced Harrower gives nothing on death");

        // Opponent-side Harrower death goes to the opponent's hand only.
        EmptyHand(p); EmptyHand(p.Opponent);
        var enemy = Summon(p.Opponent, id); Kill(g, enemy);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Opponent.HandZone.Count == 1
            && p.Opponent.HandZone.Single()[GameTag.DISPLAYED_CREATOR] == enemy.Id, "opponent Deathrattle generates for its owner");
        EmptyHand(p.Opponent);

        // A real death into a full hand burns the exact generated Outcast card.
        FillHand(p);
        Kill(g, left);
        var burned = p.GraveyardZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == left.Id).ToArray();
        Core2026Proof.Check(p.HandZone.Count == 10 && burned.Length == 1 && burned[0].Zone == p.GraveyardZone
            && burned[0].Card[GameTag.OUTCAST] == 1, "full-hand Deathrattle burns one Outcast card");
        EmptyHand(p);

        // Empty/singleton pool and full hand: Deathrattle path.
        HandBoundaries(g, p, id, 1, death: true);
    });
}
