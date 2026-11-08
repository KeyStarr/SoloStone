using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Definition callbacks hold no live state; eligible initial sources carry their saved phase.</summary>
    public static class StartOfGameRules
    {
        public const GameTag Pending = (GameTag)32600;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 32600 && (int)tag <= 32602;
        private static readonly Dictionary<int, (Action<IPlayable> Effect, Func<Controller,bool> Eligible, int Priority)>
            Definitions = new Dictionary<int, (Action<IPlayable>, Func<Controller,bool>, int)>();

        private static readonly HashSet<int> NeverOpening = new HashSet<int>();

        /// <param name="neverOpening">Self-removing source: never dealt in an opening hand or mulligan replacement.</param>
        public static void Declare(Card card, Action<IPlayable> effect, Func<Controller,bool> eligible = null, int priority = 0,
            bool neverOpening = false)
        {
            Definitions.Add(card.AssetId, (effect, eligible, priority));
            if (neverOpening) NeverOpening.Add(card.AssetId);
        }

        public static bool HasEffect(Card card) => card != null && Definitions.ContainsKey(card.AssetId);

        internal static void Capture(Game game)
        {
            foreach (var owner in new[]{game.Player1,game.Player2})
                foreach (var source in owner.DeckZone)
                    if (source[Pending] == 0 && Definitions.TryGetValue(source.Card.AssetId,out var definition))
                        source[Pending] = definition.Eligible == null || definition.Eligible(owner) ? 1 : -1;
        }

        /// <summary>Opening/mulligan pick that skips pending self-removing sources (data/rules-evidence/start-of-game/
        /// sog-timing-broxigar.yaml); null keeps the ordinary draw when no such source is in the deck.</summary>
        internal static IPlayable OpeningPick(Controller c)
        {
            if (!c.DeckZone.Any(Excluded)) return null;
            var candidates = c.DeckZone.Where(e => !Excluded(e)).ToArray();
            if (candidates.Length == 0) return null;
            return c.DeckZone.DrawWithRandom ? candidates[c.Game.Random.Next(candidates.Length)] : candidates[candidates.Length-1];
        }

        /// <summary>Only excluded sources remain: deal nothing rather than an excluded card.</summary>
        internal static bool OpeningBlocked(Controller c) => c.DeckZone.Any(Excluded) && c.DeckZone.All(Excluded);

        private static bool Excluded(IPlayable e) => e[Pending] == 1 && NeverOpening.Contains(e.Card.AssetId);

        /// <summary>Runs in MainBegin: retail lands Start of Game effects after both mulligans, before MAIN_READY
        /// (data/rules-evidence/start-of-game/sog-timing-generic.yaml). Eligibility stays captured before the deal.</summary>
        internal static void Resolve(Game game)
        {
            var sources = game.IdEntityDic.Values.Where(e=>e[Pending] == 1)
                .OrderBy(e=>Definitions[e.Card.AssetId].Priority)
                .ThenBy(e=>e.Controller == game.FirstPlayer ? 0 : 1).ThenBy(e=>e.Id).ToArray();
            // Ordinary legacy games must keep their post-mulligan queue timing.
            if (sources.Length > 0) game.ProcessTasks();
            foreach (var source in sources)
            {
                source[Pending] = -1;
                if (game.History) game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockStart(
                    BlockType.TRIGGER,source.Id,"",-1,0,effectIndex:-1));
                Definitions[source.Card.AssetId].Effect(source);
                game.ProcessTasks();
                if (game.History) game.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.BlockEnd());
            }
        }

    }
}
