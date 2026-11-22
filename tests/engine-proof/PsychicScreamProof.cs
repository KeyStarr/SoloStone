using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

// Psychic Scream (LOOT_008): "Shuffle all minions into your opponent's deck." Both players' minions go to the CASTER's
// OPPONENT's deck, and the opponent controls them there. Before the fix they went into the caster's own deck.
internal static class PsychicScreamProof
{
    [GroupProof("psychic-scream")]
    internal static void Run()
    {
        foreach (bool history in new[] { true, false })
        {
            var g = L.Game(history: history); var p1 = g.Player1; var p2 = g.Player2;
            var own = L.Minion(p1, "CS2_182"); var own2 = L.Minion(p1, "CS2_231");
            var enemy = L.Minion(p2, "CS2_182"); own.Damage = 2;
            int deck1 = p1.DeckZone.Count, deck2 = p2.DeckZone.Count;
            var scream = Entity.FromCard(p1, Cards.FromId("LOOT_008"), zone: p1.HandZone);
            L.Check(g.Process(PlayCardTask.Spell(p1, scream)), "play Psychic Scream");
            L.Check(p1.BoardZone.Count == 0 && p2.BoardZone.Count == 0, "boards empty");
            L.Check(p1.DeckZone.Count == deck1, "caster's deck unchanged");
            L.Check(p2.DeckZone.Count == deck2 + 3, "all three minions in the opponent's deck");
            foreach (var m in new[] { own, own2, enemy })
                L.Check(m.Zone == p2.DeckZone && p2.DeckZone.Contains(m) && m.Controller == p2
                    && (m == enemy || m[GameTag.CONTROLLER] == p2.PlayerId) && m.Damage == 0,
                    "minion " + m.Card.Id + $" belongs to the opponent's deck, reset (zone {m.Zone?.Type}, "
                    + $"controller {m.Controller?.PlayerId}, tag {m[GameTag.CONTROLLER]}, damage {m.Damage})");
        }
        // Full destination. UNPINNED ASSUMPTION, not a retail ruling: the wiki Advanced rulebook rule Z1 says a board
        // minion that cannot enter a full deck dies where it stands and fires its Deathrattle. The engine instead
        // moves it to the deck owner's graveyard without dying (as Typhoon does). This case pins that behaviour so a
        // later Z1 change (Typhoon, Sheltered Survivor, Blessing of the Dragon too) trips it on purpose.
        var h = L.Game(); var a = h.Player1; var b = h.Player2;
        var mine = L.Minion(a, "EX1_096"); var theirs = L.Minion(b, "CS2_182"); // Loot Hoarder: Deathrattle draws a card
        while (!b.DeckZone.IsFull) Entity.FromCard(b, Cards.FromId("CS2_231"), zone: b.DeckZone);
        while (!a.DeckZone.IsFull) Entity.FromCard(a, Cards.FromId("CS2_231"), zone: a.DeckZone);
        int hand1 = a.HandZone.Count, hand2 = b.HandZone.Count;
        var s2 = Entity.FromCard(a, Cards.FromId("LOOT_008"), zone: a.HandZone);
        L.Check(h.Process(PlayCardTask.Spell(a, s2)), "play with a full opponent deck");
        L.Check(mine.Zone == b.GraveyardZone && theirs.Zone == b.GraveyardZone && b.DeckZone.IsFull,
            "minions that do not fit go to the deck owner's graveyard (assumption)");
        L.Check(!mine.ToBeDestroyed && !theirs.ToBeDestroyed && a.HandZone.Count == hand1 && b.HandZone.Count == hand2
            && a.DeckZone.IsFull && b.DeckZone.IsFull, "no death and no Deathrattle: Loot Hoarder draws nothing (assumption)");
        // Entomb (LOE_104) is the mirror: an enemy minion goes into the CASTER's own deck and the caster controls it.
        var e = L.Game(); var c1 = e.Player1; var c2 = e.Player2;
        var victim = L.Minion(c2, "CS2_182"); int d1 = c1.DeckZone.Count, d2 = c2.DeckZone.Count;
        var entomb = Entity.FromCard(c1, Cards.FromId("LOE_104"), zone: c1.HandZone);
        L.Check(e.Process(PlayCardTask.SpellTarget(c1, entomb, victim)), "play Entomb");
        L.Check(c1.DeckZone.Count == d1 + 1 && c2.DeckZone.Count == d2 && victim.Zone == c1.DeckZone
            && victim.Controller == c1 && victim[GameTag.CONTROLLER] == c1.PlayerId, "Entomb moves the minion into the caster's deck");
        Console.WriteLine("PASS Psychic Scream and Entomb deck ownership");
    }
}
