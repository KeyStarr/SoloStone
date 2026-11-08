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
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	// TODO: Should use Generic.Copy()
	/// <summary>
	/// Summon a copy of one (or more) existing entity.
	/// </summary>
	/// <seealso cref="SimpleTask" />
	public class SummonCopyTask : SimpleTask
	{
		private readonly bool _addToStack;

		/// <summary>
		/// If there are multiple entities to summon it will randomly summon them.
		/// </summary>
		private readonly bool _randomFlag;

		private readonly SummonSide _side;

		/// <summary>
		/// Entities to summon.
		/// </summary>
		private readonly EntityType _type;

		/// <summary>
		/// Summons a copy of the chosen entitytype.
		/// </summary>
		/// <param name="type">Selector of entity to copy.</param>
		/// <param name="randomFlag">
		/// <c>true</c> if the copies need to be summoned
		/// in random order, <c>false</c> otherwise.
		/// </param>
		/// <param name="addToStack">Add the summoned entity to the stack.</param>
		/// <param name="side">The side in which the summoned entity should be place.</param>
		public SummonCopyTask(EntityType type, bool randomFlag = false, bool addToStack = false,
			SummonSide side = SummonSide.DEFAULT)
		{
			_type = type;
			_randomFlag = randomFlag;
			_addToStack = addToStack;
			_side = side;
		}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			int alternateCount = 0;

			if (controller.BoardZone.IsFull)
				return TaskState.STOP;

			IList<IPlayable> entities =
				IncludeTask.GetEntities(_type, controller, source, target, stack?.Playables);

            // Summon-copy is a minion operation, including when its selector is TARGET or STACK.
            foreach (IPlayable entity in entities)
                if (entity is BoardObject) return TaskState.STOP;
			if (entities.Count < 1) return TaskState.STOP;

			// shuffle list randomly if needed
			entities = _randomFlag ? entities.Shuffle(game.Random) : entities;

			if (_randomFlag)
				game.OnRandomHappened(true);


			BoardZone board = controller.BoardZone;
			int space = board.FreeSpace;

			space = entities.Count > space ? space : entities.Count;

			if (entities[0].Zone == null || entities[0].Zone.Type != Zone.PLAY)
				for (int i = 0; i < space; i++)
				{
					if (board.IsFull)
						break;

					var minion = (Minion)Entity.FromCard(in controller, entities[i].Card,
						new EntityData
						{
							{GameTag.DISPLAYED_CREATOR, source.Id}
						});

                    PayloadRules.MarkCopy(entities[i], minion);
                    if (entities[i].Zone?.Type == Zone.GRAVEYARD) PayloadRules.CopyState(entities[i],minion,resummon: true);
					Generic.SummonBlock.Invoke(game, minion,
						SummonTask.GetPosition(in source, _side, stack?.Number ?? 0, ref alternateCount),
						source);

					if (_addToStack)
						stack.AddPlayable(minion);
				}
			else
				for (int i = 0; i < entities.Count; i++)
				{
					if (controller.BoardZone.IsFull) break;

					var minion = (Minion)entities[i];

					var tags = new EntityData((EntityData)minion.NativeTags);
					// Clear inherited exhaustion only when board entry leaves the new copy ready.
					if (minion[GameTag.EXHAUSTED] != 0 &&
					    (controller != game.CurrentPlayer || minion.HasCharge || minion.IsRush))
						tags[GameTag.EXHAUSTED] = 0;
                    tags.Remove(OutcastRules.PlayedFromEdgeTag);

					if (minion.Controller != controller)
						tags[GameTag.CONTROLLER] = controller.PlayerId;

					int zonePosition = SummonTask.GetPosition(in source, _side, stack?.Number ?? 0, ref alternateCount);

					var copy = (Minion)Entity.FromCard(in controller, minion.Card, tags);
					minion.CopyInternalAttributes(copy);
                    PayloadRules.MarkCopy(minion, copy);
                    if (DormantRules.HasState(minion))
                    {
                        copy.IsSilenced = minion.IsSilenced;
                        DormantRules.CopyState(minion, copy);
                    }
					// Temporary copied stats need their own expiry, including without history.
					for (int j = game.OneTurnEffects.Count - 1; j >= 0; j--)
					{
						var (id, effect) = game.OneTurnEffects[j];
						if (id == minion.Id)
							game.OneTurnEffects.Add((copy.Id, effect));
					}

					if (minion.AppliedEnchantments != null)
						foreach (Enchantment e in minion.AppliedEnchantments)
						{
                            if (DormantRules.IsHelper(e)) continue;
							Enchantment instance = Enchantment.CopyAttachment(controller, copy, copy, e);
							if (e[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0)
							{
								instance[GameTag.TAG_SCRIPT_DATA_NUM_1] = e[GameTag.TAG_SCRIPT_DATA_NUM_1];
								if (e[GameTag.TAG_SCRIPT_DATA_NUM_2] > 0)
									instance[GameTag.TAG_SCRIPT_DATA_NUM_2] = e[GameTag.TAG_SCRIPT_DATA_NUM_2];
							}
							
							instance.CapturedCard = e.CapturedCard;
                            PayloadRules.CopyState(e, instance);

							// Copied stats are already realized; Soulrest still needs its death/zone triggers.
							if (e.Card.Id == "DINO_417e" || e.Card.Id == "TIME_024e2") instance.Power.Trigger.Activate(instance);

							if (e.IsOneTurnActive)
								instance.Game.OneTurnEffectEnchantments.Add(instance);
						}

                    OldGodRules.CopyState(minion,copy);
					PayloadRules.CopyState(minion, copy);

					// Copied attributes and enchantments must exist before AFTER_SUMMON runs.
					Generic.SummonBlock.Invoke(game, copy, zonePosition, source);
                    OldGodRules.FinishCopy(copy);

					if (!copy.IsDormant && minion.OngoingEffect != null && copy.OngoingEffect == null)
						minion.OngoingEffect.Clone(copy);

					if (_addToStack)
						stack.AddPlayable(copy);
				}


			return TaskState.COMPLETE;
		}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}
}
