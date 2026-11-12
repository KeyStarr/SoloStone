using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PsychicConjurerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_193", (g, p, id) => {
        var deck = p.Opponent.DeckZone.ToArray();
        var source = (Minion)Play(g, p, id);
        var copy = p.HandZone.Single();
        Core2026Proof.Check(source.IsRace(Race.UNDEAD) && copy.Card.Id == "CS2_182" && copy.Id != deck[0].Id, "fresh hand identity, printed Undead");
        Core2026Proof.Check(p.Opponent.DeckZone.Select(x => x.Id).SequenceEqual(deck.Select(x => x.Id)), "opponent deck untouched");
        Entity.FromCard(p.Opponent, Cards.FromId("CS2_029"), zone: p.Opponent.DeckZone);
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, id);
        Core2026Proof.Check(p.HandZone.Count == 1, "clone copy leaves original hand untouched");
        Play(g, p, id);
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "seeded deck selection stable across clone");
        foreach (var card in p.Opponent.DeckZone.ToArray()) { p.Opponent.DeckZone.Remove(card); p.Opponent.SetasideZone.Add(card); }
        int count = p.HandZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == count, "empty opponent deck adds nothing");
        Entity.FromCard(p.Opponent, Cards.FromId("CS2_029"), zone: p.Opponent.DeckZone);
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Opponent.DeckZone.Count == 1, "full hand cannot receive a copy");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        p.Opponent.DeckZone.Remove(p.Opponent.DeckZone.Single());
        var enchanted = (Minion)Entity.FromCard(p.Opponent, Cards.FromId("CS2_182"), zone: p.Opponent.DeckZone);
        Generic.AddEnchantmentBlock(g, Cards.FromId("BT_292e"), p.Opponent.Hero, enchanted);
        enchanted.Cost = 2;
        ClearBoard(g, p);
        Play(g, p, id);
        var enchantedCopy = (Minion)p.HandZone.Single();
        Core2026Proof.Check(enchantedCopy.Card.Id == "CS2_182" && enchantedCopy.AttackDamage == 6
            && enchantedCopy.BaseHealth == 6 && enchantedCopy.Health == 6 && enchantedCopy.Cost == 2
            && enchantedCopy[GameTag.COST] == 2, "deck copy retains buffed Attack, Health and visible modified COST");
        var originalBuff = enchanted.AppliedEnchantments.Single(e => e.Card.Id == "BT_292e");
        var copiedBuff = enchantedCopy.AppliedEnchantments.Single(e => e.Card.Id == "BT_292e");
        Core2026Proof.Check(originalBuff.Id != copiedBuff.Id && originalBuff.Target == enchanted
            && copiedBuff.Target == enchantedCopy, "copy has independently attached enchantment");
        enchantedCopy.Silence();
        Core2026Proof.Check(enchantedCopy.AttackDamage == 4 && enchantedCopy.BaseHealth == 5
            && enchanted.AttackDamage == 6 && enchanted.BaseHealth == 6 && enchanted.Cost == 2
            && p.Opponent.DeckZone.Single() == enchanted && originalBuff.Target == enchanted,
            "copy Silence leaves original deck entity and enchantment untouched");
    });
}
