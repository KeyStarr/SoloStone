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
using System.Linq;
using System.Text;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using System;
using SabberStoneCore.Loader;

namespace SabberStoneCore.Model
{
	/// <summary>
	/// Object representing a known entity.
	///
	/// All properties exposed by these instances are defined by the per-meta file in `facts/`.
	/// <see cref="CardLoader"/> for extraction procedures.
	/// </summary>
	public sealed class Card
	{
		/// <summary>
		/// Constraint condition based on the state of a target <see cref="ICharacter"/>.
		/// Returns true if the target is valid for this card.
		/// The result of this predicate is independent of <see cref="TargetingType"/> of this card.
		/// null if there is no such a condtion for this card.
		/// </summary>
		public TargetingPredicate TargetingPredicate { get; private set; }
        // Immutable recipe predicate; current source stats are read only at the instance boundary.
        internal Func<IPlayable, ICharacter, bool> SourceTargetingPredicate { get; set; }
		/// <summary>
		/// Returns true if playing this card requires targeting, based on the given state of a <see cref="Controller"/>.
		/// Can be null.
		/// </summary>
		public AvailabilityPredicate TargetingAvailabilityPredicate { get; internal set; }
		/// <summary>
		/// Returns false if this card cannot be played with respect to the given state of a <see cref="Controller"/>.
		/// </summary>
		public AvailabilityPredicate PlayAvailabilityPredicate { get; internal set; }
		/// <summary>
		/// True if playing this card requires at least one valid target.
		/// </summary>
		public bool MustHaveTargetToPlay { get; private set; }
		/// <summary>
		/// Represents the range of characters in which a card can target.
		/// </summary>
		public TargetingType TargetingType { get; private set; }

		public int ATK { get; private set; }
		public int Health { get; private set; }
		public int SpellPower { get; private set; }
		public bool Taunt { get; private set; }
		public bool Charge { get; private set; }
		public bool Stealth { get; internal set; }
		public bool Poisonous { get; private set; }
		public bool DivineShield { get; private set; }
		public bool Windfury { get; private set; }
		public bool LifeSteal { get; private set; }
		public bool Echo { get; private set; }
		public bool Rush { get; internal set; }
		public bool CantBeTargetedBySpells { get; private set; }
		public bool CantBeTargetedByHeroPowers => CantBeTargetedBySpells;
		public bool CantAttack { get; private set; }
		public bool Modular { get; private set; }
		public bool ChooseOne { get; private set; }
		public bool Combo { get; private set; }
		public bool IsSecret { get; private set; }
		public bool IsQuest { get; private set; }
		public bool Deathrattle { get; private set; }
		public bool Untouchable { get; private set; }
		public bool HideStat { get; private set; }
		public bool ReceivesDoubleSpelldamageBonus { get; private set; }
		public bool Freeze { get; }
		public bool Overkill { get; }

		public bool TwinSpell { get; }
		private Card()
		{

		}

		internal Card(string id, int assetId, Tag[] tags,
			Dictionary<PlayReq, int> playRequirements, string[] entourage, Tag[] refTags, bool preserveText = false)
		{
			Id = id;
			AssetId = assetId;
			Entourage = entourage;
			PlayRequirements = playRequirements;
			var tagDict = new Dictionary<GameTag, int>();
			var refTagDict = new Dictionary<GameTag, int>();

			#region Preprocessing tags.
			foreach (Tag tag in tags)
			{
				if (tag.TagValue.HasIntValue)
				{
					tagDict.Add(tag.GameTag, tag.TagValue);
					switch (tag.GameTag)
					{
						case GameTag.COST:
							Cost = tag.TagValue;
							break;
						case GameTag.ATK:
							ATK = tag.TagValue;
							break;
						case GameTag.HEALTH:
							Health = tag.TagValue;
							break;
						case GameTag.OVERLOAD:
							HasOverload = true;
							Overload = tag.TagValue;
							break;
						case GameTag.SPELLPOWER:
							SpellPower = tag.TagValue;
							break;
						case GameTag.CHOOSE_ONE:
							ChooseOne = true;
							break;
						case GameTag.COMBO:
							Combo = true;
							break;
						case GameTag.TAUNT:
							Taunt = true;
							break;
						case GameTag.CHARGE:
							Charge = true;
							break;
						case GameTag.STEALTH:
							Stealth = true;
							break;
						case GameTag.POISONOUS:
							Poisonous = true;
							break;
						case GameTag.DIVINE_SHIELD:
							DivineShield = true;
							break;
						case GameTag.WINDFURY:
							Windfury = true;
							break;
						case GameTag.LIFESTEAL:
							LifeSteal = true;
							break;
						case GameTag.ECHO:
							Echo = true;
							break;
						case GameTag.RUSH:
							Rush = true;
							break;
						case GameTag.OVERKILL:
							Overkill = true;
							break;
						case GameTag.TWINSPELL:
							TwinSpell = true;
							break;
						// Client Elusive (1211) has the same spell/hero-power targeting rule.
						case (GameTag)1211 when preserveText:
						case GameTag.CANT_BE_TARGETED_BY_SPELLS:
							CantBeTargetedBySpells = true;
							break;
						case GameTag.CANT_ATTACK:
							CantAttack = true;
							break;
						case GameTag.MODULAR:
							Modular = true;
							break;
						case GameTag.SECRET:
							IsSecret = true;
							break;
						// 1192 is the Sidequest tag.
						case (GameTag)1192 when id == "TLC_EVENT_400":
						case GameTag.QUEST:
							IsQuest = true;
							break;
						case GameTag.DEATHRATTLE:
							Deathrattle = true;
							break;
						case GameTag.UNTOUCHABLE:
							Untouchable = true;
							break;
						case GameTag.HIDE_STATS:
							HideStat = true;
							break;
						case GameTag.RECEIVES_DOUBLE_SPELLDAMAGE_BONUS:
							ReceivesDoubleSpelldamageBonus = true;
							break;
						case GameTag.FREEZE:
							Freeze = true;
							break;
						case GameTag.CARDRACE:
							Race = (Race)(int)tag.TagValue;
							break;
						case GameTag.CLASS:
							Class = (CardClass)(int)tag.TagValue;
							break;
						case GameTag.CARDTYPE:
							Type = (CardType)(int)tag.TagValue;
							break;
					}
				}
				else if
					(tag.TagValue.HasBoolValue)
				{
					tagDict.Add(tag.GameTag, tag.TagValue ? 1 : 0);
				}
				else if
					(tag.TagValue.HasStringValue)
				{
					switch (tag.GameTag)
					{
						case GameTag.CARDNAME:
							Name = tag.TagValue;
							break;
						case GameTag.CARDTEXT:
							Text = tag.TagValue;
							break;
					}
				}
			}
			foreach (Tag tag in refTags)
			{
				if (refTagDict.ContainsKey(tag.GameTag))
					continue;

				if (tag.TagValue.HasIntValue)
				{
					refTagDict.Add(tag.GameTag, tag.TagValue);
				}
				else if (tag.TagValue.HasBoolValue)
				{
					refTagDict.Add(tag.GameTag, tag.TagValue ? 1 : 0);
				}
			}
			#endregion

			#region Preprocessing requirements
			int characterType = 0;
			int friendlyCheck = 0;
			bool needsTarget = false;
			foreach (KeyValuePair<PlayReq, int> requirement in playRequirements)
			{
				switch (requirement.Key)
				{
					case PlayReq.REQ_TARGET_TO_PLAY:
						MustHaveTargetToPlay = true;
						needsTarget = true;
						break;
					case PlayReq.REQ_DRAG_TO_PLAY:  // TODO
					case PlayReq.REQ_NONSELF_TARGET:
					case PlayReq.REQ_TARGET_IF_AVAILABLE:
						needsTarget = true;
						break;
					case PlayReq.REQ_MINION_TARGET:
						characterType = 1;
						break;
					case PlayReq.REQ_FRIENDLY_TARGET:
						friendlyCheck = 1;
						break;
					case PlayReq.REQ_ENEMY_TARGET:
						friendlyCheck = -1;
						break;
					case PlayReq.REQ_HERO_TARGET:
						characterType = -1;
						break;
					case PlayReq.REQ_TARGET_WITH_RACE:
						TargetingPredicate += TargetingPredicates.ReqTargetWithRace(requirement.Value);
						break;
					case PlayReq.REQ_FROZEN_TARGET:
						TargetingPredicate += TargetingPredicates.ReqFrozenTarget;
						break;
					case PlayReq.REQ_DAMAGED_TARGET:
						TargetingPredicate += TargetingPredicates.ReqDamagedTarget;
						break;
					case PlayReq.REQ_UNDAMAGED_TARGET:
						TargetingPredicate += TargetingPredicates.ReqUndamagedTarget;
						break;
					case PlayReq.REQ_TARGET_MAX_ATTACK:
						TargetingPredicate += TargetingPredicates.ReqTargetMaxAttack(requirement.Value);
						break;
					case PlayReq.REQ_TARGET_MIN_ATTACK:
						TargetingPredicate += TargetingPredicates.ReqTargetMinAttack(requirement.Value);
						break;
					case PlayReq.REQ_MUST_TARGET_TAUNTER:
						TargetingPredicate += TargetingPredicates.ReqMustTargetTaunter;
						break;
					case PlayReq.REQ_STEALTHED_TARGET:
						TargetingPredicate += TargetingPredicates.ReqStealthedTarget;
						break;
					case PlayReq.REQ_TARGET_WITH_DEATHRATTLE:
						TargetingPredicate += TargetingPredicates.ReqTargetWithDeathrattle;
						break;
					case PlayReq.REQ_LEGENDARY_TARGET:
						TargetingPredicate += TargetingPredicates.ReqLegendaryTarget;
						break;
					case PlayReq.REQ_TARGET_FOR_COMBO:
						needsTarget = true;
						TargetingAvailabilityPredicate += TargetingPredicates.ReqTargetForCombo;
						break;
					case PlayReq.REQ_TARGET_IF_AVAILABE_AND_ELEMENTAL_PLAYED_LAST_TURN:
						needsTarget = true;
						TargetingAvailabilityPredicate += TargetingPredicates.ElementalPlayedLastTurn;
						break;
					case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_DRAGON_IN_HAND:
						needsTarget = true;
						TargetingAvailabilityPredicate += TargetingPredicates.DragonInHand;
						break;
					case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_MINIMUM_FRIENDLY_MINIONS:
						needsTarget = true;
						TargetingAvailabilityPredicate += TargetingPredicates.MinimumFriendlyMinions(requirement.Value);
						break;
					case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_MINIMUM_FRIENDLY_SECRETS:
						needsTarget = true;
						TargetingAvailabilityPredicate += TargetingPredicates.MinimumFriendlySecrets(requirement.Value);
						break;
					case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_NO_3_COST_CARD_IN_DECK:
						// TODO
						TargetingType = TargetingType.AllMinions;
						break;
					case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_HERO_HAS_ATTACK:
						needsTarget = true;
						TargetingAvailabilityPredicate += TargetingPredicates.ReqHeroHasAttack;
						break;
					case PlayReq.REQ_NUM_MINION_SLOTS:
						PlayAvailabilityPredicate += TargetingPredicates.ReqNumMinionSlots;
						break;
					case PlayReq.REQ_MINIMUM_ENEMY_MINIONS:
						PlayAvailabilityPredicate += TargetingPredicates.ReqMinimumEnemyMinions(requirement.Value);
						break;
					case PlayReq.REQ_MINIMUM_TOTAL_MINIONS:
						PlayAvailabilityPredicate += TargetingPredicates.ReqMinimumTotalMinions(requirement.Value);
						break;
					case PlayReq.REQ_HAND_NOT_FULL:
						PlayAvailabilityPredicate += TargetingPredicates.ReqHandNotFull;
						break;
					case PlayReq.REQ_WEAPON_EQUIPPED:
						PlayAvailabilityPredicate += TargetingPredicates.ReqWeaponEquipped;
						break;
					case PlayReq.REQ_ENTIRE_ENTOURAGE_NOT_IN_PLAY:
						PlayAvailabilityPredicate += TargetingPredicates.ReqEntireEntourageNotInPlay(assetId);
						break;
					case PlayReq.REQ_FRIENDLY_MINION_DIED_THIS_GAME:
						PlayAvailabilityPredicate += TargetingPredicates.ReqFriendlyMinionDiedThisGame;
						break;
					case PlayReq.REQ_FRIENDLY_MINION_OF_RACE_DIED_THIS_TURN:
						PlayAvailabilityPredicate +=
							TargetingPredicates.ReqFriendlyMinionOfRaceDiedThisTurn((Race) requirement.Value);
						break;
					case PlayReq.REQ_MUST_PLAY_OTHER_CARD_FIRST:
						PlayAvailabilityPredicate += (c, card) => false;
						break;
					//	REQ_STEADY_SHOT
					//	REQ_MINION_OR_ENEMY_HERO	//	Steady Shot
					//	REQ_MINION_SLOT_OR_MANA_CRYSTAL_SLOT	//	Jade Blossom
					case PlayReq.REQ_SECRET_ZONE_CAP_FOR_NON_SECRET:
						PlayAvailabilityPredicate += TargetingPredicates.ReqSecretZoneCapForNonSecret;
						break;
				}
			}

			if (needsTarget)
			{
				if (characterType > 0)
				{
					if (friendlyCheck == 0)
						TargetingType = TargetingType.AllMinions;
					else if (friendlyCheck == 1)
						TargetingType = TargetingType.FriendlyMinions;
					else
						TargetingType = TargetingType.EnemyMinions;
				}
				else if (characterType == 0)
				{
					if (friendlyCheck == 0)
						TargetingType = TargetingType.All;
					else if (friendlyCheck == 1)
						TargetingType = TargetingType.FriendlyCharacters;
					else
						TargetingType = TargetingType.EnemyCharacters;
				}
				else
					TargetingType = TargetingType.Heroes;
			}
			#endregion

			Tags = tagDict;
			ExcludeFromLegacyRandomPools = this[(GameTag)2936] != 0 || this[(GameTag)4445] != 0;
			Races = Array.AsReadOnly(DecodeRaces().ToArray());
			RefTags = refTagDict;
			// spell damage information add ... 
			if (Text != null && (Text.Contains("$") || tagDict.ContainsKey(GameTag.AFFECTED_BY_SPELL_POWER)))
			{
				if (!preserveText) Text += " @spelldmg";
				IsAffectedBySpellDamage = true;
			}
		}

        internal void SetPrintedCost(int cost)
        { Cost = cost; Tags[GameTag.COST] = cost; }

        internal void SetPrintedClass(CardClass cardClass)
        { Class = cardClass; Tags[GameTag.CLASS] = (int)cardClass; }

        internal void SetPrintedCharge(bool charge)
        { Charge = charge; if (charge) Tags[GameTag.CHARGE] = 1; else Tags.Remove(GameTag.CHARGE); }

        internal void SetPrintedLifesteal(bool lifesteal)
        { LifeSteal = lifesteal; if (lifesteal) Tags[GameTag.LIFESTEAL] = 1; else Tags.Remove(GameTag.LIFESTEAL); }

        internal void SetPrintedStats(int attack, int health)
        {
            ATK = attack; Health = health;
            Tags[GameTag.ATK] = attack; Tags[GameTag.HEALTH] = health;
        }

        internal void SetPrintedRaces(Race primary, params Race[] additional)
        {
            Race = primary;
            Tags[GameTag.CARDRACE] = (int)primary;
            foreach (var pair in AdditionalRaceTags) Tags.Remove(pair.Key);
            foreach (var race in additional) Tags[AdditionalRaceTags.First(p => p.Value == race).Key] = 1;
            Races = Array.AsReadOnly(DecodeRaces().ToArray());
        }

        internal void SetPrintedDeathrattle()
        {
            Deathrattle = true;
            Tags[GameTag.DEATHRATTLE] = 1;
        }

		internal void UseBehaviourFrom(Card original)
		{
			Power = original.Power;
			// Registry includes native-only cards (e.g. Reborn) without a legacy CardDef entry.
			Implemented = true;
			PlayRequirements = new Dictionary<PlayReq, int>(original.PlayRequirements);
			Entourage = (string[])original.Entourage.Clone();
			TargetingPredicate = original.TargetingPredicate;
			TargetingAvailabilityPredicate = original.TargetingAvailabilityPredicate;
            SourceTargetingPredicate = original.SourceTargetingPredicate;
			PlayAvailabilityPredicate = original.PlayAvailabilityPredicate;
			MustHaveTargetToPlay = original.MustHaveTargetToPlay;
			TargetingType = original.TargetingType;
		}

		internal void SetPlayRequirements(Dictionary<PlayReq, int> playReqs)
		{
			PlayRequirements = playReqs;
			bool needsTarget = false;
			TargetingType type = TargetingType.All;
			foreach (KeyValuePair<PlayReq, int> requirement in playReqs)
			{
				switch (requirement.Key)
			{
				case PlayReq.REQ_TARGET_TO_PLAY:
					MustHaveTargetToPlay = true;
					needsTarget = true;
					break;
				case PlayReq.REQ_DRAG_TO_PLAY:  // TODO
				case PlayReq.REQ_NONSELF_TARGET:
				case PlayReq.REQ_TARGET_IF_AVAILABLE:
					needsTarget = true;
					break;
				case PlayReq.REQ_MINION_TARGET:
					type &= ~TargetingType.Hero;
					break;
				case PlayReq.REQ_FRIENDLY_TARGET:
					type &= ~TargetingType.Enemy;
					break;
				case PlayReq.REQ_ENEMY_TARGET:
					type &= ~TargetingType.Friendly;
					break;
				case PlayReq.REQ_HERO_TARGET:
					type &= ~TargetingType.Minion;
					break;
				case PlayReq.REQ_TARGET_WITH_RACE:
					TargetingPredicate += TargetingPredicates.ReqTargetWithRace(requirement.Value);
					break;
				case PlayReq.REQ_FROZEN_TARGET:
					TargetingPredicate += TargetingPredicates.ReqFrozenTarget;
					break;
				case PlayReq.REQ_DAMAGED_TARGET:
					TargetingPredicate += TargetingPredicates.ReqDamagedTarget;
					break;
				case PlayReq.REQ_UNDAMAGED_TARGET:
					TargetingPredicate += TargetingPredicates.ReqUndamagedTarget;
					break;
				case PlayReq.REQ_TARGET_MAX_ATTACK:
					TargetingPredicate += TargetingPredicates.ReqTargetMaxAttack(requirement.Value);
					break;
				case PlayReq.REQ_TARGET_MIN_ATTACK:
					TargetingPredicate += TargetingPredicates.ReqTargetMinAttack(requirement.Value);
					break;
				case PlayReq.REQ_MUST_TARGET_TAUNTER:
					TargetingPredicate += TargetingPredicates.ReqMustTargetTaunter;
					break;
				case PlayReq.REQ_STEALTHED_TARGET:
					TargetingPredicate += TargetingPredicates.ReqStealthedTarget;
					break;
				case PlayReq.REQ_TARGET_WITH_DEATHRATTLE:
					TargetingPredicate += TargetingPredicates.ReqTargetWithDeathrattle;
					break;
				case PlayReq.REQ_LEGENDARY_TARGET:
					TargetingPredicate += TargetingPredicates.ReqLegendaryTarget;
					break;
				case PlayReq.REQ_TARGET_FOR_COMBO:
					needsTarget = true;
					TargetingAvailabilityPredicate += TargetingPredicates.ReqTargetForCombo;
					break;
				case PlayReq.REQ_TARGET_IF_AVAILABE_AND_ELEMENTAL_PLAYED_LAST_TURN:
					needsTarget = true;
					TargetingAvailabilityPredicate += TargetingPredicates.ElementalPlayedLastTurn;
					break;
				case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_DRAGON_IN_HAND:
					needsTarget = true;
					TargetingAvailabilityPredicate += TargetingPredicates.DragonInHand;
					break;
				case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_MINIMUM_FRIENDLY_MINIONS:
					needsTarget = true;
					TargetingAvailabilityPredicate += TargetingPredicates.MinimumFriendlyMinions(requirement.Value);
					break;
				case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_MINIMUM_FRIENDLY_SECRETS:
					needsTarget = true;
					TargetingAvailabilityPredicate += TargetingPredicates.MinimumFriendlySecrets(requirement.Value);
					break;
				case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_NO_3_COST_CARD_IN_DECK:
					// TODO
					TargetingType = TargetingType.AllMinions;
					break;
				case PlayReq.REQ_TARGET_IF_AVAILABLE_AND_HERO_HAS_ATTACK:
					needsTarget = true;
					TargetingAvailabilityPredicate += TargetingPredicates.ReqHeroHasAttack;
					break;
				case PlayReq.REQ_NUM_MINION_SLOTS:
					PlayAvailabilityPredicate += TargetingPredicates.ReqNumMinionSlots;
					break;
				case PlayReq.REQ_MINIMUM_ENEMY_MINIONS:
					PlayAvailabilityPredicate += TargetingPredicates.ReqMinimumEnemyMinions(requirement.Value);
					break;
				case PlayReq.REQ_MINIMUM_TOTAL_MINIONS:
					PlayAvailabilityPredicate += TargetingPredicates.ReqMinimumTotalMinions(requirement.Value);
					break;
				case PlayReq.REQ_HAND_NOT_FULL:
					PlayAvailabilityPredicate += TargetingPredicates.ReqHandNotFull;
					break;
				case PlayReq.REQ_WEAPON_EQUIPPED:
					PlayAvailabilityPredicate += TargetingPredicates.ReqWeaponEquipped;
					break;
				case PlayReq.REQ_ENTIRE_ENTOURAGE_NOT_IN_PLAY:
					PlayAvailabilityPredicate += TargetingPredicates.ReqEntireEntourageNotInPlay(AssetId);
					break;
				case PlayReq.REQ_FRIENDLY_MINION_DIED_THIS_GAME:
					PlayAvailabilityPredicate += TargetingPredicates.ReqFriendlyMinionDiedThisGame;
					break;
				case PlayReq.REQ_FRIENDLY_MINION_OF_RACE_DIED_THIS_TURN:
					PlayAvailabilityPredicate +=
						TargetingPredicates.ReqFriendlyMinionOfRaceDiedThisTurn((Race) requirement.Value);
					break;
				case PlayReq.REQ_MUST_PLAY_OTHER_CARD_FIRST:
					PlayAvailabilityPredicate += (c, card) => false;
					break;
				//	REQ_STEADY_SHOT
				//	REQ_MINION_OR_ENEMY_HERO	//	Steady Shot
				//	REQ_MINION_SLOT_OR_MANA_CRYSTAL_SLOT	//	Jade Blossom
				case PlayReq.REQ_SECRET_ZONE_CAP_FOR_NON_SECRET:
					PlayAvailabilityPredicate += TargetingPredicates.ReqSecretZoneCapForNonSecret;
					break;
			}
			}

			if (needsTarget)
			{
				//if ((type & TargetingType.Enemy) != TargetingType.Enemy &&
				//    (type & TargetingType.Friendly) != TargetingType.Friendly)
				//{
				//	type |= TargetingType.Enemy;
				//	type |= TargetingType.Friendly;
				//}
				TargetingType = type;
			}
		}

		/// <summary>
		/// Unique asset id of that card nummeric representation.
		/// </summary>
		public int AssetId { get; }

		/// <summary>
		/// Unique card ID, as defined in
		/// </summary>
		public string Id { get; private set; }

		/// <summary>
		/// Name of the card, localized in the extracted language.
		/// </summary>
		public string Name { get; internal set; }

		/// <summary>
		/// Flavour text of the card, localized in the extracted language.
		/// </summary>
		public string Text { get; internal set; }

		/// <summary>
		/// Contains all card ID's which are generated by this specific card.
		///
		/// For example Ysera, the dragon which produces on DREAM card after your turn,
		/// has entourage: DREAM_01, DREAM_02, DREAM_03, DREAM_04, DREAM_05
		/// </summary>
		public string[] Entourage { get; internal set; }

		/// <summary>
		/// Properties set on this instance.
		/// These properties represent health, attack, # turns in play etc.
		/// <see cref="GameTag"/> for all possibilities.
		/// </summary>
		public Dictionary<GameTag, int> Tags { get; private set; }

		/// <summary>
		/// Declares all effects that are triggered by this instance.
		/// Possibilities are SpellPower, DeathRattle, Charge etc.
		/// </summary>
		public Dictionary<GameTag, int> RefTags { get; private set; }

		/// <summary>
		/// Requirements that must have been met before this card can be moved into
		/// play zone.
		/// <see cref="PlayReq"/> for all possibilities.
		/// </summary>
		public Dictionary<PlayReq, int> PlayRequirements { get; private set; }

		/// <summary>
		/// Provides easy access to the value of a specific Tag set on this instance.
		/// <seealso cref="Tags"/>
		/// </summary>
		/// <param name="t">The <see cref="GameTag"/> which value is queried</param>
		/// <returns></returns>
		public int this[GameTag t]
			=> Tags.TryGetValue(t, out int value) ? value : 0;

		/// <summary>
		/// Indicates if this card occurs in the player's collection. Only collectible
		/// cards can be put together in a deck.
		///
		/// Non-collectible cards are generated during the game, like Ysera's Dream cards.
		/// </summary>
		public bool Collectible => this[GameTag.COLLECTIBLE] == 1;

		/// <summary>
		/// A card can have NO class or AT MOST one.
		/// The cardclass is coupled with the chosen hero to represent the player.
		///
		/// Cards with a specific class can NOT be put into a deck with other classcards.
		/// <seealso cref="CardClass"/>
		/// </summary>
		public CardClass Class { get; private set; }


		private Race Race;

		/// <summary>Printed types; ALL is retained as a wildcard, not expanded.</summary>
		public IReadOnlyList<Race> Races { get; private set; } = Array.AsReadOnly(Array.Empty<Race>());

		/// <summary>Additional type flags observed in the pinned 37.0.0.254477 export.</summary>
		public static IReadOnlyDictionary<GameTag, Race> AdditionalRaceTags { get; } =
			new System.Collections.ObjectModel.ReadOnlyDictionary<GameTag, Race>(new Dictionary<GameTag, Race> {
				[(GameTag)2522] = Race.PIRATE, [(GameTag)2523] = Race.DRAGON,
				[(GameTag)2525] = Race.DRAENEI, [(GameTag)2534] = Race.UNDEAD,
				[(GameTag)2536] = Race.MURLOC, [(GameTag)2537] = Race.DEMON,
				[(GameTag)2539] = Race.MECHANICAL, [(GameTag)2540] = Race.ELEMENTAL,
				[(GameTag)2542] = Race.BEAST, [(GameTag)2546] = Race.QUILBOAR,
				[(GameTag)2553] = Race.NAGA
			});

		private static bool IsMinionType(Race race)
		{
			switch (race)
			{
				case Race.DRAENEI: case Race.UNDEAD: case Race.MURLOC: case Race.DEMON:
				case Race.MECHANICAL: case Race.ELEMENTAL: case Race.BEAST: case Race.TOTEM:
				case Race.PIRATE: case Race.DRAGON: case Race.QUILBOAR: case Race.NAGA:
					return true;
				default: return false;
			}
		}

		private IEnumerable<Race> DecodeRaces()
		{
			if (Race == Race.ALL || IsMinionType(Race)) yield return Race;
			foreach (var pair in AdditionalRaceTags)
				if (pair.Value != Race && this[pair.Key] == 1) yield return pair.Value;
		}

		/// <summary>Exported primary type only. Rules should use IsRace or SharesRaceWith.</summary>
		public Race GetRawRace() => Race;

		public bool IsRace(Race race) => race == Race.ALL ? Races.Contains(Race.ALL)
			: IsMinionType(race) && (Races.Contains(Race.ALL) || Races.Contains(race));

		// Legacy random-generation pools are replay-pinned; new modules filter with IsRace instead.
		internal bool IsRaceForLegacyRandomPool(Race race) => Race == Race.ALL || Race == race;

		/// <summary>Whether two minions share any printed type. Typeless minions never match.</summary>
		public bool SharesRaceWith(Card other) => other != null && Type == CardType.MINION
			&& other.Type == CardType.MINION && Races.Count > 0 && other.Races.Count > 0
			&& (IsRace(Race.ALL) || other.IsRace(Race.ALL) || Races.Any(other.IsRace));

		/// <summary>Whether all minions have a common type; ignores non-minions. Empty input succeeds.</summary>
		public static bool MinionsShareRace(IEnumerable<Card> cards)
		{
			HashSet<Race> common = null;
			foreach (Card card in cards)
			{
				if (card.Type != CardType.MINION || card.IsRace(Race.ALL)) continue;
				if (common == null) common = new HashSet<Race>(card.Races);
				else common.IntersectWith(card.Races);
				if (common.Count == 0) return false;
			}
			return true;
		}

		/// <summary>
		/// <see cref="Faction"/>
		/// </summary>
		public Faction Faction => (Faction)this[GameTag.FACTION];

		/// <summary>
		/// <see cref="Rarity"/>
		/// </summary>
		public Rarity Rarity => (Rarity)this[GameTag.RARITY];

		/// <summary>
		/// The actual type of a card has limitations on it's usage.
		/// A hero card cannot be put into a deck for example.. imagine that!
		/// Knights of the Frozen Throne , is beyond imagination? With
		/// Deathknigth heros in the deck!
		/// <see cref="CardType"/>
		/// </summary>
		public CardType Type { get; }

		/// <summary>
		/// <see cref="CardSet"/>
		/// </summary>
		public CardSet Set => (CardSet)this[GameTag.CARD_SET];

		/// <summary>
		/// Original mana-cost of this card.
		/// </summary>
		public int Cost { get; private set; }

		/// <summary>
		/// True if this card will incur Overload when played.
		/// Overload is an effect that locks mana crystals.
		/// Locked mana crystals can't be spent during one turn.
		/// </summary>
		public bool HasOverload { get; internal set; }

		/// <summary>
		/// The amount of overload incurred by this card when played.
		/// </summary>
		public int Overload { get; internal set; }

		/// <summary>
		/// Returns to which multi class group this card belongs.
		/// <see cref="MultiClassGroup"/>
		/// </summary>
		public bool RequiresTarget => PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_TO_PLAY);

		/// <summary>
		/// Requires a target for combo
		/// </summary>
		public bool RequiresTargetForCombo => PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_FOR_COMBO);

		/// <summary>
		/// Requires a target if available
		/// </summary>
		public bool RequiresTargetIfAvailable => PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_IF_AVAILABLE) ||
		                                         PlayRequirements.ContainsKey(PlayReq.REQ_DRAG_TO_PLAY);

		/// <summary>
		/// Requires a target if available and dragon in hand
		/// </summary>
		public bool RequiresTargetIfAvailableAndDragonInHand
			=> PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_IF_AVAILABLE_AND_DRAGON_IN_HAND);

		/// <summary>
		/// Requires a target if available and element played last turn
		/// </summary>
		public bool RequiresTargetIfAvailableAndElementalPlayedLastTurn
			=> PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_IF_AVAILABE_AND_ELEMENTAL_PLAYED_LAST_TURN);

		/// <summary>
		/// Requires a target if available and minimum friendly minions
		/// </summary>
		public bool RequiresTargetIfAvailableAndMinimumFriendlyMinions
			=> PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_IF_AVAILABLE_AND_MINIMUM_FRIENDLY_MINIONS);

		/// <summary>
		/// Requires a target if available and minimum friendly secrets
		/// </summary>
		public bool RequiresTargetIfAvailableAndMinimumFriendlySecrets
			=> PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_IF_AVAILABLE_AND_MINIMUM_FRIENDLY_SECRETS);

		public bool RequiresTargetIfAvailableAndNo3CostCardInDeck
			=> PlayRequirements.ContainsKey(PlayReq.REQ_TARGET_IF_AVAILABLE_AND_NO_3_COST_CARD_IN_DECK);

		/// <summary>
		/// Maximum amount of copies that are allowed in one deck.
		/// </summary>
		public int MaxAllowedInDeck => Rarity == Rarity.LEGENDARY ? 1 : 2;

		/// <summary>
		/// True if the effects of this card are implemented.
		/// </summary>
		public bool Implemented { get; set; }

		// New explicit-deck printings can preserve the accepted legacy random-generation corpus.
		internal bool ExcludeFromLegacyRandomPools { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> ColossalAppendageIds => Actions.ColossalRules.AppendagesFor(Id);

		/// <summary>
		/// Holds a list of Buffs/Debuffs on this card instance.
		/// <seealso cref="Power"/>
		/// </summary>
		//public List<Power> Powers { get; set; } = new List<Power>();
		public Power Power { get; set; }

		/// <summary>
		/// True if this card increases it's owners spell damage.
		/// </summary>
		public bool IsAffectedBySpellDamage { get; set; }

		/// <summary>
		/// Multi class group.
		/// </summary>
		public int MultiClassGroup => this[GameTag.MULTI_CLASS_GROUP];

		/// <summary>
		/// Check whether this card is playable with respect to the state of a given <see cref="Controller"/>.
		/// </summary>
		public bool IsPlayableByCardReq(in Controller c)
		{
			//return PlayAvailabilityPredicate?.Invoke(c) ?? true;

			if (PlayAvailabilityPredicate != null)
			{
				//if (PlayAvailabilityPredicate.)

				bool flag = true;
				foreach (Delegate predicate in PlayAvailabilityPredicate.GetInvocationList())
					flag &= ((AvailabilityPredicate) predicate).Invoke(c, this);
				return flag;
			}
			return true;
		}

		/// <summary>Calculates if a target is valid by testing the game state for each hardcoded requirement.
		/// </summary>
		/// <param name="c">The controller of the source.</param>
		/// <param name="target">The proposed target.</param>
		/// <returns><c>true</c> if the proposed target is valid, <c>false</c> otherwise.</returns>
		public bool TargetingRequirements(in Controller c, in ICharacter target)
		{
			if (target is Minion dormant && dormant.Untouchable)
				return false;

			if (target.Controller != c && Actions.CombatRules.PreventsFire(Type, this[GameTag.SPELL_SCHOOL], target))
                return false;

			if ((target.HasStealth || target.IsImmune) && target.Controller != c)
				return false;

			if (!TargetingPredicate?.Invoke(target) ?? false)
				return false;

			return true;
		}

		public List<ICharacter> GetValidPlayTargets(in Controller c)
		{
			var output = new List<ICharacter>(2);

			if (!TargetingAvailabilityPredicate?.Invoke(c, this) ?? false)
				return output;

			bool friendlyMinions = false;
			bool enemyMinions = false;
			bool hero = false;
			bool enemyHero = false;
			switch (TargetingType)
			{
				case TargetingType.None:
					// If this is a non-targeting card, return an empty list
					return output;
				case TargetingType.All:
					friendlyMinions = true;
					enemyMinions = true;
					hero = true;
					enemyHero = true;
					break;
				case TargetingType.FriendlyCharacters:
					friendlyMinions = true;
					hero = true;
					break;
				case TargetingType.EnemyCharacters:
					enemyMinions = true;
					enemyHero = true;
					break;
				case TargetingType.AllMinions:
					friendlyMinions = true;
					enemyMinions = true;
					break;
				case TargetingType.FriendlyMinions:
					friendlyMinions = true;
					break;
				case TargetingType.EnemyMinions:
					enemyMinions = true;
					break;
				case TargetingType.Heroes:
					hero = true;
					enemyHero = true;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			if (friendlyMinions)
			{
				var span = c.BoardZone.GetSpan();
				for (int i = 0; i < span.Length; i++)
					if (TargetingRequirements(in c, span[i]))
						output.Add(span[i]);
			}

			if (enemyMinions)
			{
				var span = c.Opponent.BoardZone.GetSpan();
				for (int i = 0; i < span.Length; i++)
					if (TargetingRequirements(in c, span[i]))
						output.Add(span[i]);
			}

			if (hero)
			{
				if (TargetingRequirements(in c, c.Hero))
					output.Add(c.Hero);
			}

			if (enemyHero)
			{
				if (TargetingRequirements(in c, c.Opponent.Hero))
					output.Add(c.Opponent.Hero);
			}

			return output;
		}


		/// <summary>
		/// Returns a string representation.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return $"[{Name}]";
		}
		/// <summary>
		/// Returns a substring of the name of this instance.
		/// </summary>
		/// <param name="size">The maximum length of the substring in # characters.</param>
		/// <returns></returns>
		public string AbbreviatedName(int size)
		{
			if (Name.Length <= size)
			{
				return Name;
			}
			else if (!Name.Contains(" "))
			{
				return Name.Substring(0, size);
			}
			else
			{
				string[] strArray = Name.Split(' ');
				return String.Join("", strArray.Select(p => p.Length > 4 ? p.Substring(0, 4) : p).ToList()).Substring(0, 7);
			}

		}

		/// <summary>
		/// Returns a string containing all information about this instance.
		/// </summary>
		/// <param name="gameTag"></param>
		/// <param name="playReq"></param>
		/// <returns></returns>
		public string FullPrint(bool gameTag = false, bool playReq = false)
		{
			var builder = new StringBuilder();
			builder.Append($"[CARD: {Name} - {Id} (Col={Collectible},Set={Set})]");
			if (gameTag)
			{
				builder.Append("\n   GameTags:");
				Tags.ToList().ForEach(pair => builder.Append($"\n   - {pair.Key} -> {pair.Value}"));
			}
			if (playReq)
			{
				builder.Append("\n   PlayReq:");
				PlayRequirements.ToList().ForEach(pair => builder.Append($"\n   - {pair.Key} -> {pair.Value}"));
			}
			return builder.ToString();
		}

		internal static readonly Card CardGame = new Card()
		{
			Id = "Game",
			Name = "Game",
			Tags = new Dictionary<GameTag, int> { [GameTag.CARDTYPE] = (int)CardType.GAME },
			//PlayRequirements = new Dictionary<PlayReq, int>(),
		};

		internal static readonly Card CardPlayer = new Card()
		{
			Id = "Player",
			Name = "Player",
			Tags = new Dictionary<GameTag, int> { [GameTag.CARDTYPE] = (int)CardType.PLAYER },
			//PlayRequirements = new Dictionary<PlayReq, int>(),
		};

		/// <summary>Definition metadata: enroll games when an entity of this definition is created.</summary>
		public Actions.ShatterDefinition Shatter { get; set; }

        public Actions.HandProgressDefinition HandProgress { get; set; }

        public Func<IPlayable,bool> HealthCostCondition { get; internal set; }
        public bool ConsumesHistory { get; set; }
        public bool CopyHandTrigger { get; set; }
        public bool ExpiresOnHandExit { get; set; }

		/// <summary>
		/// Registers the SAME static predicate that the card's power evaluates.
		/// It must be phase-invariant: the same answer in hand and during the card's own effect;
		/// exclude the source entity from hand, board and history counts.
		/// It must be read-only: no tag writes, RNG, history writes or tasks.
		/// </summary>
		public void PoweredUpWhen(Func<IPlayable, bool> condition) => PoweredUp.Register(this, condition);

		private sealed class HistoryDisplayRecipe
		{
			internal readonly GameTag Tag;
			internal readonly Func<IPlayable, int> Value;
			internal HistoryDisplayRecipe(GameTag tag, Func<IPlayable, int> value) { Tag = tag; Value = value; }
		}
		private HistoryDisplayRecipe[] _historyDisplays;

        public DormantDefinition Dormant { get; set; }
        internal void AddTargetingPredicate(TargetingPredicate predicate) => TargetingPredicate += predicate;
        internal void UseGeneratedTargeting()
        {
            TargetingPredicate = null;
            SourceTargetingPredicate = Actions.GeneratedDefinitionRules.ValidTarget;
            TargetingAvailabilityPredicate = Actions.GeneratedDefinitionRules.TargetAvailable;
            PlayAvailabilityPredicate = Actions.GeneratedDefinitionRules.PlayAvailable;
        }
        private HistoryDisplayRecipe[] _boardHistoryDisplays;
        internal bool HasBoardHistoryDisplays => _boardHistoryDisplays != null;
        public void BoardHistoryDisplay(GameTag tag, Func<IPlayable,int> value)
        {
            ValidateHistoryDisplay(tag,value);
            if (_boardHistoryDisplays != null && _boardHistoryDisplays.Any(d => d.Tag == tag))
                throw new ArgumentException("Duplicate board history display tag.",nameof(tag));
            _boardHistoryDisplays = (_boardHistoryDisplays ?? Array.Empty<HistoryDisplayRecipe>())
                .Concat(new[] { new HistoryDisplayRecipe(tag,value) }).ToArray();
            ConsumesHistory = true;
        }
        internal void UpdateBoardHistoryDisplays(IPlayable owner)
        {
            if (_boardHistoryDisplays == null || owner is Minion m && m.IsSilenced) return;
            foreach (var display in _boardHistoryDisplays) owner[display.Tag] = Math.Max(0,display.Value(owner));
        }

		/// <summary>
		/// Sets a numeric text value at aura-update boundaries while in the owner's hand.
		/// Supply a pure static method reading only the entity and its game's history; no captured state.
		/// </summary>
		public void HistoryDisplay(GameTag displayTag, Func<IPlayable, int> value)
		{
			ValidateHistoryDisplay(displayTag, value);
			if (_historyDisplays != null && _historyDisplays.Any(p => p.Tag == displayTag))
				throw new ArgumentException("Duplicate history display tag.", nameof(displayTag));
			_historyDisplays = (_historyDisplays ?? Array.Empty<HistoryDisplayRecipe>())
				.Concat(new[] { new HistoryDisplayRecipe(displayTag, value) }).ToArray();
			ConsumesHistory = true;
		}

		private static void ValidateHistoryDisplay(GameTag tag, Func<IPlayable, int> value)
		{
			if (tag != GameTag.TAG_SCRIPT_DATA_NUM_1 && tag != GameTag.TAG_SCRIPT_DATA_NUM_2 && tag != GameTag.TAG_SCRIPT_DATA_NUM_6 && tag != (GameTag)955)
				throw new ArgumentOutOfRangeException(nameof(tag), "History display requires a numeric script-data tag.");
			if (value == null) throw new ArgumentNullException(nameof(value));
			if (!value.Method.IsStatic || value.Target != null || value.GetInvocationList().Length != 1)
				throw new ArgumentException("History display requires a pure static method recipe.", nameof(value));
		}

		internal void ValidateHistoryDisplays()
		{
			if (_historyDisplays == null) return;
			var tags = new HashSet<GameTag>();
			foreach (var display in _historyDisplays)
			{
				if (display == null) throw new ArgumentException("Missing history display recipe.");
				ValidateHistoryDisplay(display.Tag, display.Value);
				if (!tags.Add(display.Tag) || !ConsumesHistory)
					throw new ArgumentException("Invalid history display metadata.");
			}
		}

		internal void UpdateHistoryDisplays(IPlayable owner)
		{
			if (_historyDisplays == null) return;
			foreach (var display in _historyDisplays)
				owner[display.Tag] = Math.Max(0, display.Value(owner));
		}

		public Card Clone()
		{
			var clone = (Card)MemberwiseClone();
			clone.Tags = new Dictionary<GameTag, int>(Tags);
			clone.RefTags = new Dictionary<GameTag, int>(RefTags);
			return clone;
		}

        internal Card WithVisibleBase(Card visible, Dictionary<GameTag, int> tags)
        {
            // Keep the visible definition's recipe metadata; allocation must not reveal how it was generated.
            var result = Clone();
            result.Tags = tags.OrderBy(pair => (int)pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
            result.Name = visible.Name;
            result.Text = visible.Text;
            result.Cost = visible.Cost;
            result.ATK = visible.ATK;
            result.Health = visible.Health;
            result.Races = new System.Collections.ObjectModel.ReadOnlyCollection<Race>(visible.Races.ToArray());
            result.PlayRequirements = new Dictionary<PlayReq, int>(PlayRequirements);
            result.Entourage = (string[])Entourage.Clone();
            return result;
        }

        public static Card CreateAssembledCard(Card body, Card top, Card bottom)
        {
            if (!Loader.AssembleCatalog.Supports(body) || !Loader.AssembleCatalog.Supports(top)
                || !Loader.AssembleCatalog.Supports(bottom))
                throw new ArgumentException("Assemble requires audited split recipes.");
            var result = body.Clone();
            result.Power = Loader.AssembleCatalog.ComposePower(top, bottom);
            result.Text = Loader.AssembleCatalog.Half(top, true) + Loader.AssembleCatalog.Half(bottom, false);
            foreach (var tag in new[] { GameTag.TAUNT, GameTag.CHARGE, GameTag.RUSH, GameTag.STEALTH,
                GameTag.DIVINE_SHIELD, GameTag.WINDFURY, GameTag.LIFESTEAL, GameTag.POISONOUS, GameTag.REBORN,
                GameTag.SPELLPOWER, GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS,
                GameTag.BATTLECRY, GameTag.DEATHRATTLE, (GameTag)32, (GameTag)3744, (GameTag)4577 }) result.Tags[tag] = top[tag];
            result.Taunt = top.Taunt; result.Charge = top.Charge; result.Rush = top.Rush;
            result.Stealth = top.Stealth; result.DivineShield = top.DivineShield; result.Windfury = top.Windfury;
            result.LifeSteal = top.LifeSteal; result.Poisonous = top.Poisonous; result.SpellPower = top.SpellPower;
            result.CantBeTargetedBySpells = top.CantBeTargetedBySpells;
            result.Deathrattle = result.Power.DeathrattleTask != null;
            result.Tags[Actions.AssembleRules.FirstHalf] = top.AssetId;
            result.Tags[Actions.AssembleRules.SecondHalf] = bottom.AssetId;
            result.SetPlayRequirements(new Dictionary<PlayReq, int>());
            return result;
        }

		public static Card CreateZombeastCard(in Card firstCard, in Card secondCard, bool modifyTags, bool eventReward = false)
        {
            if (eventReward)
            {
                var result = Actions.GeneratedDefinitionRules.Creation(firstCard, secondCard);
                var template = Cards.FromId("ICC_828t");
                result.Tags[GameTag.CLASS] = template[GameTag.CLASS];
                result.Tags[GameTag.CARD_SET] = template[GameTag.CARD_SET];
                result.Tags[GameTag.CARDRACE] = (int)Race.UNDEAD;
                foreach (var tag in AdditionalRaceTags.Keys) result.Tags.Remove(tag);
                result.Tags[AdditionalRaceTags.First(x => x.Value == Race.BEAST).Key] = 1;
                result.Tags[GameTag.OVERRIDECARDNAME] = 46119;
                var raw = result.Tags.Select(x => new Tag(x.Key, x.Value)).Concat(new[] {
                    new Tag(GameTag.CARDNAME, "Zombeast"), new Tag(GameTag.CARDTEXT, result.Text) }).ToArray();
                var beast = new Card("ICC_828t", 46119, raw, new Dictionary<PlayReq,int>(result.PlayRequirements),
                    result.Entourage, result.RefTags.Select(x => new Tag(x.Key,x.Value)).ToArray(), preserveText:true);
                beast.Power = result.Power; beast.Implemented = true; beast.ExcludeFromLegacyRandomPools = true;
                beast.UseGeneratedTargeting();
                return beast;
            }
            Card zombeast = firstCard.Clone();
			zombeast.ATK += secondCard.ATK;
			zombeast.Health += secondCard.Health;
			zombeast.Cost += secondCard.Cost;
			zombeast.Taunt = secondCard.Taunt;
			zombeast.Poisonous = secondCard.Poisonous;
			zombeast.Stealth = secondCard.Stealth;
			zombeast.Windfury = secondCard.Windfury;
			zombeast.Charge = secondCard.Charge;
			zombeast.LifeSteal = secondCard.LifeSteal;
			zombeast.CantBeTargetedBySpells = secondCard.CantBeTargetedBySpells;
			zombeast.Rush = secondCard.Rush;
			zombeast.Echo = secondCard.Echo;
			zombeast.Name = "Zombeast";
			zombeast.Text = secondCard.Text + "\n" + zombeast.Text;

			zombeast.Tags.Add(GameTag.MODULAR_ENTITY_PART_1, firstCard.AssetId);
			zombeast.Tags.Add(GameTag.MODULAR_ENTITY_PART_2, secondCard.AssetId);

			if (modifyTags)
			{
				zombeast.Tags[GameTag.ATK] += secondCard[GameTag.ATK];
				zombeast.Tags[GameTag.HEALTH] += secondCard[GameTag.HEALTH];
				zombeast.Tags[GameTag.COST] += secondCard[GameTag.COST];
				if (secondCard.Tags.ContainsKey(GameTag.TAUNT))
					zombeast.Tags[GameTag.TAUNT] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.POISONOUS))
					zombeast.Tags[GameTag.POISONOUS] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.STEALTH))
					zombeast.Tags[GameTag.STEALTH] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.WINDFURY))
					zombeast.Tags[GameTag.WINDFURY] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.CHARGE))
					zombeast.Tags[GameTag.CHARGE] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.LIFESTEAL))
					zombeast.Tags[GameTag.LIFESTEAL] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.CANT_BE_TARGETED_BY_HERO_POWERS))
					zombeast.Tags[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.CANT_BE_TARGETED_BY_SPELLS))
					zombeast.Tags[GameTag.CANT_BE_TARGETED_BY_SPELLS] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.RUSH))
					zombeast.Tags[GameTag.RUSH] = 1;
				if (secondCard.Tags.ContainsKey(GameTag.ECHO))
					zombeast.Tags[GameTag.ECHO] = 1;

				// OVERRIDECARDNAME: 46119
				// OVERRIDECARDTEXTBUILDER: 14
				// RARITY: INVALID;
			}

			return zombeast;
		}

		public static Card CreateKazakusPotion(in Card firstCard, in Card secondCard, in Card thirdCard, bool modifyTags)
		{
			// TODO: Use placeholders
			Card potion = firstCard.Clone();

			potion.Text = secondCard.Text + "\n" + thirdCard.Text;

			foreach (KeyValuePair<PlayReq, int> kvp in secondCard.PlayRequirements)
			{
				if (!potion.PlayRequirements.ContainsKey(kvp.Key))
					potion.PlayRequirements.Add(kvp.Key, kvp.Value);
			}

			potion.MustHaveTargetToPlay = secondCard.MustHaveTargetToPlay;
			potion.TargetingType = secondCard.TargetingType;
			potion.PlayAvailabilityPredicate = secondCard.PlayAvailabilityPredicate;

			return potion;
		}

		public static Card GetTigerCard(int value, bool modifyTags)
		{
			Card instance = Cards.FromId("TRL_309t");
			instance.ATK = value;
			instance.Health = value;
			if (modifyTags)
			{
				instance.Tags[GameTag.ATK] = value;
				instance.Tags[GameTag.HEALTH] = value;
			}

			return instance;
		}
	}
}
