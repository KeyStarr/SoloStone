using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model.Effects
{
    public enum SelectorSource { Hand, Board, CapturedEntities, PlayTargetOrBoard, DeathrattleBoard, HandOrBoard }
    public enum SelectorSide { Friendly, Enemy, Both }
    public enum EmptySelectorAction { Continue, StopRecipe }

    /// <summary>Immutable recipe definition. Saved frames retain only IDs, scalars and the recipe key.</summary>
    public sealed class SelectorSpec
    {
        public SelectorSource Source { get; }
        public SelectorSide Side { get; }
        public bool IncludeHeroes { get; }
        public CardType? CardType { get; }
        public int? SpellSchool { get; }
        public int? MaximumCurrentCost { get; }
        public string GroupKey { get; }
        public bool ExcludePlayTarget { get; }
        public bool UsePlayTarget => Source == SelectorSource.PlayTargetOrBoard;
        internal bool IsBoard => Source == SelectorSource.Board || Source == SelectorSource.DeathrattleBoard || Source == SelectorSource.HandOrBoard || UsePlayTarget;
        public bool ExcludeEarlierPicks { get; }
        public bool ApplyTargetingRules { get; }
        public EmptySelectorAction IfEmpty { get; }

        public SelectorSpec(SelectorSource source, SelectorSide side = SelectorSide.Friendly,
            bool includeHeroes = false, CardType? cardType = null, int? spellSchool = null,
            int? maximumCurrentCost = null, string groupKey = null, bool excludePlayTarget = false,
            bool excludeEarlierPicks = false, EmptySelectorAction ifEmpty = EmptySelectorAction.Continue,
            bool applyTargetingRules = true)
        {
            Source = source; Side = side; IncludeHeroes = includeHeroes; CardType = cardType;
            SpellSchool = spellSchool; MaximumCurrentCost = maximumCurrentCost; GroupKey = groupKey;
            ExcludePlayTarget = excludePlayTarget; ExcludeEarlierPicks = excludeEarlierPicks;
            IfEmpty = ifEmpty; ApplyTargetingRules = applyTargetingRules;
            Validate();
        }

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(SelectorSource), Source) || !Enum.IsDefined(typeof(SelectorSide), Side) ||
                !Enum.IsDefined(typeof(EmptySelectorAction), IfEmpty) ||
                CardType.HasValue && !Enum.IsDefined(typeof(CardType), CardType.Value) ||
                SpellSchool < 0 || SpellSchool > 9 || MaximumCurrentCost < 0 ||
                (Source == SelectorSource.Hand || Source == SelectorSource.CapturedEntities || Source == SelectorSource.HandOrBoard) && (Side != SelectorSide.Friendly || IncludeHeroes) ||
                Source == SelectorSource.HandOrBoard && CardType != Enums.CardType.MINION ||
                ExcludeEarlierPicks && string.IsNullOrWhiteSpace(GroupKey) || GroupKey != null && string.IsNullOrWhiteSpace(GroupKey))
                throw new ArgumentException("Invalid selector descriptor.");
        }

        internal static bool Matches(Game game, Controller controller, EffectFrame frame, SelectorSpec spec, int id)
        {
            if (!game.IdEntityDic.TryGetValue(id, out var entity)) return false;
            if (spec.Source == SelectorSource.Hand || spec.Source == SelectorSource.CapturedEntities ||
                spec.Source == SelectorSource.HandOrBoard && entity.Zone == controller.HandZone)
            {
                if (spec.Source == SelectorSource.CapturedEntities && !frame.CapturedCandidateIds.Contains(id)) return false;
                if (entity.Controller != controller || entity.Zone != controller.HandZone) return false;
            }
            else
            {
                if (!(entity is Minion) && !(spec.IncludeHeroes && entity is Hero) ||
                    entity.Zone != entity.Controller.BoardZone && entity != entity.Controller.Hero ||
                    spec.Side == SelectorSide.Friendly && entity.Controller != controller ||
                    spec.Side == SelectorSide.Enemy && entity.Controller != controller.Opponent) return false;
                var character = (ICharacter)entity;
                if (character.IsDead || entity is Minion minion && minion.IsDormant) return false;
                if (spec.Source == SelectorSource.DeathrattleBoard && !((Minion)entity).HasDeathrattle) return false;
                if (spec.ApplyTargetingRules && game.IdEntityDic.TryGetValue(frame.SourceId, out var source) && source is Spell spell &&
                    (character.CantBeTargetedBySpells || !spell.TargetingRequirements(character))) return false;
                if (spec.ApplyTargetingRules && (entity.Card.Untouchable ||
                    entity.Controller != controller && (character.HasStealth || character.IsImmune || character.CantBeTargetedByOpponents)))
                    return false;
            }
            return (!spec.CardType.HasValue || entity.Card.Type == spec.CardType) &&
                (!spec.SpellSchool.HasValue || entity.Card[GameTag.SPELL_SCHOOL] == spec.SpellSchool) &&
                (!spec.MaximumCurrentCost.HasValue || entity.Cost <= spec.MaximumCurrentCost) &&
                (!spec.ExcludePlayTarget || id != frame.TargetId) &&
                (!spec.ExcludeEarlierPicks || !frame.SelectedInGroup.Contains(id));
        }
    }
}
