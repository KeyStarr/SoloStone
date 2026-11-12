using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System.Text.Json;

namespace TransportInvariants;
using Entity = SabberStoneCore.Model.Entities.Entity;
using Game = SabberStoneCore.Model.Game;

internal static class CanonicalPolicyChecks
{
    static void Assert(bool valid, string name)
    {
        if (!valid) throw new Exception(name);
    }

    internal static void Run()
    {
        var random = new PolicyRandom(0);
        Assert(random.Next() == 0xe220a8397b1dcdafUL, "SplitMix64 vector 1");
        Assert(random.Next() == 0x6e789e6aa1b965f4UL, "SplitMix64 vector 2");
        Assert(random.Next() == 0x06c45d188009454fUL, "SplitMix64 vector 3");
        try { random.Index(0); throw new Exception("zero bound accepted"); } catch (ArgumentOutOfRangeException) { }
        for (int i = 0; i < 1000; i++) Assert(random.Index(7) is >= 0 and < 7, "policy index bound");
        Assert(Canonical.Sha("abc") == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "SHA UTF-8");
        Assert(Canonical.Value(1234567890123L) == "1234567890123", "history wide numeric fields retained");
        var tags = new Dictionary<GameTag, int> { [(GameTag)10] = 4, [(GameTag)2] = 3 };
        Assert(Canonical.Value(tags) == "{\"2\":3,\"10\":4}", "numeric tag sorting");
        var reverse = new Dictionary<GameTag, int> { [(GameTag)2] = 3, [(GameTag)10] = 4 };
        Assert(Canonical.Value(tags) == Canonical.Value(reverse), "dictionary insertion independence");
        var entry = new PowerHistoryBlockStart { Index = 7, Source = 9, Target = 10, EffectCardId = "quote\"\n", BlockType = BlockType.PLAY };
        string encoded = Canonical.Value(entry);
        using (var parsed = JsonDocument.Parse(encoded))
        {
            var root = parsed.RootElement;
            Assert(root.GetProperty("type").GetString() == "PowerHistoryBlockStart", "history type");
            Assert(root.GetProperty("Index").GetInt32() == 7 && root.GetProperty("Source").GetInt32() == 9
                && root.GetProperty("Target").GetInt32() == 10 && root.GetProperty("EffectCardId").GetString() == "quote\"\n", "all block fields and string escaping");
        }
        entry.Target++;
        Assert(Canonical.Value(entry) != encoded, "history target change retained");
        var game = Simulation.Create(new GameRecord { Player = "mage", Opponent = "warrior", Seed = 0 });
        string state = Canonical.State(game);
        game.Player1.Hero.NativeTags[(GameTag)99999] = 0;
        Assert(state == Canonical.State(game), "implicit zero tags have canonical default representation");
        game.Player1.Hero.Health--;
        Assert(state != Canonical.State(game), "effective entity state retains health changes");
        foreach (var tag in new[] { GameTag.HEALING_DOES_DAMAGE, GameTag.HERO_POWER_DISABLED, GameTag.SPELL_HEALING_DOUBLE })
        {
            Assert(!game.Player1.NativeTags.ContainsKey(tag) && !game.Player1.Card.Tags.ContainsKey(tag), "aura-only fixture");
            string before = Canonical.Sha(Canonical.State(game));
            game.Player1.ControllerAuraEffects[tag] = 1;
            Assert(before != Canonical.Sha(Canonical.State(game)), $"aura-only {tag} changes state hash");
            game.Player1.ControllerAuraEffects[tag] = 0;
            Assert(before == Canonical.Sha(Canonical.State(game)), $"aura-only {tag} restored");
        }
        var choice = PowerChoicesBuilder.EntityChoices(game, game.Player2.Choice);
        string choicePacket = Canonical.Value(choice, 1);
        using (var parsed = JsonDocument.Parse(choicePacket))
        {
            var root = parsed.RootElement;
            Assert(root.GetProperty("Index").GetInt32() == 1 && root.GetProperty("CountMin").GetInt32() == 0
                && root.GetProperty("CountMax").GetInt32() == choice.Entities.Count && root.GetProperty("HideChosen").GetBoolean()
                && root.GetProperty("PlayerId").GetInt32() == 2, "mulligan packet fields");
            Assert(root.GetProperty("Entities").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual(choice.Entities), "choice entity order");
        }
        choice.Index += 100;
        Assert(choicePacket == Canonical.Value(choice, 1), "global choice id normalized");
        choice.CountMax--;
        Assert(choicePacket != Canonical.Value(choice, 1), "choice constraints retained");
        choice.CountMax++;
        choice.HideChosen = false;
        Assert(choicePacket != Canonical.Value(choice, 1), "choice visibility retained");
        var source = Entity.FromCard(game.Player1, Cards.FromId("EX1_166"));
        var tasks = new List<PlayerTask> {
            PlayCardTask.Any(game.Player1, source, game.Player2.Hero, chooseOne: -1),
            PlayCardTask.Any(game.Player1, source, game.Player1.Hero, chooseOne: 1),
            PlayCardTask.Any(game.Player1, source, game.Player2.Hero, chooseOne: 2),
            TradeCardTask.Any(game.Player1, source)
        };
        var options = Canonical.Options(game, tasks);
        Assert(options.PowerOptionList[0].MainOption.Targets.SequenceEqual(new[] { game.Player2.Hero.Id }), "ordinary chooseOne -1 target repair");
        Assert(options.PowerOptionList[0].SubOptions.Count == 2, "choose-one sub-options built");
        Assert(options.PowerOptionList[1].MainOption.PowerKeyword == (int)GameTag.TRADEABLE
            && options.PowerOptionList[1].MainOption.Targets.Count == 0, "trade keyword and target isolation");
        string optionPacket = Canonical.Value(options, 1);
        using (var parsed = JsonDocument.Parse(optionPacket))
            Assert(parsed.RootElement.GetProperty("PowerOptionList")[1].GetProperty("MainOption")
                .GetProperty("PowerKeyword").GetInt32() == (int)GameTag.TRADEABLE, "trade keyword serialized");
        options.Index += 100;
        Assert(optionPacket == Canonical.Value(options, 1) && !optionPacket.Contains("PlayerTaskList"), "global option id normalized; resolution cache excluded");
        options.PowerOptionList[0].SubOptions[0].Targets.Add(game.Player2.Hero.Id);
        Assert(optionPacket != Canonical.Value(options, 1), "sub-option targets retained");
        #if SOURCE_ENGINE
        PoolSnapshots.Save();
        #endif
        var gameRecord = new GameRecord { Player = "mage", Opponent = "warrior", Seed = 0 };
        Game played = Simulation.Run(gameRecord);
        Assert(gameRecord.LegalShas.Count == gameRecord.Actions.Count, "every action has ordered legal hash");
        Assert(Canonical.History(played) == Canonical.History(Simulation.Run(gameRecord, replay: true)), "packet counters restart per game in same process");
        string[] decisions = Canonical.History(played).Split("--- DECISIONS ---\n")[1].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert(decisions.Length == gameRecord.Actions.Count, "every decision captured");
        using (var first = JsonDocument.Parse(decisions[0]))
            Assert(first.RootElement.GetProperty("choices").GetProperty("PlayerId").GetInt32() == 2, "opponent mulligan captured first");
        gameRecord.LegalShas[0] = new string('0', 64);
        try { Simulation.Run(gameRecord, replay: true); throw new Exception("legal hash mismatch accepted"); }
        catch (InvariantException error) { Assert(error.Invariant == "replay.legal" && error.Message.Contains("action 0"), "clear legal mismatch failure before valid index"); }
        Console.WriteLine("canonical/policy checks passed");
    }
}
