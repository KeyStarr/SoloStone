using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B15PoolFiltersTHELOSTCITYProof
{
    [GroupProof("b15-pool-filters-the-lost-city-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("DINO_427", Unpinned_MerchantFiveMasksAndBrann);
        StandardSetsProof.Case("TLC_824", Unpinned_OddMapDormantConsumesFollowup);
    }
    private static void Unpinned_MerchantFiveMasksAndBrann(Game g, Controller p, string id)
    {
        var filter = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single().Filter;
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count == 5, "exact five named Masks");
        var random = g.Random.Clone(); string expected = candidates[random.Next(candidates.Count)].Id;
        Play(g, p, id); var mask = p.HandZone.Single();
        Core2026Proof.Check(mask.Card.Id == expected && mask.Cost == mask.Card.Cost, "seeded first-play Mask without Combo discount");
        StandardSetsProof.ParkBoard(p); PoolPilotProof.EmptyHand(p);
        Summon(p, "LOE_077"); Play(g, p, "GAME_005");
        B15PoolTest.Resume(g, next => Play(next, next.Player1, id));
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(c => candidates.Any(candidate => candidate.Id == c.Card.Id)
            && c.Cost == c.Card.Cost - 2), "Pinned_BrannRepeatsAcquisitionWithComboDiscount");
        p.Hero[GameTag.CLASS] = (int)CardClass.MAGE;
        Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, filter, Cards.FromId(id).AssetId).Count == 4, "stolen Merchant excludes current hero class Mask");
    }
    private static void Unpinned_OddMapDormantConsumesFollowup(Game g, Controller p, string id)
    {
        p.Hero[GameTag.CLASS] = (int)CardClass.HUNTER;
        var filter = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single().Filter;
        var expected = OfflineCardPools.SampleDistinct(g.CloneExact(history: true), filter, p.HeroClass, Cards.FromId(id).AssetId);
        Play(g, p, id); var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Select(i => g.IdEntityDic[i].Card.Id).SequenceEqual(expected.Select(c => c.Id))
            && offers.All(i => g.IdEntityDic[i].Card.ATK % 2 == 1 && g.IdEntityDic[i].Card.IsRace(Race.BEAST)), "seeded odd printed-Attack Beast offers");
        PlayerDecisionProof.Answer(g, offers[0]);
        var marker = p.AppliedEnchantments.Single(e => e.Card.Id == "TLC_824e");
        var rejected = offers.Skip(1).Select(i => g.IdEntityDic[i].Card.Id).ToArray();
        var duplicate = Add(p, g.IdEntityDic[offers[0]].Card.Id);
        PlayEntity(g, p, duplicate); B15PoolTest.AnswerAll(g);
        Core2026Proof.Check(p.AppliedEnchantments.Contains(marker), "different hand copy cannot consume marker");
        StandardSetsProof.ParkBoard(p);
        B15PoolTest.Resume(g, next => {
            PlayEntity(next, next.Player1, next.IdEntityDic[offers[0]]);
            while (next.Player1.Choice != null && next.Player1.AppliedEnchantments.Any(e => e.Id == marker.Id))
                PlayerDecisionProof.Answer(next, next.Player1.Choice.Choices[0]);
        });
        Core2026Proof.Check(!p.AppliedEnchantments.Contains(marker), "exact play consumes marker once after own Battlecry");
        if (g.IdEntityDic[offers[0]] is Minion played && played.IsDormant)
            Core2026Proof.Check(p.Choice == null, "Dormant consumes marker without bonus choice");
        else
        {
            Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id).SequenceEqual(rejected), "bonus contains original rejected definitions only");
            int bonus = p.Choice.Choices[0]; PoolPilotProof.FillHand(p);
            B15PoolTest.Resume(g, next => PlayerDecisionProof.Answer(next, bonus));
            Core2026Proof.Check(g.IdEntityDic[bonus].Zone == p.GraveyardZone && p.HandZone.Count == 10, "full-hand bonus burns once");
        }
        var dormant = GiftedDiscoverProof.New(g.Seed, CardClass.HUNTER);
        B15PoolTest.WithMembers(dormant, new[] { "TIME_046", "CORE_EX1_028" }, () => {
            Play(dormant, dormant.Player1, id);
            int choice = dormant.Player1.Choice.Choices.Single(i => dormant.IdEntityDic[i].Card.Id == "TIME_046");
            PlayerDecisionProof.Answer(dormant, choice);
            B15PoolTest.Resume(dormant, next => PlayEntity(next, next.Player1, next.IdEntityDic[choice]));
            Core2026Proof.Check(((Minion)dormant.IdEntityDic[choice]).IsDormant && dormant.Player1.Choice == null
                && !dormant.Player1.AppliedEnchantments.Any(e => e.Card.Id == "TLC_824e"), "Unpinned_OddMap_DormantConsumesFollowup");
        });
        StandardSetsProof.ParkBoard(p); PoolPilotProof.EmptyHand(p);
        Play(g, p, id); offers = p.Choice.Choices.ToArray(); PlayerDecisionProof.Answer(g, offers[0]);
        End(g);
        Core2026Proof.Check(!p.AppliedEnchantments.Any(e => e.Card.Id == "TLC_824e"), "unused marker expires at turn end");
    }
}
