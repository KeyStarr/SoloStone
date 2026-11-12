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
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static partial class Generic
#pragma warning disable CS1591
    {
        private static bool AddDrawnHandPhase(Controller controller, IPlayable card)
        {
            if (card == null) return false;
            using (controller.Game.Facts.HandEntry(card, HistoryHandEntryKind.Draw, Zone.DECK))
                return AddHandPhase(controller, card);
        }

        public static IPlayable DrawCard(Controller controller, Card card) => DrawCardBlock(controller, card);
        public static IPlayable Draw(Controller controller, IPlayable cardToDraw = null) => DrawBlock(controller, cardToDraw);

        public static Func<Controller, Card, IPlayable> DrawCardBlock => (controller, card) =>
        {
            var playable = Entity.FromCard(controller, card);
            AddHandPhase(controller, playable);
            return playable;
        };

        public static Func<Controller, IPlayable, IPlayable> DrawBlock => (controller, selected) =>
        {
            if (selected != null && selected.Zone != controller.DeckZone)
                return DrawFrom(controller, selected.Controller, selected);
            if (!PreDrawPhase(controller)) return null;
            return CompleteDraw(controller, controller.DeckZone.Draw(selected));
        };

        /// <summary>Draw from a specified deck, using the recipient's counters, hand and reactions.</summary>
        public static IPlayable DrawFrom(Controller recipient, Controller deckOwner, IPlayable selected = null)
        {
            if (recipient.Game != deckOwner.Game || selected != null && selected.Zone != deckOwner.DeckZone)
                throw new ArgumentException("Draw source must be in the specified game's source deck.");
            if (deckOwner.DeckZone.IsEmpty) { PreDrawPhase(recipient, true); return null; }
            var playable = deckOwner.DeckZone.Draw(selected);
            using var transfer=recipient.Game.Facts.CardTransfer(playable,deckOwner.Id,Zone.DECK);
            if (playable.Controller != recipient)
            {
                playable.Controller = recipient;
                playable[GameTag.CONTROLLER] = recipient.PlayerId;
                if (playable.AppliedEnchantments != null)
                    foreach (var effect in playable.AppliedEnchantments)
                    { effect.Controller = recipient; effect[GameTag.CONTROLLER] = recipient.PlayerId; }
            }
            return CompleteDraw(recipient, playable);
        }

        public static IPlayable Draw(Controller controller, int index) => CompleteDraw(controller, controller.DeckZone.Remove(index));

        private static IPlayable CompleteDraw(Controller controller, IPlayable playable)
        {
            using var cardStateEntry = controller.HandZone.DeferCardStateEntry();
            var game = controller.Game;
            game.Log(LogLevel.INFO, BlockType.ACTION, "DrawPhase", !game.Logging ? "" : $"{controller.Name} draws {playable}");
            controller.NumCardsDrawnThisTurn++;
            controller.LastCardDrawn = playable.Id;

            bool entered = !DrawReplacementRules.RetainOverdraw(controller, playable) && AddDrawnHandPhase(controller, playable);
            game.TaskQueue.StartEvent();
            game.TriggerManager.OnDrawTrigger(playable);
            game.ProcessTasks();
            game.TaskQueue.EndEvent();
            if (!entered) DrawReplacementRules.Overdrawn(playable);

            if (entered && playable.Zone == controller.HandZone && playable is Spell granted
                && granted.AppliedEnchantments?.Any(e => e.Card.Id == "BE_143t2e") == true)
            {
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, granted.Id, "", 0, 0));
                using (var cast = game.Facts.Cast(granted, HistoryCastKind.Drawn))
                {
                    cast.AllowBodyClaim = true;
                    new PlayTask(PlayType.SPELL, randTarget: true).Process(game, controller, granted, null,
                        new TaskStack { Playables = new System.Collections.Generic.List<IPlayable> { granted } });
                    cast.Complete();
                }
                Draw(controller);
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }

            var task = entered && playable.Zone == controller.HandZone ? playable.Power?.TopdeckTask : null;
            if (task != null)
            {
                if (game.History)
                    // Retail: TRIGGER, EffectIndex 0, TriggerKeyword TOPDECK. The keyword is what tells the client (through
                    // NativeMatch's ShowInHistory) that this is a card drawn and cast, so it gets a history tile.
                    game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, playable.Id, "", 0, 0,
                        effectIndex: 0, triggerKeyword: (int)GameTag.TOPDECK));
                // Illusion retains its actual hand residence when its summon cannot fit.
                if (playable.Card.Id != "EDR_260t" || !controller.BoardZone.IsFull)
                    controller.SetasideZone.Add(controller.HandZone.Remove(playable));
                game.Log(LogLevel.INFO, BlockType.TRIGGER, "TOPDECK",
                    !game.Logging ? "" : $"{playable}'s TOPDECK effect is activated.");
                using (var cast = playable is Spell drawn && (drawn.Card[GameTag.CASTSWHENDRAWN] != 0
                    || drawn.Card.RefTags.TryGetValue(GameTag.CASTSWHENDRAWN, out int castWhenDrawn) && castWhenDrawn != 0)
                    ? game.Facts.Cast(drawn, HistoryCastKind.Drawn) : null)
                {
                    if (cast != null) cast.AllowBodyClaim = true;
                    task.Process(game, controller, playable, null);
                    cast?.Complete();
                }
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }

            QuestRules.SettleHandCountChanges(controller);
            return playable;
        }

        private static bool PreDrawPhase(Controller controller, bool emptySource = false)
        {
            if (!emptySource && !controller.DeckZone.IsEmpty) return true;
            int fatigueDamage = controller.Hero.Fatigue == 0 ? 1 : controller.Hero.Fatigue + 1;
            if (controller.Game.History)
                controller.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.FATIGUE, controller.Hero.Id, "", -1, 0));
            // Advance on the draw attempt, including prevented or redirected damage.
            controller.Hero.Fatigue = fatigueDamage;
            using (controller.Game.Facts.Fatigue()) DamageCharFunc(controller.Hero, controller.Hero, fatigueDamage, false);
            if (controller.Game.History) controller.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            return false;
        }
    }
#pragma warning restore CS1591
}
