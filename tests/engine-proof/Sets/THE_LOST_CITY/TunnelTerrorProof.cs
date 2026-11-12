using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class TunnelTerrorProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_469", (g, p, id) => {
        p.Hero.Card = Cards.HeroCard(CardClass.WARLOCK);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 3 && source.Card.Cost == 3 && source.IsRace(Race.BEAST)
            && source.HasDeathrattle && p.HandZone.Count == 0, "3-Cost 4/3 Beast Deathrattle; no Battlecry grant");
        Kill(g, Summon(p.Opponent, "CS2_231"));
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Opponent.HandZone.Count == 0, "other-side death cannot trigger this Deathrattle");
        var filter = LostCityPoolProof.Filter(id);
        Core2026Proof.Check(filter.ClassMode == PoolClassMode.Any && filter.Type == CardType.MINION && filter.MinCost == 2 && filter.MaxCost == 2,
            "Unpinned_TemporaryPools TT2 any-class printed-2-Cost minions");
        Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, filter, source.Card.AssetId).Count >= 3,
            "TT2 at least three admitted candidates");
        var copied = g.CloneExact(history: true); var copier = copied.Player1;
        Kill(copied, (Minion)copied.IdEntityDic[source.Id]);
        PoolPilotProof.EmptyHand(copier);
        var undatakah = (Minion)Play(copied, copier, "TRL_537");
        Core2026Proof.Check(undatakah.AppliedEnchantments.Count(e => e.Card.Id == "TRL_537e"
            && e.CapturedCard?.Id == id) == 1, "Undatakah captured Tunnel Terror");
        var copiedRandom = copied.Random.Clone();
        var copiedPool = OfflineCardPools.Candidates(copied, copier.HeroClass, filter, undatakah.Card.AssetId);
        var copiedExpected = new[] { copiedPool[copiedRandom.Next(copiedPool.Count)].Id,
            copiedPool[copiedRandom.Next(copiedPool.Count)].Id };
        PoolPilotProof.Resume(copied, next => Kill(next, (Minion)next.IdEntityDic[undatakah.Id]));
        Core2026Proof.Check(copier.HandZone.Select(m => m.Card.Id).SequenceEqual(copiedExpected)
            && copier.HandZone.All(m => m.Card.Type == CardType.MINION && m.Card.Cost == 2
                && TemporaryRules.IsTemporary(m) && m[GameTag.DISPLAYED_CREATOR] == undatakah.Id)
            && copied.Random.CaptureState().Equals(copiedRandom.CaptureState()),
            "copied Deathrattle uses Tunnel Terror's declared pool, runtime creator and two RNG draws after clone/snapshot");
        foreach (bool opponentTurn in new[] { false, true })
        foreach (int hand in new[] { 0, 9, 10 })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            if (opponentTurn) TemporaryRulesProof.End(branch);
            PoolPilotProof.FillHand(owner, hand);
            while (!owner.BoardZone.IsFull) Summon(owner, "CS2_231");
            var board = owner.BoardZone.Slots.Where(c => c.Id != source.Id).Select(c => c.Id).ToArray();
            var predicted = branch.CloneExact(history: true);
            var pool = OfflineCardPools.Candidates(predicted, owner.HeroClass, filter, source.Card.AssetId);
            var expected = new[] { pool[predicted.Random.Next(pool.Count)].Id, pool[predicted.Random.Next(pool.Count)].Id };
            long draws = branch.Random.DrawCount; int discarded = owner.DiscardedEntities.Count;
            PoolPilotProof.Resume(branch, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
            var outputs = branch.IdEntityDic.Values.OfType<Minion>().Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).OrderBy(m => m.Id).ToArray();
            Core2026Proof.Check(outputs.Length == 2 && outputs.Select(m => m.Card.Id).SequenceEqual(expected) && branch.Random.DrawCount == draws + 2
                && branch.Random.CaptureState().Equals(predicted.Random.CaptureState()), "two sequential independent picks, including overflow, use game RNG");
            int admitted = Math.Min(2, 10 - hand);
            Core2026Proof.Check(outputs.Take(admitted).All(m => m.Zone == owner.HandZone && TemporaryRules.IsTemporary(m)
                    && m.AppliedEnchantments.Single().Card.Id == "GBL_999e")
                && outputs.Skip(admitted).All(m => m.Zone == owner.GraveyardZone && !TemporaryRules.IsTemporary(m))
                && owner.DiscardedEntities.Count == discarded, "Unpinned_TemporaryProjection sequential pre-marked grants; remaining results burn without discard");
            Batch9PoolProof.Generated(branch, branch.IdEntityDic[source.Id], outputs.Take(admitted), admitted);
            Core2026Proof.Check(owner.BoardZone.Slots.Select(c => c.Id).SequenceEqual(board), "Deathrattle grants hand cards; full board has no summons or slot changes");
            if (opponentTurn)
            {
                TemporaryRulesProof.End(branch);
                Core2026Proof.Check(outputs.Take(admitted).All(m => m.Zone == owner.HandZone), "opponent-turn grants survive opponent end");
            }
            TemporaryRulesProof.End(branch);
            Core2026Proof.Check(outputs.All(m => m.Zone == owner.GraveyardZone) && outputs.Take(admitted).All(m => owner.DiscardedEntities.Count(i => i == m.Id) == 1),
                "Unpinned_TemporaryExpiry Unpinned_TemporaryEndOrder real discard at next owner end, after source death");
        }
        var silenced = g.CloneExact(history: true); var mute = (Minion)silenced.IdEntityDic[source.Id]; mute.Silence();
        var rng = silenced.Random.CaptureState(); Kill(silenced, mute);
        Core2026Proof.Check(silenced.Player1.HandZone.Count == 0 && silenced.Random.CaptureState().Equals(rng), "Silence removes Deathrattle");
        var bounced = g.CloneExact(history: true); var returned = (Minion)bounced.IdEntityDic[source.Id]; rng = bounced.Random.CaptureState();
        Generic.ReturnToHandBlock(bounced.Player1, returned);
        Core2026Proof.Check(bounced.Player1.HandZone.Single() == returned && bounced.Random.CaptureState().Equals(rng), "non-death source exit generates nothing");
        foreach (int count in new[] { 0, 1 })
        {
            var branch = g.CloneExact(history: true);
            Batch9PoolProof.WithPools(branch, id, count, () => {
                var before = branch.Random.CaptureState(); Kill(branch, (Minion)branch.IdEntityDic[source.Id]);
                Core2026Proof.Check(branch.Player1.HandZone.Count == (count == 0 ? 0 : 2), "empty/singleton pool output count");
                if (count == 0) Core2026Proof.Check(branch.Random.CaptureState().Equals(before), "empty pool consumes no RNG");
                else Core2026Proof.Check(branch.Player1.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1, "with-replacement picks allow duplicate identities");
            });
        }
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var played = p.HandZone[0]; PlayEntity(g, p, played);
        Core2026Proof.Check(played.Zone == p.BoardZone && !TemporaryRules.IsTemporary(played), "Unpinned_TemporaryResidence playing chosen minion clears mark");
        TemporaryRulesProof.End(g);
        Core2026Proof.Check(played.Zone == p.BoardZone, "played minion survives expiry boundary");
    });
}
