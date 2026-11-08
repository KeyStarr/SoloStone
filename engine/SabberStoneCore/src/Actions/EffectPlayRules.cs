using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // Retail presents an automatic play (Fyrakk the Blazing's casts, Yogg-style casters, autoplayed cards) as its own
    // PLAY block: the native client creates the big card and paces "card, its effects, next card" only
    // for PLAY blocks (PowerProcessor), and ShouldCreatePlayBlockHistoryTile folds plays under a CAST_RANDOM_SPELLS caster
    // into the caster's tile. History only: no engine tag, zone or RNG changes, so state and AI fingerprints are unchanged.
    internal static class EffectPlayRules
    {
        internal static IDisposable Open(Game game, IPlayable card, ICharacter target)
        {
            if (!game.History || card == null) return null;
            var history = game.PowerHistory;
            // A nested re-cast of an open play is already presented. A card resolving inside its own TRIGGER block
            // (Casts When Drawn's TOPDECK) keeps that trigger presentation and its sub-spell recipes; it is revealed
            // as it leaves the hand (NativeMatch identity normalizer).
            if (history.HasEffectPlay(card.Id) || history.CurrentBlock?.BlockType == BlockType.TRIGGER && history.CurrentBlock.Source == card.Id)
                return null;
            int caster = history.CurrentBlock?.Source ?? 0;
            if (caster == card.Id || !game.IdEntityDic.ContainsKey(caster) || game.IdEntityDic[caster] is Enchantment) caster = 0;
            // A freshly created card has no client zone yet; retail plays it out of SETASIDE.
            if (card[GameTag.ZONE] == (int)Zone.INVALID)
                history.Add(PowerHistoryBuilder.TagChange(card.Id, GameTag.ZONE, (int)Zone.SETASIDE));
            if (caster != 0 && !history.HasEffectPlayCaster(caster))
                history.Add(PowerHistoryBuilder.TagChange(caster, GameTag.CAST_RANDOM_SPELLS, 1));
            var start = new PowerHistoryEffectPlayStart {
                BlockType = BlockType.PLAY, Source = card.Id, Target = target?.Id ?? 0, Index = -1, EffectCardId = "", Caster = caster,
                // A Secret keeps the hand-played Secret presentation (no big card; the opponent's stays hidden).
                ForceShowBigCard = !(card.Card.IsSecret && !card.Card.IsQuest)
            };
            history.ApplyAutoChoice(start);
            history.Add(start);
            return new Scope(history, caster);
        }

        private sealed class Scope : IDisposable
        {
            private PowerHistory history;
            private readonly int caster;
            internal Scope(PowerHistory history, int caster) { this.history = history; this.caster = caster; }
            public void Dispose()
            {
                if (history == null) return;
                history.Add(PowerHistoryBuilder.BlockEnd());
                if (caster != 0 && !history.HasEffectPlayCaster(caster))
                    history.Add(PowerHistoryBuilder.TagChange(caster, GameTag.CAST_RANDOM_SPELLS, 0));
                history = null;
            }
        }
    }
}
