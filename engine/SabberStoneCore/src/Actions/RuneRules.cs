using System;
using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.Actions
{
    public readonly struct RuneRequirements
    {
        public int Blood { get; }
        public int Frost { get; }
        public int Unholy { get; }
        public int Total => checked(Blood + Frost + Unholy);
        public RuneRequirements(int blood, int frost, int unholy)
        {
            if (blood < 0 || frost < 0 || unholy < 0) throw new ArgumentOutOfRangeException(nameof(blood));
            Blood = blood; Frost = frost; Unholy = unholy;
        }
    }

    /// <summary>Deck rune requirements are component-wise maxima, independent of card copies.</summary>
    public static class RuneRules
    {
        public static RuneRequirements FromCards(IEnumerable<Card> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            int blood = 0, frost = 0, unholy = 0;
            foreach (Card card in cards)
            {
                if (card == null) throw new ArgumentException("Unknown deck card.", nameof(cards));
                blood = Math.Max(blood, card[GameTag.COST_BLOOD]);
                frost = Math.Max(frost, card[GameTag.COST_FROST]);
                unholy = Math.Max(unholy, card[GameTag.COST_UNHOLY]);
            }
            return new RuneRequirements(blood, frost, unholy);
        }
        public static bool IsLegalForClass(RuneRequirements requirements, CardClass heroClass) =>
            heroClass != CardClass.DEATHKNIGHT || requirements.Total <= 3;
    }
}
