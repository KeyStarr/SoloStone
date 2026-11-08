using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
    /// <summary>Read-only hand conditions shared with card effects, keyed by definition identity.</summary>
    public static class PoweredUp
    {
        private const GameTag KindredTag = (GameTag)4028;
        private static readonly Dictionary<string, Func<IPlayable, bool>> Conditions =
            new Dictionary<string, Func<IPlayable, bool>>(StringComparer.Ordinal);

        internal static void Register(Card card, Func<IPlayable, bool> condition)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (!condition.Method.IsStatic || condition.Target != null || condition.GetInvocationList().Length != 1)
                throw new ArgumentException("Powered-up conditions require a pure static method recipe.", nameof(condition));
            if (Conditions.ContainsKey(card.Id))
                throw new ArgumentException("Duplicate powered-up condition.", nameof(card));
            Conditions.Add(card.Id, condition);
        }

        public static bool HasCondition(Card card) => Conditions.ContainsKey(card.Id)
            || card[GameTag.OUTCAST] == 1 || card[KindredTag] == 1
            || card.Id == "CATA_470t1" && GeneratedDefinitionRules.Parts(card).Any(HasCondition);

        public static bool IsMet(IPlayable entity)
        {
            if (entity.Game.State != State.RUNNING || entity.Zone != entity.Controller.HandZone) return false;
            return (Conditions.TryGetValue(entity.Card.Id, out var condition) && condition(entity))
                || (entity.Card.Id == "CATA_470t1" && GeneratedDefinitionRules.Parts(entity.Card)
                    .Any(part => Conditions.TryGetValue(part.Id, out var inherited) && inherited(entity)))
                || OutcastRules.AtEdge(entity)
                || (entity.Card[KindredTag] == 1 && entity.Controller.HasKindredMatch(entity.Card));
        }

        public static IReadOnlyCollection<string> RegisteredIds
        {
            get { _ = Cards.Count; return Array.AsReadOnly(Conditions.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray()); }
        }
    }
}
