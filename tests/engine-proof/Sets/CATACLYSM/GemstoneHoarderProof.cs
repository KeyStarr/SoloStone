using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GemstoneHoarderProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_897", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasBattleCry && source.HasDeathrattle && source.AttackDamage == 3 && source.Health == 4
            && p.Choice == null, "printed Battlecry/Deathrattle; empty hand has no choice");
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.IsEmpty, "no captured card means no return");

        var first = Add(p, "CS2_182"); var selected = (Minion)Add(p, "CS2_182"); var last = Add(p, "CS2_029");
        selected.Cost = 1; selected.AttackDamage = 9;
        var opponent = Add(p.Opponent, "CS2_029");
        source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { first.Id, selected.Id, last.Id })
            && p.Options().Count == 3 && p.Options().All(t => t is ChooseTask), "bot offers exact friendly hand entities, duplicates distinct");
        var rng = g.Random.CaptureState();
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        PlayerDecisionProof.Answer(clone, selected.Id);
        Core2026Proof.Check(p.Choice != null && selected.Zone == p.HandZone, "clone answer leaves original selection pending");
        foreach (var game in new[] { g, loaded }) PlayerDecisionProof.Answer(game, selected.Id);
        Core2026Proof.Check(selected.Zone == p.GraveyardZone && p.LastCardDiscarded == selected.Id
            && p.DiscardedEntities.SequenceEqual(new[] { selected.Id }), "selected identity uses ordinary discard bookkeeping once");
        Core2026Proof.Check(p.HandZone.Select(c => c.Id).SequenceEqual(new[] { first.Id, last.Id })
            && opponent.Zone == p.Opponent.HandZone && g.Random.CaptureState().Equals(rng), "other cards and RNG unchanged");
        var capture = source.AppliedEnchantments.Single(e => e.Card.Id == "CATA_897e3");
        Core2026Proof.Check(capture.Card.AssetId == 130826 && capture.CapturedCard.Id == "CS2_182", "exact export capture enchantment remembers identity");
        foreach (var game in new[] { g, clone, loaded }) Kill(game, (Minion)game.IdEntityDic[source.Id]);
        var returned = (Minion)p.HandZone.Last();
        Core2026Proof.Check(returned.Id != selected.Id && returned.Card.Id == "CS2_182" && returned.Card.AssetId == 90
            && returned.AttackDamage == 4 && returned.Health == 5 && returned.Cost == 3 && returned[GameTag.COST] == 3
            && returned.Controller == p && returned.Zone == p.HandZone && returned.ZonePosition == 2
            && returned[GameTag.DISPLAYED_CREATOR] == source.Id,
            $"return creates base card with visible permanent minus-one cost at hand end: {returned.Card.Id}/{returned.Card.AssetId} {returned.AttackDamage}/{returned.Health} cost={returned.Cost}/{returned[GameTag.COST]} position={returned.ZonePosition} creator={returned[GameTag.DISPLAYED_CREATOR]}/{source.Id}");
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
            "captured identity and return survive clone/snapshot");
        End(g); End(g);
        Core2026Proof.Check(returned.Cost == 3, "discount persists across turns");
        PlayEntity(g, p, returned); returned.Silence();
        Core2026Proof.Check(returned.AttackDamage == 4 && returned.Health == 5, "returned base minion can be played and silenced");
        ClearHand(p); ClearBoard(g, p);

        var coin = Add(p, "GAME_005"); source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, coin.Id);
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "GAME_005" && p.HandZone.Single().Cost == 0, "zero-cost return floors at zero");
        ClearHand(p);
        var weapon = Add(p, "CS2_106"); source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, weapon.Id);
        source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.IsEmpty, "Silence removes captured return");

        var spell = Add(p, "CS2_029"); source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, spell.Id);
        for (int n = 0; n < 10; n++) Add(p, "CS2_171");
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.Id == "CS2_171")
            && p.GraveyardZone.Count(c => c.Card.Id == "CS2_029") == 2, "full hand burns returned spell without replacing held cards");
        ClearHand(p);

        Summon(p, "LOE_077");
        var left = Add(p, "CS2_029"); var right = Add(p, "CS2_106");
        source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, left.Id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { right.Id }), "repeated Battlecry rebuilds hand after first discard");
        PlayerDecisionProof.Answer(g, right.Id); Kill(g, source);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 2
            && p.HandZone.Any(c => c.Card.Id == "CS2_029" && c.Cost == 3)
            && p.HandZone.Any(c => c.Card.Id == "CS2_106" && c.Cost == Cards.FromId("CS2_106").Cost - 1),
            "separate captures return both chosen cards at engine base cost minus one: "
                + string.Join(",", p.HandZone.Select(c => c.Card.Id + ":" + c.Cost)));
        ClearHand(p); ClearBoard(g, p);

        Summon(p, "KAR_089"); var victim = Add(p, "CS2_029"); int deck = p.DeckZone.Count;
        source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, victim.Id);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1, "normal discard reactions draw before Battlecry completes");
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Last().Card.Id == "CS2_029" && p.HandZone.Last().Cost == 3, "reaction draw is not the captured card");
    });

    private static void ClearHand(Controller player)
    {
        foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
    }
}
