using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>Effect plays use the matching cast/summon/equip path, never hand payment.</summary>
    public static class AutoPlayRules
    {
        internal const GameTag Active = (GameTag)33120;
        internal const GameTag EndTurn = (GameTag)33121;
        private const GameTag Draining = (GameTag)33123;
        private const int DiscoverQueue = 118191;
        public static bool IsInternalTag(GameTag tag) => tag == Active || tag == EndTurn || tag == Draining || tag == LocationRules.PendingActivation;

        /// <summary>Freeze rejected options while the real Discover and its enhancements still exist.</summary>
        public static void CaptureDiscover(Choice choice, int selected)
        {
            // Called only by the semantic-completion boundary, after history consumes its operation ID.
            if (DiscoverRealRules.IsRefresh(choice, selected)) return;
            var c = choice.Controller;
            var stone = ReserveDiscoverCharge(c, choice.SourceId);
            if (stone == null) return;
            foreach (int id in choice.Choices.Where(id => id != selected))
            {
                var option = c.Game.IdEntityDic[id];
                if (option is HeroPower || option.Card.Type == CardType.ENCHANTMENT || option.Card.Id == "JAIL_319t") continue;
                var original = option[DiscoverRealRules.BoundEntity] != 0
                    ? c.Game.IdEntityDic[option[DiscoverRealRules.BoundEntity]] : option;
                var entry = PayloadRules.Store(c, DiscoverQueue, original, 0);
                entry[GameTag.CREATOR] = stone.Id;
                // Gift links belong to the offer, even when it is a proxy for a deck original.
                entry[GameTag.DARK_GIFT_ENTITY] = option[GameTag.DARK_GIFT_ENTITY];
                DarkGifts.ApplyChosen(choice, entry);
            }
        }

        internal static Weapon ReserveDiscoverCharge(Controller c, int sourceId)
        {
            var stone = c.Hero.Weapon;
            if (stone?.Card.Id != "TLC_460t" || stone.ToBeDestroyed || stone.Durability <= 0) return null;
            sourceId = DarkGifts.RecipeSource(c.Game, sourceId);
            if (!c.Game.IdEntityDic.TryGetValue(sourceId, out var source) || source is HeroPower) return null;
            // Reserve once per Discover before nested work or retained-card delivery.
            stone.Damage++;
            return stone;
        }

        internal static void AdvanceDiscoverQuest(Controller c)
        {
            foreach (var quest in c.SecretZone.ModernQuests.Where(q => q.Card.Id == "TLC_460").ToArray())
                QuestRules.Advance(quest);
        }

        internal static void DrainDiscover(Controller c)
        {
            var game = c.Game;
            if (c[Draining] != 0 || c.Choice != null || game.Resolution.HasSuspendedEffect
                || !PayloadRules.Entries(c, DiscoverQueue).Any()) return;
            c[Draining] = 1;
            try
            {
                IPlayable entry;
                while (game.State != State.COMPLETE && (entry = PayloadRules.Entries(c, DiscoverQueue).FirstOrDefault()) != null)
                {
                    var source = game.IdEntityDic[entry[GameTag.CREATOR]];
                    var copy = Generic.Copy(c, source, entry, Zone.SETASIDE, preserveCapturedState: true);
                    PayloadRules.Remove(entry);
                    copy[PayloadRules.Stored] = 0;
                    copy[Generic.PrivateDecisionOfferTag] = 0;
                    Play(copy, source);
                }
            }
            finally { c[Draining] = 0; }
        }

        public static bool Play(IPlayable card, IEntity source, bool preferEnemies = false,
            bool drawWhenFull = false, bool battlecry = false)
        {
            var c = card.Controller; var game = c.Game;
            if (game.State == State.COMPLETE) return false;
            if ((card is Minion || card is BoardObject) && c.BoardZone.IsFull)
            {
                if (drawWhenFull && card.Zone == c.DeckZone) Generic.Draw(c, card);
                else { Generic.RemoveFromZone(c, card); c.GraveyardZone.Add(card); }
                return false;
            }
            if (game.Resolution.HasSuspendedEffect) throw new InvalidOperationException("Cannot autoplay over a pending decision.");
            using var resolution = game.Resolution.Enter(ResolutionKind.EffectResolution, card.Id);
            int previous = c[Active], automatic = c[Generic.AutomaticDecisionTag];
            var pending = c.Choice;
            c[Active] = 1; c[Generic.AutomaticDecisionTag] = 1; c.Choice = null;
            game.TaskQueue.StartEvent();
            IDisposable effectPlay = null;
            try
            {
                int mode = card.ChooseOne && !c.ChooseBoth ? game.Random.Next(1, card.ChooseOnePlayables.Length + 1) : 0;
                var profile = card is Spell spellProfile ? Generic.SpellTargetingProfile(spellProfile, mode)
                    : mode > 0 ? card.ChooseOnePlayables[mode - 1] : card;
                ICharacter target = null;
                int legalTargets = -1;
                if (card is Spell || battlecry)
                {
                    var legal = profile.ValidPlayTargets.Where(profile.IsValidPlayTarget).ToArray();
                    var enemies = preferEnemies ? legal.Where(t => t.Controller == c.Opponent).ToArray() : Array.Empty<ICharacter>();
                    if (enemies.Length != 0) legal = enemies;
                    if (legal.Length != 0) target = legal[game.Random.Next(legal.Length)];
                    if (target != null) card.CardTarget = target.Id;
                    legalTargets = legal.Length;
                }
                // History diagnostics only: what this automatic play chose from (reads, draws nothing).
                if (game.History)
                    game.PowerHistory.NoteAutoChoice(card.Id, legalTargets, mode,
                        mode > 0 && card.ChooseOnePlayables != null && mode <= card.ChooseOnePlayables.Length ? card.ChooseOnePlayables[mode - 1].Card.Id : null, mode > 0);
                if (card is Spell spell)
                {
                    // Mixed-card attempts consume rejected profiles too; spell-only casts retain their policy.
                    if (!HeraldRules.CanCast(c, spell, target) || !ImbueRules.CanCast(c, spell, target))
                    {
                        Generic.RemoveFromZone(c, spell);
                        c.GraveyardZone.Add(spell);
                        return false;
                    }
                    Generic.CastAutomaticSpell(c, game, spell, target, mode);
                }
                else
                {
                    // Spells open theirs in ResolveSpell; a card put into play keeps its battlecry inside the block.
                    effectPlay = EffectPlayRules.Open(game, card, target);
                    Generic.RemoveFromZone(c, card);
                    switch (card)
                    {
                        case Minion minion:
                            Generic.SummonBlock(game, minion, -1, source);
                            break;
                        case BoardObject location:
                            Generic.PlayLocation(c, game, location, -1, automatic: true);
                            break;
                        case Weapon weapon:
                            weapon.Power?.Aura?.Activate(weapon);
                            weapon.Power?.Trigger?.Activate(weapon);
                            c.Hero.AddWeapon(weapon);
                            break;
                        case Hero hero:
                            Generic.PlayHeroCore(c, game, hero, target, mode, automatic: true, battlecry: battlecry);
                            break;
                        default: throw new NotSupportedException("Unsupported automatic card type: " + card.Card.Type);
                    }
                    if (battlecry && (card is Minion || card is Weapon))
                    {
                        card.ActivateTask(PowerActivation.POWER, target, mode);
                        if (DarkGifts.DoublesBattlecry(card) || c.ExtraBattlecry && card.Card[GameTag.BATTLECRY] != 0)
                            card.ActivateTask(PowerActivation.POWER, target, mode);
                    }
                }
                game.ProcessTasks();
                while (c.Choice != null) Generic.ChoicePick(c, game, c.Choice.Choices.Choose(game.Random));
                effectPlay?.Dispose();
                game.DeathProcessingAndAuraUpdate();
                return true;
            }
            finally
            {
                if (game.History) game.PowerHistory.ClearAutoChoice();
                effectPlay?.Dispose();
                game.TaskQueue.EndEvent();
                c.Choice = pending; c[Active] = previous; c[Generic.AutomaticDecisionTag] = automatic;
            }
        }

        public static void RequestTurnEnd(Controller controller)
        {
            var game = controller.Game;
            if (game.CurrentPlayer == controller && game.Step == Step.MAIN_ACTION)
                controller[EndTurn] = game.Turn;
        }

        internal static void CompleteTurn(Game game)
        {
            var c = game.CurrentPlayer;
            if (c[EndTurn] == 0 || game.Resolution.HasSuspendedEffect || game.Player1.Choice != null || game.Player2.Choice != null) return;
            int turn = c[EndTurn]; c[EndTurn] = 0;
            if (turn != game.Turn || game.State == State.COMPLETE || game.Step != Step.MAIN_ACTION) return;
            game.Step = Step.MAIN_END;
            game.MainEnd();
        }
    }
}
