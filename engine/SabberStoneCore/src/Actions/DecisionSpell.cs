using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Actions
{
    public static partial class Generic
    {
        internal static bool HasDecisionSpell(Controller c) => c.Game.SpellCompletion?.ControllerId == c.Id;

        private static void BeginDecisionSpell(Controller c, Spell spell, ICharacter target, int chooseOne, SuspendableTask recipe,
            HistoryRecorder.CastScope historyCast, (int EntityId, int ResidenceGeneration)[] handProgress)
        {
            var game = c.Game;
            spell[RepeatRules.TargetDefinition] = target?.Card.AssetId ?? 0;
            if (spell[RepeatRules.Remaining] > 0)
            {
                spell[RepeatRules.ChooseBoth] = UsesChooseBoth(spell, c) ? 1 : 0;
                if (target != null) target[RepeatRules.TargetWitness] = spell.Id;
            }
            if (game.SpellCompletion != null || HasDecisionPlay(c))
                throw new InvalidOperationException("A decision play is already pending.");
            game.SpellCompletion = new DecisionCompletion { ControllerId = c.Id, SourceId = spell.Id,
                TargetId = target?.Id ?? 0, DefinitionDbfId = spell.Card.AssetId, ChooseOne = chooseOne, Echo = spell.IsEcho,
                HandProgress = handProgress, ThroughPower = recipe == null };
            using (game.Resolution.Enter(ResolutionKind.SpellText, spell.Id))
            {
                game.TaskQueue.StartEvent();
                // Normal power dispatch owns its activation block; only direct recipes need one here.
                if (game.History && recipe != null)
                {
                    game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.POWER, spell.Id, "",
                        spell.ChooseOne && chooseOne > 0 && !UsesChooseBoth(spell) ? chooseOne - 1 : -1, target?.Id ?? 0));
                    if (target != null)
                        game.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.TARGET,
                            Data = spell.Id, Info = new List<int> { target.Id } });
                }
                if (recipe != null)
                {
                    spell.Power.Trigger?.Activate(spell);
                    spell.Power.Aura?.Activate(spell);
                    if (spell.IsTwinSpell)
                        Entity.FromCard(c, Cards.FromAssetId(spell.Card[GameTag.TWINSPELL_COPY]), zone: c.HandZone);
                    recipe.Process(game, c, spell, target);
                }
                else
                {
                    historyCast.AllowBodyClaim = true;
                    ResolveSpell(c, game, spell, target, chooseOne, true);
                }
                if (!SpellBodyPending(game))
                {
                    CloseDecisionSpellPower(game);
                    if (spell[RepeatRules.Remaining] > 0) historyCast.Complete();
                    FinishDecisionCast(c, spell);
                }
                else game.ProcessTasks();
                game.TaskQueue.EndEvent();
            }
            // History: one Completed fact per hand cast, here even while the answer is pending (the scope is
            // invocation-local and snapshots reject dangling Started casts); the resume adds no cast facts.
            historyCast.Complete();
            if (spell[RepeatRules.Remaining] == 0 && spell[RepeatRules.PrimaryDone] == 0) OutcastRules.EndPrimary(spell);
            if (!SpellBodyPending(game)) FinishDecisionSpell(c, false);
        }

        internal static bool SpellBodyPending(Game game) => game.Resolution.HasSuspendedEffect ||
            game.Player1.Choice != null || game.Player2.Choice != null;

        internal static void CloseDecisionSpellPower(Game game)
        {
            if (game.History && game.PowerHistory.CurrentBlock is { BlockType: BlockType.POWER } block
                && block.Source == game.SpellCompletion.SourceId)
                game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            var spell = game.IdEntityDic[game.SpellCompletion.SourceId];
            if (game.History && spell[RepeatRules.PrimaryDone] != 0 &&
                game.PowerHistory.CurrentBlock is { BlockType: BlockType.TRIGGER } trigger &&
                trigger.Source == (spell[RepeatRules.Origin] != 0 ? spell[RepeatRules.Origin] : spell.Id))
                game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        }

        internal static bool IsTrackedSpellBody(Game game, IEntity source) =>
            game.SpellCompletion is { ThroughPower: true, Stage: DecisionCompletionStage.Recipe } completion && completion.SourceId == source.Id;

        internal static void CompleteOrdinarySpellChoice(Game game)
        {
            var completion = game.SpellCompletion;
            if (completion == null || SpellBodyPending(game)) return;
            var c = game.ControllerById(completion.ControllerId);
            var spell = (Spell)game.IdEntityDic[completion.SourceId];
            using var play = game.Resolution.Sequence(ResolutionKind.PlayCard, game, spell);
            using var cast = game.Resolution.Sequence(ResolutionKind.SpellCast, game, spell);
            game.CurrentEventData = new EventMetaData(spell,
                completion.TargetId == 0 ? null : game.IdEntityDic[completion.TargetId]);
            CloseDecisionSpellPower(game);
            FinishDecisionCast(c, spell, deferGraveyard: true);
            if (!SpellBodyPending(game)) FinishDecisionSpell(c, true);
        }

        private static void FinishDecisionCast(Controller c, Spell spell, bool deferGraveyard = false)
        {
            var game = c.Game;
            var completion = game.SpellCompletion;
            if (completion.Stage != DecisionCompletionStage.Recipe) return;
            game.CheckDeadHeroes();
            if (spell[RepeatRules.PrimaryDone] == 0)
            {
                spell[RepeatRules.PrimaryDone] = 1;
                if (game.State != State.COMPLETE)
                    HandProgressRules.CompleteCast(c, completion.HandProgress, HistoryCastKind.Player, HistoryCastOutcome.Completed);
                completion.HandProgress = null;
            }
            if (game.State == State.COMPLETE)
            {
                RepeatRules.Clear(spell);
                completion.Stage = DecisionCompletionStage.CastTail;
                return;
            }
            AuraSpellRules.FlushCasts(spell);
            if (!spell.IsSecret && !spell.IsQuest)
            {
                game.ProcessTasks();
                OverloadBlock(c, spell, game.History);
            }
            while (spell[RepeatRules.Remaining] > 0 && game.State != State.COMPLETE)
            {
                spell[RepeatRules.Remaining]--;
                game.DeathProcessingAndAuraUpdate();
                if (game.State == State.COMPLETE) break;
                completion.ThroughPower = true;
                var target = RepeatRules.Target(spell, completion.TargetId, completion.ChooseOne);
                if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER,
                    spell[RepeatRules.Origin] != 0 ? spell[RepeatRules.Origin] : spell.Id,"",0,target?.Id ?? 0));
                using var repeat = game.Facts.Cast(spell, HistoryCastKind.Repeat);
                repeat.AllowBodyClaim = true;
                ResolveSpell(c, game, spell, target, completion.ChooseOne, true, repeat: true);
                repeat.Complete();
                if (SpellBodyPending(game)) return;
                CloseDecisionSpellPower(game);
                if (!spell.IsSecret && !spell.IsQuest) OverloadBlock(c, spell, game.History);
            }
            if (game.State != State.COMPLETE) RepeatRules.RunPayloads(spell);
            RepeatRules.Clear(spell);
            completion.Stage = DecisionCompletionStage.CastTail;
            if (!spell.IsSecret && !spell.IsQuest && !AuraSpellRules.IsActive(spell) && !ObjectiveRules.IsActive(spell) && !deferGraveyard) RetireResolvedSpell(c, spell);
        }

        private static void FinishDecisionSpell(Controller c, bool finishCard)
        {
            var game = c.Game;
            var completion = game.SpellCompletion;
            var spell = (Spell)game.IdEntityDic[completion.SourceId];
            if (completion.Stage == DecisionCompletionStage.CastTail)
            {
                completion.Stage = DecisionCompletionStage.PlayTail;
                game.DeathProcessingAndAuraUpdate();
                FinishSpellPlay(c, game, spell);
            }
            completion.Stage = DecisionCompletionStage.CardTail;
            game.SpellCompletion = null;
            if (finishCard) FinishCardPlay(c, spell, completion.Echo);
            // Retail keeps the resolving spell in PLAY through its choices and after-play reactions.
            if (!spell.IsSecret && !spell.IsQuest && !AuraSpellRules.IsActive(spell) && !ObjectiveRules.IsActive(spell) && spell.Zone != c.GraveyardZone
                && !(SupplierActive(spell) && (spell.Zone == c.HandZone || spell[RetainedWaiting] != 0))) RetireResolvedSpell(c, spell);
        }

        internal static void ResumeDecisionSpell(Controller c, int selected)
        {
            var game = c.Game;
            var completion = game.SpellCompletion;
            var spell = (Spell)game.IdEntityDic[completion.SourceId];
            using var play = game.Resolution.Sequence(ResolutionKind.PlayCard, game, spell);
            using var cast = game.Resolution.Sequence(ResolutionKind.SpellCast, game, spell);
            game.CurrentEventData = new EventMetaData(spell,
                completion.TargetId == 0 ? null : game.IdEntityDic[completion.TargetId]);
            using (game.Resolution.Enter(ResolutionKind.SpellText, spell.Id))
            {
                game.TaskQueue.StartEvent();
                game.Resolution.ResumeEffect(game, selected);
                if (!SpellBodyPending(game))
                {
                    CloseDecisionSpellPower(game);
                    FinishDecisionCast(c, spell, deferGraveyard: true);
                }
                else game.ProcessTasks();
                game.TaskQueue.EndEvent();
            }
            if (SpellBodyPending(game))
            {
                game.CurrentEventData = null;
            }
            else FinishDecisionSpell(c, true);
        }

        internal static void ValidateDecisionSpell(Game game)
        {
            var completion = game.SpellCompletion;
            if (completion == null) return;
            if (completion.ControllerId != game.Player1.Id && completion.ControllerId != game.Player2.Id)
                throw new SerializationException("Invalid decision spell controller.");
            var c = game.ControllerById(completion.ControllerId);
            var power = Cards.FromAssetId(completion.DefinitionDbfId)?.Power?.PowerTask;
            var recipe = (power as DecisionSpellTask)?.Recipe ?? power as SuspendableTask;
            if (completion.Stage != DecisionCompletionStage.Recipe || HasDecisionPlay(c) ||
                !game.IdEntityDic.TryGetValue(completion.SourceId, out var source) || !(source is Spell) ||
                source.Controller != c || source.Card.AssetId != completion.DefinitionDbfId ||
                source.Zone == c.HandZone || source.Zone == c.GraveyardZone || ((Spell)source).IsCountered ||
                completion.TargetId != 0 && !game.IdEntityDic.ContainsKey(completion.TargetId) ||
                completion.ChooseOne < -1 || completion.ChooseOne > (source.ChooseOnePlayables?.Length ?? 0) || !SpellBodyPending(game))
                throw new SerializationException("Invalid decision spell continuation.");
            // Alternate powers and nested effects own their registered cursors; ordinary choices own no cursor.
            if (!completion.ThroughPower && (DarkGifts.RecipeSource(game, c.Choice?.SourceId ?? 0) != completion.SourceId ||
                recipe == null || !game.Resolution.HasSuspendedEffect ||
                game.Resolution.EffectFrames.First().RecipeKey != recipe.RecipeKey ||
                game.Resolution.EffectFrames.First().ControllerId != completion.ControllerId ||
                game.Resolution.EffectFrames.First().SourceId != completion.SourceId ||
                game.Resolution.EffectFrames.First().TargetId != completion.TargetId))
                throw new SerializationException("Invalid decision spell continuation.");
        }
    }
}
