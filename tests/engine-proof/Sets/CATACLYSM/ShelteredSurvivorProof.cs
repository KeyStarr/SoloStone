using SabberStoneCore.Model.Zones;
using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShelteredSurvivorProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_721", (g, p, id) => {
        int deck = p.DeckZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 1 && p.DeckZone.Count == deck - 1, "empty hand still draws once");
        ClearBoard(g, p);
        var selected = p.HandZone.Single(); selected.Cost = 1;
        new AddEnchantmentTask("MEND_305e", EntityType.TARGET).Process(g, p, selected, selected);
        var unselected = Add(p, "CS2_029"); var opponent = Add(p.Opponent, "CS2_171");
        deck = p.DeckZone.Count; int hand = p.HandZone.Count;
        var oldDeck = p.DeckZone.Select(c => c.Id).ToArray();
        Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { selected.Id, unselected.Id }), "choose existing friendly hand only");
        var clone = g.Clone(); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        var before = g.SaveSnapshot();
        PlayerDecisionProof.Answer(clone, selected.Id);
        Core2026Proof.Check(before.SequenceEqual(g.SaveSnapshot()), "clone shuffle and RNG do not alter original");
        foreach (var candidate in new[] { g, restored }) PlayerDecisionProof.Answer(candidate, selected.Id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == hand && p.DeckZone.Count == deck && selected.Cost == 4,
            "shuffle resets hand discount, then exactly one draw");
        Core2026Proof.Check(selected.AppliedEnchantments.Count == 0 && ((Minion)selected).AttackDamage == 4 && ((Minion)selected).Health == 5,
            "shuffle removes hand enchantments as well as modified stats");
        Core2026Proof.Check(unselected.Zone == p.HandZone && opponent.Zone == p.Opponent.HandZone &&
            oldDeck.Append(selected.Id).Order().SequenceEqual(p.DeckZone.Concat(p.HandZone.Where(c => c.Id != unselected.Id)).Select(c => c.Id).Order()),
            "chosen entity shuffled without copy/loss; unselected and opponent hand preserved");
        Core2026Proof.Check(!oldDeck.Where(entityId => p.DeckZone.Any(c => c.Id == entityId)).SequenceEqual(p.DeckZone.Where(c => c.Id != selected.Id).Select(c => c.Id)),
            "whole old deck shuffled, not merely random insertion");
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(restored) && g.Random.CaptureState().Equals(restored.Random.CaptureState()), "snapshot preserves shuffle/draw future and RNG");
        ClearBoard(g, p);
        foreach (var held in p.HandZone.ToArray()) { p.HandZone.Remove(held); p.SetasideZone.Add(held); }
        foreach (var held in p.DeckZone.ToArray()) { p.DeckZone.Remove(held); p.SetasideZone.Add(held); }
        var only = Add(p, "CS2_171"); int damage = p.Hero.Damage;
        Play(g, p, id); PlayerDecisionProof.Answer(g, only.Id);
        Core2026Proof.Check(p.HandZone.Single().Id == only.Id && p.DeckZone.Count == 0 && p.Hero.Damage == damage,
            "sole selected card enters empty deck before draw; no fatigue");
        p.HandZone.Remove(only); p.SetasideZone.Add(only); ClearBoard(g, p);
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && p.Hero.Damage == damage + 1, "empty hand and deck cause one fatigue draw");
        ClearBoard(g, p); Summon(p, "LOE_077");
        only = Add(p, "CS2_171"); Play(g, p, id);
        PlayerDecisionProof.Answer(g, only.Id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { only.Id }), "Brann second choice sees card drawn by first activation");
        PlayerDecisionProof.Answer(g, only.Id);
        Core2026Proof.Check(p.Choice == null && p.HandZone.Single().Id == only.Id && p.Hero.Damage == damage + 1, "Brann independently shuffles and draws twice");
        ClearBoard(g, p);
        p.HandZone.Remove(only); p.SetasideZone.Add(only);
        while (p.DeckZone.Count < DeckZone.DeckMaximumCapcity) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        only = Add(p, "CS2_171"); Play(g, p, id); PlayerDecisionProof.Answer(g, only.Id);
        Core2026Proof.Check(only.Zone == p.GraveyardZone && p.DeckZone.Count == (DeckZone.DeckMaximumCapcity - 1) && p.HandZone.Count == 1 && p.DiscardedEntities.Count == 0,
            "full deck burns chosen card without discard, then still draws");
    });
}
