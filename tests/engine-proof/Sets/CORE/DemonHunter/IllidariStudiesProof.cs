using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class IllidariStudiesProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_YOP_001", (g, p, id) => {
        p.AddHeroAndPower(Cards.FromId("HERO_10")); CostSliceProof.Definition("YOP_001e");
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c[GameTag.OUTCAST] > 0 && c.Id != id), "admitted Outcast filter");
        var outcast = Add(p, "CORE_BT_491"); var ordinary = Add(p, "CS2_182");
        var pyro = Summon(p, "CORE_NEW1_020");
        int spells = p.NumSpellsPlayedThisGame;
        var clone = g.CloneExact(history: true); var source = Play(g, p, id); Play(clone, clone.Player1, id);
        // Selector integration contract (2026-10-20-selectors.md): completion and after-cast wait for the answer.
        CostSliceProof.Visible(outcast, 2);
        Core2026Proof.Check(pyro.Damage == 0 && g.SpellCompletion?.SourceId == source.Id
            && source.Zone != p.GraveyardZone && p.NumSpellsPlayedThisGame == spells, "Studies waits before discount and after-cast");
        var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Length == 3 && offers.Select(x => g.IdEntityDic[x].Card.Id).Distinct().Count() == 3
            && offers.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id))
            && offers.Select(x => g.IdEntityDic[x].Card.Id).SequenceEqual(clone.Player1.Choice.Choices.Select(x => clone.IdEntityDic[x].Card.Id)), "distinct deterministic Outcast offers");
        int chosen = offers[0]; Resume(g, next => {
            CoreCostProof.Answer(next, chosen);
            Core2026Proof.Check(((Minion)next.IdEntityDic[pyro.Id]).Damage == 1 && next.SpellCompletion == null
                && next.IdEntityDic[source.Id].Zone == next.Player1.GraveyardZone
                && next.Player1.NumSpellsPlayedThisGame == spells + 1, "Studies selection completes once before Pyromancer");
            CostSliceProof.Visible(next.IdEntityDic[outcast.Id], 1);
        });
        StandardSetsProof.ParkBoard(p);
        CostSliceProof.Visible(outcast, 1); CostSliceProof.Visible(ordinary, 4);
        PlayEntity(g, p, ordinary); CostSliceProof.Visible(outcast, 1);
        End(g); End(g); CostSliceProof.Visible(outcast, 1);
        var later = Add(p, "CORE_BT_491"); g.DeathProcessingAndAuraUpdate(); CostSliceProof.Visible(later, 1);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[outcast.Id]));
        CostSliceProof.Visible(later, 2);
        Core2026Proof.Check(p.AppliedEnchantments == null || p.AppliedEnchantments.All(e => e.Card.Id != "YOP_001e"), "next Outcast consumes controller discount");
        EmptyHand(p); FillHand(p, 9); Play(g, p, id); chosen = p.Choice.Choices[0]; FillHand(p);
        Resume(g, next => CoreCostProof.Answer(next, chosen));
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[chosen].Zone != p.HandZone, "full hand does not overflow");
        EmptyHand(p); var held = Add(p, "CORE_BT_491"); g.DeathProcessingAndAuraUpdate(); CostSliceProof.Visible(held, 1);
        Pool(g, id, 0, () => { long draws = g.Random.DrawCount; Play(g, p, id);
            Core2026Proof.Check(p.Choice == null && g.Random.DrawCount == draws, "empty pool no prompt/RNG"); });
        CostSliceProof.Visible(held, 0);
        var enemy = Add(p.Opponent, "CORE_BT_491"); CostSliceProof.Visible(enemy, 2);
        EmptyHand(p);
        foreach (var enchantment in p.AppliedEnchantments?.Where(e => e.Card.Id == "YOP_001e").ToArray() ?? Array.Empty<Enchantment>()) enchantment.Remove();
        var indirectHeld = Add(p, "CORE_BT_491"); g.DeathProcessingAndAuraUpdate();
        var indirectPyro = Summon(p, "CORE_NEW1_020");
        var indirect = (Spell)Entity.FromCard(p, Cards.FromId(id));
        Generic.CastSpell(p, g, indirect, null, 0);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null
            && indirect.Zone == p.GraveyardZone
            && p.HandZone.Count(c => c[GameTag.DISPLAYED_CREATOR] == indirect.Id && candidates.Any(x => x.Id == c.Card.Id)) == 1,
            "indirect Studies auto-picks an eligible card and completes synchronously");
        Core2026Proof.Check(indirectPyro.Damage == 0, "indirect Studies is not a hand play and does not trigger Pyromancer");
        CostSliceProof.Visible(indirectHeld, 1);
    });
}
