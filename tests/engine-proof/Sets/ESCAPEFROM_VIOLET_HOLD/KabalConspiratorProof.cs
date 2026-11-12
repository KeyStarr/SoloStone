using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class KabalConspiratorProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_400", (g, p, id) => {
        int ownDeck = p.DeckZone.Count, enemyDeck = p.Opponent.DeckZone.Count;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Opponent.DeckZone.Count == enemyDeck, "no tokens before death"); Kill(g, source);
        var tokens = p.Opponent.DeckZone.Where(c => c.Card.Id == "CAP_400t2t").Cast<Minion>().ToArray();
        Core2026Proof.Check(tokens.Length == 2 && p.DeckZone.Count == ownDeck && p.Opponent.DeckZone.Count == enemyDeck + 2
            && tokens.All(m => m.Card.Implemented && m.AttackDamage == 3 && m.Health == 3 && m.Cost == 3 && m.HasLifeSteal && m.Card.GetRawRace() == Race.DEMON), "two exact Imp-formants in enemy deck only");
        var clone = g.Clone(); Generic.Draw(clone.Player2, clone.IdEntityDic[tokens[0].Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 1 && p.BoardZone.Count == 0 && tokens[0].Zone == p.Opponent.DeckZone, "clone draw summons only in clone");
        var left = Summon(p, "EX1_008"); var right = Summon(p, "EX1_008");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CAP_406e2"), source, tokens[0]);
        Generic.Draw(p.Opponent, tokens[0]); g.DeathProcessingAndAuraUpdate();
        var imp = p.BoardZone.Last();
        Core2026Proof.Check(imp.Card.Id == "CAP_400t2t" && imp.AttackDamage == 5 && imp.Health == 5 && imp.HasLifeSteal
            && p.BoardZone.Select(m => m.Id).Take(2).SequenceEqual(new[] { left.Id, right.Id })
            && p.Opponent.BoardZone.Count == 0 && p.Opponent.HandZone.Count == 1 && p.Opponent.HandZone[0].Card.Id != "CAP_400t2t", "drawn token preserves buffs, appends for opponent and replaces draw");
        p.Hero.Damage = 5; Attack(g, imp, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 0, "Lifesteal belongs to summoning player");
        while (p.BoardZone.Count < 7) Summon(p, "EX1_008"); int before = p.Opponent.HandZone.Count;
        Generic.Draw(p.Opponent, tokens[1]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CAP_400t2t") == 1
            && p.Opponent.HandZone.Count == before + 1, "full receiving board discards token and still replaces draw");
        ClearBoard(g, p); source = Summon(p, id); source.Silence(); enemyDeck = p.Opponent.DeckZone.Count; Kill(g, source);
        Core2026Proof.Check(p.Opponent.DeckZone.Count == enemyDeck, "silence removes Deathrattle");
        source = Summon(p, id); Kill(g, source);
        var burned = p.Opponent.DeckZone.First(c => c.Card.Id == "CAP_400t2t");
        while (p.Opponent.HandZone.Count < 10) Add(p.Opponent, "CS2_182"); enemyDeck = p.Opponent.DeckZone.Count;
        Generic.Draw(p.Opponent, burned); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.HandZone.Count == 10 && p.Opponent.DeckZone.Count == enemyDeck - 1, "full hand burns token without summon or replacement draw");
        while (p.Opponent.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) p.Opponent.DeckZone.Add(Entity.FromCard(p.Opponent, Cards.FromId("CS2_182")));
        source = Summon(p, id); Kill(g, source); Core2026Proof.Check(p.Opponent.DeckZone.Count == DeckZone.DeckMaximumCapcity, "deck capacity respected");
    });
}
