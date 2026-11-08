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
using System.Runtime.CompilerServices;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	internal static class KazakusPower
	{
		private static readonly Card[][] PotionCards;

		static KazakusPower()
		{
			PotionCards = new Card[5][];

            for (int i = 0; i < PotionCards.Length; i++)
                PotionCards[i] = LegacyGenerationV1.Get("Potion/" + i).ToArray();
		}

		internal static void CreateCostChoices(Controller c, IEntity source)
		{
			Generic.CreateChoiceCards(c, source, null, ChoiceType.GENERAL, ChoiceAction.KAZAKUS, PotionCards[0], null);
		}

		internal static void Action(in Game g, in Controller c, in List<int> stack)
		{
			switch (stack.Count)
			{
				case 1:
					CreateSecondChoices(in c, g.IdEntityDic[stack[0]].Card.Cost);
					break;
				case 2:
					CreateThirdChoices(in c, g.IdEntityDic[stack[1]].Card);
					break;
				case 3:
					GeneratePotion(in g, in c, in stack);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static void CreateSecondChoices(in Controller c, int cost)
		{
			Card[] cards = GetCostCards(cost);

			cards = cards.ChooseNElements(3, c.Game.Random);

			CreateChoices(in c, in cards);
		}

		private static void CreateThirdChoices(in Controller c, in Card secondCard)
		{
			Card[] cards = GetCostCards(secondCard.Cost);

			Card[] results = new Card[3];
			#region Choose 3 random cards from not previously chosen ones
			Util.DeepCloneableRandom rnd = c.Game.Random;
			Span<int> indices = stackalloc int[3];
			for (int i = 0; i < 3; i++)
			{
				int j;
				bool flag;
				do
				{
					j = rnd.Next(cards.Length);
					flag = false;

					if (cards[j] == secondCard)
					{
						flag = true;
						continue;
					}

					for (int k = 0; k < i; k++)
						if (indices[k] == j)
						{
							flag = true;
							break;
						}

				} while (flag);

				results[i] = cards[j];
				indices[i] = j;
			}
			#endregion

			CreateChoices(in c, in results);
		}

		private static void GeneratePotion(in Game g, in Controller c, in List<int> ids)
		{
			EntityList dict = g.IdEntityDic;
			Card baseCard = dict[ids[0]].Card;
			Card spell1 = dict[ids[1]].Card;
			Card spell2 = dict[ids[2]].Card;

			Card kazakusPotion;
			if (spell1[GameTag.TAG_SCRIPT_DATA_NUM_1] > spell2[GameTag.TAG_SCRIPT_DATA_NUM_1])
				kazakusPotion = Card.CreateKazakusPotion(in baseCard, in spell2, in spell1, g.History);
			else
				kazakusPotion = Card.CreateKazakusPotion(in baseCard, in spell1, in spell2, g.History);

			Entity.FromCard(in c, in kazakusPotion,
				zone: c.HandZone,
				creator: dict[c.Choice.SourceId]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Card[] GetCostCards(int cost)
		{
			switch (cost)
			{
				case 1:
					return PotionCards[1];
				case 5:
					return PotionCards[2];
				case 10:
					return PotionCards[3];
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static void CreateChoices(in Controller c, in Card[] cards)
		{
			int sourceId = c.Choice.SourceId;
			List<int> choices = new List<int>(cards.Length);

			// TODO: Pre-made potion entities for non-historic games
			for (int i = 0; i < cards.Length; i++)
			{
				IPlayable choiceEntity = Entity.FromCard(in c, in cards[i],
					new EntityData
					{
						{GameTag.CREATOR, sourceId},
						{GameTag.DISPLAYED_CREATOR, sourceId}
					}, c.SetasideZone);
				choices.Add(choiceEntity.Id);
			}

			c.Choice.AppendNext(choices);
		}
	}

	public class PotionGenerating : SimpleTask
	{
		#region Overrides of SimpleTask

		public override TaskState Process(in Game game, in Controller controller, in IEntity source, in IPlayable target,
			in TaskStack stack = null)
		{
			KazakusPower.CreateCostChoices(controller, source);
			return TaskState.COMPLETE;
		}

		#endregion
	}
}
