using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class PortalVanguardProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_003", (g, p, id) => {
        ProjectedMixedDeck(g.CloneExact(), id);
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        var spell = Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId("CS2_029"), zone: p.DeckZone);
        var first = Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId("CS2_171"), zone: p.DeckZone);
        var second = Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId("CS2_171"), zone: p.DeckZone);
        var source = (Minion)Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(source.AttackDamage == 2 && source.BaseHealth == 2 && source.IsRace(Race.DRAGON), "pinned 2/2 Dragon");
        var drawn = (Minion)p.HandZone.Single();
        Core2026Proof.Check((drawn.Id == first.Id || drawn.Id == second.Id) && drawn.AttackDamage == 3 && drawn.BaseHealth == 3 &&
            drawn.AppliedEnchantments.Single().Card.Id == "TIME_003e" && p.DeckZone.Contains(spell), "random physical minion drawn and buffed, spell untouched");
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.DeckZone.Count == 1 && p.HandZone.Count == 2, "remaining minion drawn");
        var rng = g.Random.CaptureState(); int fatigue = p.Hero.Damage;
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.Hero.Damage == fatigue && rng.Equals(g.Random.CaptureState()), "no minion: no draw/fatigue/RNG");
        p.SetasideZone.Add(p.DeckZone.Remove(spell)); Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.Hero.Damage == fatigue, "empty deck does not fatigue");
        var last = Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId("CS2_171"), zone: p.DeckZone);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.BoardZone.IsFull && last.Zone.Type == Zone.HAND && ((Minion)last).AttackDamage == 3,
            "last board slot still draws and buffs");
        var burned = Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId("CS2_171"), zone: p.DeckZone);
        PoolPilotProof.FillHand(p);
        // Full-hand Battlecry repetition: the matching minion burns and receives no buff.
        var ctx = new SabberStoneCore.EffectSystem.EffectContext(g, p.Id, source.Id);
        Core2026Proof.Check(ctx.RunLegacy(source.Card.Power.PowerTask) == SabberStoneCore.Tasks.TaskState.COMPLETE &&
            burned.Zone.Type == Zone.GRAVEYARD && (burned.AppliedEnchantments?.Count ?? 0) == 0, "overdraw burns without buff");
    });

    // Regression: search crash "Cannot apply ADD Health 2 to ... Spell" (Egg Paladin vs Herald DH, live-t5).
    // A fair-view deck sampled only in part holds Unknown slots beside known cards; the projected draw
    // took the top card regardless of type, so a known spell got the +2/+2 enchantment.
    private static void ProjectedMixedDeck(SabberStoneCore.Model.Game g, string id)
    {
        var p = g.Player1;
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        g.ProjectHistoryForFairView(p.PlayerId);
        var hidden = new[] { Entity.FromCard(p, Unknown.UnknownCard, zone: p.DeckZone),
            Entity.FromCard(p, Unknown.UnknownCard, zone: p.DeckZone) };
        var spell = Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId("CS2_029"), zone: p.DeckZone);
        Core2026Proof.Check(hidden.All(h => h is Unknown) && p.DeckZone.TopCard == spell, "projected deck: Unknown slots under a known top spell");
        // Search applies the Battlecry body; a hand-made Unknown has no snapshot identity for a Rewind baseline.
        var source = Entity.FromCard(p, SabberStoneCore.Model.Cards.FromId(id), zone: p.SetasideZone);
        SabberStoneCore.Tasks.TaskState Battlecry() =>
            new SabberStoneCore.EffectSystem.EffectContext(g, p.Id, source.Id).RunLegacy(source.Card.Power.PowerTask);
        for (int i = 1; i <= 2; i++)
            Core2026Proof.Check(Battlecry() == SabberStoneCore.Tasks.TaskState.COMPLETE && p.HandZone.Count == i
                && p.HandZone.All(c => c is Unknown) && hidden.Count(h => h.Zone.Type == Zone.HAND) == i
                && spell.Zone.Type == Zone.DECK && (spell.AppliedEnchantments?.Count ?? 0) == 0,
                "projected mixed deck: draw takes an Unknown slot, known spell neither drawn nor buffed");
        var rng = g.Random.CaptureState();
        Core2026Proof.Check(Battlecry() == SabberStoneCore.Tasks.TaskState.COMPLETE && spell.Zone.Type == Zone.DECK
            && p.HandZone.Count == 2 && rng.Equals(g.Random.CaptureState()) && (spell.AppliedEnchantments?.Count ?? 0) == 0,
            "projected known-only deck: no minion, no draw/RNG");
    }
}
