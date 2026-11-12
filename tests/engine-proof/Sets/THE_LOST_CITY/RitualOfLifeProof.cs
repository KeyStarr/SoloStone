using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static LostCityPoolProof;

internal static class RitualOfLifeProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_426", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_09"));
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c.Cost == 3), "declared controller/neutral pool has at least three exact-filter candidates");
        PoolPilotProof.SourceExcluded(g, id); Empty(g, p, id); TinyPools(g, id);
        PoolPilotProof.EmptyHand(p); ParkBoard(p);
        var source = Play(g, p, id); var offers = Offers(g, p, source, c => c.Type == CardType.MINION && c.Cost == 3);
        Core2026Proof.Check(g.SpellCompletion?.SourceId == source.Id && source.Zone != p.GraveyardZone && p.BoardZone.SlotCount == 0 && p.HandZone.Count == 0, "whole spell waits for Discover before summon/cast tail");
        int selected = offers[1]; string chosen = g.IdEntityDic[selected].Card.Id;
        var before = g.SaveSnapshot(); var decision = g.GetPendingDecisions().Single();
        Core2026Proof.Check(!g.TryAnswer(new(decision.Id, decision.ControllerId, new[] { int.MaxValue }), out _) && before.SequenceEqual(g.SaveSnapshot()), "invalid answer preserves pending state/RNG");
        PoolPilotProof.Resume(g, next => {
            Answer(next, selected); var owner = next.Player1;
            var made = owner.BoardZone.Single();
            Core2026Proof.Check(made.Card.Id == chosen && made.AttackDamage == 2 && made.Health == 3 && made[GameTag.DISPLAYED_CREATOR] == source.Id, "selected identity summoned with exact set stats and creator");
            Core2026Proof.Check(owner.HandZone.Count == 1 && owner.HandZone.Single().Card.Id == chosen && next.SpellCompletion == null && next.IdEntityDic[source.Id].Zone == owner.GraveyardZone,
                "hand reward and spell completion happen exactly once on clone/snapshot/original");
        });
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        // Exact export identity: King Mukla is a 3-Cost 5/6 Legendary; its Battlecry must never run on summon.
        WithPool(g, id, new[] { "CORE_EX1_014" }, () => {
            var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
            source = Play(g, p, id); int picked = p.Choice.Choices.Single();
            PoolPilotProof.FillHand(p); Answer(g, picked);
            var made = p.BoardZone.Single(m => m.Card.Id == "CORE_EX1_014");
            Core2026Proof.Check(p.BoardZone.GetSlot(0) == location && p.BoardZone.GetSlot(1) == left && p.BoardZone.GetSlot(2) == made
                && made.AttackDamage == 2 && made.Health == 3 && p.HandZone.Count == 10 && p.Opponent.HandZone.Count == 0,
                "full hand still summons at physical far right; Battlecry not activated");
            Core2026Proof.Check(made.AppliedEnchantments.Any(e => e.Card.Id == "DINO_426e"), "exact pinned stat enchantment attached");
            made.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(made.AttackDamage == 5 && made.Health == 6, "Silence restores printed stats");
            ParkBoard(p); PoolPilotProof.EmptyHand(p); FullBoard(p);
            source = Play(g, p, id); picked = p.Choice.Choices.Single(); Answer(g, picked);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.All(m => m.Card.Id == "CS2_182") && p.HandZone.Count == 1,
                "full mixed board consumes choice and respects hand reward without eighth slot");
            ParkBoard(p); PoolPilotProof.EmptyHand(p); var leader = Summon(p, "CORE_CS2_122");
            Play(g, p, id); Answer(g, p.Choice.Choices.Single());
            made = p.BoardZone.Single(m => m.Card.Id == "CORE_EX1_014");
            Core2026Proof.Check(made.AttackDamage == 3 && made.Health == 3, "set stats retain external attack aura");
            Kill(g, leader); Core2026Proof.Check(made.AttackDamage == 2, "aura removal exposes set attack once");
            ParkBoard(p); PoolPilotProof.EmptyHand(p);
            var automatic = (Spell)Entity.FromCard(p, Cards.FromId(id)); Generic.CastSpell(p, g, automatic, null, 0); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null
                && automatic.Zone == p.GraveyardZone && p.BoardZone.Single().Card.Id == "CORE_EX1_014"
                && p.BoardZone.Single().AttackDamage == 2 && p.BoardZone.Single().Health == 3 && p.HandZone.Count == 1,
                "automatic cast resolves mandatory Discover synchronously and preserves full effect");
        });
    });
}
