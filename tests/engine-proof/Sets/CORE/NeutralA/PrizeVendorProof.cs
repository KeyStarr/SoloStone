using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PrizeVendorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DMF_067", (g, p, id) => {
        int ownDeck = p.DeckZone.Count, enemyDeck = p.Opponent.DeckZone.Count;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 3 && source.IsRace(Race.MURLOC)
            && source.HasDeathrattle, "printed 2/3 Murloc with Deathrattle");
        Core2026Proof.Check(p.HandZone.Count == 1 && p.Opponent.HandZone.Count == 1
            && p.DeckZone.Count == ownDeck - 1 && p.Opponent.DeckZone.Count == enemyDeck - 1, "Battlecry draws one for each side");
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "CS2_182" && p.Opponent.HandZone.Single().Card.Id == "CS2_182", "draws actual deck identities");
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 2
            && p.DeckZone.Count == ownDeck - 2 && p.Opponent.DeckZone.Count == enemyDeck - 2, "Deathrattle draws one for each side");
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 2, "summon does not trigger Battlecry; silenced death draws nothing");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        while (!p.Opponent.HandZone.IsFull) Add(p.Opponent, "CS2_182");
        ownDeck = p.DeckZone.Count; enemyDeck = p.Opponent.DeckZone.Count;
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Opponent.HandZone.Count == 10
            && p.DeckZone.Count == ownDeck - 1 && p.Opponent.DeckZone.Count == enemyDeck - 1, "full hands burn both draws");
        foreach (var owner in new[] { p, p.Opponent })
            foreach (var card in owner.DeckZone.ToArray()) { owner.DeckZone.Remove(card); owner.SetasideZone.Add(card); }
        int ownDamage = p.Hero.Damage, enemyDamage = p.Opponent.Hero.Damage;
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage == ownDamage + 1 && p.Opponent.Hero.Damage == enemyDamage + 1, "empty decks fatigue both players");
    });
}
