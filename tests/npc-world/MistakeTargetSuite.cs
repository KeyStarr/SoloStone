using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

// Guards skill "mistakes" that look like blunders a human would never make. Before the guard, softmax and typed
// mistakes could swap the search's face ping for a ping on the NPC's own hero (14 of 200 seeds) or its own minion (11
// of 200). A mistake may now only pick a target of the same polarity (hurt an enemy, help a friend), unless the search
// itself hurt or helped that same entity the same way.
internal static class MistakeTargetSuite
{
    internal static void Run()
    {
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.PRIEST,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = false, Logging = false, RandomSeed = 7 });
        game.StartGame();
        var own = game.Player1; var enemy = game.Player2;
        foreach (var card in own.HandZone.ToArray()) own.HandZone.Remove(card);
        own.BaseMana = 10; own.UsedMana = 0;
        var friend = Entity.FromCard(own, Cards.FromId("CS2_182"), zone: own.BoardZone);
        var foe = Entity.FromCard(enemy, Cards.FromId("CS2_182"), zone: enemy.BoardZone);
        var sources = new Dictionary<string, IPlayable>();
        foreach (string id in new[] { "CS2_029", "EX1_161", "CS2_022", "EX1_332", "CS2_024", "EX1_581", "CS2_089", "CS2_087", "CS2_039" })
            sources[id] = Entity.FromCard(own, Cards.FromId(id), zone: own.HandZone);
        var facts = FairBotState.Create(game, 1, 7, DeckStyle.Tempo).Facts;
        var guard = new MistakeTargetGuard(facts);
        Func<string, IPlayable, BotAction> play = (id, target) => BotAction.From(PlayCardTask.Any(own, sources[id], target));
        var end = BotAction.From(EndTurnTask.Any(own));
        foreach (string id in sources.Keys.Take(6))
        {
            Program.Require(!guard.Allows(end, play(id, friend)), "friendly damage/removal rejected: " + id);
            Program.Require(guard.Allows(end, play(id, foe)), "enemy damage/removal allowed: " + id);
            Program.Require(guard.Allows(play(id, friend), play(id, friend)), "search self-harm preserved: " + id);
            Program.Require(!guard.Allows(play(id, friend), play(id, own.Hero)), "self-harm exception bound to same entity: " + id);
        }
        foreach (string id in sources.Keys.Skip(6))
        {
            Program.Require(guard.Allows(end, play(id, friend)), "friendly heal/buff/keyword allowed: " + id);
            Program.Require(!guard.Allows(end, play(id, foe)), "enemy heal/buff/keyword rejected: " + id);
            Program.Require(!guard.Allows(end, play(id, enemy.Hero)), "enemy hero benefit rejected: " + id);
            Program.Require(guard.Allows(play(id, foe), play(id, foe)), "search enemy benefit preserved: " + id);
        }
        Program.Require(!guard.Allows(play("CS2_089", friend), play("CS2_029", friend)), "friendly heal cannot license damage");
        Program.Require(!guard.Allows(play("CS2_029", foe), play("CS2_087", foe)), "enemy damage cannot license buff");
        Program.Require(guard.Allows(play("CS2_029", friend), play("CS2_024", friend)), "different harmful source may retain search's synergy target");
        var best = play("CS2_029", foe);
        var bad = play("CS2_029", own.Hero);
        var context = new MistakeContext(5, false, 0, false, enemy.Hero.Id, new[] { foe.Id },
            new[] { best, bad }, null, false, facts: facts);
        var pairs = new[] { Pair(best, 20), Pair(bad, 19.9), Pair(end, 19.8) };
        var regular = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("regular");
        Program.Require(Enumerable.Range(0, 200).All(seed => SkilledPilot.Select(pairs, regular, seed, context).Index != 1), "softmax rejects own hero damage");
        var enemyBenefit = play("CS2_087", foe);
        Program.Require(Enumerable.Range(0, 200).All(seed => SkilledPilot.Select(
            new[] { Pair(best, 20), Pair(enemyBenefit, 19.9) }, regular, seed, context).Index == 0), "softmax rejects enemy benefits");
        // Passivity rule: softmax never ends the turn early.
        Program.Require(Enumerable.Range(0, 200).All(seed => SkilledPilot.Select(pairs, regular, seed, context).Index != 2), "softmax never ends the turn");
        foreach (var kind in new[] { MistakeKind.HoldTooLong, MistakeKind.Misorder })
        {
            // HoldTooLong may use another source; Misorder may use a later action on the search's line.
            var alternative = BotAction.From(HeroPowerTask.Any(own, own.Hero));
            var ctx = new MistakeContext(5, false, 0, false, enemy.Hero.Id, new[] { foe.Id },
                new[] { best, alternative }, null, false, facts: facts);
            var skill = new SkillProfile("test", 1, 0, 1, regular.Budget, 0, 1, 1, new[] { new MistakeRule(kind, 1, 4) });
            Program.Require(Enumerable.Range(0, 200).All(seed => SkilledPilot.Select(new[] { Pair(best, 20), Pair(alternative, 19.9) }, skill, seed, ctx).Index == 0), "typed friendly target guard: " + kind);
            var enemyBuff = play("CS2_087", foe);
            ctx = new MistakeContext(5, false, 0, false, enemy.Hero.Id, new[] { foe.Id }, new[] { best, enemyBuff }, null, false, facts: facts);
            Program.Require(SkilledPilot.Select(new[] { Pair(best, 20), Pair(enemyBuff, 19.9) }, skill, 0, ctx).Index == 0, "typed enemy buff guard: " + kind);
        }
        // Opaque card text still uses direct engine damage/destroy/transform/heal tasks.
        var opaque = Cards.FromId("CS2_029").Clone();
        typeof(Card).GetProperty("Text").SetValue(opaque, "Unclassified effect.");
        foreach (var task in new SabberStoneCore.Tasks.ISimpleTask[] { new DamageTask(1, EntityType.TARGET),
            new DestroyTask(EntityType.TARGET), new TransformTask("CS2_182", EntityType.TARGET), new SilenceTask(EntityType.TARGET) })
        {
            opaque.Power = new Power { PowerTask = task };
            Program.Require(MistakeTargetGuard.Classify(opaque) == MistakeTargetGuard.Effect.Harmful, "unknown text engine harm: " + task);
        }
        opaque.Power = new Power { PowerTask = new HealTask(2, EntityType.TARGET) };
        Program.Require(MistakeTargetGuard.Classify(opaque) == MistakeTargetGuard.Effect.Beneficial, "unknown text engine healing");
        Program.Require(MistakeTargetGuard.Classify(opaque, true) == MistakeTargetGuard.Effect.Harmful, "healing conversion is harmful");
        opaque.Power = null;
        Program.Require(MistakeTargetGuard.Classify(opaque) == MistakeTargetGuard.Effect.Unknown, "unknown polarity remains unrestricted");
        typeof(Card).GetProperty("Text").SetValue(opaque, "Give a minion +1 Spell Damage.");
        Program.Require(MistakeTargetGuard.Classify(opaque) == MistakeTargetGuard.Effect.Beneficial, "Spell Damage buff does not deal damage");
        typeof(Card).GetProperty("Text").SetValue(opaque, "Give a minion -2/-2.");
        Program.Require(MistakeTargetGuard.Classify(opaque) == MistakeTargetGuard.Effect.Harmful, "negative stats are a debuff");
        var holyFire = Cards.FromId("EX1_624");
        Program.Require((MistakeTargetGuard.Classify(holyFire) & MistakeTargetGuard.Effect.Beneficial) == 0, "healing own hero does not make targeted enemy damage beneficial");
        Console.WriteLine("PASS mistake target guard: damage/removal/debuff, enemy benefits, typed mistakes, exact synergy target, unknown effects");
    }
    private static KeyValuePair<BotAction, double> Pair(BotAction action, double value) => new KeyValuePair<BotAction, double>(action, value);
}
