using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class GlaciateProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_AV_107", (g, p, id) => {
        var replay = GiftedDiscoverProof.New(g.Seed, CardClass.WARRIOR);
        var owner = replay.Player1;
        // Tess must finish the replayed Discover, not consume its choice and strand the recipe.
        owner.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        PoolPilotProof.Resume(replay, next => {
            Core2026Proof.Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " Tess replay completes Discover synchronously");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1,
                id + " Tess replay completes cast once");
            Core2026Proof.Check(next.Player1.BoardZone.Count(m => m.Card.Cost == 8 && m.IsFrozen) == 1,
                "Tess Glaciate summons and freezes the selected minion");
            Core2026Proof.End(next);
        });

        p.AddHeroAndPower(Cards.FromId("HERO_02")); CoreCostProof.Count(g, id);
        B14K3BEProof.Pool(g,PinnedPoolFixture.Manifest.Standard.Select(card=>card.CardId).ToArray(),()=>{
        p.BoardZone.Add((BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")));
        Summon(p, "CS2_182"); Summon(p.Opponent, "CS2_182");
        p.BoardZone.Add((BoardObject)Entity.FromCard(p, Cards.FromId("CORE_REV_990")));
        foreach (bool drake in new[] { false, true })
        {
            // Keep the seeded Drake fixture stable as live admission grows.
            if (drake) g.SetRandomSeed(1);
            EmptyHand(p); FillHand(p, 9); var source = Play(g, p, id);
            Core2026Proof.Check(g.SpellCompletion?.SourceId == source.Id && source.Zone != p.GraveyardZone,
                "Glaciate waits for Discover before completing its cast");
            var offers = p.Choice.Choices.ToArray();
            Core2026Proof.Check(offers.Length == 3 && offers.All(x => g.IdEntityDic[x].Card.Cost == 8), "three eight-cost offers");
            int selectedId = drake ? offers.Single(x => g.IdEntityDic[x].Card.Id == "CORE_UNG_848") : offers[2];
            string selectedCardId = g.IdEntityDic[selectedId].Card.Id;
            FillHand(p);
            var hand = p.HandZone.Select(c => c.Id).ToArray(); var grave = p.GraveyardZone.Select(c => c.Id).ToArray();
            var slots = p.BoardZone.Slots.Select(c => c.Id).ToArray();
            var damage = p.BoardZone.Concat(p.Opponent.BoardZone).ToDictionary(c => c.Id, c => c.Damage);
            Resume(g, other => {
                CoreCostProof.Answer(other, selectedId); var owner = other.Player1; var board = owner.BoardZone;
                var chosen = (Minion)other.IdEntityDic[selectedId];
                Core2026Proof.Check(chosen.Card.Id == selectedCardId && chosen.Zone == board && chosen.IsFrozen
                    && board.SlotCount == slots.Length + 1 && board.GetSlot(slots.Length).Id == selectedId
                    && board.Slots.Take(slots.Length).Select(c => c.Id).SequenceEqual(slots),
                    "exact selected Glaciate entity/card frozen at physical far right of mixed board");
                Core2026Proof.Check(owner.HandZone.Select(c => c.Id).SequenceEqual(hand)
                    && owner.GraveyardZone.Select(c => c.Id).SequenceEqual(grave.Append(source.Id))
                    && offers.Where(x => x != selectedId).All(x => other.IdEntityDic[x].Zone == owner.SetasideZone),
                    "direct summon never changes full hand or burns any offered card");
                Core2026Proof.Check(damage.All(pair => ((Minion)other.IdEntityDic[pair.Key]).Damage == pair.Value),
                    "summoned minion Battlecry does not damage allies or enemies, including Primordial Drake");
            });
        }
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        EmptyHand(p); var blockedSource = Play(g, p, id);
        var fullOffers = p.Choice.Choices.ToArray(); int blockedId = fullOffers[2];
        string blockedCardId = g.IdEntityDic[blockedId].Card.Id;
        var fullSlots = p.BoardZone.Slots.Select(c => c.Id).ToArray(); var fullGrave = p.GraveyardZone.Select(c => c.Id).ToArray();
        Resume(g, other => {
            CoreCostProof.Answer(other, blockedId); var owner = other.Player1;
            Core2026Proof.Check(owner.BoardZone.Slots.Select(c => c.Id).SequenceEqual(fullSlots) && owner.HandZone.Count == 0
                && owner.GraveyardZone.Select(c => c.Id).SequenceEqual(fullGrave.Append(blockedSource.Id))
                && other.IdEntityDic[blockedId].Card.Id == blockedCardId
                && fullOffers.All(x => other.IdEntityDic[x].Zone == owner.SetasideZone),
                "full mixed board leaves exact selected identity set aside; no hand card, burn or eighth slot");
        });
        });
    });
}
