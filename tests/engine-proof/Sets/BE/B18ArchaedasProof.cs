using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B18ArchaedasProof
{
    [GroupProof("b18-t-archaedas-cards", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("BE_055", Unpinned_Archaedas_DiscoverWeapon);

    internal static void Answer(Game game, int selected)
    {
        var d = game.GetPendingDecisions().Single();
        Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(d.Id, d.ControllerId, new[] { selected }), out var error), "Archaedas answer: " + error);
    }

    private static void Unpinned_Archaedas_DiscoverWeapon(Game g, Controller p, string id)
    {
        var filter = OfflinePoolFilter.Discover(type: CardType.SPELL);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, filter, 129245).ToList();
        var random = g.CloneExact().Random;
        var expected = new string[3];
        for (int i = 0; i < 3; i++)
        {
            int index = i + random.Next(candidates.Count - i);
            expected[i] = candidates[index].Id;
            (candidates[i], candidates[index]) = (candidates[index], candidates[i]);
        }
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3, "three distinct spell offers");
        Core2026Proof.Check(p.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id).SequenceEqual(expected),
            "Discover preserves exact seeded offer order");
        Core2026Proof.Check(g.Random.CaptureState().Equals(random.CaptureState()), "Discover consumes exactly three RNG draws");
        Kill(g, body); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var snapshot = g.SaveSnapshot();
        foreach (bool full in new[] { false, true })
        foreach (bool discounted in new[] { false, true })
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(snapshot) })
        {
            var owner = next.Player1; int selected = owner.Choice.Choices[1];
            var offered = next.IdEntityDic[selected]; var spell = offered.Card;
            if (discounted) CostRules.AddModifier((Playable)offered, EffectOperator.SET, 0);
            int cost = offered.Cost;
            if (full) while (!owner.HandZone.IsFull) Add(owner, "CS2_182");
            Answer(next, selected);
            var weapon = (Weapon)next.IdEntityDic[selected];
            Core2026Proof.Check(weapon.Card.Id == "BE_055t" && weapon.Cost == cost && weapon.AttackDamage == 3 && weapon.Durability == 3,
                "same selected actor becomes exported 3/3 weapon at CURRENT zero-floored Cost");
            Core2026Proof.Check(weapon.Zone.Type == (full ? Zone.GRAVEYARD : Zone.HAND),
                "source death/full board do not block transformation; full hand burns weapon");
            Core2026Proof.Check(full ? PayloadRules.Entries(weapon).Length == 0
                : PayloadRules.Entries(weapon, ArchaedasPayloadProof.Key).Single().Card.Id == spell.Id,
                "held weapon retains selected spell; burned weapon retires removable payload");
            Core2026Proof.Check(owner.Choice == null && !next.Resolution.HasSuspendedEffect, "Battlecry finishes once with no pending choice");
        }
        Answer(g, p.Choice.Choices[1]);
        foreach (CardClass cls in new[] { CardClass.WARRIOR, CardClass.ROGUE, CardClass.MAGE })
            Console.WriteLine("Archaedas pool " + cls + "=" + OfflineCardPools.Candidates(g, cls, filter, 129245).Count);

    }
}
