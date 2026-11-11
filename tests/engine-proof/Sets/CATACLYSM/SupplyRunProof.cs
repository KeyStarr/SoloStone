using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SupplyRunProof
{
    private static void Deck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string id in ids) Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
    }
    private static void EmptyHand(Controller p)
    {
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
    }
    internal static void Run() => StandardSetsProof.Case("CATA_820", (g, p, id) => {
        ShatterSpellProof.Family(g, p, id, "CATA_820t", "CATA_820t2", "CATA_820e");
        Deck(p, "CS2_182", "CS2_029", "CS2_231", "CS2_179", "CS2_200");
        var held = (Minion)Add(p, "CS2_182"); var spell = Add(p, "CS2_029"); var weapon = Add(p, "CS2_106");
        var opponent = (Minion)Add(p.Opponent, "CS2_182"); var board = Summon(p, "CS2_182");
        var source = ShatterRules.GiveCombined(p, id, p.Hero);
        ShatterSpellProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.DeckZone.Count == 2 && p.DeckZone.Count(c => c is Minion) == 1 && p.DeckZone.Any(c => c.Card.Id == "CS2_029")
            && p.HandZone.OfType<Minion>().Count() == 4 && p.HandZone.OfType<Minion>().All(m => m.AttackDamage == m.Card.ATK + 2
                && m.BaseHealth == m.Card.Health + 2 && m.AppliedEnchantments.Single().Card.Id == "CATA_820e"),
            "draw exactly three deck minions, then buff all current held minions including preexisting");
        Core2026Proof.Check(spell.AppliedEnchantments?.Any() != true && weapon.AppliedEnchantments?.Any() != true && opponent.AttackDamage == 4
            && board.AttackDamage == 4, "spells/weapons/opponent hand/board are excluded");
        PlayEntity(g, p, held); Core2026Proof.Check(held.AttackDamage == 6 && held.Health == 7, "hand stats survive play after spell departure");
        held.Silence(); Core2026Proof.Check(held.AttackDamage == 4 && held.Health == 5, "Silence removes hand-granted stats");
        var future = (Minion)Add(p, "CS2_182"); Core2026Proof.Check(future.AttackDamage == 4, "future hand entries are unbuffed");
        EmptyHand(p); Deck(p, "CS2_182", "CS2_029", "CS2_231", "CS2_179");
        Play(g, p, "CATA_820t");
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.OfType<Minion>().All(m => m.AttackDamage == m.Card.ATK)
            && p.DeckZone.Single().Card.Id == "CS2_029", "first half draws only minions, no buffs");
        Play(g, p, "CATA_820t2"); Play(g, p, "CATA_820t2");
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.OfType<Minion>().All(m => m.AttackDamage == m.Card.ATK + 4), "second half no draw, independent buffs stack");
        foreach (int count in new[] { 0, 1, 2 })
        {
            EmptyHand(p); Deck(p, Enumerable.Repeat("CS2_182", count).Append("CS2_029").ToArray());
            var existing = (Minion)Add(p, "CS2_231");
            PlayEntity(g, p, ShatterRules.GiveCombined(p, id, p.Hero));
            Core2026Proof.Check(p.HandZone.Count == count + 1 && existing.AttackDamage == 3 && existing.Health == 3
                && p.DeckZone.Single().Card.Id == "CS2_029", "short deck still buffs current hand: " + count);
        }
        EmptyHand(p); Deck(p); var survivor = (Minion)Add(p, "CS2_231"); int fatigue = p.Hero.Damage;
        PlayEntity(g, p, ShatterRules.GiveCombined(p, id, p.Hero));
        Core2026Proof.Check(survivor.AttackDamage == 3 && survivor.Health == 3 && p.Hero.Damage == fatigue, "empty tutor still buffs, no fatigue");
        EmptyHand(p); Deck(p, "CS2_182", "CS2_231", "CS2_179");
        source = ShatterRules.GiveCombined(p, id, p.Hero);
        while (!p.HandZone.IsFull) Add(p, "CS2_029");
        int discards = p.DiscardedEntities.Count;
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.OfType<Minion>().Count() == 1 && p.DeckZone.Count == 0
            && p.GraveyardZone.OfType<Minion>().Count(m => new[] { "CS2_182", "CS2_231", "CS2_179" }.Contains(m.Card.Id)) == 2
            && p.HandZone.OfType<Minion>().Single().AttackDamage == p.HandZone.OfType<Minion>().Single().Card.ATK + 2
            && p.DiscardedEntities.Count == discards, "hand full draws/burns all three, buffs only survivor; burn is not discard");
        var failures = new List<string>();
        foreach (string form in new[] { id, "CATA_820t" })
        {
            EmptyHand(p); StandardSetsProof.ParkBoard(p.Opponent);
            // Bottom-to-top: the Imp's replacement draw can remove the next cached tutor index.
            Deck(p, "CS2_029", "CS2_182", "CAP_400t2t");
            var heldBefore = (Minion)Add(p, "CS2_231");
            try
            {
                PlayEntity(g, p, form == id ? ShatterRules.GiveCombined(p, id, p.Hero) : Add(p, form));
                int buff = form == id ? 2 : 0;
                Core2026Proof.Check(p.HandZone.OfType<Minion>().Count() == 2
                    && p.HandZone.OfType<Minion>().All(m => m.AttackDamage == m.Card.ATK + buff && m.BaseHealth == m.Card.Health + buff)
                    && heldBefore.Zone == p.HandZone && !p.DeckZone.Any(c => c is Minion)
                    && p.Opponent.BoardZone.Single().Card.Id == "CAP_400t2t",
                    "replacement draw completes tutor and independent buff: " + form);
            }
            catch (Exception e) { failures.Add(form + ": " + e.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
    });
}
