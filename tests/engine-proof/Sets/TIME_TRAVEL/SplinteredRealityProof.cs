using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SplinteredRealityProof
{
    internal static void Run() => StandardSetsProof.Case("END_009", (g, p, id) => {
        HistoryCardProofChecks.HiddenDisplay(g, id);
        var held = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "Reality initially displays zero Treant deaths");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "END_009t" && m.AttackDamage == 2 && m.Health == 2
            && m.Card[(GameTag)2831] == 1 && m.Card.Cost == 1), "zero-history exact 2/2 Treants, tag, cost");
        ClearBoard(g, p); Kill(g, Summon(p.Opponent, "END_009t")); Kill(g, Summon(p, "CS2_182"));
        var impostorDefinition = Cards.FromId("END_009t").Clone(); impostorDefinition.Tags.Remove((GameTag)2831);
        var impostor = (Minion)Entity.FromCard(p, impostorDefinition);
        Generic.SummonBlock(g, impostor, -1, null); Kill(g, impostor);
        // Frozen death-form tag survives later in-place graveyard transformation.
        var dead = p.GraveyardZone.OfType<Minion>().First(m => m.Card.Id == "END_009t");
        Generic.ChangeEntityBlock(p, dead, Cards.FromId("CS2_182"), false);
        g.AuraUpdate();
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "live bonus counts frozen friendly Treant deaths only");
        End(g); End(g);
        var clone = g.Clone(); Kill(clone, Summon(clone.Player1, "END_009t")); Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.Player1.BoardZone.All(m => m.AttackDamage == 5 && m.Health == 5) && p.BoardZone.IsEmpty, "clone-only death isolated");
        var left = Summon(p, "CS2_182");
        held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 99;
        PlayEntity(g, p, held);
        var tokens = p.BoardZone.Where(m => m.Card.Id == "END_009t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.AttackDamage == 4 && m.Health == 4
            && m.AppliedEnchantments.Single().Card.Id == "END_009e"), "previous-turn deaths before creation count; opponent/non-Treant deaths excluded");
        Core2026Proof.Check(tokens.Max(m => m.Id) < tokens.Min(m => m.AppliedEnchantments.Single().Id), "both Treants created before either receives its buff");
        Core2026Proof.Check(p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { left.Id }.Concat(tokens.Select(m => m.Id))), "spell appends in creation order");
        ClearBoard(g, p);
        var transformed = Summon(p, "END_009t"); Generic.ChangeEntityBlock(p, transformed, Cards.FromId("CS2_182"), false); Kill(g, transformed);
        var becomingTreant = Summon(p, "CS2_182"); Generic.ChangeEntityBlock(p, becomingTreant, Cards.FromId("END_009t"), false); Kill(g, becomingTreant);
        int count = 5; // Two initial + two buffed Treants + one transformed into a Treant.
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.All(m => m.AttackDamage == 2 + count), "death-form Treant tag counts; Treant name without tag does not");
        ClearBoard(g, p);
        var location = Entity.FromCard(p, Cards.FromId("TIME_044")); p.BoardZone.Add((BoardObject)location);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "END_009t") == 1
            && p.BoardZone.GetSlot(0) == location && p.BoardZone.GetSlot(6).Card.Id == "END_009t", "mixed board only admits one token in last physical slot");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board admits none");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "declared history consumer");
    });
}
