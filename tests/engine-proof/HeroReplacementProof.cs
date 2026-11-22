using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HeroReplacementProof
{
    [GroupProof("hero-replacement")]
    internal static void Run()
    {
        foreach (string scenario in new[] { "Jaraxxus", "Majordomo killed", "Majordomo played", "Majordomo silenced", "Majordomo and hero killed" })
        {
            var game = new Game(new GameConfig { RandomSeed = 3, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                Player1HeroClass = CardClass.PALADIN, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
            game.StartGame();
            var owner = game.Player1;
            foreach (var opening in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(opening));
            owner.BaseMana = 10;
            Summon(owner, "AT_003");
            // AddHeroAndPower shares AuraEffects, but the Corrupted Sister's enchantment is local to each hero.
            var protector = Summon(owner, "EDR_846t3");
            var oldHero = owner.Hero;
            Core2026Proof.Check(oldHero[GameTag.HEROPOWER_DAMAGE] == 1, "fixture: aura on old hero");
            Core2026Proof.Check(oldHero.AppliedEnchantments.Any(e => e.Card.Id == "EDR_846t3e" && e.Creator == protector),
                "fixture: HERO aura enchantment on old hero");
            Minion source;
            if (scenario == "Jaraxxus") source = (Minion)Play(game, owner, "EX1_323");
            else if (scenario == "Majordomo played") source = (Minion)Play(game, owner, "BRM_027");
            else
            {
                source = Summon(owner, "BRM_027");
                if (scenario == "Majordomo silenced") source.Silence();
                // A real lethal spell runs the Deathrattle through normal resolution.
                if (scenario == "Majordomo and hero killed")
                {
                    source.Damage = source.Health - 3;
                    oldHero.Damage = oldHero.Health - 3;
                    Play(game, owner, "CS2_062");
                }
                else Play(game, owner, "EX1_279", source);
            }
            game.DeathProcessingAndAuraUpdate();
            bool replaced = scenario == "Jaraxxus" || scenario == "Majordomo killed";
            var hero = owner.Hero;
            Core2026Proof.Check((hero.Id != oldHero.Id) == replaced, scenario + " replacement timing");
            if (scenario == "Majordomo and hero killed")
                Core2026Proof.Check(game.State == State.COMPLETE && owner.PlayState == PlayState.LOST
                    && owner.Opponent.PlayState == PlayState.WON
                    && !game.PowerHistory.Full.OfType<PowerHistoryFullEntity>().Any(row => row.Entity.Name == "BRM_027h"),
                    scenario + " ends in a loss without a replacement FULL");
            if (replaced)
            {
                Core2026Proof.Check(hero.Card.Id == (scenario == "Jaraxxus" ? "EX1_323h" : "BRM_027h"), scenario + " replacement card");
                Core2026Proof.Check(hero[GameTag.CONTROLLER] == owner.PlayerId && hero[GameTag.ENTITY_ID] == hero.Id, scenario + " live identity");
                var full = game.PowerHistory.Full.OfType<PowerHistoryFullEntity>().Last(row => row.Entity.Id == hero.Id);
                Core2026Proof.Check(full.Entity.Tags.TryGetValue(GameTag.CONTROLLER, out int controller) && controller == owner.PlayerId
                    && full.Entity.Tags.TryGetValue(GameTag.ENTITY_ID, out int id) && id == hero.Id, scenario + " FULL identity");
                Core2026Proof.Check(hero[GameTag.HEROPOWER_DAMAGE] == 1, scenario + " aura rebound to new hero");
                Core2026Proof.Check(hero.AppliedEnchantments?.Count(e => e.Card.Id == "EDR_846t3e" && e.Creator == protector && e.Target == hero) == 1
                    && !oldHero.AppliedEnchantments.Any(e => e.Card.Id == "EDR_846t3e" && e.Creator == protector),
                    scenario + " HERO aura enchantment moves from old hero to replacement");
            }
            if (scenario.StartsWith("Majordomo"))
                Core2026Proof.Check(source.Zone.Type == (scenario == "Majordomo played" ? Zone.PLAY : Zone.GRAVEYARD), scenario + " source stays in its zone");
            Console.WriteLine("PASS hero replacement engine: " + scenario);
        }
    }
}
