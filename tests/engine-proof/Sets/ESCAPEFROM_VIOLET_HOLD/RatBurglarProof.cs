using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class RatBurglarProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_205", (g, p, id) => {
        var old = Add(p.Opponent, "CS2_231");
        End(g); var enemyTurn = Add(p.Opponent, "CS2_231"); End(g);
        var generated = Add(p.Opponent, "CS2_023");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CAP_005e4"), p.Opponent.Hero, generated);
        Core2026Proof.Check(generated.Cost == 0, "permanent minus-three enchantment discounts enemy card before theft");
        var drawn = p.Opponent.DeckZone.TopCard; Generic.Draw(p.Opponent, drawn);
        var bounced = Summon(p.Opponent, "CS2_182"); Generic.ReturnToHandBlock(p.Opponent, bounced);
        var playedAway = Add(p.Opponent, "GAME_005");
        p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(playedAway));
        var mine = Add(p, "CS2_231");
        var burglar = (Minion)Play(g, p, id);
        Core2026Proof.Check(burglar.AttackDamage == 2 && burglar.Health == 6, "printed Burglar 2/6");
        var clone = g.Clone(); var cloneOnly = Add(clone.Player2, "CS2_029"); End(clone);
        Core2026Proof.Check(cloneOnly.Controller == clone.Player1 && cloneOnly.Zone == clone.Player1.HandZone
            && generated.Controller == p.Opponent && generated.Zone == p.Opponent.HandZone,
            "clone sees independent new hand entry and leaves original unaffected");
        Core2026Proof.Check(clone.IdEntityDic[generated.Id][GameTag.COST] == 0 && generated.Cost == 0,
            "clone theft preserves permanent discount and original card cost");
        End(g);
        Core2026Proof.Check(new IPlayable[] { generated, drawn, bounced }.All(c => c.Controller == p && c.Zone == p.HandZone)
            && p.HandZone.Where(c => c.Id == generated.Id || c.Id == drawn.Id || c.Id == bounced.Id).Select(c => c.Id).SequenceEqual(new[] { generated.Id, drawn.Id, bounced.Id }),
            "steals actual entities in hand order: generation, draw, bounce before Burglar created");
        Core2026Proof.Check(generated.Cost == 0 && generated[GameTag.COST] == 0
            && generated.AppliedEnchantments.Any(e => e.Card.Id == "CAP_005e4"),
            "theft preserves attached permanent discount and visible COST on actual entity");
        Core2026Proof.Check(old.Zone == p.Opponent.HandZone && enemyTurn.Zone == p.Opponent.HandZone
            && playedAway.Zone == p.Opponent.SetasideZone && mine.Zone == p.HandZone,
            "previous own/opponent turns, exited cards and own hand excluded");
        var onEnemyTurn = Add(p.Opponent, "CS2_023"); End(g);
        Core2026Proof.Check(onEnemyTurn.Zone == p.Opponent.HandZone, "opponent end turn does not activate Burglar");
        // Same entity's latest entry matters, including in-place definition changes.
        var returned = old; p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(returned));
        p.Opponent.SetasideZone.Remove(returned); Generic.AddHandPhase(p.Opponent, returned);
        Generic.ChangeEntityBlock(p.Opponent, returned, Cards.FromId("CS2_029"), false);
        returned = g.IdEntityDic[returned.Id];
        var second = (Minion)Play(g, p, id); End(g);
        Core2026Proof.Check(returned.Controller == p && returned.Zone == p.HandZone && returned.Card.Id == "CS2_029",
            "latest re-entry qualifies transformed actual entity; multiple Burglars do not duplicate it");
        End(g); burglar.Silence(); second.Silence();
        var protectedCard = Add(p.Opponent, "CS2_023"); End(g);
        Core2026Proof.Check(protectedCard.Zone == p.Opponent.HandZone, "Silence removes end-turn triggers");
        End(g); Kill(g, burglar); Kill(g, second);
        var held = Add(p, id); var deckCopy = Entity.FromCard(p, Cards.FromId(id)); p.DeckZone.Add(deckCopy);
        var hiddenSafe = Add(p.Opponent, "CS2_023"); End(g);
        Core2026Proof.Check(hiddenSafe.Zone == p.Opponent.HandZone && held.Zone == p.HandZone && deckCopy.Zone == p.DeckZone,
            "dead, hand and deck Burglars cannot steal");
        End(g); PlayEntity(g, p, held);
        while (p.HandZone.Count > 9) p.SetasideZone.Add(p.HandZone.Remove(p.HandZone[0]));
        while (p.HandZone.Count < 9) Add(p, "CS2_231");
        while (p.Opponent.HandZone.Count > 7) p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(p.Opponent.HandZone[0]));
        var first = Add(p.Opponent, "CS2_029"); var overflow = Add(p.Opponent, "CS2_023"); End(g);
        Core2026Proof.Check(first.Controller == p && first.Zone == p.HandZone
            && overflow.Controller == p && overflow.Zone == p.GraveyardZone && p.HandZone.Count == 10,
            "left-to-right theft fills last slot and burns overflow without copying");
        foreach (var owner in new[] { p, p.Opponent })
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
        Add(p.Opponent, "GAME_005"); End(g);
        var adaptive = Add(p.Opponent, "JAIL_503"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(adaptive.Cost == 2, "enemy Coin discounts adaptive cost before theft");
        End(g);
        Core2026Proof.Check(adaptive.Controller == p && adaptive.Zone == p.HandZone && adaptive[GameTag.COST] == 3
            && g.Auras.Count(a => a.Owner.Id == adaptive.Id) == 1,
            "theft rebinds adaptive cost to new hand without duplicating its aura");
        End(g); PlayEntity(g, p, adaptive);
        Core2026Proof.Check(g.Auras.All(a => a.Owner.Id != adaptive.Id), "playing stolen adaptive card leaves no orphaned cost aura");
    });
}
