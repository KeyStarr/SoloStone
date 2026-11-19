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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.RandomPools;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public enum DiscoverType
	{
		INVALID,
		BASIC_HEROPOWERS,
		DRAGON,
		OP_DECK,
		BASIC_TOTEM,
		MINION,
		DECK_MINION,
		SPELL_RANDOM,
		SPELL,
		DEATHRATTLE,
		ONE_COST,
		THREE_COST,
		SIX_COST_SUMMON,
		BEAST,
		MECHANICAL,
		ARTIFACT,
		DRUID_ROGUE_SHAMAN,
		MAGE_PRIEST_WARLOCK,
		OVERLOAD,
		TAUNT,
		HUNTER_PALADIN_WARRIOR,
		MURLOC,
		SECRET,
		ELEMENTAL,
		ALL,
		ELEMENTAL_INVOCATION,
		OWN_SPELL,
		COST_8_MORE_SUMMON,
		OP_HERO,
		DIED_THIS_GAME,
		BRANCHING_PATHS,
		LEGENDARY_MINIONS,
		BATTLECRY,
		DEMON,
		OP_DECK_MINION,
		DEATHRATTLE_MINIONS,
		SPELL_COSTS_5_OR_MORE,
		WEAPON_ANOTHERCLASS,
		_d_WEAPON_ANOTHERCLASS,
		_h_WEAPON_ANOTHERCLASS,
		_m_WEAPON_ANOTHERCLASS,
		_pa_WEAPON_ANOTHERCLASS,
		_pr_WEAPON_ANOTHERCLASS,
		_r_WEAPON_ANOTHERCLASS,
		_s_WEAPON_ANOTHERCLASS,
		_wl_WEAPON_ANOTHERCLASS,
		_wr_WEAPON_ANOTHERCLASS,
		HEISTBARON_TOGWAGGLE,
	}

	public class DiscoverTask : SimpleTask
	{
		// Criteria pools depend only on (match pool, criteria, class). They are memoised on the MatchRandomPool
		// instance, which is per manifest and pool policy, so no game's pool can leak into another's. Typed pools
		// also read the controller's class, hero, opponent and RNG, so they are rebuilt per call.

		private readonly DiscoverType _discoverType;
		//private readonly Card _enchantmentCard;
		private readonly int _numberOfChoices = 3;
		private readonly ISimpleTask _taskTodo;
		private readonly Predicate<Card[]> _keepAllCondition;

		private readonly DiscoverCriteria _discoverCriteria;
		private readonly ChoiceAction _choiceAction;
		private readonly int _repeat;

		// These recipes reuse the picker for fixed choices without semantic Discover.
		private bool IsDiscover => _discoverType != DiscoverType.BRANCHING_PATHS
			&& _discoverType != DiscoverType.BASIC_TOTEM
			&& _discoverType != DiscoverType.ELEMENTAL_INVOCATION
			&& _discoverType != DiscoverType.HEISTBARON_TOGWAGGLE;

		private ref readonly DiscoverCriteria Criteria => ref _discoverCriteria;

		public DiscoverTask(CardType cardType = CardType.INVALID, CardClass cardClass = CardClass.INVALID,
			(GameTag tag, RelaSign relaSign, int value) tagValueCriteria = default, ChoiceAction choiceAction = ChoiceAction.HAND,
			ISimpleTask afterDiscoverTask = null, int repeat = 1, Predicate<Card[]> keepAllCondition = null)
		{
			_discoverCriteria =
				new DiscoverCriteria(cardType, cardClass, tagValueCriteria);
			_choiceAction = choiceAction;
			_repeat = repeat;
			_taskTodo = afterDiscoverTask;
			_keepAllCondition = keepAllCondition;
		}

		public DiscoverTask(DiscoverType discoverType, int numberOfChoices = 3)
		{
			_discoverType = discoverType;
			_numberOfChoices = numberOfChoices;
			//if (enchantmentId != null)
			//	_enchantmentCard = Cards.FromId(enchantmentId);
		}

		public DiscoverTask(DiscoverType discoverType, ISimpleTask afterDiscoverTask)
		{
			_discoverType = discoverType;
			_taskTodo = afterDiscoverTask;
		}

		/// <summary>
		/// Keep all cards if the given condition is satisfied.
		/// </summary>
		public DiscoverTask(DiscoverType discoverType, Predicate<Card[]> keepAllCondition)
		{
			_discoverType = discoverType;
			_keepAllCondition = keepAllCondition;
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			Card[][] cardsToDiscover;
			ChoiceAction choiceAction;
			var pool = Cards.RandomPool(game, PoolDestination.Hand, source);

			// Sets of cards to discover.
			if (_discoverType != DiscoverType.INVALID)
				cardsToDiscover = Discover(pool, controller, source, in _discoverType, out choiceAction);
			else
			{
				CardClass cls = Criteria.CardClass == CardClass.OP_CLASS
					? controller.Opponent.HeroClass
					: controller.HeroClass;

				if (cls == CardClass.NEUTRAL)
				{
					if (source.Card.Class != CardClass.NEUTRAL)
						cls = source.Card.Class;
					else
						cls = (CardClass) game.Random.Next(2, 11);
				}

				cardsToDiscover = Discover(pool, in _discoverCriteria, cls);
				choiceAction = _choiceAction;
			}

			// Random Discover cannot offer its source; copies from existing zones and fixed choices are exempt.
			if (!pool.Legacy && IsDiscover && _discoverType != DiscoverType.BASIC_HEROPOWERS
				&& _discoverType != DiscoverType.ARTIFACT && _discoverType != DiscoverType.DECK_MINION
				&& _discoverType != DiscoverType.OP_DECK_MINION && _discoverType != DiscoverType.OP_DECK
				&& _discoverType != DiscoverType.OWN_SPELL && _discoverType != DiscoverType.DIED_THIS_GAME)
			{
				var discoverSource = source;
				// Keep the memo's sets intact for other sources. WithoutSource only copies a set containing the source.
				cardsToDiscover = cardsToDiscover.Select(set => (Card[])pool.WithoutSource(set, discoverSource)).ToArray();
				// Source exclusion may leave a tri-class set empty or fewer than three distinct choices.
				if (cardsToDiscover.Length == 3 && (cardsToDiscover.Any(set => set.Length == 0)
					|| cardsToDiscover.SelectMany(set => set).Distinct().Count() < 3))
					cardsToDiscover = new[] { cardsToDiscover.SelectMany(set => set).Distinct().ToArray() };
			}

			if (cardsToDiscover.Length == 1)
				cardsToDiscover[0] = cardsToDiscover[0].Distinct().ToArray();

			// Gets cards to choose from the sets. 
			Card[] result = GetChoices(cardsToDiscover, _numberOfChoices, game.Random);

			// TODO work on it ...
			//if (game.Splitting)
			//{
			//    ProcessSplit(cardsToDiscover, choiceAction);
			//}


			if (result.Length == 0)
			{
				game.Log(LogLevel.INFO, BlockType.PLAY, "DiscoverTask",
					!game.Logging ? "" : $"Found no potential cards to use for {_discoverType}");

				return TaskState.STOP;
			}

			if (_keepAllCondition?.Invoke(result) ?? false)
			{
				if (IsDiscover) DiscoverRealRules.CompletedAll(controller, source, result);
                int i = 0;
				for (; i < result.Length && !controller.HandZone.IsFull; i++)
				{
					IPlayable entity = Entity.FromCard(in controller, result[i]);
					if (_discoverType == DiscoverType.OP_DECK || _discoverType == DiscoverType.OP_DECK_MINION)
						PayloadRules.MarkCopy(controller.Opponent, entity);
					entity[GameTag.DISPLAYED_CREATOR] = source.Id;
                    if (IsDiscover) DiscoverRealRules.Completed(controller, entity);
					Generic.AddHandPhase.Invoke(controller, entity);
				}
                // Keep-all selections still count when delivery runs out of hand space.
                if (IsDiscover) for (; i < result.Length; i++) AutoPlayRules.AdvanceDiscoverQuest(controller);

				return TaskState.COMPLETE;
			}

			Generic.CreateChoiceCards.Invoke(controller, source, null, ChoiceType.GENERAL, choiceAction,
					result, _taskTodo);

			var semanticChoice = controller.Choice;
			while (semanticChoice.NextChoice != null) semanticChoice = semanticChoice.NextChoice;
			if (_discoverType == DiscoverType.OP_DECK || _discoverType == DiscoverType.OP_DECK_MINION)
				foreach (int offer in semanticChoice.Choices) PayloadRules.MarkCopy(controller.Opponent, game.IdEntityDic[offer]);
			if (IsDiscover) game.Facts.MarkDiscover(semanticChoice);
			semanticChoice.HistoryOptionResolution = _discoverType == DiscoverType.BRANCHING_PATHS
				|| _discoverType == DiscoverType.ELEMENTAL_INVOCATION;

			if (_repeat > 1)
			{
				Choice current = controller.Choice;
				current.AfterChooseTask = null;
				for (int i = _repeat - 2; i >= 0; i--)
				{
					Choice choice = new Choice(controller, cardsToDiscover)
					{
						ChoiceAction = choiceAction,
						ChoiceType = ChoiceType.GENERAL,
						SourceId = source.Id,
					};
					if (IsDiscover) game.Facts.MarkDiscover(choice);
					choice.HistoryOptionResolution = semanticChoice.HistoryOptionResolution;
					current.NextChoice = choice;
					current = choice;
				}

				current.AfterChooseTask = _taskTodo;
			}

			return TaskState.COMPLETE;
		}

		//private void ProcessSplit(Game game, Controller controller, IEntity source, Card[][] cardsToDiscover,
		//	ChoiceAction choiceAction)
		//{
		//	int neutralCnt = cardsToDiscover[0].Length;
		//	int classCnt = 0;
		//	var uniqueList = new List<Card>(cardsToDiscover[0]);

		//	if (cardsToDiscover.Length > 1)
		//	{
		//		classCnt = cardsToDiscover[1].Length;
		//		uniqueList.AddRange(cardsToDiscover[1]);
		//	}

		//	List<IEnumerable<Card>> combinations = Util.GetDiscoverSets(uniqueList).ToList();

		//	game.Log(LogLevel.INFO, BlockType.PLAY, "DiscoverTask",
		//		!game.Logging
		//			? ""
		//			: $"... found {combinations.Count} discovery splits [class: {classCnt}, neutral: {neutralCnt}]");
		//	combinations.ForEach(p =>
		//	{
		//		Game cloneGame = game.Clone();
		//		Controller cloneController = cloneGame.ControllerById(controller.Id);
		//		bool success = Generic.CreateChoiceCards.Invoke(cloneController, source, null, ChoiceType.GENERAL,
		//			choiceAction, p.ToArray(), null, _taskTodo);
		//		cloneGame.TaskQueue.CurrentTask.State = TaskState.COMPLETE;
		//	});
		//}

		private Card[][] Discover(MatchRandomPool pool, Controller controller, IEntity source, in DiscoverType discoverType,
			out ChoiceAction choiceAction)
		{
			switch (discoverType)
			{
				case DiscoverType.DRUID_ROGUE_SHAMAN:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						GetTriClass(pool, CardClass.DRUID, CardClass.ROGUE, CardClass.SHAMAN);
					return cardSets;
				}
				case DiscoverType.MAGE_PRIEST_WARLOCK:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						GetTriClass(pool, CardClass.MAGE, CardClass.PRIEST, CardClass.WARLOCK);
					return cardSets;
				}
				case DiscoverType.HUNTER_PALADIN_WARRIOR:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetTriClass(pool, CardClass.HUNTER, CardClass.PALADIN,
						CardClass.WARRIOR);
					return cardSets;
				}
				case DiscoverType.BASIC_HEROPOWERS:
				{
					choiceAction = ChoiceAction.HEROPOWER;
					Card[][] cardSets =
					{
						Cards.HeroCards().Where(p => !p.ExcludeFromLegacyRandomPools && p != controller.Hero.Card)
							.Select(p => Cards.FromAssetId(p[GameTag.HERO_POWER])).ToArray()
					};
					return cardSets;
				}
				case DiscoverType.DRAGON:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.IsRace(Race.DRAGON)));
					return cardSets;
				}
				case DiscoverType.OVERLOAD:
				{
					choiceAction = ChoiceAction.HAND;
					IEnumerable<Card> cardSet = pool.All;
					Card[][] cardSets = {cardSet.Where(p => p.HasOverload).ToArray()};
					return cardSets;
				}
				case DiscoverType.TAUNT:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p[GameTag.TAUNT] == 1));
					return cardSets;
				}
				case DiscoverType.SECRET:
				{
					choiceAction = ChoiceAction.HAND;
					CardClass classForSecret =
						controller.BaseClass == CardClass.PALADIN
						|| controller.BaseClass == CardClass.MAGE
						|| controller.BaseClass == CardClass.HUNTER
							? controller.BaseClass
							: CardClass.PALADIN;
					Card[][] cardSets = GetClassCard(pool, classForSecret,
						list => list.Where(p => p[GameTag.SECRET] == 1));
					return cardSets;
				}
				case DiscoverType.BEAST:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.IsRace(Race.BEAST)));
					return cardSets;
				}
				case DiscoverType.MURLOC:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.IsRace(Race.MURLOC)));
					return cardSets;
				}
				case DiscoverType.ELEMENTAL:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.IsRace(Race.ELEMENTAL)));
					return cardSets;
				}
				case DiscoverType.MECHANICAL:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.IsRace(Race.MECHANICAL)));
					return cardSets;
				}
				case DiscoverType.DEMON:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.IsRace(Race.DEMON)));
					return cardSets;
				}
				case DiscoverType.ALL:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						GetFilter(pool, in controller, list => list.Where(p => p.Cost >= 0));
					return cardSets;
				}
				case DiscoverType.ARTIFACT:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
					{
                            LegacyGenerationV1.Get("Discover/ARTIFACT").ToArray()
					};
					return cardSets;
				}
				case DiscoverType.ELEMENTAL_INVOCATION:
				{
					choiceAction = ChoiceAction.CAST;
					Card[][] cardSets =
					{
                            LegacyGenerationV1.Get("Discover/ELEMENTAL_INVOCATION").ToArray()
					};
					return cardSets;
				}
				case DiscoverType.MINION:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.Type == CardType.MINION));
					return cardSets;
				}
				case DiscoverType.DECK_MINION:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						{controller.DeckZone.Where(p => p is Minion).Select(p => p.Card).ToArray()};
					return cardSets;
				}
				case DiscoverType.OP_DECK_MINION:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						{controller.Opponent.DeckZone.Where(p => p is Minion).Select(p => p.Card).ToArray()};
					return cardSets;
				}
				case DiscoverType.DEATHRATTLE:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p[GameTag.DEATHRATTLE] == 1));
					return cardSets;
				}
				case DiscoverType.DEATHRATTLE_MINIONS:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p[GameTag.DEATHRATTLE] == 1 && p.Type == CardType.MINION));
					return cardSets;
				}
				case DiscoverType.BATTLECRY:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p[GameTag.BATTLECRY] == 1 && p.Type == CardType.MINION));
					return cardSets;
				}
				case DiscoverType.ONE_COST:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						GetFilter(pool, in controller, list => list.Where(p => p.Cost == 1));
					return cardSets;
				}
				case DiscoverType.THREE_COST:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						GetFilter(pool, in controller, list => list.Where(p => p.Cost == 3));
					return cardSets;
				}
				case DiscoverType.SIX_COST_SUMMON:
				{
					choiceAction = ChoiceAction.SUMMON;
					Card[][] cardSets =
						GetFilter(pool, in controller, list => list.Where(p => p.Cost == 6 && p.Type == CardType.MINION));
					return cardSets;
				}
				case DiscoverType.SPELL:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.Type == CardType.SPELL));
					return cardSets;
				}
				case DiscoverType.SPELL_COSTS_5_OR_MORE:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.Type == CardType.SPELL && p.Cost >= 5));
					return cardSets;
				}
				case DiscoverType.SPELL_RANDOM:
				{
					choiceAction = ChoiceAction.SPELL_RANDOM;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.Type == CardType.SPELL && !p.HideStat));
					return cardSets;
				}
				case DiscoverType.OWN_SPELL:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
						{controller.DeckZone.Where(p => p is Spell).Select(p => p.Card).ToArray()};
					return cardSets;
				}
				case DiscoverType.BASIC_TOTEM:
				{
					choiceAction = ChoiceAction.SUMMON;
					Card[][] cardSets =
					{
                            LegacyGenerationV1.Get("Discover/BASIC_TOTEM").ToArray()
					};
					return cardSets;
				}
				case DiscoverType.COST_8_MORE_SUMMON:
				{
					choiceAction = ChoiceAction.SUMMON;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.Cost >= 8 && p.Type == CardType.MINION));
					return cardSets;
				}
				case DiscoverType.OP_DECK:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = { controller.Opponent.DeckZone.Select(p => p.Card).ToArray()};
					return cardSets;
				}
				case DiscoverType.OP_HERO:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets = GetClassCard(pool, controller.Opponent.HeroClass, list => list);
					return cardSets;
				}
				case DiscoverType.DIED_THIS_GAME:
				{
					choiceAction = ChoiceAction.SUMMON;
					Card[][] cardSets =
					{
						controller.GraveyardZone.Where(p => p.ToBeDestroyed && p.Card.Type == CardType.MINION)
							.Select(p => p.Card).ToArray()
					};
					return cardSets;
				}
				case DiscoverType.BRANCHING_PATHS:
				{
					choiceAction = ChoiceAction.CAST;
					Card[][] cardSets =
					{
                            LegacyGenerationV1.Get("Discover/BRANCHING_PATHS").ToArray()
					};
					return cardSets;
				}
				case DiscoverType.LEGENDARY_MINIONS:
				{
					choiceAction = ChoiceAction.SUMMON;
					Card[][] cardSets = GetFilter(pool, in controller,
						list => list.Where(p => p.Rarity == Rarity.LEGENDARY && p.Type == CardType.MINION));
					return cardSets;
				}
				case DiscoverType.WEAPON_ANOTHERCLASS:
				{
					choiceAction = ChoiceAction.HAND;
					return GetAnotherClassWeapons(controller.HeroClass, pool);
				}
				case DiscoverType.HEISTBARON_TOGWAGGLE:
				{
					choiceAction = ChoiceAction.HAND;
					Card[][] cardSets =
					{
                            LegacyGenerationV1.Get("Discover/HEISTBARON_TOGWAGGLE").ToArray()
					};
					return cardSets;
				}
				default:
					throw new ArgumentOutOfRangeException(nameof(discoverType), discoverType, null);
			}
		}

		private Card[][] GetClassCard(MatchRandomPool pool, CardClass heroClass,
			in Func<IEnumerable<Card>, IEnumerable<Card>> filter)
		{
			IEnumerable<Card> classCards = filter.Invoke(ClassPart(pool, heroClass));
			return new[] {classCards.ToArray()};
		}

		// The class side of a class pool. Legacy replays keep the frozen predicate verbatim.
		private static IEnumerable<Card> ClassPart(MatchRandomPool pool, CardClass heroClass) => pool.Legacy
			? Cards.FormatTypeClassCards(pool.LegacyFormat)[heroClass].Where(p => (p.Class == heroClass || heroClass == CardClass.DEATHKNIGHT && p[GameTag.MULTIPLE_CLASSES] != 0) && !p.IsQuest)
			: pool.ClassOnly(heroClass).Where(p => !p.IsQuest);

		private static IEnumerable<Card> TriClassPart(MatchRandomPool pool, CardClass cls) => pool.Legacy
			? Cards.FormatTypeClassCards(pool.LegacyFormat)[cls].Where(p => (p.Class == cls || p.MultiClassGroup != 0) && !p.IsQuest)
			: pool.ClassOnly(cls).Where(p => !p.IsQuest);

		private Card[][] GetTriClass(MatchRandomPool pool, CardClass class1, CardClass class2, CardClass class3)
		{
			return new[]
			{
				TriClassPart(pool, class1).ToArray(),
				TriClassPart(pool, class2).ToArray(),
				TriClassPart(pool, class3).ToArray()
			};
		}

		private Card[][] GetFilter(MatchRandomPool pool, in Controller controller,
			in Func<IEnumerable<Card>, IEnumerable<Card>> filter)
		{
			CardClass heroClass = controller.BaseClass != CardClass.NEUTRAL
				? controller.BaseClass
				: Util.Choose(Cards.HeroClasses, controller.Game.Random);
			IEnumerable<Card> nonClassSide = pool.Legacy
				? Cards.FormatTypeClassCards(pool.LegacyFormat)[heroClass].Where(p => p.Class != heroClass && (heroClass != CardClass.DEATHKNIGHT || p[GameTag.MULTIPLE_CLASSES] == 0))
				: pool.NonClass(heroClass);
			IEnumerable<Card> nonClassCards = filter.Invoke(nonClassSide);
			IEnumerable<Card> classCards = filter.Invoke(ClassPart(pool, heroClass));
			return new[] {nonClassCards.ToArray(), classCards.ToArray()};
		}

		private Card[][] GetAnotherClassWeapons(CardClass myClass, MatchRandomPool pool)
		{
			if (myClass == CardClass.DEMONHUNTER || myClass == CardClass.DEATHKNIGHT)
				return new[] { pool.All.Where(p => p.Collectible && p.Implemented &&
					p.Type == CardType.WEAPON && Cards.HeroClasses.Contains(p.Class)).ToArray() };

			switch (myClass)
			{
				case CardClass.DRUID:
				case CardClass.HUNTER:
				case CardClass.MAGE:
				case CardClass.PALADIN:
				case CardClass.PRIEST:
				case CardClass.ROGUE:
				case CardClass.SHAMAN:
				case CardClass.WARLOCK:
				case CardClass.WARRIOR:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(myClass), myClass, null);
			}

			return new[] {
				pool.All
					.Where(p => p.Type == CardType.WEAPON && (pool.Legacy ? p.Class != myClass : !pool.InClass(p, myClass)))
					.ToArray()
			};
		}

		private Card[][] Discover(MatchRandomPool pool, in DiscoverCriteria criteria, CardClass cls)
		{
			Card[][] cards;
			if (criteria.CardClass != CardClass.INVALID && criteria.CardClass != CardClass.ANOTHER_CLASS && criteria.CardClass != CardClass.OP_CLASS)
				cls = CardClass.INVALID;

			if (pool.DiscoverMemo.TryGetValue((criteria, cls), out cards))
				return cards;

			if (criteria.CardClass == CardClass.INVALID || criteria.CardClass == CardClass.OP_CLASS)
			{ // Use the player's Class
				IReadOnlyList<Card> allCards = pool.ClassAndNeutral(cls);
				List<Card> classCards = new List<Card>();
				List<Card> neutralCards = new List<Card>();
				foreach (Card card in allCards)
				{
					if (!criteria.Evaluate(card)) continue;

					if (pool.Legacy ? card.Class == cls || cls == CardClass.DEATHKNIGHT && card[GameTag.MULTIPLE_CLASSES] != 0 : pool.InClass(card, cls))
						classCards.Add(card);
					else
						neutralCards.Add(card);
				}

				cards = new[]
				{
					neutralCards.ToArray(),
					classCards.ToArray()
				};
			}
			else if
				(criteria.CardClass == CardClass.ANOTHER_CLASS)
			{
				IEnumerable<Card> allCards = pool.All;
				List<Card> matching = new List<Card>();
				foreach (Card card in allCards)
				{
					if (!criteria.Evaluate(card)) continue;

					if ((pool.Legacy ? card.Class != cls : !pool.InClass(card, cls)) && ((cls != CardClass.DEMONHUNTER && cls != CardClass.DEATHKNIGHT) ||
						card.Implemented && Cards.HeroClasses.Contains(card.Class)))
						matching.Add(card);
				}

				cards = new[] {matching.ToArray(), new Card[0]};
			}
			else
			{	// Use the given class criterion
				IReadOnlyList<Card> allCards = pool.Legacy ? Cards.FormatTypeClassCards(pool.LegacyFormat)[criteria.CardClass] : pool.ClassOnly(criteria.CardClass);

				List<Card> classCards = new List<Card>();
				foreach (Card card in allCards)
				{
					if (!criteria.Evaluate(card)) continue;

					if (!pool.Legacy || card.Class == criteria.CardClass || criteria.CardClass == CardClass.DEATHKNIGHT && card[GameTag.MULTIPLE_CLASSES] != 0)
						classCards.Add(card);
				}

				cards = new[] {classCards.ToArray(), new Card[0]};
			}

			pool.DiscoverMemo.TryAdd((criteria, cls), cards);

			return cards;
		}

		// 2 Sets means Class cards / Neutral cards.
		// 3 Sets means Tri-Class discovers. (Gangs)
		public static Card[] GetChoices(Card[][] cardsToDiscover, int numberOfChoices, Util.DeepCloneableRandom rnd)
		{
			Card[] result;

			switch (cardsToDiscover.Length)
			{
				case 1:
					Card[] distinct = cardsToDiscover[0].Distinct().ToArray();
					if (numberOfChoices >= distinct.Length)
						result = distinct;
					else
					{
						result = new Card[numberOfChoices];
						Card pick;
						for (int i = 0; i < result.Length; i++)
						{
							do
							{
								pick = cardsToDiscover[0][rnd.Next(cardsToDiscover[0].Length)];
							} while (result.Contains(pick));

							result[i] = pick;
						}
					}
					break;
				case 2:
					int classCount = cardsToDiscover[1].Length;
					int neutralCount = cardsToDiscover[0].Length;
					numberOfChoices = Math.Min(numberOfChoices, neutralCount + classCount);
					result = new Card[numberOfChoices];
					for (int i = 0; i < result.Length; i++)
					{
						int roll = rnd.Next(neutralCount + (classCount << 2));
						Card pick;
						if (roll < neutralCount)
							pick = cardsToDiscover[0][rnd.Next(cardsToDiscover[0].Length)];
						else
							pick = cardsToDiscover[1][rnd.Next(cardsToDiscover[1].Length)];

						bool contains = false;
						for (int j = 0; j < i; j++)
							if (result[j] == pick)
								contains = true;

						if (contains)
						{
							i--;
							continue;
						}

						result[i] = pick;

						if (roll < neutralCount)
							neutralCount--;
						else
							classCount--;
					}
					break;
				case 3:
					result = new Card[3];
					for (int i = 0; i < result.Length; i++)
					{
						Card pick;
						bool contains;
						do
						{
							// Reset per attempt: a card listed in two of the three class sets (a gang card) used to
							// make this loop spin forever once it had been drawn twice.
							contains = false;
							pick = cardsToDiscover[i][rnd.Next(cardsToDiscover[i].Length)];
							for (int j = 0; j < i; j++)
								if (result[j] == pick)
									contains = true;
						} while (contains);

						result[i] = pick;
					}
					break;
				default:
					throw new NotImplementedException();
			}

			return result;
		}

		private readonly struct DiscoverCriteria : IEquatable<DiscoverCriteria>
		{
			public readonly CardType CardType;
			public readonly CardClass CardClass;
			public readonly GameTag Tag;
			public readonly RelaSign RelaSign;
			public readonly int Value;

			public DiscoverCriteria(CardType cardType, CardClass cardClass, (GameTag tag, RelaSign relaSign, int value) tagValueCriteria)
			{
				CardType = cardType;
				CardClass = cardClass;
				(Tag, RelaSign, Value) = tagValueCriteria;
			}

			public bool Evaluate(Card card)
			{
				return
					(CardType == CardType.INVALID || card.Type == CardType) &&
					//(CardClass == CardClass.INVALID || card.Class == CardClass) &&
					(Tag == default ||
					 ((RelaSign == RelaSign.EQ && card[Tag] == Value) ||
					 (RelaSign == RelaSign.LEQ && card[Tag] <= Value) ||
					  (RelaSign == RelaSign.GEQ && card[Tag] >= Value)));
			}

			#region Equality members

			public bool Equals(DiscoverCriteria other)
			{
				return CardType == other.CardType && CardClass == other.CardClass && Tag == other.Tag && RelaSign == other.RelaSign && Value == other.Value;
			}

			public override bool Equals(object obj)
			{
				return obj is DiscoverCriteria other && Equals(other);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					var hashCode = (int) CardType;
					hashCode = (hashCode * 397) ^ (int) CardClass;
					hashCode = (hashCode * 397) ^ (int) Tag;
					hashCode = (hashCode * 397) ^ (int) RelaSign;
					hashCode = (hashCode * 397) ^ Value;
					return hashCode;
				}
			}

			#endregion

			public override string ToString()
			{
				var sb = new StringBuilder();
				if (CardType != CardType.INVALID)
					sb.Append($"[{CardType}]");
				if (CardClass != CardClass.INVALID)
					sb.Append($"[{CardClass}]");
				if (Tag != default)
					sb.Append($"[{Tag},{RelaSign},{Value}]");
				return sb.ToString();
			}
		}
	}
}
