using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B15HeldFormCardsProof
{
    [GroupProof("b15-held-form-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CORE_RLK_567", ShadowOfDemise);
        StandardSetsProof.Case("DINO_407", MirrexHeldFormProof.Unpinned_Mirrex);
        StandardSetsProof.Case("TIME_876", ShapeshifterHeldFormProof.Unpinned_Shapeshifter);
        StandardSetsProof.Case("CATA_614", InformantHeldFormProof.Unpinned_Informant);
        StandardSetsProof.Case("BE_259", IsisetHeldFormProof.Unpinned_Isiset);
    }

    private static void ShadowOfDemise(Game g, Controller p, string id)
    {
        var held = Add(p, id); int entityId = held.Id, slot = held.ZonePosition;
        int actorId = held.AppliedEnchantments.Single(e => e.Card.Id == "RLK_567e2").Id;
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held.Card.Id == "GAME_005" && held.Id == entityId && held.ZonePosition == slot,
            "Demise copies first spell in place");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var discounted = (Playable)Add(next.Player1, "CS2_029");
            SabberStoneCore.Enchants.Effects.ReduceCost(2).ApplyTo(discounted);
            PlayEntity(next, next.Player1, discounted, next.Player2.Hero);
            var copy = next.IdEntityDic[entityId];
            Core2026Proof.Check(copy.Card.Id == "CS2_029" && copy.Cost == 4 &&
                copy.AppliedEnchantments.Single(e => e.Card.Id == "RLK_567e2").Id == actorId,
                "Demise recurs without discounts, retaining the same trigger actor/order");
            PlayEntity(next, next.Player1, copy, next.Player2.Hero);
            Core2026Proof.Check(copy.Zone == next.Player1.GraveyardZone && next.Player2.Hero.Damage == 12,
                "copied spell plays normally; no self transform");
        }
    }
}
