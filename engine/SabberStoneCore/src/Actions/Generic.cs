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
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Loader;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Actions
{
	/// <summary>
	/// Container of game logic functionality, which is invoked by processing a selected option
	/// through <see cref="Game.Process(PlayerTask)"/>.
	/// </summary>
	public static partial class Generic
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	{
		public static Func<IPlayable, ICharacter, int, bool, int> DamageCharFunc
			=> (source, target, amount, applySpellDmg) =>
			{
				// Legacy hero powers can request spell scaling; their bonuses still belong to the hero-power branch.
				if (applySpellDmg && source is Spell spell)
				{
                    amount = SpellDamageRules.Amount(source, amount);
				}
				else if (source is HeroPower)
				{
					// TODO: Consider this part only when TGT or Rumble is loaded
					amount = DamageArithmetic.Add(amount, source.Controller.Hero.HeroPowerDamage);
					if (source.Controller.ControllerAuraEffects[GameTag.HERO_POWER_DOUBLE] > 0)
						amount = DamageArithmetic.Double(amount, source.Controller.ControllerAuraEffects[GameTag.HERO_POWER_DOUBLE]);
				}
				return target.TakeDamage(source, amount);
			};

		public static Func<Controller, int, bool> AddTempMana
			=> delegate (Controller c, int amount)
			{
				if (c.RemainingMana + amount > c[GameTag.MAXRESOURCES])
					c.TemporaryMana += c[GameTag.MAXRESOURCES] - c.RemainingMana;
				else
					c.TemporaryMana += amount;
				return true;
			};

		public static Func<Controller, int, bool, bool> ChangeManaCrystal
			=> delegate (Controller c, int amount, bool fill)
			{
				int baseMana = c.BaseMana;
				int total = baseMana + amount;
				if (total > c[GameTag.MAXRESOURCES])
				{
					c.Game.Log(LogLevel.INFO, BlockType.PLAY, "ChangeManaCrystal", !c.Game.Logging ? "" : $"{c.Name} is already capped in {c[GameTag.MAXRESOURCES]} mana crystals.");
					if (!fill)
						c.UsedMana += c[GameTag.MAXRESOURCES] - c.BaseMana;
					c.BaseMana = c[GameTag.MAXRESOURCES];

				}
				else if (total < 0)
				{
					c.Game.Log(LogLevel.INFO, BlockType.PLAY, "ChangeManaCrystal", !c.Game.Logging ? "" : $"{c.Name} is back to minimum of 0 mana crystals.");
					c.BaseMana = 0;
				}
				else
				{
					c.BaseMana = total;
					if (!fill)
						c.UsedMana += amount;
				}
				c.Game.Facts.BaseManaChanged(c,baseMana);

				return true;
			};

		public static Func<Controller, Minion, bool> ReturnToHandBlock
			=> delegate (Controller c, Minion minion)
			{
                var fromZone = minion.Zone?.Type ?? Zone.INVALID;
                DarkGifts.RemoveForBackwardMove(minion, fromZone, Zone.HAND);
				if (!RemoveFromZone.Invoke(c, minion))
					return false;

				// reset minion
				minion.Reset();

				c.Game.Log(LogLevel.INFO, BlockType.PLAY, "ReturnToHandBlock", !c.Game.Logging ? "" : $"{c.Name} gets {minion} returned.");

				using var historyReturn = c.Game.Facts.HandEntry(minion, HistoryHandEntryKind.Other, fromZone);
				if (!AddHandPhase.Invoke(c, minion))
					return false;

				return true;
			};

		public static Func<Controller, IPlayable, bool> RemoveFromZone
			=> delegate (Controller c, IPlayable playable)
			{
				playable.Zone.Remove(playable);
				return true;
			};

		public static Func<Controller, IPlayable, bool> AddHandPhase
			=> delegate (Controller c, IPlayable playable)
			{
				var entryKind = playable == null ? HistoryHandEntryKind.Other : c.Game.Facts.HandEntryKind(playable);
				if (c.HandZone.IsFull)
				{
					c.Game.Log(LogLevel.INFO, BlockType.PLAY, "AddHandPhase", !c.Game.Logging ? "" : $"Hand ist full. Card {playable} drawn is burnt to graveyard.");
					c.Game.Facts.CardBurned(playable);
					c.GraveyardZone.Add(playable);
					if (c.Game.History && (entryKind == HistoryHandEntryKind.Draw || entryKind == HistoryHandEntryKind.TurnStartDraw))
						c.Game.PowerHistory.BurnedCard(playable.Id);
					return false;
				}

				using var visual = c.Game.History && entryKind != HistoryHandEntryKind.Other
					? c.Game.PowerHistory.Visual(c.Game, entryKind == HistoryHandEntryKind.Draw
						? Presentation.VisualPhase.Draw : Presentation.VisualPhase.Create, target: playable) : null;
				// Retail ordinary draws SHOW HAND; automatic draws SHOW DECK before HAND and the TOPDECK trigger.
				if (c.Game.History && playable != null)
				{
					var show = PowerHistoryBuilder.ShowEntity(playable);
					if (show.Entity.Tags[GameTag.ZONE] == (int)Zone.DECK && playable.Power?.TopdeckTask == null)
						show.Entity.Tags[GameTag.ZONE] = (int)Zone.HAND;
					c.Game.PowerHistory.Add(show);
				}

				c.Game.Log(LogLevel.INFO, BlockType.PLAY, "AddHandPhase", !c.Game.Logging ? "" : $"adding to hand {playable}.");
				c.HandZone.Add(playable);

				return true;
			};

		public static Func<Controller, IPlayable, bool> DiscardBlock
			=> delegate (Controller c, IPlayable playable)
			{
				c.Game.TaskQueue.StartEvent();
				bool temporaryDiscard = TemporaryRules.IsTemporary(playable);
                c.Game.TriggerManager.OnDiscardTrigger(playable);
				IPlayable discard = c.HandZone.Remove(playable);
                if (temporaryDiscard) TemporaryRules.PublishDiscard(discard);
				c.Game.Log(LogLevel.INFO, BlockType.PLAY, "DiscardBlock", !c.Game.Logging ? "" : $"{discard} is beeing discarded.");
				c.GraveyardZone.Add(discard);
				DrawReplacementRules.OffboardDeathrattle(discard);

				c.DiscardedEntities.Add(discard.Id);
				c.LastCardDiscarded = discard.Id;
                // The ordinary tag setter omits 1005; retain each resolved discard for presentation, including repeats.
                if (c.Game.History) c.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(c.Id, GameTag.LAST_CARD_DISCARDED, discard.Id));
                c.Game.Facts.Discarded(discard);
				c.Game.ProcessTasks();
				c.Game.TaskQueue.EndEvent();
				return true;
			};

		public static Func<Controller, CardType, IPlayable> JoustBlock
			=> delegate (Controller c, CardType type)
			{
				IPlayable card, cardOp;
				{
					var rnd = c.Game.Random;
					var span = c.DeckZone.GetSpan();
					Span<int> buffer = stackalloc int[Math.Max(c.DeckZone.Count, c.Opponent.DeckZone.Count)];
					int k = 0;
					for (int i = 0; i < span.Length; i++)
					{
						if (span[i].Card.Type != type) continue;
						buffer[k++] = i;
					}
					card = k == 0 ? null : span[rnd.Next(k)];

					span = c.Opponent.DeckZone.GetSpan();
					k = 0;
					for (int i = 0; i < span.Length; i++)
					{
						if (span[i].Card.Type != type) continue;
						buffer[k++] = i;
					}
					cardOp = k == 0 ? null : span[rnd.Next(k)];
				}

				if (c.Game.History)
				{
					// TODO: should create new entities?

					var info = new List<int>();
					if (card != null)
					{
						info.Add(card.Id);
						card[GameTag.REVEALED] = 1;
					}
					if (cardOp != null)
					{
						info.Add(cardOp.Id);
						cardOp[GameTag.REVEALED] = 1;
					}
					if (card != null)
						c.Game.PowerHistory.Add(PowerHistoryBuilder.FullEntity(card));
					if (cardOp != null)
						c.Game.PowerHistory.Add(PowerHistoryBuilder.FullEntity(cardOp));
					if (card != null)
						c.Game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(card));
					if (cardOp != null)
						c.Game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(cardOp));
					var metaData = new PowerHistoryMetaData()
					{
						Type = MetaDataType.JOUST,

						Info = info
					};
					c.Game.PowerHistory.Add(metaData);
					if (card != null)
					{
						c.Game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(card));
						card[GameTag.REVEALED] = 0;
					}
					if (cardOp != null)
					{
						c.Game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(cardOp));
						cardOp[GameTag.REVEALED] = 0;
					}

					// if new entities are created, must be moved to setaside
				}

				bool success = (card?.Cost ?? -1) > (cardOp?.Cost ?? -1);
				c.Game.Log(LogLevel.INFO, BlockType.JOUST, "JoustBlock", !c.Game.Logging ? "" : $"{c.Name} initiatets joust with {card} {card?.Cost} vs. {cardOp?.Cost} {cardOp}, {(success ? "Won" : "Loose")} the joust.");

				// TODO shuffle deck .... or ... just let it be?
				return success ? card : null;
			};

		public static bool ShuffleIntoDeck(Controller c, IEntity sender, IPlayable playable, bool revealGenerated = false)
			{
				using var operation = c.Game.Facts.EnsureShuffle(sender?.Controller.Id ?? 0);
				if (c.DeckZone.IsFull)
				{
					c.Game.Log(LogLevel.INFO, BlockType.PLAY, "ShuffleIntoDeck",
						!c.Game.Logging ? "" : "Can't add a card to full deck.");
					return false;
				}

				c.Game.Log(LogLevel.INFO, BlockType.PLAY, "ShuffleIntoDeck", !c.Game.Logging ? "" : $"adding to deck {playable}.");

				// don't activate powers when shuffling cards back into the deck
				//c.DeckZone.Add(playable, c.DeckZone.Count == 0 ? -1 : Util.Random.Next(c.DeckZone.Count + 1));
				c.DeckZone.AddAtRandomPosition(playable);
				bool presented = c.Game.History && c.Game.PowerHistory.GeneratedShuffle(playable, revealGenerated);

				if (sender is IPlayable p && c.Game.TriggerManager.HasShuffleIntoDeckTrigger)
				{
					using var nestedShuffle = c.Game.Facts.SuspendShuffle();
					EventMetaData temp = c.Game.CurrentEventData;

					c.Game.CurrentEventData = new EventMetaData(p, playable);

					c.Game.TriggerManager.OnShuffleIntoDeckTrigger(playable);

					c.Game.CurrentEventData = temp;
				}

				// add hide entity 
				if (c.Game.History && !presented)
					c.Game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(playable));

				return true;
			}

		public static Func<Controller, Card, Minion, bool> TransformBlock
			=> delegate (Controller c, Card card, Minion oldMinion)
			{
				if (oldMinion?.Zone?.Type != Zone.PLAY || card.Type == CardType.LOCATION)
					return false;

				TransformRules.Intercept(oldMinion, card, out card);
                if (card == null) return false;

				if (!(Entity.FromCard(c, card) is Minion newMinion))
				{
					c.Game.Log(LogLevel.WARNING, BlockType.PLAY, "TransformBlock", !c.Game.Logging ? "" : $"missing final tranformation.");
					return false;
				}

				//oldMinion[GameTag.LINKED_ENTITY] = newMinion.Id;
				//newMinion[GameTag.LINKED_ENTITY] = oldMinion.Id;
				if (c.Game.CurrentEventData?.EventSource == oldMinion)
					c.Game.CurrentEventData.EventSource = newMinion;

				c.BoardZone.Replace(oldMinion, newMinion);

				c.Game.Log(LogLevel.INFO, BlockType.PLAY, "TransformBlock", !c.Game.Logging ? "" : $"{oldMinion} got transformed into {newMinion}.");
				return true;
			};

		/// <summary>
		/// Creates a new <see cref="Enchantment"/> and attaches it to the given target.
		/// </summary>
		/// <param name="g">The game context.</param>
		/// <param name="enchantmentCard">The base Card for the enchantment.</param>
		/// <param name="creator">The creator entity.</param>
		/// <param name="target">The target entity.</param>
		/// <param name="num1">ScriptTag1</param>
		/// <param name="num2">ScriptTag2</param>
		/// <param name="entityId">The entity ID to be stored in the enchantment. (e.g. carnivorous Cube)</param>
		public static void AddEnchantmentBlock(in Game g, in Card enchantmentCard, in IPlayable creator, IEntity target, int num1 = -1, int num2 = -1, int entityId = 0)
		{
			Power power = enchantmentCard.Power;
            using var statGain = StatGainRules.Begin(target, creator, enchantmentCard);
            // A fair-view simulation cannot see a hidden card, so its buff is unknowable (e.g. draw-then-buff).
            if (target is Unknown && g.HistoryFacts.IsProjected) return;
            // Location combat/stat auras are unsupported; trigger and Deathrattle attachments are valid.
            if (target is BoardObject location && (power.Aura != null || power.Enchant != null &&
                !(location.Zone == location.Controller.HandZone && Array.TrueForAll(power.Enchant.Effects,
                    effect => effect.Tag == GameTag.SHIFTING || effect.Tag == GameTag.COST)))) return;

			if (power.Enchant is OngoingEnchant &&
			    target is IPlayable entity &&
			    entity.OngoingEffect is OngoingEnchant ongoingEnchant)
			{	// Increment the count of existing OngoingEnchant
				ongoingEnchant.Count++;
				return;
			}

            bool spellPower = target is Spell && power.Enchant != null && Array.Exists(power.Enchant.Effects, effect => effect.Tag == GameTag.SPELLPOWER);
            int previousSpellPower = target[GameTag.SPELLPOWER];
            if (spellPower) CardStateBoundary.EnrollAttachments(target);
            using var handBuff = g.History ? g.PowerHistory.HandBuff(target) : null;
            using var boardBuff = g.History && (target.Zone?.Type == Zone.PLAY
                || target is Hero hero && hero == hero.Controller.Hero)
                ? g.PowerHistory.Visual(g, Presentation.VisualPhase.BoardBuff, creator, target) : null;
            int costEnchantmentId = 0;
            // Jailbird's retained discount needs its actor even without presentation history.
			if (g.History || spellPower || StatGainRules.RetainActor(enchantmentCard) || LifecycleRules.RetainActor(enchantmentCard) || BonusEffects.RetainActor(enchantmentCard) || target.Card.Shatter != null || target.Card.HandProgress != null || target.Card.Id == "CATA_615" || target.Card.Id == "CATA_615t" || power.Aura != null || power.Trigger != null || power.DeathrattleTask != null || enchantmentCard.Modular || enchantmentCard[GameTag.IS_NIGHTMARE_BONUS] == 1 || enchantmentCard.Id == "JAIL_453e" || enchantmentCard.Id == "DINO_407e2")
			{	// Create Enchantment instance Only when it is needed.
				// As an owner entity for Auras, Triggers or Deathrattle tasks.
				// We also maintain Modular (Magnetic) Enchantments for Kangor's Endless Army.
				Enchantment enchantment = Enchantment.GetInstance(creator.Controller, in creator, in target, in enchantmentCard, num1, num2,
					entityId > 0 && PayloadRules.UsesReferenceTag(enchantmentCard) ? entityId : 0);
                costEnchantmentId = enchantment.Id;

				power.Aura?.Activate(enchantment);
				power.Trigger?.Activate(enchantment);

				if (power.Enchant?.RemoveWhenPlayed ?? false)
					Enchant.RemoveWhenPlayedTrigger.Activate(enchantment);

				if (entityId > 0)
				{
					enchantment.CapturedCard = g.IdEntityDic[entityId].Card;
					if (g.Logging)
						g.Log(LogLevel.DEBUG, BlockType.POWER,
							"AddEnchantmentBlock", $"{g.IdEntityDic[entityId]} is captured in {enchantment}.");
				}
			}

			DarkGifts.TrackKeywordGrants(target, enchantmentCard);
			//	no indicator enchantment entities when History option is off
			power.Enchant?.ActivateBoundTo(target, num1, num2, costEnchantmentId, enchantmentCard[GameTag.TAG_ONE_TURN_EFFECT] == 1);
            if (spellPower && g.History && previousSpellPower != target[GameTag.SPELLPOWER])
                g.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id, GameTag.SPELLPOWER, target[GameTag.SPELLPOWER]));
		}

		public static Func<Controller, IPlayable, Card, bool, IPlayable> ChangeEntityBlock
			=> (c, p, newCard, removeEnchantments) => ChangeEntity(c, p, newCard, removeEnchantments);

        internal static IPlayable ChangeEntity(Controller c, IPlayable p, Card newCard, bool removeEnchantments,
            Minion dormantCopy = null, bool resetHeldState = false, bool intercept = true)
			{
                if (intercept) TransformRules.Intercept(p as Minion, newCard, out newCard);
                if (newCard == null) return p;
                // Aya also replaces Coins made in place; resolve before publishing CHANGE_ENTITY.
                if (p.Zone is HandZone) newCard = PayloadRules.CoinDefinition(p.Controller, newCard);
                if ((p is BoardObject || newCard.Type == CardType.LOCATION) &&
                    !(p.Zone is HandZone || p.Zone is DeckZone || p.Zone is BoardZone)) return p;
                // Cross-type board transforms need replacement rules; refuse before changing either entity.
                if (p.Zone is BoardZone && p.Card.Type != newCard.Type &&
                    (p is BoardObject || newCard.Type == CardType.LOCATION)) return p;
                Card oldCard=p.Card;
                DiscoverRealRules.ChangingDefinition(p);
                // Captured printing metadata belongs to the old identity, even when enchantments are retained.
                p.NativeTags.Remove(GameTag.CARDRACE);
                foreach (var tag in Card.AdditionalRaceTags.Keys) p.NativeTags.Remove(tag);
                // A transformation replaces a retained repeat target even when the definition and ID stay equal.
                if (p[RepeatRules.TargetWitness] != 0) p[RepeatRules.TargetWitness] = 0;
				c.Game.Log(LogLevel.VERBOSE, BlockType.TRIGGER, "ChangeEntityBlock",
					!c.Game.Logging ? "" : $"{p} is changed into {newCard}.");

				//if (!(p.Zone is HandZone hand))
				//	throw new InvalidOperationException($"{p} is not in Hand. ({p.Zone})");

                OldGodRules.Reset(p);
                PayloadRules.Clear(p);
                SwapRules.ChangingDefinition(p);
                if (p.Card.Shatter != null || newCard.Shatter != null) ShatterRules.ChangingDefinition(p, newCard);
                if (p.Card.Shatter != null || p.Card.HandProgress != null) CardStateBoundary.ChangingDefinition(p, newCard);

                if (p is Minion modern && DormantRules.HasState(modern) && p.Zone is BoardZone) DormantRules.LeavePlay(modern);
                bool rebasePrepare = removeEnchantments && (p[GameTag.PREPARE] != 0 || p[GameTag.PREPARED] != 0 || newCard[GameTag.PREPARE] != 0);
				if (removeEnchantments)
				{
                    CostRules.ResetManaNumbersForTransform(p, newCard);
                    PrepareRules.Reset(p);
					// 12.0 Game Mechanics Update: Clear all applied enchantments when shifting
					if (p.AppliedEnchantments != null)
						for (int i = p.AppliedEnchantments.Count - 1; i >= 0; i--)
							if (p.Zone is HandZone && (p.AppliedEnchantments[i].Card.Id == "GBL_999e" || p.AppliedEnchantments[i].Card.Id == "CATA_EVENT_001e")) continue;
                            else p.AppliedEnchantments[i].Remove();

					if (p is Minion m)
						m.ResetAttributes();
					else
						((Playable) p).ResetCost();
				}

                if (resetHeldState) CardStateBoundary.ResetHeldModifiers(p, newCard);
				p.ActivatedTrigger?.Remove();
				p.OngoingEffect?.Remove();

				HandZone hand = p.Zone as HandZone;
				BoardZone board = p.Zone as BoardZone;
				int id = p.Id;

				// Detach the target from Auras
				if (hand != null)
					hand.Auras.ForEach(a => a.DeApply(p));
				else if
					(board != null && p is Minion)
				{
					board.Auras.ForEach(a => a.DeApply(p));

					if (p.Card.Untouchable)
					{
						board.DecrementUntouchablesCount();
					}
				}


                if (HeraldRules.IsSoldier(p.Card))
                {
                    p[GameTag.TAG_SCRIPT_DATA_NUM_1] = newCard[GameTag.TAG_SCRIPT_DATA_NUM_1];
                    p[HeraldRules.RealizedFactor] = 0;
                }

                if (p is Minion sleeping && sleeping.IsDormant)
                {
                    sleeping.RemoveDormantCondition();
                    p[GameTag.DORMANT] = 0;
                    p[GameTag.DORMANT_VISUAL] = 0;
                    p[GameTag.UNTOUCHABLE] = 0;
                    p[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
                    sleeping.AttackableByRush = false;
                }

				if (p.Card.Type == newCard.Type)
				{
					if (c.Game.History)
					{
						EntityData differTags = new EntityData();
						Dictionary<GameTag, int> newTags = newCard.Tags;
						foreach (KeyValuePair<GameTag, int> item in p.Card.Tags)
							differTags.Add(item.Key,
								newTags.TryGetValue(item.Key, out int value)
									? value
									: 0);
						foreach (KeyValuePair<GameTag, int> item in newTags)
							if (!differTags.ContainsKey(item.Key))
								differTags.Add(item);

                        // Explicit grants survive retained-enchantment transforms; snapshot them before later mutations.
                        if (p is Minion retained && retained._modifiedTaunt.HasValue)
                            differTags[GameTag.TAUNT] = retained._modifiedTaunt.Value ? 1 : 0;

						c.Game.PowerHistory.Add(new PowerHistoryChangeEntity()
						{
							CardId = newCard.Id,
							Entity = new PowerHistoryEntity()
							{
								Id = p.Id,
								Name = "",
								Tags = differTags
							}
						});
					}


					c.Game.Facts.AdmitDefinition(newCard);
					p.Card = newCard;
					Playable pp = (Playable)p;
					if (pp._costManager != null)
						pp._modifiedCost = pp._costManager.EntityChanged(newCard.Cost);
				}
				else
				{
					Playable entity;
					switch (newCard.Type)
					{
                        case CardType.LOCATION:
                            entity = new BoardObject(c, newCard, p.NativeTags, id);
                            break;
						case CardType.MINION:
							entity = new Minion(c, newCard, p.NativeTags, id);
							break;
						case CardType.SPELL:
							entity = new Spell(c, newCard, p.NativeTags, id);
							break;
						case CardType.HERO:
							entity = new Hero(c, newCard, p.NativeTags, id);
							break;
						case CardType.WEAPON:
							entity = new Weapon(c, newCard, p.NativeTags, id);
							break;
						default:
							throw new ArgumentNullException();
					}

					if (c.Game.History)
					{
						EntityData differTags = new EntityData();
						Dictionary<GameTag, int> newTags = newCard.Tags;
						foreach (KeyValuePair<GameTag, int> item in p.Card.Tags)
							differTags.Add(item.Key,
								newTags.TryGetValue(item.Key, out int value)
									? value
									: 0);
						foreach (KeyValuePair<GameTag, int> item in newTags)
							if (!differTags.ContainsKey(item.Key))
								differTags.Add(item);

						c.Game.PowerHistory.Add(new PowerHistoryChangeEntity()
						{
							CardId = newCard.Id,
							Entity = new PowerHistoryEntity()
							{
								Id = p.Id,
								Name = "",
								Tags = differTags
							}
						});
					}

					if (hand != null)
                    {
                        if (TemporaryRules.IsTemporary(p)) TemporaryRules.TransferTransform(p, entity);
                        if (HandTimelineRules.IsBurning(p)) HandTimelineRules.TransferTransform(p, entity);
						hand.ChangeEntity(p, entity);
                    }
					else if
						(board != null && p is Minion oldMinion && entity is Minion newMinion)
						board.ChangeEntity(oldMinion, newMinion);
					else if (p.Zone is DeckZone deck)
						deck.ChangeEntity(p, entity);

					c.Game.IdEntityDic[id] = entity;

					Playable pp = (Playable)p;
					if (pp._costManager != null)
						entity._modifiedCost = pp._costManager.EntityChanged(newCard.Cost);
					entity._costManager = pp._costManager;
                    entity._costOverlay = pp._costOverlay;
					p = entity;
				}
                if (rebasePrepare)
                {
                    p[GameTag.PREPARE] = newCard[GameTag.PREPARE];
                    p[GameTag.DECK_ACTION_COST] = newCard[GameTag.DECK_ACTION_COST];
                }


				if (newCard.ChooseOne || EitherSideRules.Supports(newCard))
				{
					EntityData tags = null;
					if (c.Game.History || EitherSideRules.Supports(newCard))
					{
						tags = ChooseOneOptions.OptionData(c, id, newCard);
					}

					if (newCard.AssetId == 43310)
					{
						var chooseOnes = new IPlayable[4];
						chooseOnes[0] = Entity.FromCard(in c, Cards.FromId("TRL_343at1"), tags, c.SetasideZone);
						chooseOnes[1] = Entity.FromCard(in c, Cards.FromId("TRL_343ct1"), tags, c.SetasideZone);
						chooseOnes[2] = Entity.FromCard(in c, Cards.FromId("TRL_343dt1"), tags, c.SetasideZone);
						chooseOnes[3] = Entity.FromCard(in c, Cards.FromId("TRL_343bt1"), tags, c.SetasideZone);

						p.ChooseOnePlayables = chooseOnes;
					}
					else
					{
						if (p.ChooseOnePlayables == null)
							p.ChooseOnePlayables = new IPlayable[2];


						Card[] options = ChooseOneOptions.Resolve(newCard);
						p.ChooseOnePlayables[0] = Entity.FromCard(c, options[0], tags, c.SetasideZone);
						p.ChooseOnePlayables[1] = Entity.FromCard(c, options[1], tags, c.SetasideZone);
					}
				}

				//switch (p.Zone.Type)
				//{
				//	case Zone.HAND:
				//		p.Power?.Trigger?.Activate(p, TriggerActivation.HAND);
				//		if (p.Power?.Aura is AdaptiveCostEffect e)
				//			e.Activate((Playable)p);
				//		break;
				//	case Zone.DECK:
				//		p.Power?.Trigger?.Activate(p, TriggerActivation.DECK);
				//		break;
				//	case Zone.PLAY:
				//		BoardZone.ActivateAura((Minion) p);
				//		break;
				//}


				// Reapply auras
				if (hand != null)
				{
                    // Held transforms initialize after rebinding retained actors; ordinary changes initialize here.
                    if (!resetHeldState && p.Power?.HandEntryTask != null)
                    {
                        p[GameTag.TAG_SCRIPT_DATA_NUM_1] = newCard[GameTag.TAG_SCRIPT_DATA_NUM_1];
                        p[GameTag.TAG_SCRIPT_DATA_NUM_2] = newCard[GameTag.TAG_SCRIPT_DATA_NUM_2];
                        p.Power.HandEntryTask.Process(p.Game, p.Controller, p, null);
                    }
					p.Power?.Trigger?.Activate(p, TriggerActivation.HAND);
					if (p.Power?.Aura is AdaptiveCostEffect e)
						e.Activate((Playable) p);
					else if (p.Power?.Aura is HandAdjacencyCostAura adjacency)
						adjacency.Activate(p);
					hand.Auras.ForEach(a => a.EntityAdded(p));
                    CostRules.RefreshHandAuras(c.Game);
                    if (TemporaryRules.IsTemporary(p)) TemporaryRules.OnHandEntry(p);
				}
				else if (board != null && p is Minion m)
				{
                    m.BoardEntryTurn = c.Game.Turn;
                    if (removeEnchantments && dormantCopy == null)
                    {
                        // Readiness must use the new form, not a granted Charge/Rush on the old body.
                        m.ClearNativeKeywords();
                        m.AttackableByRush = false;
                        c.Game.RushMinions.Remove(m.Id);
                    }
                    if (dormantCopy != null) m.IsSilenced = dormantCopy.IsSilenced;
                    if (dormantCopy != null && DormantRules.HasState(dormantCopy))
                    {
                        m.IsSilenced = dormantCopy.IsSilenced;
                        DormantRules.CopyState(dormantCopy, m);
                    }
                    if (m.Card.Dormant != null || DormantRules.HasState(m)) DormantRules.Initialize(m);
					if (m.Controller == c.Game.CurrentPlayer)
					{
						if (!m.HasCharge)
						{
							if (m.IsRush)
							{
								m.IsExhausted = false;
								m.AttackableByRush = true;
								c.Game.RushMinions.Add(m.Id);
							}
							else
								m.IsExhausted = true;
						}
						else
							m.IsExhausted = false;
					}
					else
						m.IsExhausted = true;
					BoardZone.ActivateAura(m);
					board.Auras.ForEach(a => a.EntityAdded(p));
					board.AdjacentAuras.ForEach(a => a.BoardChanged = true);
				}
                else if (p is Weapon weapon && weapon.Controller.Hero.Weapon == weapon)
                {
                    weapon.Power?.Trigger?.Activate(weapon);
                    weapon.Power?.Aura?.Activate(weapon);
                }
				else if (p.Zone?.Type == Zone.DECK)
				{
					p.Power?.Trigger?.Activate(p, TriggerActivation.DECK);
				}

				// Not sure C'Thun from Shifter Zerus will have Proxy's buffs

				if (board != null && board.Controller.SecretZone.ModernQuests.Count != 0)
					QuestRules.FriendlyBoardChanged(board.Controller);

                if (hand != null)
                {
                    if (p.Card.HandProgress != null) HandProgressRules.OnHandEntry(p, p[HandProgressRules.Residence] == 0);
                    if (p.Card.Shatter != null) { p[ShatterRules.Residence]++; ShatterRules.AfterHandEntry(p); }
                    ShatterRules.StabilizeHand(c);
                    QuestRules.CombineHeldRewards(hand.Controller);
                }
                if (dormantCopy != null) LifecycleRules.CopyState(dormantCopy, p);
                else LifecycleRules.AdmitRecruit(p);
                c.Game.Facts.FormChanged(p,oldCard);
				return p;
			}

		public static void OverloadBlock(Controller controller, IPlayable source, bool history)
		{
			if (!source.Card.HasOverload)
				return;

			if (source is Spell s && s.IsCountered)
				return;

			if (history)
				controller.Game.PowerHistory.Add(
					PowerHistoryBuilder.BlockStart(BlockType.POWER, source.Id, "", 1, 0));

			int amount = source.Card.Overload;

			controller.OverloadOwed += amount;
			controller.OverloadThisGame += amount;
			controller.Game.TriggerManager.OnOverloadTrigger(source, amount);

			if (history)
				controller.Game.PowerHistory.Add(
					PowerHistoryBuilder.BlockEnd());
		}

		// Work in progress
		public static void RevealCardBlock(IPlayable source, IPlayable target)
		{
			Game game = source.Game;
			if (!game.History) return;

			game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.REVEAL_CARD, source.Id, "", 0, target.Id));
			target.NativeTags[GameTag.REVEALED] = 1;
			game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(target));
			game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(target));
			target[GameTag.REVEALED] = 0;
			game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
		}


	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
