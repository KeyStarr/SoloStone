using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SmuggledShovelProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_380", (g, p, id) => {
        // Same printed spell, distinct physical origin; the fact predates creating the weapon.
        int originalId = p.DeckZone[0].Id;
        Generic.ChangeEntityBlock(p, p.DeckZone[0], Cards.FromId("CS2_023"), false);
        var original = g.IdEntityDic[originalId];
        var generated = Entity.FromCard(p, Cards.FromId("CS2_023"));
        Generic.ShuffleIntoDeck(p, p.Hero, generated);
        var enemySpell = Entity.FromCard(p.Opponent, Cards.FromId("CS2_023"));
        Generic.ShuffleIntoDeck(p.Opponent, p.Opponent.Hero, enemySpell);
        End(g); End(g);
        var weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 1 && weapon.Durability == 2 && weapon.HasDeathrattle, "printed Shovel 1/2 Deathrattle");
        var clone = g.Clone();
        Break(clone, clone.Player1);
        Core2026Proof.Check(clone.Player1.HandZone.Any(c => c.Id == generated.Id)
            && generated.Zone == p.DeckZone && original.Zone == p.DeckZone && p.Hero.Weapon == weapon,
            "clone deathrattle draws generated same-ID spell, original game untouched");
        Break(g, p);
        Core2026Proof.Check(generated.Zone == p.HandZone && original.Zone == p.DeckZone
            && enemySpell.Zone == p.Opponent.DeckZone, "only own generated spell drawn despite earlier-turn origin");
        var entry = g.HistoryFacts.Query.LatestHandEntry(generated.Id);
        Core2026Proof.Check(entry.EntryKind == HistoryHandEntryKind.Draw, "filtered draw records actual draw, not generation");
        var generatedMinion = Entity.FromCard(p, Cards.FromId("CS2_231"));
        Generic.ShuffleIntoDeck(p, p.Hero, generatedMinion);
        int fatigue = p.Hero.Damage; int deck = p.DeckZone.Count; int hand = p.HandZone.Count;
        Play(g, p, id); Play(g, p, "CS2_082");
        Core2026Proof.Check(p.DeckZone.Count == deck && p.HandZone.Count == hand && p.Hero.Damage == fatigue
            && original.Zone == p.DeckZone && generatedMinion.Zone == p.DeckZone,
            "replacement deathrattle excludes generated minion with no eligible spells: no draw or fatigue");
        // An enrolled spell remains original after shuffling out/back; a copy never acquires membership.
        p.DeckZone.Remove(original); Generic.ShuffleIntoDeck(p, p.Hero, original);
        var copy = Generic.Copy(p, p.Hero, original, Zone.DECK);
        Play(g, p, id); Break(g, p);
        Core2026Proof.Check(copy.Zone == p.HandZone && original.Zone == p.DeckZone, "original reshuffle excluded, copied spell eligible");
        var gear = Entity.FromCard(p, Cards.FromId("JAIL_386t")); Generic.ShuffleIntoDeck(p, p.Hero, gear);
        int armor = p.Hero.Armor;
        Play(g, p, id); Break(g, p);
        Core2026Proof.Check(gear.Zone == p.SetasideZone && p.Hero.Armor == armor + 2
            && g.HistoryFacts.Query.Casts().Count(f => f.EntityId == gear.Id && f.CasterKind == HistoryCastKind.Drawn) == 2
            && !g.HistoryFacts.Query.Plays().Any(f => f.EntityId == gear.Id), "filtered draw keeps casts-when-drawn path: exact Gear, two armor, no play");
        var extra = Entity.FromCard(p, Cards.FromId("CS2_023")); Generic.ShuffleIntoDeck(p, p.Hero, extra);
        Play(g, p, id); while (!p.HandZone.IsFull) Add(p, "CS2_231"); Break(g, p);
        Core2026Proof.Check(extra.Zone == p.GraveyardZone && p.HandZone.Count == 10, "full hand burns eligible spell through normal draw");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        Play(g, p, id); fatigue = p.Hero.Damage; Break(g, p);
        Core2026Proof.Check(p.Hero.Damage == fatigue, "empty deck has no qualifying draw and no fatigue");
    });

    private static void Break(Game game, Controller player)
    {
        player.Hero.Weapon.Damage = 1;
        player.Hero.IsExhausted = false; player.Hero.NumAttacksThisTurn = 0;
        var attack = player.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == player.Opponent.Hero.Id);
        Core2026Proof.Check(game.Process(attack) && player.Hero.Weapon == null, "last durability destroys weapon");
    }
}
