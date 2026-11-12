using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PaladinAurasProof;

internal static class B14F2VioletAuraProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_327", Unpinned_ReinforcementDeckEntity);
    private static void Unpinned_ReinforcementDeckEntity(Game g, Controller p, string id)
    {
        foreach (bool reverse in new[] { false, true })
        {
            var randomGame = HeroDivineShieldProof.NewGame(true); var owner = randomGame.Player1;
            foreach (var card in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(card));
            var names = new[] { "CS2_231", "CS2_171", "CS2_168" };
            foreach (string name in reverse ? names.Reverse() : names)
                Entity.FromCard(owner, Cards.FromId(name), zone: owner.DeckZone);
            Install(owner, id);
            NonFirstPick(randomGame, 3);
            var candidates = owner.DeckZone.ToArray(); var rng = randomGame.Random.Clone();
            int expected = candidates[rng.Next(3)].Id;
            End(randomGame);
            Core2026Proof.Check(owner.BoardZone.Single().Id == expected && randomGame.Random.CaptureState().Equals(rng.CaptureState()),
                "seeded reordered-deck pick uses one bounded draw, not first minion");
        }
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        var low = Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
        var high = Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id); End(g);
        Core2026Proof.Check(low.Zone == p.BoardZone && high.Zone == p.DeckZone && low[GameTag.ENTITY_ID] == low.Id,
            "eligible original minion moves from deck without Battlecry or copy");
        End(g);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
        var blocked = Entity.FromCard(p, Cards.FromId("CS2_231"), zone: p.DeckZone);
        End(g);
        Core2026Proof.Check(blocked.Zone == p.DeckZone, "full board does not consume deck minion");
    }
}
