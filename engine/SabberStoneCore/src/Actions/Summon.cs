#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	public static partial class Generic
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	{
		internal static void ResolveDeferredSummons(Game game)
		{
            if (game[SwapRules.ResolvingSummons] != 0 || game.Resolution.IsResolvingDeaths) return;
            game.NativeTags[SwapRules.ResolvingSummons] = 1;
            try
            {
                while (true)
                {
                    Minion waiting = null;
                    int waitingOrder = 0;
                    FindPendingSummon(game.Player1.HandZone, ref waiting, ref waitingOrder);
                    FindPendingSummon(game.Player1.DeckZone, ref waiting, ref waitingOrder);
                    FindPendingSummon(game.Player2.HandZone, ref waiting, ref waitingOrder);
                    FindPendingSummon(game.Player2.DeckZone, ref waiting, ref waitingOrder);
                    if (waiting != null)
                    {
                        using var mutation = waiting.Controller.HandZone.BeginCardStateMutation();
                        waiting[SwapRules.PendingSummon] = 0;
                        waiting.Zone.Remove(waiting);
                        SummonBlock(game, waiting, -1, waiting);
                    }
                    else
                    {
                        var body = FindRefill(game.Player1.BoardZone, null);
                        body = FindRefill(game.Player2.BoardZone, body);
                        if (body == null) break;
                        ColossalRules.TryRefill(body);
                    }
                    game.DeathProcessingAndAuraUpdate();
                }
            }
            finally { game.NativeTags.Remove(SwapRules.ResolvingSummons); }
		}

        // Lowest PendingSummon wins, the first one met on ties (a stable OrderBy over hand/deck of both players).
        private static void FindPendingSummon(SabberStoneCore.Model.Zones.Zone<IPlayable> zone, ref Minion best, ref int bestOrder)
        {
            for (int i = 0; i < zone.Count; i++)
            {
                if (!(zone[i] is Minion card)) continue;
                int order = card[SwapRules.PendingSummon];
                if (order != 0 && !card.Controller.BoardZone.IsFull && (best == null || order < bestOrder))
                { best = card; bestOrder = order; }
            }
        }

        private static Minion FindRefill(SabberStoneCore.Model.Zones.BoardZone board, Minion best)
        {
            for (int i = 0; i < board.Count; i++)
            {
                var card = board[i];
                if (ColossalRules.CanRefill(card) && (best == null || card.OrderOfPlay < best.OrderOfPlay)) best = card;
            }
            return best;
        }

        public static Action<Game, Minion, int, IEntity> SummonBlock => (game, minion, position, summoner) =>
        {
            var providers = summoner?.Controller == minion.Controller
                ? minion.Controller.BoardZone.Where(m => (m.Card.Id == "CORE_DAL_575" || m.Card.Id == "DAL_575")
                    && DeathContextRules.Active(m)).ToArray() : Array.Empty<Minion>();
            if (providers.Length == 0) { SummonSingle(game, minion, position, summoner); return; }
            // Freeze multiplicity before entry; a summoned Khadgar cannot double itself.
            using var resolution = game.Resolution.Sequence(ResolutionKind.Summon, game, minion);
            using (game.History ? game.PowerHistory.Visual(game, Presentation.VisualPhase.Summon, summoner as IPlayable, minion) : null)
                SummonPhase(game, minion, position);
            DeathContextRules.Summoned(minion, summoner);
            int extras = Math.Min((1 << providers.Length) - 1, minion.Controller.BoardZone.FreeSpace);
            var copies = new List<Minion>(extras);
            for (int i = 0; i < extras && !minion.Controller.BoardZone.IsFull && minion.Zone == minion.Controller.BoardZone; i++)
                copies.Add((Minion)Copy(minion.Controller, providers[0], minion, Zone.PLAY,
                    initialize: RestoreKhadgarReborn, zonePosition: Math.Min(minion.ZonePosition + 1 + i, minion.Controller.BoardZone.SlotCount),
                    preserveBoardMinionState: true, deferSummonAfter: true));
            AfterSummonTrigger(game, minion, summoner);
            foreach (var copy in copies)
                if (copy.Zone == copy.Controller.BoardZone) AfterSummonTrigger(game, copy, providers[0]);
        };

        private static void RestoreKhadgarReborn(IPlayable copy)
        {
            if (copy[GameTag.SILENCED] == 0 && copy[GameTag.HAS_BEEN_REBORN] == 1)
            { copy[GameTag.REBORN] = copy.Card[GameTag.REBORN]; copy[GameTag.HAS_BEEN_REBORN] = 0; }
        }

        internal static void SummonEntry(Game game, Minion minion, int position, IEntity summoner)
        {
            using (game.History ? game.PowerHistory.Visual(game, Presentation.VisualPhase.Summon, summoner as IPlayable, minion) : null)
                SummonPhase(game, minion, position);
            DeathContextRules.Summoned(minion, summoner);
        }

        private static void SummonSingle(Game game, Minion minion, int position, IEntity summoner)
        {
            using var resolution = game.Resolution.Sequence(ResolutionKind.Summon, game, minion);
            using (game.History ? game.PowerHistory.Visual(game, Presentation.VisualPhase.Summon, summoner as IPlayable, minion) : null)
                SummonPhase(game, minion, position);
            DeathContextRules.Summoned(minion, summoner);
            AfterSummonTrigger(game, minion, summoner);
        }

		private static Action<Game, Minion, int> SummonPhase
			=> delegate (Game g, Minion minion, int zonePosition)
			{
				using var resolution = g.Resolution.Enter(ResolutionKind.SummonOn, minion.Id);
				g.Log(LogLevel.INFO, BlockType.PLAY, "SummonPhase", !g.Logging? "":$"Summon Minion {minion} to Board of {minion.Controller.Name}.");
                minion.Controller.BoardZone.Add(minion, zonePosition, applyEntrySetters: true);

				g.AuraUpdate();
				ColossalRules.OnBodyEntered(minion);

				g.SummonedMinions.Add(minion);

				// add summon block show entity 
				if (g.History)
					g.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(minion));
			};

		private static Action<Game, Minion, IEntity> AfterSummonTrigger
			=> delegate (Game g, Minion minion, IEntity summoner)
			{
				using var resolution = g.Resolution.Enter(ResolutionKind.SummonAfter, minion.Id);
				//minion.IsSummoned = true;

				g.TaskQueue.StartEvent();
				EventMetaData temp = g.CurrentEventData;
				if (summoner != null)
					g.CurrentEventData = new EventMetaData(summoner as IPlayable, minion);
				g.TriggerManager.OnAfterSummonTrigger(minion);
				g.ProcessTasks();
				g.CurrentEventData = temp;
				g.TaskQueue.EndEvent();

				if (minion.IsRace(Race.TOTEM))
					minion.Controller.NumTotemSummonedThisGame++;
			};
	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
