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
using SabberStoneCore.Model.Events;
using SabberStoneCore.Kettle;
using System.Text;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static partial class Generic
    {
        public const GameTag RetainedSupplier = (GameTag)32119;
        private const GameTag RetainedCard = (GameTag)32120, RetainedWaiting = (GameTag)32121;
        private static bool SupplierActive(IPlayable spell) => spell[RetainedSupplier] != 0
            && spell.Game.IdEntityDic.TryGetValue(spell[RetainedSupplier], out var entity)
            && entity is Minion source && source.Card.Id == "TLC_241" && source.Controller == spell.Controller
            && source.Zone == source.Controller.BoardZone && !source.IsSilenced && !source.IsDormant && !source.ToBeDestroyed;

        internal static void RefreshRetainedSpells(Game game)
        {
            for (int side = 0; side < 2; side++)
            {
                var owner = side == 0 ? game.Player1 : game.Player2;
                // Snapshots (lazily, usually empty) before the loops mutate zones; no LINQ chain per call.
                List<IPlayable> retained = null;
                CollectRetained(owner.HandZone, false, ref retained);
                CollectRetained(owner.DeckZone, false, ref retained);
                CollectRetained(owner.SetasideZone, true, ref retained);
                if (retained != null) foreach (var spell in retained)
                {
                    if (SupplierActive(spell)) continue;
                    spell.NativeTags.Remove(RetainedWaiting);
                    spell[ShatterRules.PrivateHandOwner] = owner.PlayerId;
                    CardStateBoundary.EnrollAttachments(spell);
                    spell[CardStateBoundary.ReplacedHiddenZone] = (int)spell.Zone.Type;
                    owner.SetasideZone.Add(spell.Zone.Remove(spell));
                    spell[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
                    if (game.History) game.PowerHistory.Add(new PowerHistoryHideEntity { EntityID = spell.Id, Zone = Zone.REMOVEDFROMGAME });
                }
                List<Minion> sources = null;
                var board = owner.BoardZone;
                for (int i = 0; i < board.Count; i++)
                {
                    var m = board[i];
                    if (m.Card.Id == "TLC_241" && !m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed)
                        (sources ??= new List<Minion>()).Add(m);
                }
                if (sources != null) foreach (var source in sources)
                {
                    if (source[RetainedCard] != 0 && game.IdEntityDic.TryGetValue(source[RetainedCard], out var linked)
                        && linked[RetainedSupplier] == source.Id && linked.Controller == owner
                        && linked[GameTag.ZONE] != (int)Zone.REMOVEDFROMGAME)
                    {
                        if (linked[RetainedWaiting] != 0 && !owner.HandZone.IsFull)
                        {
                            linked.NativeTags.Remove(RetainedWaiting);
                            owner.HandZone.Add(linked.Zone.Remove(linked));
                        }
                        continue;
                    }
                    if (owner.HandZone.IsFull) continue;
                    var spell = Entity.FromCard(owner, Cards.FromId("TLC_241t"), new EntityData {
                        [GameTag.ZONE] = (int)Zone.HAND, [GameTag.CREATOR] = source.Id,
                        [GameTag.DISPLAYED_CREATOR] = source.Id, [RetainedSupplier] = source.Id });
                    source[RetainedCard] = spell.Id;
                    owner.HandZone.Add(spell);
                }
            }
        }

        private static void CollectRetained(Zone<IPlayable> zone, bool waitingOnly, ref List<IPlayable> found)
        {
            for (int i = 0; i < zone.Count; i++)
            {
                var c = zone[i];
                if (waitingOnly && c[RetainedWaiting] == 0) continue;
                if (c[RetainedSupplier] != 0) (found ??= new List<IPlayable>()).Add(c);
            }
        }

        internal static void RetireResolvedSpell(Controller owner, Spell spell)
        {
            if (SupplierActive(spell))
            {
                if (spell.Zone != null) spell.Zone.Remove(spell);
                spell.IsCountered = false;
                // shortcut: overflow waits for space; replace when retail timing is pinned.
                if (owner.HandZone.IsFull) { spell[RetainedWaiting] = 1; owner.SetasideZone.Add(spell); }
                else owner.HandZone.Add(spell);
            }
            else owner.GraveyardZone.Add(spell);
        }

        internal static IPlayable SpellTargetingProfile(Spell spell, int chooseOne) =>
            spell.ChooseOne && !UsesChooseBoth(spell) && chooseOne > 0
                ? spell.ChooseOnePlayables[chooseOne - 1] : spell;

	    public static Action<Controller, Game, Spell, ICharacter, int> CastSpell
		    => (c, game, spell, target, chooseOne) => CastAutomaticSpell(c, game, spell, target, chooseOne);

        internal static void CastAutomaticSpell(Controller c, Game game, Spell spell, ICharacter target, int chooseOne,
            bool lockedTarget = false)
            {
                int previous = c[AutomaticDecisionTag];
                // Standalone reactivation keeps its resumable API; effect/choice callers must finish inline.
                if (game.Resolution.Depth != 0 || c.Choice != null) c[AutomaticDecisionTag] = 1;
                try { ResolveSpell(c, game, spell, target, chooseOne, true, automatic: true, lockedTarget: lockedTarget); }
                finally { c[AutomaticDecisionTag] = previous; }
            }

		internal static void ResolveSpellOption(Controller c, Game game, Spell spell)
			=> ResolveSpell(c, game, spell, null, 0, false);

		private static void ResolveSpell(Controller c, Game game, Spell spell, ICharacter target, int chooseOne, bool isCast, bool automatic = false,
            bool lockedTarget = false, bool repeat = false)
		{
                if (automatic || repeat) CostRules.ClearAutomaticQualification(spell);
                CardSets.Client.Sets.DragonSoulShattered.Reserve(spell);
				if (!repeat && !HeraldRules.CanCast(c, spell, target)) return;
				if (!repeat && !ImbueRules.CanCast(c, spell, target)) return;
                if (isCast && !repeat) CompositeLocationRules.ApplyNextSpell(spell);
                // Opened first, closed last: the attempt, its fizzle/failed-Secret reveal and its retirement are inside.
                using var effectPlay = automatic ? EffectPlayRules.Open(game, spell, target) : null;
				using var historyOption = !isCast ? game.Facts.SpellOption(spell) : null;
				using var historyCast = isCast ? game.Facts.Cast(spell, HistoryCastKind.Effect, claimBody: true) : null;
				using var sequence = game.Resolution.Current.Kind == ResolutionKind.SpellCast && game.Resolution.Current.SourceId == spell.Id
                    ? default : game.Resolution.Sequence(ResolutionKind.SpellCast, game, spell);
				using var resolution = game.Resolution.Enter(ResolutionKind.SpellText, spell.Id);
				if (game.Logging)
				{
					var sb = new StringBuilder();
					sb.Append("Spell ");
					sb.Append(spell);
					sb.Append(" is cast");
					if (target != null)
					{
						sb.Append(" to ");
						sb.Append(target);
					}
					sb.Append(".");
					if (chooseOne > 0)
						sb.Append($"(ChooseOne: {{{chooseOne}}}.)");

					game.Log(LogLevel.INFO, BlockType.PLAY, "CastSpell", sb.ToString());
				}

                bool revealFromHiddenZone = automatic && spell.IsSecret && (spell.Zone == c.DeckZone || spell.Zone == c.HandZone);
                // Hand-play completion owns PLAY; automatic deck casts consume the original before any draw.
                if (automatic && spell.Zone != null && spell.Zone != c.BoardZone && spell.Zone != c.SecretZone)
                    RemoveFromZone(c, spell);
                bool failedSecret = spell.IsSecret && (c.SecretZone.IsFull || c.SecretZone.Any(p => p.Card.Id == spell.Card.Id));
                bool failedQuest = spell.IsQuest && !QuestRules.CanInstall(c, spell.Card);
                var targeting = SpellTargetingProfile(spell, chooseOne);
                if ((automatic || repeat) && (failedSecret || failedQuest
                    || automatic && !lockedTarget && targeting.Card.MustHaveTargetToPlay && !targeting.HasAnyValidPlayTargets))
                {
                    OverloadBlock(c, spell, game.History);
                    // DECK/HAND exits already freeze a SHOW snapshot in that zone (Entity ZONE setter), the
                    // native burned-card order (SHOW ZONE=DECK, then ZONE=GRAVEYARD). Only other zones need this reveal.
                    if (failedQuest && spell.Zone != c.SecretZone && game.History
                        && spell[GameTag.ZONE] != (int)Zone.DECK && spell[GameTag.ZONE] != (int)Zone.HAND)
                    {
                        var reveal = PowerHistoryBuilder.ShowEntity(spell);
                        reveal.Entity.Tags[GameTag.ZONE] = (int)Zone.GRAVEYARD;
                        game.PowerHistory.Add(reveal);
                    }
                    if (spell.Zone != c.SecretZone) RetireResolvedSpell(c, spell);
                    if (failedSecret && spell.Zone != c.SecretZone && game.History) game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(spell));
                    historyCast?.Complete();
                    return;
                }
                // w08-repeat-target: an illegal locked target loses only the targeted portion, never the draw tail.
                if (lockedTarget && target != null && !targeting.ValidPlayTargets.Any(t => t.Id == target.Id && targeting.IsValidPlayTarget(t)))
                    target = null;

				game.TaskQueue.StartEvent();
			    if (spell.IsQuest && QuestDefinition.TryGet(spell.Card.AssetId, out _))
                {
                    if (!QuestRules.Install(spell)) RetireResolvedSpell(c, spell);
                }
                else if (spell.IsSecret || spell.IsQuest)
			    {
				    spell.Power.Trigger?.Activate(spell);
				    c.SecretZone.Add(spell);
				    spell.IsExhausted = true;
                    if (revealFromHiddenZone && game.History) game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(spell));
			    }
			    else
			    {
				    //spell.Power?.Trigger?.Activate(spell);
				    if (spell.Power?.Trigger != null && spell.ActivatedTrigger == null)
					    spell.Power.Trigger.Activate(spell);
				    if (!AuraSpellRules.IsActive(spell) && !ObjectiveRules.IsActive(spell)) spell.Power?.Aura?.Activate(spell);

				    if (spell.Power?.PowerTask is DecisionSpellTask decision && !IsTrackedSpellBody(game, spell))
                        game.TaskQueue.Execute(decision, c, spell, target,
                            subOption: spell.ChooseOne && chooseOne > 0 && !UsesChooseBoth(spell) ? chooseOne - 1 : -1);
                    else if (repeat && spell.ChooseOne && (chooseOne > 0 || spell[RepeatRules.ChooseBoth] != 0) &&
                        spell.Card.Id != "EX1_165" && spell.Card.Id != "BRM_010" && spell.Card.Id != "AT_042" &&
                        spell.Card.Id != "UNG_101" && spell.Card.Id != "ICC_051" && spell.Card.Id != "ICC_047")
                    {
                        if (spell[RepeatRules.ChooseBoth] != 0)
                            foreach (var option in spell.ChooseOnePlayables) option.ActivateTask(PowerActivation.POWER,target,chooseOne,spell);
                        else spell.ChooseOnePlayables[chooseOne-1].ActivateTask(PowerActivation.POWER,target,chooseOne,spell);
                    }
                    else if (spell.Combo && c.IsComboActive)
                    {
                        game.Facts.ComboActivated(spell);
					    spell.ActivateTask(PowerActivation.COMBO, target);
                    }
				    else
					    spell.ActivateTask(PowerActivation.POWER, target, chooseOne);

				    if (spell.IsTwinSpell)
				    {
					    Entity.FromCard(in c, Cards.FromAssetId(spell.Card[GameTag.TWINSPELL_COPY]), zone: c.HandZone);
				    }

				    // process power tasks
				    game.ProcessTasks();

				    if (CompanionRules.IsSummoningSpell(spell.Card)) CompanionRules.SummonExtras(c, spell);

                    if (!IsTrackedSpellBody(game, spell))
                    {
                        OverloadBlock(c, spell, game.History);
                        // After-cast repeats may reuse this entity; the suspended hand play owns its exit.
                        if (!AuraSpellRules.IsActive(spell) && !ObjectiveRules.IsActive(spell) && (game.SpellCompletion?.SourceId != spell.Id || spell[GameTag.ZONE] != (int)Zone.PLAY))
                            RetireResolvedSpell(c, spell);
                    }
			    }
                // Quests stay public in SECRET; effect casts have no hand-play reveal.
                if (automatic && spell.IsQuest && game.History)
                {
                    spell[GameTag.ZONE_POSITION] = spell.ZonePosition + 1;
                    game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(spell));
                }
			    game.TaskQueue.EndEvent();
				historyCast?.Complete();
                CardSets.Client.Sets.DragonSoulShattered.Finish(spell);
		}
    }
}
