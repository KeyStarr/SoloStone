using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static RangerGeneralSylvanasProof;

internal static class RangerInitiateVereesaProof
{
    internal static void Run() => CompanionCase("TIME_609t2", (g, p) => {
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(spell);
        var hand = (Minion)Add(p, "CS2_182"); var board = Summon(p, "CS2_182");
        var deck = p.DeckZone.OfType<Minion>().First(m => m.Card.Id == "CS2_182");
        Summon(p, "TIME_609t2");
        Core2026Proof.Check(deck.AttackDamage == 4 && (deck.AppliedEnchantments?.Count ?? 0) == 0, "summoning Vereesa never buffs deck");
        PlayEntity(g, p, p.HandZone.Single(c => c.Card.Id == "TIME_609t2"));
        Core2026Proof.Check(deck.AttackDamage == 5 && deck.Health == 6 && deck.AppliedEnchantments.Single().Card.Id == "TIME_609t2e"
            && hand.AttackDamage == 4 && board.AttackDamage == 4 && (spell.AppliedEnchantments?.Count ?? 0) == 0,
            "base +1/+1 applies only to own deck minions using exact enchantment");
        Core2026Proof.Check(p.Opponent.DeckZone.OfType<Minion>().All(m => m.AttackDamage == 4 && m.Health == 5), "opponent deck untouched");
        ParkBoard(p); End(g); Play(g, p.Opponent, "TIME_609"); End(g);
        Play(g, p, "TIME_609t2"); Core2026Proof.Check(deck.AttackDamage == 6, "opponent named partner excluded");
        ParkBoard(p); Play(g, p, "TIME_609"); ParkBoard(p);
        Play(g, p, "TIME_609t2"); Core2026Proof.Check(deck.AttackDamage == 8 && deck.Health == 9, "one friendly partner repeats once");
        ParkBoard(p); Play(g, p, "TIME_609"); Play(g, p, "TIME_609t1"); AnswerAll(g, 3); ParkBoard(p);
        var clone = g.CloneExact(history: true);
        Play(clone, clone.Player1, "TIME_609t2");
        Core2026Proof.Check(((Minion)clone.IdEntityDic[deck.Id]).AttackDamage == 11 && deck.AttackDamage == 8, "two distinct partners; copies count once; clone buff isolated");
        Play(g, p, "TIME_609t2");
        Core2026Proof.Check(deck.AttackDamage == 11 && deck.Health == 12 && deck.AppliedEnchantments.Count == 7, "three separate +1/+1 enchantments for last Battlecry");
        PoolPilotProof.EmptyHand(p); Generic.Draw(p, deck); PlayEntity(g, p, deck);
        Core2026Proof.Check(deck.AttackDamage == 11 && deck.Health == 12, "deck enchantments persist through draw and play");
        deck.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(deck.AttackDamage == 4 && deck.Health == 5, "Silence removes deck buffs");
        ParkBoard(p);
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        Play(g, p, "TIME_609t2"); Core2026Proof.Check(p.DeckZone.IsEmpty, "empty deck no-op");
        FullBoardRejects(g, p, "TIME_609t2");
    });
}
