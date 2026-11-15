using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // Cards that scale with a run's progress ("for each Boss you've defeated this run", Dungeon Run's Loyal Sidekick,
    // Primordial Wand, Shifting Hourglass). At the start of a run fight (GameConfig.PlayerNRunBossesDefeated) each such card in
    // its owner's hand and deck carries the count as TAG_SCRIPT_DATA_NUM_1, which the client's card text shows and the card's
    // effect reads; a registered card may also change itself (Loyal Sidekick's stats).
    public static class RunProgressRules
    {
        private static readonly Dictionary<int, Action<IPlayable, int>> Scaled = new Dictionary<int, Action<IPlayable, int>>();

        internal static void Mark(Card card, Action<IPlayable, int> apply = null) => Scaled[card.AssetId] = apply;

        public static int Count(IPlayable card) => card[GameTag.TAG_SCRIPT_DATA_NUM_1];

        internal static void Apply(Controller owner, int bossesDefeated)
        {
            if (bossesDefeated < 0 || bossesDefeated > 100) throw new ArgumentOutOfRangeException(nameof(bossesDefeated));
            if (bossesDefeated == 0 || Scaled.Count == 0) return;
            foreach (var card in owner.HandZone.Concat(owner.DeckZone).Where(p => Scaled.ContainsKey(p.Card.AssetId)).ToList())
            {
                card[GameTag.TAG_SCRIPT_DATA_NUM_1] = bossesDefeated;
                Scaled[card.Card.AssetId]?.Invoke(card, bossesDefeated);
            }
        }
    }
}
