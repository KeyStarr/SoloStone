using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class RunedOrbProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BAR_541", (g, p, id) => {
        KnownMetaProofFixture.UseBlackEmpire(g);
        p.AddHeroAndPower(Cards.FromId("HERO_08"));
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.SPELL && c.Id != id), "admitted filter and source exclusion");
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var location = LocationProofs.Location(p); var spell = Add(p, id);
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id).Select(t => t.Target?.Id).ToArray();
        Core2026Proof.Check(targets.Contains(p.Hero.Id) && targets.Contains(p.Opponent.Hero.Id)
            && targets.Contains(friendly.Id) && targets.Contains(enemy.Id) && !targets.Contains(location.Id), "any character but never location");
        EmptyHand(p);
        var pyro = Summon(p, "CORE_NEW1_020");
        int spells = p.NumSpellsPlayedThisGame;
        var clone = g.CloneExact(history: true);
        var source = Play(g, p, id, p.Opponent.Hero);
        Play(clone, clone.Player1, id, clone.Player2.Hero);
        var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Length == 3 && offers.Select(x => g.IdEntityDic[x].Card.Id).Distinct().Count() == 3
            && offers.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id))
            && offers.Select(x => g.IdEntityDic[x].Card.Id).SequenceEqual(clone.Player1.Choice.Choices.Select(x => clone.IdEntityDic[x].Card.Id)), "three distinct eligible offers; deterministic seed");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2, "two damage before Discover");
        // Selector integration contract (2026-10-20-selectors.md): Discover resolves before after-cast triggers.
        Core2026Proof.Check(pyro.Damage == 0 && g.SpellCompletion?.SourceId == source.Id
            && source.Zone != p.GraveyardZone && p.NumSpellsPlayedThisGame == spells, "Orb waits for selection before after-cast");
        int chosen = offers[1];
        Resume(g, next => {
            CoreCostProof.Answer(next, chosen);
            Core2026Proof.Check(((Minion)next.IdEntityDic[pyro.Id]).Damage == 1 && next.SpellCompletion == null
                && next.IdEntityDic[source.Id].Zone == next.Player1.GraveyardZone
                && next.Player1.NumSpellsPlayedThisGame == spells + 1 && next.IdEntityDic[chosen].Zone == next.Player1.HandZone,
                "Orb delivers selected spell and completes once before Pyromancer");
        });
        Generated(g, source, p.HandZone, 1);
        Core2026Proof.Check(p.HandZone.Single().Id == chosen, "exact selected entity delivered");
        EmptyHand(p); StandardSetsProof.ClearBoard(g, p); FillHand(p, 9);
        Play(g, p, id, p.Opponent.Hero); chosen = p.Choice.Choices[0]; FillHand(p);
        Resume(g, next => CoreCostProof.Answer(next, chosen));
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Choice == null && g.IdEntityDic[chosen].Zone != p.HandZone, "full hand never exceeds capacity");
        EmptyHand(p); StandardSetsProof.ClearBoard(g, p);
        Pool(g, id, 0, () => {
            long draws = g.Random.DrawCount; Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && g.Random.DrawCount == draws, "empty pool no choice or RNG");
        });
        StandardSetsProof.ParkBoard(p); Summon(p, "CS2_052");
        var target = Summon(p.Opponent, "CS2_182");
        Play(g, p, id, target); int offer = p.Choice.Choices[0];
        Core2026Proof.Check(target.Damage == 3, "Runed Orb includes friendly Spell Damage");
        CoreCostProof.Answer(g, offer);
        EmptyHand(p); StandardSetsProof.ParkBoard(p);
        var indirectPyro = Summon(p, "CORE_NEW1_020");
        var indirect = (Spell)Entity.FromCard(p, Cards.FromId(id));
        int damage = p.Opponent.Hero.Damage;
        Generic.CastSpell(p, g, indirect, p.Opponent.Hero, 0);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null
            && indirect.Zone == p.GraveyardZone && p.Opponent.Hero.Damage == damage + 2,
            "indirect Orb auto-picks and completes synchronously");
        Core2026Proof.Check(indirectPyro.Damage == 0, "indirect Orb is not a hand play and does not trigger Pyromancer");
        Generated(g, indirect, p.HandZone, 1);
    });
}
