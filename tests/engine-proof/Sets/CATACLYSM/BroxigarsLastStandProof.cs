using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BroxigarsLastStandProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_526", (g, p, id) => {
        // Each casualty must attempt its own draw, including every fatigue hit.
        foreach (int remaining in new[] { 0, 1 })
        {
            var exhausted = g.CloneExact(history: true); var owner = exhausted.Player1;
            while (owner.DeckZone.Count > remaining)
            {
                var card = owner.DeckZone[0]; owner.DeckZone.Remove(card); owner.SetasideZone.Add(card);
            }
            Summon(owner, "CS2_171"); Summon(owner.Opponent, "CS2_171");
            if (remaining == 1) Summon(owner.Opponent, "CS2_171");
            Play(exhausted, owner, id);
            Core2026Proof.Check(owner.Hero.Damage == 3 && owner.DeckZone.Count == 0 && owner.HandZone.Count == remaining,
                remaining == 0 ? "two empty-deck draws deal 1+2 fatigue" : "one card then two empty-deck draws deal 1+2 fatigue");
        }
        var friendly = Summon(p, "CS2_171"); var enemy = Summon(p.Opponent, "CS2_171");
        var survivor = Summon(p.Opponent, "CS2_182"); var shield = Summon(p, "EX1_008"); shield.HasDivineShield = true;
        int deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(friendly.Zone.Type == Zone.GRAVEYARD && enemy.Zone.Type == Zone.GRAVEYARD && survivor.Damage == 1 && shield.Damage == 0 && !shield.HasDivineShield, "one damage to both boards; shield prevents death");
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && p.HandZone.Count == 2, "draws once per minion killed by damage");
        deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && shield.Zone.Type == Zone.GRAVEYARD, "later damage draws only for newly killed minion");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); deck = p.DeckZone.Count;
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == deck, "empty board draws nothing");
        var totem = Summon(p, "CS2_052"); var boosted = Summon(p.Opponent, "CS2_120"); boosted.Damage = 1;
        deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(boosted.Zone.Type == Zone.GRAVEYARD && totem.Zone.Type == Zone.GRAVEYARD && p.DeckZone.Count == deck - 2, "spell damage kills two-Health target and own totem, drawing two");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var abomination = Summon(p.Opponent, "EX1_097"); abomination.Damage = 3;
        var secondary = Summon(p.Opponent, "CS2_120");
        deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(abomination.Zone.Type == Zone.GRAVEYARD && secondary.Zone.Type == Zone.GRAVEYARD && p.DeckZone.Count == deck - 1, "Deathrattle resolves before drawing; its secondary casualty adds no draw");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        Summon(p.Opponent, "CS2_171"); Summon(p.Opponent, "CS2_171"); deck = p.DeckZone.Count;
        var fullHandSpell = p.HandZone[0]; p.HandZone.Remove(fullHandSpell); p.SetasideZone.Add(fullHandSpell);
        Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && p.HandZone.Count == 10, "each death draws even when later card burns at full hand");
    });
}
