using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

public sealed class ProofBot
{
    private int _lastTradeTurn = -1;
    private int _lastTradePlayer = -1;

    public PlayerTask Choose(List<PlayerTask> tasks)
    {
        if (tasks == null || tasks.Count == 0)
            throw new ArgumentException("A legal task list is required.", nameof(tasks));

        PlayerTask first = tasks[0];
        int turn = first.Game.Turn;
        int player = first.Controller.PlayerId;
        PlayerTask trade = tasks.Where(task => task is TradeCardTask)
            .OrderBy(task => task.Source.Card.Id, StringComparer.Ordinal)
            .ThenBy(task => task.Source.Id)
            .FirstOrDefault();
        if (trade != null && (_lastTradeTurn != turn || _lastTradePlayer != player))
        {
            _lastTradeTurn = turn;
            _lastTradePlayer = player;
            return trade;
        }

        return tasks
            .Where(task => task.PlayerTaskType != PlayerTaskType.CONCEDE &&
                           task.PlayerTaskType != PlayerTaskType.TRADE_CARD)
            .OrderBy(Priority)
            .ThenBy(task => task.Source?.Card.Id ?? "", StringComparer.Ordinal)
            .ThenBy(task => task.Source?.Id ?? 0)
            .ThenBy(task => task.Target?.Id ?? 0)
            .ThenBy(task => task.ChooseOne)
            .First();
    }

    private static int Priority(PlayerTask task)
    {
        bool enemyHero = task.Target is Hero && task.Target.Controller != task.Controller;
        return task.PlayerTaskType switch
        {
            PlayerTaskType.PLAY_CARD when enemyHero => 0,
            PlayerTaskType.PLAY_CARD => 1,
            PlayerTaskType.MINION_ATTACK when enemyHero => 2,
            PlayerTaskType.HERO_ATTACK when enemyHero => 3,
            PlayerTaskType.MINION_ATTACK => 4,
            PlayerTaskType.HERO_ATTACK => 5,
            PlayerTaskType.HERO_POWER when enemyHero => 6,
            PlayerTaskType.HERO_POWER => 7,
            PlayerTaskType.END_TURN => 9,
            _ => 8
        };
    }
}
