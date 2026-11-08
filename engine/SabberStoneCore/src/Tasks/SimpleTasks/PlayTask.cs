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
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public enum PlayType
	{
		SPELL,
        CARD
	}

	/// <summary>
	///     Allows to have a playable played out in a task
	/// </summary>
	public class PlayTask : SimpleTask
	{
		private readonly PlayType _playType;
		private readonly bool _randTarget;
        private readonly bool _preferSource;
		private readonly EntityType _targetType;

		/// <summary>
		/// Create a PlayTask to play a card as a task.
		/// </summary>
		/// <param name="playType">The type of playable.</param>
		/// <param name="randTarget">true if the target of the playable is chosen randomly</param>
		public PlayTask(PlayType playType, bool randTarget = false, bool preferSource = false)
		{
			_playType = playType;
			_randTarget = randTarget;
            _preferSource = preferSource;
		}

		public PlayTask(PlayType playType, EntityType targetType)
		{
			_playType = playType;
			_targetType = targetType;
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			switch (_playType)
			{
				case PlayType.CARD:
                    foreach (var card in stack?.Playables?.ToArray() ?? Array.Empty<IPlayable>())
                        AutoPlayRules.Play(card, source);
                    return TaskState.COMPLETE;
                case PlayType.SPELL:
					foreach (IPlayable p in stack?.Playables)
					{
						if (!(p is Spell spell)) throw new Exception();
						Controller c = spell.Controller;
						if (!HeraldRules.CanAutoCast(spell)) continue;
						if (!ImbueRules.CanAutoCast(spell)) continue;

                        int mode = game.SpellCompletion?.SourceId == spell.Id ? game.SpellCompletion.ChooseOne : 0;
                        if (_randTarget && spell.ChooseOne && !Generic.UsesChooseBoth(spell, c))
                            mode = game.Random.Next(1, spell.ChooseOnePlayables.Length + 1);
                        ICharacter cardTarget = null;
                        int noteLegal = -1;
                        if (_randTarget)
                        {
                            game.OnRandomHappened(true);
                            var profile = Generic.SpellTargetingProfile(spell, mode);
                            var legal = profile.ValidPlayTargets.Where(profile.IsValidPlayTarget).ToArray();
                            noteLegal = legal.Length;
                            if (_preferSource && source is ICharacter preferred && legal.Any(t => t.Id == preferred.Id))
                            { cardTarget = preferred; noteLegal = 1; }
                            else if (legal.Length > 0)
                                cardTarget = legal[game.Random.Next(legal.Length)];
                            if (cardTarget != null) spell.CardTarget = cardTarget.Id;
                        }
						else if
							(_targetType != EntityType.INVALID)
						{
							IList<IPlayable> targets = IncludeTask.GetEntities(_targetType, in controller, source, target,
								stack?.Playables);

							if (targets.Count == 0)
								return TaskState.STOP;

							cardTarget = (ICharacter)targets[0];
						}

						if (!HeraldRules.CanCast(c, spell, cardTarget)) continue;
						if (!ImbueRules.CanCast(c, spell, cardTarget)) continue;
                        // History diagnostics only (match event log): what this automatic cast chose from; reads, draws nothing.
                        if (game.History)
                            game.PowerHistory.NoteAutoChoice(spell.Id, _randTarget ? noteLegal : -2, mode,
                                mode > 0 && spell.ChooseOnePlayables != null && mode <= spell.ChooseOnePlayables.Length ? spell.ChooseOnePlayables[mode - 1].Card.Id : null,
                                _randTarget && spell.ChooseOne && !Generic.UsesChooseBoth(spell, c));
                        // An automatic cast owns only its new choices, never its caller's pending prompt.
                        var pendingChoice = c.Choice;
                        c.Choice = null;
                        try
                        {
                            using var repeat = source.Card.Id == "BOT_411e" ? game.Facts.Cast(spell, HistoryCastKind.Repeat) : null;
                            if (repeat != null) repeat.AllowBodyClaim = true;
                            Generic.CastAutomaticSpell(c, game, spell, cardTarget, mode, lockedTarget: _targetType != EntityType.INVALID);
                            repeat?.Complete();

							while (c.Choice != null)
								Generic.ChoicePick(c, game, c.Choice.Choices.Choose(game.Random));
                        }
                        finally { c.Choice = pendingChoice; if (game.History) game.PowerHistory.ClearAutoChoice(); }
					}

					return TaskState.COMPLETE;

				default:
					throw new NotImplementedException();
			}
		}
	}
}
