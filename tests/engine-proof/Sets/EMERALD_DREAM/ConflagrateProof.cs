using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ConflagrateProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_954", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182"); TargetRules(g, p, id, enemy, friend, false);
        int theirDeck = p.Opponent.DeckZone.Count; int ownDeck = p.DeckZone.Count;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.Opponent.DeckZone.Count == theirDeck - 1 && p.DeckZone.Count == ownDeck, "lethal enemy target still draws for its owner only");
        friend.HasDivineShield = true; Play(g, p, id, friend);
        Core2026Proof.Check(friend.Damage == 0 && !friend.HasDivineShield && p.DeckZone.Count == ownDeck - 1, "friendly shield absorbs; owner draws regardless");
        var big = Summon(p.Opponent, "EX1_298"); Summon(p, "EX1_012"); Play(g, p, id, big);
        Core2026Proof.Check(big.Damage == 6, "Spell Damage increases damage");
        while (!p.Opponent.HandZone.IsFull) Add(p.Opponent, "CS2_182"); theirDeck = p.Opponent.DeckZone.Count;
        Play(g, p, id, big); Core2026Proof.Check(p.Opponent.HandZone.Count == 10 && p.Opponent.DeckZone.Count == theirDeck - 1, "full hand burns owner draw");
        EmeraldDreamBatch3Proof.EmptyDeck(p.Opponent); enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id, enemy);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 1, "empty owner deck causes fatigue");
    });
}
