using EngineController = SabberStoneCore.Model.Entities.Controller;
using EngineMinion = SabberStoneCore.Model.Entities.Minion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineGame = SabberStoneCore.Model.Game;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static class QuestNativeProof
{
    internal static void Check(bool value, string message)
    { if (!value) throw new Exception("Quest native: " + message); }

    internal static EngineGame Fixture()
    {
        var game = new EngineGame(new GameConfig { RandomSeed = 19, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 })
        {
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
            p.BaseMana = 10;
        }
        return game;
    }
    internal static IPlayable Add(EngineController p, string id) => Generic.DrawCard(p, Cards.FromId(id));
    internal static IPlayable Play(EngineGame game, EngineController p, string id)
    {
        p.UsedMana = 0;
        var card = Add(p, id);
        Check(game.Process(p.Options().First(t => t.Source?.Id == card.Id)), "play " + id);
        return card;
    }
    internal static void Summon(EngineGame g, EngineController p, string id)
    {
        var card = (EngineMinion)EngineEntity.FromCard(p, Cards.FromId(id));
        Generic.SummonBlock(g, card, -1, null); g.DeathProcessingAndAuraUpdate();
    }

    internal static void Run()
    {
        var game = Fixture(); var p = game.Player1;
        AllOptions options = null;
        var match = new NativeMatch((id, packet) => { if (packet is AllOptions all) options = all; });
        typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
        void Publish() => typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(match, null);
        var assistant = Add(p, "TLC_987"); var quest = Add(p, "TLC_426");
        Summon(game, p.Opponent, "CS2_182");
        Publish();
        var questOption = options.Options.Single(o => o.MainOption?.Id == quest.Id);
        Check(questOption.Type_ == PegasusGame.Option.Type.POWER && questOption.MainOption.Targets.Count == 0
            && questOption.SubOptions.Count == 0, "quest POWER has no target/suboptions");
        Check(options.Options.Single(o => o.MainOption?.Id == assistant.Id).MainOption.Targets.Count == 0,
            "Assistant untargeted before any quest play");
        // Resolve the same tuple the native input uses, without starting the asynchronous bot turn.
        var engineOptions = typeof(NativeMatch).GetField("humanOptions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(match);
        var task = (PlayerTask)typeof(NativeMatch).GetMethod("ResolveTask", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(match, new object[] { engineOptions, options.Options.IndexOf(questOption), 0, -1, 0 });
        Check(task.Source.Id == quest.Id && task.Target == null && game.Process(task), "native quest tuple accepted");
        var second = Add(p, "TLC_426"); Publish();
        Check(!options.Options.Any(o => o.MainOption?.Id == second.Id), "second quest not offered");
        Check(options.Options.Single(o => o.MainOption?.Id == assistant.Id).MainOption.Targets.Select(t => t.Id)
            .SequenceEqual(p.Opponent.BoardZone.Select(m => m.Id)), "Assistant targets enemy minions after quest play");
        for (int i = 0; i < 4; i++) Summon(game, p, "EX1_506");
        foreach (var resumed in new[] { game.CloneExact(), EngineGame.LoadSnapshot(game.CloneExact(history: true).SaveSnapshot()) })
        {
            Check(resumed.Player1.SecretZone.ModernQuests.Single().QuestProgress == 4
                && resumed.Player1.Options().Any(t => t.Source?.Id == assistant.Id && t.Target?.Id == p.Opponent.BoardZone[0].Id)
                && !resumed.Player1.Options().Any(t => t.Source?.Id == second.Id), "clone/restore keeps quest option rules");
            Summon(resumed, resumed.Player1, "EX1_506"); Summon(resumed, resumed.Player1, "EX1_506");
            Check(resumed.Player1.SecretZone.ModernQuests.Single().QuestProgress == 0
                && QuestRules.PlayerEffectStacks(resumed.Player1, "TLC_426e") == 1,
                "resumed native quest completes once");
        }
        Check(((SabberStoneCore.Model.Entities.Spell)quest).QuestProgress == 4
            && QuestRules.PlayerEffectStacks(p, "TLC_426e") == 0, "native clone/restore independent");
        Console.WriteLine("PASS quest native options: targetless POWER; second quest rejected; Assistant conditional targets; clone/snapshot");
    }
}
