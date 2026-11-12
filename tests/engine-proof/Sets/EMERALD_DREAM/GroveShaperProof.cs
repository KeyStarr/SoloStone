using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GroveShaperProof
{
    private static void Treant(Game g, Controller p, Minion source)
    {
        var token = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
        Core2026Proof.Check(token.Card.Id == "EDR_271t" && token.Card.AssetId == 117400 && token.Card.Implemented
            && token.AttackDamage == 2 && token.Health == 2 && token.HasDeathrattle && token.Controller == p
            && token.Zone == p.BoardZone && token.ZonePosition == source.ZonePosition + 1, "exact 2/2 Deathrattle Treant right of source");
        PoolPilotProof.EmptyHand(p);
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[token.Id]));
        Core2026Proof.Check(p.HandZone.Count == 1, "Treant death generates exactly one spell");
        var generated = p.HandZone.Single();
        int creator = generated[GameTag.DISPLAYED_CREATOR];
        if (generated.Card.Shatter is { } family)
        {
            var half = g.IdEntityDic[creator];
            var parent = g.IdEntityDic[half[GameTag.DISPLAYED_CREATOR]];
            Core2026Proof.Check(generated[SabberStoneCore.Actions.ShatterRules.AlreadyCombined] == 1 &&
                half.Card.Id == family.FirstHalfId && parent.Card.Id == family.ParentId,
                "empty-hand generated Shatter parent splits then immediately recombines");
            creator = parent[GameTag.DISPLAYED_CREATOR];
        }
        var filter = EmeraldDreamPoolProof.Filter(source.Card.Id);
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, filter, token.Card.AssetId);
        Core2026Proof.Check(pool.Any(c => c.Id == generated.Card.Id) && generated.Card.Type == CardType.SPELL
            && g.OfflinePools.Standard.Single(m => m.CardId == generated.Card.Id).SpellSchool == "NATURE"
            && generated.Card.Id != token.Card.Id && generated.Controller == p && generated.Zone == p.HandZone
            && generated.ZonePosition == 0 && creator == token.Id && g.IdEntityDic[generated.Id] == generated,
            "declared any-class Nature pool, fresh hand entity/owner/position/creator");
    }

    internal static void Run() => StandardSetsProof.Case("EDR_271", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Add(p, id); var right = Summon(p, "CS2_182");
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle source placement");
        LocationProofs.Location(p, position: 0);
        Play(g, p, "GAME_005");
        Core2026Proof.Check(p.BoardZone.SlotCount == 4, "non-Nature spell and minion do not trigger");
        End(g); Play(g, p.Opponent, "EDR_848", p.Opponent.Hero); End(g);
        Core2026Proof.Check(p.BoardZone.SlotCount == 4, "opponent Nature spell does not trigger");
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, "EDR_848", next.Player1.Hero));
        Treant(g, p, source);
        Core2026Proof.Check(p.BoardZone.GetSlot(source.ZonePosition - 1) == left && p.BoardZone.GetSlot(source.ZonePosition + 1) == right, "location and neighbors preserved");
        Play(g, p, "EX1_169"); Treant(g, p, source);
        PoolPilotProof.EmptyHand(p);
        foreach (int size in new[] { 0, 1, 2 })
            EmeraldDreamPoolProof.WithPool(g, id, size, () => {
                var token = Summon(p, "EDR_271t"); long before = g.Random.DrawCount;
                Kill(g, token);
                Core2026Proof.Check(p.HandZone.Count == (size == 0 ? 0 : 1) && g.Random.DrawCount - before == (size == 0 ? 0 : 1), "empty/tiny token death pool");
                PoolPilotProof.EmptyHand(p);
            });
        PoolPilotProof.FillHand(p); var burned = Summon(p, "EDR_271t"); long draws = g.Random.DrawCount; Kill(g, burned);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == draws + 1, "full hand burns generated Nature spell");
        PoolPilotProof.EmptyHand(p);
        var silentToken = Summon(p, "EDR_271t"); silentToken.Silence(); Kill(g, silentToken);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes token Deathrattle");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "EX1_169"); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full physical board blocks Treant");
        Kill(g, p.BoardZone.Last()); source.Silence(); g.DeathProcessingAndAuraUpdate(); Play(g, p, "EX1_169");
        Core2026Proof.Check(p.BoardZone.SlotCount == 6, "source Silence removes trigger");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source); Play(g, p, "EX1_169");
        Core2026Proof.Check(p.BoardZone.SlotCount == 5, "source leaving play stops trigger");
        Add(p, id); Play(g, p, "EX1_169"); Core2026Proof.Check(p.BoardZone.SlotCount == 5, "source in hand cannot trigger");
        ClearBoard(g, p);
        source = (Minion)Play(g, p, id);
        var victim = Summon(p, "CS2_182"); victim.Damage = 2;
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "EX1_238", victim);
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && p.BoardZone.SlotCount == 7
            && p.BoardZone.Count(m => m.Card.Id == "EDR_271t") == 1, "Nature spell frees full-board slot before after-cast Treant summon");
        Treant(g, p, source);
        ClearBoard(g, p);
        source = (Minion)Play(g, p, id); source.Damage = 3;
        Play(g, p, "EX1_238", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.BoardZone.SlotCount == 1 && p.BoardZone.Count == 0,
            "Nature spell killing Grove Shaper removes trigger before after-cast window; no Treant");
    });
}
