using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HeartrootStonesProof
{
    // Tier D: pinned 37.0.0.254477 MEND_043 text; previous-own-turn query follows MEND_041.
    // Plays MEND_043 and checks the number of draw+Armor repetitions (1 or 2).
    private static void Resolve(Game g, Controller p, string id, int times, string message)
    {
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        int deck = p.DeckZone.Count, armor = p.Hero.Armor, hand = p.HandZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck - times && p.Hero.Armor == armor + 3 * times
            && p.HandZone.Count == Math.Min(10, hand + times) && p.HandZone.All(c => c.Card.Id == "CS2_182"),
            message + $": deck {deck}->{p.DeckZone.Count}, armor {armor}->{p.Hero.Armor}");
    }

    internal static void Run() => StandardSetsProof.Case("MEND_043", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "history consumer declared");
        Resolve(g, p, id, 2, "first own turn has no previous minion play: twice");
        End(g); End(g);
        Play(g, p, "CS2_182");
        Resolve(g, p, id, 2, "previous own turn played only spells; a minion this turn does not count");
        End(g); Play(g, p.Opponent, "CS2_182"); End(g);
        Resolve(g, p, id, 1, "minion played on previous own turn: once (opponent's turn ignored)");
        End(g); Play(g, p.Opponent, "CS2_231"); End(g);
        var clone = g.CloneExact(history: true);
        Resolve(clone, clone.Player1, id, 2, "opponent minion last turn does not count; clone keeps chronology");
        Resolve(g, p, id, 2, "original resolves independently of clone");
        Summon(p, "CS2_231");
        End(g); End(g);
        Resolve(g, p, id, 2, "spell-only previous own turn: twice");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        int deck = p.DeckZone.Count, armor = p.Hero.Armor;
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 2 && p.Hero.Armor == armor + 6,
            "full hand burns the repeated draw; Armor still gained twice");
        PlayEntity(g, p, p.HandZone[0]);
        p.NumTurnsLeft = 2;
        End(g);
        Core2026Proof.Check(g.CurrentPlayer == p, "fixture: consecutive own turn");
        Resolve(g, p, id, 1, "extra own turn uses immediately preceding own turn's minion play");
    });
}
