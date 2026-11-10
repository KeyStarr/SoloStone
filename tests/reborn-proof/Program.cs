using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class Program
{
    private const GameTag Reborn = (GameTag)1085;
    private const GameTag HasBeenReborn = (GameTag)1336;

    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("exact definitions", ExactDefinitions),
            ("fresh entity, buffs and one shot", FreshEntityLosesBuffsAndReturnsOnce),
            ("silence suppresses Reborn", SilenceSuppressesReborn),
            ("Cultist damaged attack", CultistHasFiveAttackWhileDamaged),
            ("Deathrattle fills board before Reborn", DeathrattleFillsBoardBeforeReborn),
            ("simultaneous identity and history", SimultaneousDeathsHaveOrderedHistory),
            ("clone isolation", CloneIsolation),
            ("transform gains printed Reborn", TransformUsesNewPrintedCard),
            ("current controller receives Reborn", CurrentControllerReceivesReborn),
            ("copy keeps consumed Reborn", CopyKeepsConsumedReborn)
        };
        var failures = new List<string>();
        foreach ((string name, Action run) in tests)
        {
            try { run(); }
            catch (Exception error) { failures.Add($"{name}: {error.Message}"); }
        }

        if (failures.Count != 0)
        {
            foreach (string failure in failures)
                Console.Error.WriteLine("FAIL " + failure);
            return 1;
        }

        Console.WriteLine($"PASS reborn proof tests={tests.Length}");
        return 0;
    }

    private static Game Fresh()
    {
        var game = new Game(new GameConfig
        {
            StartPlayer = 1,
            Player1HeroClass = CardClass.HUNTER,
            Player2HeroClass = CardClass.MAGE,
            FillDecks = false,
            Shuffle = false,
            History = true,
            Logging = false,
            SkipMulligan = true,
            RandomSeed = 7
        });
        game.StartGame(stopBeforeShuffling: true);
        return game;
    }

    private static Minion Summon(Controller controller, string cardId) =>
        (Minion)Entity.FromCard(controller, Cards.FromId(cardId), zone: controller.BoardZone);

    private static Minion RebornMinion(Controller controller, string cardId) =>
        controller.BoardZone.Single(minion => minion.Card.Id == cardId && minion[HasBeenReborn] == 1);

    private static void Kill(Game game, params Minion[] minions)
    {
        foreach (Minion minion in minions)
            minion.Damage = minion.BaseHealth;
        game.DeathProcessingAndAuraUpdate();
    }

    private static void ExactDefinitions()
    {
        Card whelp = Cards.FromId("TIME_045");
        Check(whelp.AssetId == 120856 && whelp.Cost == 3 && whelp.ATK == 1 && whelp.Health == 4,
            "TIME_045 identity or stats differ from frozen literals");
        Check(whelp.Name == "Whelp of the Infinite" && whelp.Text == "<b>Poisonous</b>\n<b>Reborn</b>",
            "TIME_045 name/text differ from frozen literals");
        Check((int)whelp.Set == 1957 && whelp.Class == CardClass.NEUTRAL && whelp.Type == CardType.MINION,
            "TIME_045 set/class/type differs from frozen literals");
        Check(whelp[GameTag.CARDRACE] == (int)Race.DRAGON && whelp.Poisonous && whelp[Reborn] == 1 && whelp.Implemented,
            "TIME_045 race/mechanics/script closure missing");

        Card cultist = Cards.FromId("TLC_101");
        Check(cultist.AssetId == 119781 && cultist.Cost == 3 && cultist.ATK == 2 && cultist.Health == 3,
            "TLC_101 identity or stats differ from frozen literals");
        Check(cultist.Name == "Undercover Cultist" &&
              cultist.Text == "<b>Taunt</b>, <b>Reborn</b>\nHas +3 Attack while damaged.",
            "TLC_101 name/text differ from frozen literals");
        Check((int)cultist.Set == 1952 && cultist.Taunt && cultist[GameTag.ENRAGED] == 1 &&
              cultist[Reborn] == 1 && cultist.Implemented, "TLC_101 mechanics/script closure missing");
        Card helper = Cards.FromId("TLC_101e");
        Check(helper.AssetId == 119782 && helper.Name == "Enraged" && helper.Text == "+3 Attack." &&
              helper.Type == CardType.ENCHANTMENT && helper.Implemented,
            "TLC_101e helper closure missing");
    }

    private static void FreshEntityLosesBuffsAndReturnsOnce()
    {
        Game game = Fresh();
        Minion original = Summon(game.Player1, "TIME_045");
        int originalId = original.Id;
        original.AttackDamage = 8;
        original.BaseHealth = 9;

        Kill(game, original);

        Minion reborn = RebornMinion(game.Player1, "TIME_045");
        Check(reborn.Id != originalId && original.Zone == game.Player1.GraveyardZone,
            "Reborn reused original entity or failed to graveyard it");
        Check(reborn.Health == 1 && reborn.BaseHealth == 4 && reborn.Damage == 3,
            "Reborn did not return damaged at one current Health");
        Check(reborn.AttackDamage == 1 && reborn.Poisonous,
            "fresh printed stats/Poisonous were not restored");
        Check(reborn[Reborn] == 0 && reborn[HasBeenReborn] == 1,
            "Reborn was not consumed on fresh entity");

        Kill(game, reborn);
        Check(game.Player1.BoardZone.Count == 0 &&
              game.Player1.GraveyardZone.Count(card => card.Card.Id == "TIME_045") == 2,
            "second death returned another copy");
    }

    private static void SilenceSuppressesReborn()
    {
        Game game = Fresh();
        Minion whelp = Summon(game.Player1, "TIME_045");
        whelp.Silence();
        Check(whelp[Reborn] == 0, "Silence left REBORN active");
        Kill(game, whelp);
        Check(game.Player1.BoardZone.Count == 0, "silenced minion returned");
    }

    private static void CultistHasFiveAttackWhileDamaged()
    {
        Game game = Fresh();
        Minion cultist = Summon(game.Player1, "TLC_101");
        Check(cultist.AttackDamage == 2 && cultist.HasTaunt, "printed Cultist state wrong");
        Kill(game, cultist);
        Minion reborn = RebornMinion(game.Player1, "TLC_101");
        Check(reborn.Health == 1 && reborn.BaseHealth == 3 && reborn.Damage == 2,
            "Cultist did not return as damaged 1/3 body");
        Check(reborn.AttackDamage == 5 && reborn.HasTaunt, "damaged Cultist is not 5 Attack with Taunt");
        reborn.Damage = 0;
        game.AuraUpdate();
        Check(reborn.AttackDamage == 2, "Cultist kept +3 Attack after healing");
    }

    private static void DeathrattleFillsBoardBeforeReborn()
    {
        Game game = Fresh();
        for (int i = 0; i < 6; i++)
            Summon(game.Player1, "CS2_231");
        Minion creeper = Summon(game.Player1, "FP1_002");
        creeper[Reborn] = 1;

        Kill(game, creeper);

        Check(game.Player1.BoardZone.Count == 7, "Deathrattle did not refill board");
        Check(game.Player1.BoardZone.Count(minion => minion.Card.Id == "FP1_002t") == 1,
            "full-board Deathrattle slot result changed");
        Check(game.Player1.BoardZone.All(minion => minion.Card.Id != "FP1_002"),
            "Reborn resolved before Deathrattle/full-board check");
    }

    private static void SimultaneousDeathsHaveOrderedHistory()
    {
        Game game = Fresh();
        Minion whelp = Summon(game.Player1, "TIME_045");
        Minion cultist = Summon(game.Player1, "TLC_101");
        int historyStart = game.PowerHistory.Full.Count;

        Kill(game, cultist, whelp);

        Minion newWhelp = RebornMinion(game.Player1, "TIME_045");
        Minion newCultist = RebornMinion(game.Player1, "TLC_101");
        Check(newWhelp.Id != whelp.Id && newCultist.Id != cultist.Id && newWhelp.Id != newCultist.Id,
            "simultaneous Reborn identities are not fresh and distinct");
        Check(game.Player1.BoardZone.Count == 2 && newWhelp.ZonePosition == 0 && newCultist.ZonePosition == 1,
            $"simultaneous Reborn slot count or placement changed: count={game.Player1.BoardZone.Count} " +
            $"whelp={newWhelp.ZonePosition} cultist={newCultist.ZonePosition}");
        List<IPowerHistoryEntry> history = game.PowerHistory.Full.Skip(historyStart).ToList();
        int oldWhelpGrave = history.FindIndex(entry => entry is PowerHistoryTagChange tag &&
            tag.EntityId == whelp.Id && tag.Tag == GameTag.ZONE && tag.Value == (int)Zone.GRAVEYARD);
        int oldCultistGrave = history.FindIndex(entry => entry is PowerHistoryTagChange tag &&
            tag.EntityId == cultist.Id && tag.Tag == GameTag.ZONE && tag.Value == (int)Zone.GRAVEYARD);
        int newWhelpFull = history.FindIndex(entry => entry is PowerHistoryFullEntity full && full.Entity.Id == newWhelp.Id);
        int newCultistFull = history.FindIndex(entry => entry is PowerHistoryFullEntity full && full.Entity.Id == newCultist.Id);
        int newWhelpDamage = history.FindIndex(entry => entry is PowerHistoryTagChange tag &&
            tag.EntityId == newWhelp.Id && tag.Tag == GameTag.DAMAGE && tag.Value == 3);
        int newWhelpShow = history.FindIndex(entry => entry is PowerHistoryShowEntity show && show.Entity.Id == newWhelp.Id);
        Check(oldWhelpGrave >= 0 && oldCultistGrave >= 0 && oldWhelpGrave < newWhelpFull &&
              oldCultistGrave < newWhelpFull, "new entities appeared before both originals reached graveyard");
        Check(newWhelpFull < newCultistFull, "simultaneous Reborn order ignored original order of play");
        Check(newWhelpFull < newWhelpDamage && newWhelpDamage < newWhelpShow,
            "fresh entity/damage/show history order changed");
    }

    private static void CloneIsolation()
    {
        Game originalGame = Fresh();
        Minion original = Summon(originalGame.Player1, "TIME_045");
        Game clone = originalGame.Clone(history: true);
        Minion clonedOriginal = (Minion)clone.IdEntityDic[original.Id];

        Kill(clone, clonedOriginal);

        Check(original.Zone == originalGame.Player1.BoardZone && original[HasBeenReborn] == 0,
            "clone death mutated original branch");
        Check(RebornMinion(clone.Player1, "TIME_045")[HasBeenReborn] == 1,
            "clone branch did not consume Reborn independently");
        Kill(originalGame, original);
        Check(RebornMinion(originalGame.Player1, "TIME_045")[HasBeenReborn] == 1,
            "original branch lost its independent Reborn");
    }

    private static void TransformUsesNewPrintedCard()
    {
        Game game = Fresh();
        Minion wisp = Summon(game.Player1, "CS2_231");
        game.TaskQueue.Execute(new TransformTask("TIME_045", EntityType.TARGET), game.Player1, game.Player1.Hero, wisp);
        Minion transformed = game.Player1.BoardZone.Single();
        Check(transformed.Card.Id == "TIME_045" && transformed[Reborn] == 1,
            "transform did not adopt Whelp printed Reborn");
        Kill(game, transformed);
        Check(RebornMinion(game.Player1, "TIME_045").Health == 1,
            "transformed Whelp did not Reborn");
    }

    private static void CurrentControllerReceivesReborn()
    {
        Game game = Fresh();
        Minion whelp = Summon(game.Player1, "TIME_045");
        game.TaskQueue.Execute(new ControlTask(EntityType.TARGET), game.Player2, game.Player2.Hero, whelp);
        Check(whelp.Controller == game.Player2 && game.Player2.BoardZone.Contains(whelp), "control setup failed");
        Kill(game, whelp);
        Check(game.Player1.BoardZone.Count == 0 && RebornMinion(game.Player2, "TIME_045").Controller == game.Player2,
            "Reborn returned to original owner instead of current controller");
    }

    private static void CopyKeepsConsumedReborn()
    {
        Game game = Fresh();
        Minion original = Summon(game.Player1, "TIME_045");
        Kill(game, original);
        Minion consumed = RebornMinion(game.Player1, "TIME_045");
        game.TaskQueue.Execute(new SummonCopyTask(EntityType.TARGET), game.Player1, consumed, consumed);
        Minion copy = game.Player1.BoardZone.Single(minion => minion.Id != consumed.Id);
        Check(copy[Reborn] == 0 && copy[HasBeenReborn] == 1, "copy regained consumed Reborn");
        Kill(game, consumed, copy);
        Check(game.Player1.BoardZone.Count == 0, "consumed source or copy returned again");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
