using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class WallowTheWretchedProof
{
    // D8 / data/rules-evidence/dark-gift/dg17b-wallow-event-semantics.yaml:
    // duplicates stack, self-gifts count twice, mirrors never emit another grant.
    internal static void Give(Game g, IPlayable target, string spell)
    {
        var eligible = DarkGifts.Registry.Where(gift => gift.IsEligible(target)).ToArray();
        bool found = false;
        for (int seed = 1; seed <= 256; seed++) {
            g.SetRandomSeed(seed);
            if (eligible[g.Random.Clone().Next(eligible.Length)].SpellId == spell) { found = true; break; }
        }
        Core2026Proof.Check(found && DarkGifts.Give(target, target.Controller.Hero), "real requested gift fixture " + spell);
        Core2026Proof.Check(DarkGifts.GiftsOf(target).Last().Card.Id ==
            DarkGifts.Registry.Single(gift => gift.SpellId == spell).EnchantmentId, "fixture gift granted by primitive");
    }

    private static IPlayable Create(Controller p, string id, Zone zone) => Entity.FromCard(p, Cards.FromId(id),
        zone: zone == Zone.HAND ? p.HandZone : zone == Zone.DECK ? p.DeckZone :
            zone == Zone.PLAY ? p.BoardZone : p.GraveyardZone);

    private static void Scope(Game original, string id)
    {
        foreach (var zone in new[] { Zone.HAND, Zone.DECK, Zone.PLAY, Zone.GRAVEYARD })
        foreach (bool opponent in new[] { false, true }) {
            var g = original.CloneExact(history: true); var p = g.Player1;
            var wallow = Create(p, id, zone);
            var recipient = Entity.FromCard(opponent ? p.Opponent : p, Cards.FromId("CS2_182"), zone: (opponent ? p.Opponent : p).SetasideZone);
            Give(g, recipient, "EDR_100t");
            var another = Entity.FromCard(recipient.Controller, Cards.FromId("CS2_182"), zone: recipient.Controller.SetasideZone);
            Give(g, another, "EDR_100t");
            int expected = !opponent && (zone == Zone.HAND || zone == Zone.DECK) ? 2 : 0;
            Core2026Proof.Check(DarkGifts.GiftsOf(wallow).Count == expected && ((Minion)wallow).AttackDamage == 6 + 3 * expected,
                "only friendly actual grants while Wallow in hand/deck: " + zone + "/opponent=" + opponent);
            Core2026Proof.Check(DarkGifts.GiftsOf(wallow).Select(e => e.Id).Distinct().Count() == expected,
                "duplicate gifts are fresh attachments");
            if (zone == Zone.DECK) Generic.Draw(p, wallow);
            if (expected != 0) {
                PlayEntity(g, p, wallow);
                Core2026Proof.Check(wallow.Zone == p.BoardZone && ((Minion)wallow).HasLifeSteal &&
                    ((Minion)wallow).AttackDamage == 12 && ((Minion)wallow).Health == 6, "mirrored stats and keyword visible on play");
                Give(g, Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.SetasideZone), "EDR_100t");
                Core2026Proof.Check(DarkGifts.GiftsOf(wallow).Count == 2, "playing Wallow disarms listener");
            }
        }
    }

    private static void SelfAndMultiple(Game original, string id)
    {
        var g = original.CloneExact(history: true); var p = g.Player1;
        var first = Add(p, id); var second = Create(p, id, Zone.DECK);
        Give(g, first, "EDR_100t");
        Core2026Proof.Check(DarkGifts.GiftsOf(first).Count == 2 && DarkGifts.GiftsOf(second).Count == 1 &&
            ((Minion)first).AttackDamage == 12 && ((Minion)second).AttackDamage == 9, "self real grant plus mirror; other Wallow mirrors once");
        var recipient = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.SetasideZone);
        Give(g, recipient, "EDR_100t");
        Core2026Proof.Check(DarkGifts.GiftsOf(first).Count == 3 && DarkGifts.GiftsOf(second).Count == 2 &&
            DarkGifts.GiftsOf(recipient).Count == 1, "each Wallow mirrors once, no recursive event");
        PoolPilotProof.FillHand(p);
        Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.BoardZone);
        for (int i = 1; i < 7; i++) Summon(p, "CS2_231");
        Give(g, Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.SetasideZone), "EDR_100t");
        Core2026Proof.Check(p.HandZone.Count == 10 && p.BoardZone.SlotCount == 7 &&
            DarkGifts.GiftsOf(first).Count == 4 && DarkGifts.GiftsOf(second).Count == 3,
            "full hand and mixed full board do not block mirrors or create extra minions");
    }

    private static void SweetDreams(Game original, string id)
    {
        foreach (var zone in new[] { Zone.HAND, Zone.DECK }) {
            var g = original.CloneExact(history: true); var p = g.Player1;
            var wallow = Create(p, id, zone);
            Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
            var recipient = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.SetasideZone);
            Give(g, recipient, "EDR_100t2");
            Core2026Proof.Check(wallow.Cost == 5 && wallow[GameTag.COST] == 5, "Short Claws cost visible before relocation");
            // D6/D8 / dg7-backwards-moves.yaml: Wallow preserves every gift on mirrored Sweet Dreams.
            PoolPilotProof.Resume(g, next => {
                var w = next.IdEntityDic[wallow.Id]; var c = next.Player1;
                Give(next, next.IdEntityDic[recipient.Id], "EDR_100t8");
                Core2026Proof.Check(c.DeckZone.TopCard == w && DarkGifts.GiftsOf(w).Count == 2 &&
                    w.Cost == 5 && ((Minion)w).AttackDamage == 8 && ((Minion)w).Health == 11,
                    "Sweet Dreams mirrors to deck top, retaining cost, stats and all gifts from " + zone);
                Generic.Draw(c, w);
                Core2026Proof.Check(w.Zone == c.HandZone && w.Cost == 5 && DarkGifts.GiftsOf(w).Count == 2,
                    "draw retains gifts and re-arms Wallow");
                Give(next, next.IdEntityDic[recipient.Id], "EDR_100t");
                Core2026Proof.Check(DarkGifts.GiftsOf(w).Count == 3, "redrawn Wallow mirrors once");
            });
        }
        var both = original.CloneExact(history: true); var owner = both.Player1;
        var first = Add(owner, id); var second = Create(owner, id, Zone.DECK);
        var target = Entity.FromCard(owner, Cards.FromId("CS2_182"), zone: owner.SetasideZone);
        Give(both, target, "EDR_100t8");
        Core2026Proof.Check(first.Zone == owner.DeckZone && second.Zone == owner.DeckZone &&
            DarkGifts.GiftsOf(first).Count == 1 && DarkGifts.GiftsOf(second).Count == 1,
            "both Wallows mirror Sweet Dreams once despite subscription movement");
    }

    private static void SweetDreamsLosesOvergrownHorrorDiscount(Game original, string id)
    {
        var g = original.CloneExact(history: true); var p = g.Player1;
        var wallow = Add(p, id);
        var recipient = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.SetasideZone);
        Give(g, recipient, "EDR_100t2");
        Play(g, p, "EDR_654");
        Core2026Proof.Check(wallow.Cost == 3 && wallow[GameTag.COST] == 3,
            "Short Claws and Overgrown Horror each discount Wallow by two");
        // Pinned 32.2 / data/rules-evidence/dark-gift/dg7-backwards-moves.yaml: only Dark Gifts survive Wallow's move.
        PoolPilotProof.Resume(g, next => {
            var w = next.IdEntityDic[wallow.Id]; var c = next.Player1;
            Give(next, next.IdEntityDic[recipient.Id], "EDR_100t8");
            Core2026Proof.Check(c.DeckZone.TopCard == w && w.Cost == 5 && w[GameTag.COST] == 5 &&
                DarkGifts.GiftsOf(w).Select(e => e.Card.Id).SequenceEqual(new[] { "EDR_100t2e", "EDR_100t8e1" }),
                "Sweet Dreams preserves Short Claws gift discount and loses ordinary Overgrown Horror discount");
            Generic.Draw(c, w);
            Core2026Proof.Check(w.Zone == c.HandZone && w.Cost == 5 && w[GameTag.COST] == 5 && DarkGifts.GiftsOf(w).Count == 2,
                "redraw retains gift discount without restoring Overgrown Horror discount");
        });
    }

    internal static void Run() => StandardSetsProof.Case("EDR_487", (g, p, id) => {
        Scope(g, id); SelfAndMultiple(g, id); SweetDreams(g, id); SweetDreamsLosesOvergrownHorrorDiscount(g, id);
        var wallow = Create(p, id, Zone.DECK);
        var offered = Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
        var other = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        PoolPilotProof.Resume(g, next => {
            var c = next.Player1;
            new EffectContext(next, c.Id, c.Hero.Id).DiscoverEntitiesWithDarkGift(
                new[] { next.IdEntityDic[offered.Id], next.IdEntityDic[other.Id] });
            Core2026Proof.Check(!DarkGifts.Has(next.IdEntityDic[wallow.Id]) &&
                !DarkGifts.Has(next.IdEntityDic[offered.Id]) && !DarkGifts.Has(next.IdEntityDic[other.Id]), "unchosen offers give no gifts");
            var decision = next.GetPendingDecisions().Single();
            Core2026Proof.Check(next.TryAnswer(new DecisionAnswer(decision.Id, c.Id, new[] { offered.Id }), out var error), error);
            Core2026Proof.Check(DarkGifts.GiftsOf(next.IdEntityDic[wallow.Id]).Count == 1 &&
                !DarkGifts.Has(next.IdEntityDic[other.Id]), "armed deck Wallow clone/snapshot mirrors only chosen offer");
        });
    });
}
