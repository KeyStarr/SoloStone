using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class TransformRules
    {
        private static readonly Dictionary<string, Func<Minion, Card, Card>> Destinations = new Dictionary<string, Func<Minion, Card, Card>>();
        internal static void Register(string id, Func<Minion, Card, Card> destination) => Destinations.Add(id, destination);

        internal static bool Intercept(Minion source, Card intended, out Card destination)
        {
            destination = intended;
            if (source == null || source.IsSilenced || source.Zone != source.Controller.BoardZone ||
                intended?.Type != CardType.MINION || !Destinations.TryGetValue(source.Card.Id, out var intercept)) return false;
            destination = intercept(source, intended);
            return true;
        }

        public static bool HasTwoSpellTargets(Controller controller, Card spell) =>
            controller.BoardZone.Concat(controller.Opponent.BoardZone).Count(m => Selectable(controller, spell, m)) >= 2;

        internal static bool Selectable(Controller controller, Card spell, Minion minion) =>
            minion.Zone == minion.Controller.BoardZone && !minion.IsDead && !minion.Untouchable &&
            !minion.CantBeTargetedBySpells && spell.TargetingRequirements(controller, minion) &&
            (minion.Controller == controller || !minion.CantBeTargetedByOpponents);
    }
}
