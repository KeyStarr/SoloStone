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
using SabberStoneCore.Auras;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Actions
{
	public static partial class Generic
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
	{
		public static bool PlayCard(Controller c, IPlayable source, IPlayable target = null, int zonePosition = -1, int chooseOne = 0, bool skipPrePhase = false)
		{
			return PlayCardBlock.Invoke(c, source, target, zonePosition, chooseOne, skipPrePhase);
		}

		public static Func<Controller, IPlayable, IPlayable, int, int, bool, bool> PlayCardBlock
			=> delegate (Controller c, IPlayable source, IPlayable target, int zonePosition, int chooseOne, bool skipPrePhase)
			{
				if (EitherSideRules.Supports(source.Card) && !EitherSideRules.Valid(c, source, target, zonePosition, chooseOne)) return false;
				if (!ImbueRules.CanPlay(c, source.Card)) return false;
                // Options may be retained with skipPrePhase; revalidate the shared area before payment.
                if (source.Card.IsSecret && c.SecretZone.ModernQuests.Count != 0 && c.SecretZone.IsFull) return false;
                if (source.Card.IsQuest && (QuestDefinition.TryGet(source.Card.AssetId, out _) || c.SecretZone.ModernQuests.Count != 0)
                    && !QuestRules.CanInstall(c, source.Card)) return false;
                if (PrepareRules.BlocksPlay(source)) return false;
                if (PlayLockRules.BlocksPlay(source)) return false;
                if (target != null && !(target is ICharacter) &&
                    !LocationRules.TargetsLocations(source.Card)) return false;
                if (LocationRules.TargetsLocations(source.Card) &&
                    !LocationRules.ValidPlayTarget(c, source.Card, target)) return false;
				using var resolution = c.Game.Resolution.Sequence(ResolutionKind.PlayCard, c.Game, source);
				// Preplay Phase : check the given source is playable
				if (!skipPrePhase)
					if (!PrePlayPhase.Invoke(c, source, target, zonePosition, chooseOne))
						return false;

				Game game = c.Game;
				bool history = game.History;

                int playedCost = source.Cost;
                int repeatOrigin = RepeatRules.QualifiedSource(source, playedCost);
                var payment = CostRules.Quote(c, source);
                // Forced non-hand plays (skipPrePhase fixtures, Dark Gift PreparePlayTriggers) stay legal;
                // one-use cost rules match hand cards only, so they are never consumed here.
                if (!CostRules.CanPay(c, payment)) return false;

				// Start play block
				if (history)
					game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.PLAY, source.Id, "",
                        source.ChooseOne && chooseOne > 0 && !UsesChooseBoth(source) ? chooseOne - 1 : 0, target?.Id ?? 0));

				game.CurrentEventData = new EventMetaData(source, target);

				var historyEntry = new PlayHistoryEntry(in source, in target, in chooseOne);
                using var historyPayment = game.Facts.PlayPayment(source);
				// Pay Phase
				if (!CostRules.CommitPayment(c, source, payment))
					return false;
                KindredRules.CommitCostClause(source);

				bool echo = source.IsEcho;
                if (source.Power?.PowerTask is DecisionBattlecryTask or DecisionSpellTask) c[DecisionEcho] = echo ? 1 : 0;

				bool playedFromHand = source.Zone == c.HandZone;
                bool playedTemporary = TemporaryRules.IsTemporary(source);

				// remove from hand zone
				PayloadRules.CapturePlay(source);
                OutcastRules.Capture(source);
                CardSets.Client.Sets.DragonSoulShattered.Capture(source);
				if (!RemoveFromZone.Invoke(c, source))
                {
                    OutcastRules.Clear(source);
                    return false;
                }

                if (playedFromHand) RepeatRules.Capture(source, playedCost, repeatOrigin);
                DarkGifts.PreparePlayTriggers(source, playedFromHand);

                // The spell/minion phases open nested queues; settle committed hand facts before them.
                QuestRules.SettleHandCountChanges(c);

				c.NumCardsPlayedThisTurn++;
				c.LastCardPlayed = source.Id;

				// record played cards for effect of cards like Obsidian Shard and Lynessa Sunsorrow
				// or use graveyard instead with 'played' tag(or bool)?
				c.CardsPlayedThisTurn.Add(source.Card);
				c.PlayHistory.Add(historyEntry);
                CopyPlayHistory.Record(c, source);
				using var historyPlay = game.Facts.Play(source);
                if (playedFromHand) DiscoverRealRules.PlayCommitted(source);
                if (game.State == State.COMPLETE)
                {
                    c.GraveyardZone.Add(source);
                    FinishCardPlay(c, source, false);
                    return true;
                }

                PlayLockRules.CardPlayed(c);
                if (playedTemporary) QuestRules.TemporaryPlayed(c);

				//// show entity
				//if (c.Game.History)
				//	c.Game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(source));

				// target is beeing set onto this gametag
				if (target != null)
				{
					source.CardTarget = target.Id;
					Trigger.ValidateTriggers(game, source, SequenceType.Target);
				game.Resolution.CaptureValidation(SequenceType.Target);
				}


				Trigger.ValidateTriggers(game, source, SequenceType.PlayCard);
				game.Resolution.CaptureValidation(SequenceType.PlayCard);
				switch (source)
				{
					case BoardObject location:
                        PlayLocation(c, game, location, zonePosition);
                        break;
					case Hero hero:
						PlayHero.Invoke(c, game, hero, target as ICharacter, chooseOne);
						break;
					case Minion minion:
						PlayMinionCore(c, game, minion, target, zonePosition, chooseOne, playedFromHand);
						break;
					case Weapon weapon:
						PlayWeapon.Invoke(c, game, weapon, target as ICharacter, chooseOne);
						break;
					case Spell spell:
						PlaySpell.Invoke(c, game, spell, target as ICharacter, chooseOne);
						break;
				}

                if (source is Spell && source.Power?.PowerTask is DecisionSpellTask) c[DecisionEcho] = 0;
                if (HasDecisionPlay(c) || HasDecisionSpell(c))
                {
                    if (history && !HasDecisionSpell(c)) game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                    game.CurrentEventData = null;
                    return true;
                }
                FinishCardPlay(c, source, echo);

                return true;
            };

        private static void FinishCardPlay(Controller c, IPlayable source, bool echo)
        {
            PayloadRules.FinishPlay(c, source);
            OutcastRules.EndPrimary(source);
            source[RepeatRules.PlayedCost] = 0;
            var game = c.Game;
            bool history = game.History;
            game.Facts.PlayCompleted(source);
				if (echo && game.State != State.COMPLETE && !(source is Spell s && s.IsCountered))
				{
					var echoTags = new EntityData
					{
						{GameTag.GHOSTLY, 1}
					};
					IPlayable echoPlayable = Entity.FromCard(c, source.Card, echoTags, c.HandZone);
					echoPlayable[GameTag.DISPLAYED_CREATOR] = source.Id;

					c.Game.AuraUpdate();

					c.Game.GhostlyCards.Add(echoPlayable.Id);
                    QuestRules.SettleHandCountChanges(c);
				}

				if (!c.IsComboActive)
					c.IsComboActive = true;

				c.NumOptionsPlayedThisTurn++;

				if (history)
				{
					if (source[GameTag.GHOSTLY] == 1)
						source[GameTag.GHOSTLY] = 0;
					game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
				}

				game.CurrentEventData = null;
                DiscoverRealRules.PlayCompleted(source);

        }

		public static Func<Controller, IPlayable, IPlayable, int, int, bool> PrePlayPhase
			=> delegate (Controller c, IPlayable source, IPlayable target, int zonePosition, int chooseOne)
			{
				// can't play because we got already board full
				if ((source is Minion || source is BoardObject) && (EitherSideRules.Supports(source.Card) ? EitherSideRules.Destination(c, chooseOne).BoardZone.IsFull : c.BoardZone.IsFull))
				{
					c.Game.Log(LogLevel.WARNING, BlockType.ACTION, "PrePlayPhase", !c.Game.Logging? "":$"Board has already {c.BoardZone.MaxSize} minions.");
					return false;
				}

				// set choose one option
				IPlayable subSource = chooseOne > 0 && source[GameTag.CHOOSE_BOTH] == 0 && !EitherSideRules.Supports(source.Card) ? source.ChooseOnePlayables[chooseOne - 1] : source;

				// check if we can play this card and the target is valid
				if (!source.IsPlayableByPlayer || !subSource.IsPlayableByCardReq || !(LocationRules.TargetsLocations(subSource.Card)
                    ? LocationRules.ValidPlayTarget(c, subSource.Card, target)
                    : (target == null || target is ICharacter) && subSource.IsValidPlayTarget(target as ICharacter)))
				{
					return false;
				}

				return true;
			};

        public static Func<Controller, IPlayable, bool> PayPhase
            => (c, source) => CostRules.CommitPayment(c, source, CostRules.Quote(c, source));

        internal static void SpendMana(Controller payer,IPlayable source,int amount,HistoryPaymentKind? kind=null,bool preserveZeroWrites=false)
        {
            if(amount<0) throw new ArgumentOutOfRangeException(nameof(amount));
            if(amount>0 || preserveZeroWrites)
            {
                int temporary=Math.Min(payer.TemporaryMana,amount);
                if(temporary>0 || preserveZeroWrites) payer.TemporaryMana-=temporary;
                payer.UsedMana+=amount-temporary;
                payer.TotalManaSpentThisGame+=amount;
            }
            payer.Game.Facts.Paid(payer,source,amount,action:kind);
            CardSets.Client.Sets.Isiset.SpellManaPaid(payer, source, amount);
        }

		public static Func<Controller, Game, Hero, ICharacter, int, bool> PlayHero
			=> (c, game, hero, target, chooseOne) => PlayHeroCore(c, game, hero, target, chooseOne);

        internal static bool PlayHeroCore(Controller c, Game game, Hero hero, ICharacter target, int chooseOne,
            bool automatic = false, bool battlecry = true)
            {
				game.Log(LogLevel.INFO, BlockType.ACTION, "PlayHero", !game.Logging? "":$"{c.Name} plays Hero {hero} {(target != null ? "with target " + target : "to board")}.");


				Hero oldHero = c.Hero;
                int overdrawPortal = oldHero[(GameTag)4717];
                if (overdrawPortal != 0) oldHero[(GameTag)4717] = 0;
                if (hero.Card.Class == CardClass.NEUTRAL || hero.Card.Class == CardClass.INVALID) hero[GameTag.CLASS] = (int)c.HeroClass;
				hero[GameTag.ZONE] = (int)Zone.PLAY;
				// Native hero plays predict position 0, as spell plays do; retaining the hand slot leaves prediction history unmatched.
				hero.ZonePosition = -1;
				//hero[GameTag.LINKED_ENTITY] = c.Hero.Id;
				//hero[GameTag.HEALTH] = oldHero[GameTag.HEALTH];
				hero.BaseHealth = oldHero.BaseHealth;
				//hero[GameTag.DAMAGE] = oldHero[GameTag.DAMAGE];
				hero.Damage = oldHero.Damage;
				hero[GameTag.ARMOR] = oldHero[GameTag.ARMOR] + hero.Card[GameTag.ARMOR];
				hero.CarryAttackStateFrom(oldHero);

				c.SetasideZone.Add(oldHero);
				//oldHero[GameTag.REVEALED] = 1;
				//c[GameTag.HERO_ENTITY] = hero.Id;
				hero.Weapon = oldHero.Weapon;
				c.SetasideZone.Add(oldHero.HeroPower);
				hero.HeroPower = (HeroPower) Entity.FromCard(c, Cards.GetHeroPower(hero.Card[GameTag.HERO_POWER]));
				hero.HeroPower.Power?.Trigger?.Activate(hero.HeroPower);

				c.Hero = hero;
                if (overdrawPortal != 0) hero[(GameTag)4717] = overdrawPortal;
                void RebindHeroAura(IAura aura)
                {
                    if (aura is MultiAura multiple)
                        foreach (var child in multiple) RebindHeroAura(child);
                    if (aura is AdaptiveEffect adaptive) adaptive.RebindHero(oldHero, hero);
                    if (aura is Aura fixedAura && fixedAura.Type == AuraType.HERO
                        && fixedAura.Owner.Controller == c)
                    {
                        fixedAura.DeApply(oldHero);
                        fixedAura.EntityAdded(hero);
                    }
                }
                foreach (var aura in game.Auras.ToArray()) RebindHeroAura(aura);
				hero.Power?.Trigger?.Activate(hero);

				// - OnPlay Phase --> OnPlay Trigger (Illidan)
				//   (death processing, aura updates)
				if (!automatic)
                using (game.Resolution.Enter(ResolutionKind.OnPlay))
				{
					game.TaskQueue.StartEvent();
					OnPlayTrigger.Invoke(game, hero);
				}

				if (battlecry && !automatic && hero.Power?.PowerTask is DecisionBattlecryTask decision)
                {
                    BeginDecisionPlay(c,hero,target,decision);
                    return true;
                }

                // - BattleCry Phase --> Battle Cry Resolves
				//   (death processing, aura updates)
				if (battlecry)
                using (game.Resolution.Enter(ResolutionKind.Battlecry))
				{
					game.TaskQueue.StartEvent();
					hero.ActivateTask(PowerActivation.POWER, target, chooseOne);
					// check if [LOE_077] Brann Bronzebeard aura is active
					if (c.ExtraBattlecry)
					{
						hero.ActivateTask(PowerActivation.POWER, target);
					}
					if (hero.HeroPower.IsPassiveHeroPower)  // Valeera, ad hoc for now; Maybe revisit here for Bosses
						hero.HeroPower.ActivateTask();
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

                if (!battlecry && hero.HeroPower.IsPassiveHeroPower)
                {
                    hero.HeroPower.ActivateTask();
                    game.ProcessTasks();
                }
                if (automatic) game.DeathProcessingAndAuraUpdate();
                else FinishHeroPlay(c,game,hero);
                return true;
            }

        private static void FinishHeroPlay(Controller c,Game game,Hero hero)
        {
				game.DeathProcessingAndAuraUpdate();

				// - After Play Phase --> After play Trigger / Secrets (Mirror Entity)
				//   (death processing, aura updates)
				//hero.JustPlayed = false;
				using (game.Resolution.Enter(ResolutionKind.AfterPlay))
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnAfterPlayCardTrigger(hero);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

				game.DeathProcessingAndAuraUpdate();

        }

        public static void PlayLocation(Controller controller, Game game, BoardObject location, int position, bool automatic = false)
        {
            controller.BoardZone.Add(location, position);
            // Uldir starts a fresh rune latch; a new Trigger subtype would change the frozen definition codec's type keys.
            if (location.Card.Id == "BE_780") location.Location = new LocationState { ConditionLocked = true };
            location[GameTag.HEALTH] = location.Card.Health;
            location[GameTag.DAMAGE] = 0;
            location.IsExhausted = false;
            location[GameTag.EXHAUSTED] = 0;
            location[GameTag.LOCATION_ACTION_COOLDOWN] = 0;
            if (!automatic)
            using (game.Resolution.Enter(ResolutionKind.OnPlay))
            {
                game.TaskQueue.StartEvent();
                OnPlayTrigger(game, location);
            }
            if (location.Zone == controller.BoardZone)
            {
                location.Power?.Trigger?.Activate(location);
                location.Power?.Aura?.Activate(location);
            }
            if (game.History)
            {
                game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, location.Id, "", 0, 0));
                game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }
            if (!automatic)
            using (game.Resolution.Enter(ResolutionKind.AfterPlay))
            {
                game.TaskQueue.StartEvent();
                game.TriggerManager.OnAfterPlayCardTrigger(location);
                game.ProcessTasks();
                game.TaskQueue.EndEvent();
            }
            game.DeathProcessingAndAuraUpdate();
        }

		public static Func<Controller, Game, Minion, IPlayable, int, int, bool> PlayMinion
            => (c, game, minion, target, zonePosition, chooseOne) =>
                PlayMinionCore(c, game, minion, target, zonePosition, chooseOne, false);

        private static bool PlayMinionCore(Controller c, Game game, Minion minion, IPlayable target,
            int zonePosition, int chooseOne, bool playedFromHand)
            {
				using var resolution = game.Resolution.Sequence(ResolutionKind.PlayMinion, game, minion);
				Trigger.ValidateTriggers(game, minion, SequenceType.PlayMinion);
				game.Resolution.CaptureValidation(SequenceType.PlayMinion);

				game.Log(LogLevel.INFO, BlockType.ACTION, "PlayMinion", !game.Logging? "":$"{c.Name} plays Minion {minion} {(target != null ? "with target " + target : "to board")} " +
						 $"{(zonePosition > -1 ? "position " + zonePosition : "")}.");

				c.NumMinionsPlayedThisTurn++;
				if (EitherSideRules.Supports(minion.Card)) EitherSideRules.Place(c, minion, zonePosition, chooseOne);
                else c.BoardZone.Add(minion, zonePosition, applyEntrySetters: true);
				ColossalRules.OnBodyEntered(minion);
                QuestRules.BeforeMinionBattlecry(c, minion);

				// - PreSummon Phase --> PreSummon Phase Trigger (Tidecaller)
				//   (death processing, aura updates)
				// not Implemented

				// - OnPlay Phase --> OnPlay Trigger (Illidan)
				//   (death processing, aura updates)
				using (game.Resolution.Enter(ResolutionKind.OnPlay))
				{
					game.TaskQueue.StartEvent();
                    if (playedFromHand) ImbueRules.RecordUndeadPlay(c, minion);
					game.TriggerManager.OnPlayMinionTrigger(minion);
					OnPlayTrigger.Invoke(game, minion);
				}

				// - Summon Resolution Step (Work in Process)
				using (game.Resolution.Enter(ResolutionKind.SummonOn))
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnSummonTrigger(minion);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

				// Noggenfogger here
				if (target != null)
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnTargetTrigger(minion);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
					if (minion.CardTarget != target.Id)
						target = game.IdEntityDic[minion.CardTarget];
				}

                if (minion.Power?.PowerTask is DecisionBattlecryTask decision)
                {
                    BeginDecisionPlay(c, minion, target as ICharacter, decision);
                    return true;
                }

				// - BattleCry Phase --> Battle Cry Resolves
				//   (death processing, aura updates)
				using (game.Resolution.Enter(ResolutionKind.Battlecry))
				{
					game.TaskQueue.StartEvent();
                    if (minion.Card.Id == "TLC_522")
                    {
                        // Opu's Combo is an additional effect, not the legacy replacement branch.
                        int repeats = DarkGifts.DoublesBattlecry(minion) || c.ExtraBattlecry ? 2 : 1;
                        for (int i = 0; i < repeats && game.State != State.COMPLETE; i++)
                        {
                            minion.ActivateTask(PowerActivation.POWER, target);
                            game.ProcessTasks();
                            game.DeathProcessingAndAuraUpdate();
                        }
                        if (c.IsComboActive && game.State != State.COMPLETE)
                        {
                            game.Facts.ComboActivated(minion);
                            minion.ActivateTask(PowerActivation.COMBO, target);
                            if (c.ControllerAuraEffects[GameTag.EXTRA_MINION_BATTLECRIES_BASE] == 1)
                                minion.ActivateTask(PowerActivation.COMBO, target);
                        }
                    }
                    else if (DarkGifts.DoublesBattlecry(minion))
                    {
                        // Lean dg14b: double the selected branch once, including Combo/Choose One.
                        var activation = minion.Combo && c.IsComboActive ? PowerActivation.COMBO : PowerActivation.POWER;
                        if(activation==PowerActivation.COMBO) game.Facts.ComboActivated(minion);
                        minion.ActivateTask(activation, target, chooseOne);
                        minion.ActivateTask(activation, target, chooseOne);
                    }
                    else
                    {
                        if (minion.Combo && c.IsComboActive)
                        {
                            game.Facts.ComboActivated(minion);
                            minion.ActivateTask(PowerActivation.COMBO, target);
                            if (c.ControllerAuraEffects[GameTag.EXTRA_MINION_BATTLECRIES_BASE] == 1)
                                minion.ActivateTask(PowerActivation.COMBO, target);
                        }
                        else
                            minion.ActivateTask(PowerActivation.POWER, target, chooseOne);

                        // check if [LOE_077] Brann Bronzebeard aura is active
                        if ((c.ExtraBattlecry || CardSets.Client.Sets.MugZee.DoubleBattlecry(minion)) && minion.HasBattleCry)
                        {
                            minion.ActivateTask(PowerActivation.POWER, target, chooseOne);
                        }
                    }

					OverloadBlock(EitherSideRules.Supports(minion.Card) ? minion.Controller : c, minion, game.History);

					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

                FinishMinionPlay(c, game, minion);
                return true;
            }

        private static void FinishMinionPlay(Controller c, Game game, Minion minion)
        {
                // Primary power is over (a suspended registered effect parks it for its own resume).
                OutcastRules.EndPrimary(minion);
				game.DeathProcessingAndAuraUpdate();

				minion = (Minion)game.CurrentEventData.EventSource;

				// - After Play Phase --> After play Trigger / Secrets (Mirror Entity)
				//   (death processing, aura updates)
				//minion.JustPlayed = false;
				using (game.Resolution.Enter(ResolutionKind.AfterPlay))
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnAfterPlayMinionTrigger(minion);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

				// - After Summon Phase --> After Summon Trigger
				//   (death processing, aura updates)
				using (game.Resolution.Enter(ResolutionKind.AfterPlay))
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnAfterPlayCardTrigger(minion);
					AfterSummonTrigger.Invoke(game, minion, null);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

				game.DeathProcessingAndAuraUpdate();

				if (minion.IsRace(Race.ELEMENTAL))
					c.NumElementalsPlayedThisTurn++;
				if (minion.IsRace(Race.MURLOC))
					c.NumMurlocsPlayedThisGame++;

        }

		public static Func<Controller, Game, Spell, ICharacter, int, bool> PlaySpell
			=> delegate (Controller c, Game game, Spell spell, ICharacter target, int chooseOne)
			{
				using var resolution = game.Resolution.Sequence(ResolutionKind.SpellCast, game, spell);
                CompositeLocationRules.ApplyNextSpell(spell);
                var handProgress = HandProgressRules.CaptureCast(spell);
				using var historyCast = game.Facts.Cast(spell, HistoryCastKind.Player);
				Trigger.ValidateTriggers(game, spell, SequenceType.PlaySpell);
				game.Resolution.CaptureValidation(SequenceType.PlaySpell);

				if (game.History)
				{
					if (spell.IsSecret || spell.IsQuest)
						spell[GameTag.ZONE] = (int)Zone.SECRET;
					else
						spell[GameTag.ZONE] = (int)Zone.PLAY;

                    RepeatRules.RevealMarks(spell);
				}

				// Native spell plays predict position 0; retaining the hand slot leaves prediction history unmatched.
				if (!spell.IsSecret && !spell.IsQuest) spell.ZonePosition = -1;

				// - OnPlay Phase --> OnPlay Trigger (Illidan)
				//   (death processing, aura updates)
				using (game.Resolution.Enter(ResolutionKind.OnPlay))
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnCastSpellTrigger(spell);
					OnPlayTrigger.Invoke(game, spell);
				}

				game.Log(LogLevel.INFO, BlockType.ACTION, "PlaySpell", !game.Logging? "":$"{c.Name} plays Spell {spell} {(target != null ? "with target " + target.Card : "to board")}.");

				// check the spell is countered
				if (spell.IsCountered)
				{
					OutcastRules.Clear(spell);
                    CardSets.Client.Sets.DragonSoulShattered.Finish(spell);
                    RepeatRules.Clear(spell);
					game.Log(LogLevel.INFO, BlockType.ACTION, "PlaySpell", !game.Logging ? "" : $"Spell {spell} has been countred.");
					historyCast.Complete(HistoryCastOutcome.Countered);
                    HandProgressRules.CompleteCast(c, handProgress, HistoryCastKind.Player, HistoryCastOutcome.Countered);
					RetireResolvedSpell(c, spell);
				}
				else
				{
					game.Facts.SpellBodyStarted(spell);
					// check Spellbender and Mayor Noggenfogger
					if (target != null)
					{
						game.TaskQueue.StartEvent();
						int temp = game.CurrentEventData.EventNumber;
						game.CurrentEventData.EventNumber = chooseOne;
						game.TriggerManager.OnTargetTrigger(spell);
						game.ProcessTasks();
						game.CurrentEventData.EventNumber = temp;
						game.TaskQueue.EndEvent();
						if (target.Id != spell.CardTarget)
						{
							target = (ICharacter)spell.Game.IdEntityDic[spell.CardTarget];
							game.Log(LogLevel.DEBUG, BlockType.ACTION, "PlaySpell", !game.Logging ? "" : $"trigger Spellbender Phase. Target of {spell} is changed to {target}.");
						}
					}

                    var recipe = (spell.Power?.PowerTask as DecisionSpellTask)?.Recipe;
                    if (recipe != null || handProgress.Length != 0 || !spell.IsSecret && !spell.IsQuest)
                    {
                        // Dispatch the actual power (including Combo/Choose One), then wait for every choice.
                        BeginDecisionSpell(c, spell, target, chooseOne, handProgress.Length != 0 ? null : recipe, historyCast, handProgress);
                        return true;
                    }

					historyCast.AllowBodyClaim = true;
					ResolveSpell(c, game, spell, target, chooseOne, true);
					historyCast.Complete();
                    // A duplicate Secret/Quest cannot install twice; its repeated attempt still has cast provenance.
                    if (spell[RepeatRules.Remaining] > 0)
                    {
                        using var repeat = game.Facts.Cast(spell, HistoryCastKind.Repeat);
                        repeat.Complete();
                    }
                    RepeatRules.RunPayloads(spell);
                    RepeatRules.Clear(spell);
                    HandProgressRules.CompleteCast(c, handProgress, HistoryCastKind.Player, HistoryCastOutcome.Completed);
					// Before after-cast recasts (Voraxx/Electra) of this entity; a suspended effect parks it.
					OutcastRules.EndPrimary(spell);
					game.DeathProcessingAndAuraUpdate();
				}
				
                FinishSpellPlay(c, game, spell);
                return true;
            };

        private static void FinishSpellPlay(Controller c, Game game, Spell spell)
        {
				// trigger After Play Phase
				game.Log(LogLevel.DEBUG, BlockType.ACTION, "PlaySpell", !game.Logging? "":"trigger After Play Phase");
				if (game.State != State.COMPLETE)
				using (game.Resolution.Enter(ResolutionKind.AfterPlay))
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnAfterCastTrigger(spell);
					game.TriggerManager.OnAfterPlayCardTrigger(spell);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

				c.NumSpellsPlayedThisGame++;
				if (spell.IsSecret)
					c.NumSecretsPlayedThisGame++;

				game.DeathProcessingAndAuraUpdate();
        }

		public static Func<Controller, Game, Weapon, ICharacter, int, bool> PlayWeapon
			=> delegate (Controller c, Game game, Weapon weapon, ICharacter target, int chooseOne)
			{
				game.Log(LogLevel.INFO, BlockType.ACTION, "PlayWeapon", !game.Logging ? "" : $"{c.Hero} gets Weapon {c.Hero.Weapon}.");

				//c.Hero.AddWeapon(weapon);

				if (game.History)
					weapon[GameTag.ZONE] = (int) Zone.PLAY;

				// - OnPlay Phase --> OnPlay Trigger (Illidan)
				//   (death processing, aura updates)
				using (game.Resolution.Enter(ResolutionKind.OnPlay))
				{
					game.TaskQueue.StartEvent();
					OnPlayTrigger.Invoke(game, weapon);
				}

				// not sure... need some test
				weapon.Card.Power?.Aura?.Activate(weapon);
				weapon.Card.Power?.Trigger?.Activate(weapon);


				if (target != null)
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnTargetTrigger(weapon);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
					if (target.Id != weapon.CardTarget)
						target = (ICharacter) weapon.Game.IdEntityDic[weapon.CardTarget];
				}

                if (weapon.Power?.PowerTask is DecisionBattlecryTask decision)
                {
                    BeginDecisionPlay(c, weapon, target, decision);
                    return true;
                }

				OverloadBlock(c, weapon, game.History);

				// - Equipping Phase --> Resolve Battlecry, OnDeathTrigger
				// activate battlecry; normal power dispatch owns its activation block
				using (game.Resolution.Enter(ResolutionKind.Battlecry))
				{
					game.TaskQueue.StartEvent();
					weapon.ActivateTask(PowerActivation.POWER, target, chooseOne);
					if (c.ExtraBattlecry && weapon.Card[GameTag.BATTLECRY] == 1)
						weapon.ActivateTask(PowerActivation.POWER, target, chooseOne);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

                FinishWeaponPlay(c, game, weapon);
                return true;
            };

        private static void FinishWeaponPlay(Controller c, Game game, Weapon weapon)
        {
				// equip new weapon here
				game.TaskQueue.StartEvent();
				// destroy old weapon
				// equip new weapon
				// weapon's deathrattle task queues up here
				c.Hero.AddWeapon(weapon);
                if (weapon.ActivatedTrigger == null) weapon.Power?.Trigger?.Activate(weapon);
				game.ProcessTasks();
				game.TaskQueue.EndEvent();

				// deathprocessing;
				game.DeathProcessingAndAuraUpdate();

				// trigger After Play Phase
				game.Log(LogLevel.DEBUG, BlockType.ACTION, "PlayWeapon", !game.Logging? "":"trigger After Play Phase");
				using (game.Resolution.Enter(ResolutionKind.AfterPlay))
				{
					game.TaskQueue.StartEvent();
					game.TriggerManager.OnAfterPlayCardTrigger(weapon);
					game.ProcessTasks();
					game.TaskQueue.EndEvent();
				}

				game.DeathProcessingAndAuraUpdate();

				c.NumWeaponsPlayedThisGame++;
        }

		private static Action<Game, IPlayable> OnPlayTrigger
			=> delegate (Game game, IPlayable playable)
			{
				//playable.JustPlayed = true;
				game.TriggerManager.OnPlayCardTrigger(playable);
				game.ProcessTasks();
				game.TaskQueue.EndEvent();

				game.DeathProcessingAndAuraUpdate();
			};
	}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
