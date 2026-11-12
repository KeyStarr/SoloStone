using System.Xml.Linq;
using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static B14K3BEProof;

internal static class B14K3CATACLYSMProof
{
    internal static void CardsProof() => StandardSetsProof.Case("CATA_979", Unpinned_ConjurationSpecialistSplitCost);
    // cost-sampling/mage-specialist-split-cost.yaml (A wording, C/E inference; owner-approved working assumption).
    private static void Unpinned_ConjurationSpecialistSplitCost(Game g, Controller p, string id)
    {
        CheckPoolBuckets(g, id);
        KnownMetaProofFixture.UseBlackEmpire(g);
        Play(g, p, id); Core2026Proof.Check(p.Choice == null, "no spell means no selection"); ParkBoard(p);
        Add(p, "CS2_182"); Play(g, p, id); Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 1, "minions are not hand spell candidates");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        foreach (int seed in new[] { 1, 2, 3 })
        foreach (int cost in new[] { 0, 3, 8 })
        {
            var branch = g.CloneExact(history: true); var owner = branch.Player1;
            var spell = Add(owner, "CS2_029"); var keep = Add(owner, "CS2_182"); var enemy = Add(owner.Opponent, "CS2_029");
            CostRules.AddModifier((Playable)spell, EffectOperator.SET, cost);
            Generic.AddEnchantmentBlock(branch, Cards.FromId("EDR_874e"), owner.Hero, spell);
            if (seed % 2 == 0) { owner.HandZone.Remove(keep); owner.HandZone.Add(keep); }
            Play(branch, owner, id);
            Core2026Proof.Check(owner.Choice.Choices.SequenceEqual(new[] { spell.Id }), "only extant own hand spells selectable");
            branch.SetRandomSeed(seed); var expectedGame = branch.CloneExact(history: true);
            var expected = Enumerable.Range(0, 2).Select(_ => OfflineCardPools.Pick(expectedGame, Filter(id).WithCost(cost), owner.HeroClass, Cards.FromId(id).AssetId)?.Id).Where(x => x != null).ToArray();
            Future(branch, next => {
                PlayerDecisionProof.Answer(next, spell.Id);
                var player = next.Player1;
                var generated = player.HandZone.Where(c => c.Id != keep.Id).ToArray();
                Core2026Proof.Check(generated.Select(c => c.Card.Id).SequenceEqual(expected) && generated.All(c => c.Card.Type == CardType.SPELL
                    && c.Card.Cost == cost && c[GameTag.SPELLPOWER] == 0 && (c.AppliedEnchantments == null || c.AppliedEnchantments.Count == 0)), "two seeded independent fresh printed-cost spells; no inherited payload");
                Core2026Proof.Check(next.IdEntityDic[spell.Id].Zone == player.SetasideZone && next.IdEntityDic[spell.Id][GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME && player.HandZone.Contains(next.IdEntityDic[keep.Id])
                    && next.IdEntityDic[enemy.Id].Zone == player.Opponent.HandZone && player.NumDiscardedThisGame == 0, "retirement preserves others without discard");
            });
        }
        Pool(g, Array.Empty<string>(), () => {
            var chosen = Add(p, "CS2_029"); Play(g, p, id); PlayerDecisionProof.Answer(g, chosen.Id);
            Core2026Proof.Check(p.HandZone.Count == 0, "empty exact bucket retires original and generates nothing");
        });
        ParkBoard(p); Pool(g, new[] { "CORE_CS1_112" }, () => {
            var chosen = Add(p, "CS2_029"); CostRules.AddModifier((Playable)chosen, EffectOperator.SET, 3);
            FillHand(p, 9); Play(g, p, id); FillHand(p, 10); PlayerDecisionProof.Answer(g, chosen.Id);
            Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "CORE_CS1_112") == 1
                && p.GraveyardZone.Count(c => c.Card.Id == "CORE_CS1_112") == 1, "with replacement singleton; one replacement burns at capacity");
        });
    }

}
