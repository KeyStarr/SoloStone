using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;

internal static class GraftedKnightProof
{
    // Keep corpse opt-in local to this slice; existing Standard proofs retain legacy defaults.
    internal static Game NewGame(Game seeded)
    {
        var game = new Game(new GameConfig { EnableCorpses = true, RandomSeed = seeded.Random.Next(1, 100000),
            StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true, Logging = false,
            FormatType = FormatType.FT_STANDARD, Player1HeroClass = CardClass.DEATHKNIGHT,
            Player2HeroClass = CardClass.DEATHKNIGHT,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame(); EmptyHand(game.Player1); EmptyHand(game.Player2);
        game.Player1.BaseMana = 10; game.Player2.BaseMana = 10;
        return game;
    }

    internal static void FullBoardRejects(Game game, string id)
    {
        var player = game.Player1;
        while (!player.BoardZone.IsFull) Summon(player, "CS2_182");
        var card = Add(player, id);
        Core2026Proof.Check(player.BoardZone.SlotCount == 7 && player.BoardZone.HasLocations && !card.IsPlayableByPlayer
            && !player.Options().Any(t => t.Source?.Id == card.Id), id + " location occupies seventh slot");
    }

    internal static void ClearMinions(Game game, Controller player)
    {
        foreach (var minion in player.BoardZone.ToArray()) minion.Silence();
        StandardSetsProof.ClearBoard(game, player);
    }

    internal static void Run() => StandardSetsProof.Case("BE_285", (g, p, id) => {
        g = NewGame(g); p = g.Player1;
        p.Hero.Damage = 10;
        var knight = (Minion)Play(g, p, id);
        Core2026Proof.Check(knight.HasLifeSteal && knight.AttackDamage == 3 && knight.Health == 3, "printed Lifesteal body");
        Resume(g, next => Attack(next, (Minion)next.IdEntityDic[knight.Id], next.Player2.Hero));
        Core2026Proof.Check(p.Hero.Damage == 7 && p.Opponent.Hero.Damage == 3, "combat heals own hero for damage dealt");
        knight = Summon(p, id); p.Hero.Damage = 10; p.Opponent.Hero.Armor = 5;
        int healthDamage = p.Opponent.Hero.Damage;
        Resume(g, next => Attack(next, (Minion)next.IdEntityDic[knight.Id], next.Player2.Hero));
        Core2026Proof.Check(p.Hero.Damage == 7 && p.Opponent.Hero.Armor == 2 && p.Opponent.Hero.Damage == healthDamage,
            "Lean_LifestealArmorDamage: Grafted Knight heals Armor-only damage");
        knight = Summon(p, id); p.Hero.Damage = 10;
        Attack(g, knight, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 7 && p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == healthDamage + 1,
            "Lean_LifestealArmorDamage: mixed two Armor plus one Health heals three");
        knight = Summon(p, id); p.Hero.Damage = 1;
        Attack(g, knight, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 0, "Lifesteal caps at maximum health");
        knight = Summon(p, id); knight.Silence(); p.Hero.Damage = 5;
        Attack(g, knight, p.Opponent.Hero);
        Core2026Proof.Check(!knight.HasLifeSteal && p.Hero.Damage == 5, "Silence removes Lifesteal");
        LocationProofs.Location(p); FullBoardRejects(g, id);
    });
}
