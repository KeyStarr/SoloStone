using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public static class ColossalRules
    {
        public const GameTag RealizedFactor = (GameTag)31010;
        // Immutable declarations stay outside Card's serialized history-definition layout.
        private static readonly Dictionary<string, IReadOnlyList<string>> Declarations = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        private static readonly HashSet<string> RefillDeclarations = new HashSet<string>(StringComparer.Ordinal);

        internal static IReadOnlyList<string> AppendagesFor(string cardId) => Declarations.TryGetValue(cardId, out var ids) ? ids : null;

        public static void Declare(Card card, params string[] appendageIds)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (appendageIds == null || appendageIds.Any(string.IsNullOrEmpty))
                throw new ArgumentException("Colossal appendages must be card ids.", nameof(appendageIds));
            Declarations[card.Id] = Array.AsReadOnly((string[])appendageIds.Clone());
        }

        private static readonly Dictionary<string, int[]> Layouts = new Dictionary<string, int[]>(StringComparer.Ordinal);
        public static void DeclareLayout(Card card, int[] positions, params string[] appendageIds)
        {
            if (positions == null || appendageIds == null || positions.Length != appendageIds.Length || positions.Any(p => p < 0))
                throw new ArgumentException("One nonnegative slot offset per appendage is required.", nameof(positions));
            Declare(card, appendageIds);
            Layouts[card.Id] = (int[])positions.Clone();
        }

        public static void OnBodyEntered(Minion body)
        {
            var ids = body.Card.ColossalAppendageIds;
            if (ids == null || body.Zone != body.Controller.BoardZone) return;
            if (RefillDeclarations.Contains(body.Card.Id))
            {
                if (body.IsSilenced) return;
                if (body[SwapRules.RefillActive] == 0)
                {
                    body[SwapRules.RefillActive] = 1;
                    body[GameTag.TAG_SCRIPT_DATA_NUM_1] = ids.Count;
                }
                if (!CanRefill(body)) return;
                using (AppendageBlock(body))
                    while (TryRefill(body)) { }
                return;
            }
            var board = body.Controller.BoardZone;
            if (ids.Count == 0 || board.IsFull || board.FreeSpace <= 0) return;
            using (AppendageBlock(body))
                for (int i = 0; i < ids.Count && !board.IsFull && board.FreeSpace > 0; i++)
                    SummonAppendage(body, ids[i], i);
        }

        // One body-sourced TRIGGER per entry or refill; a refill inside the entry's own appendage block reuses it.
        private static IDisposable AppendageBlock(Minion body) =>
            body.Game.History ? body.Game.PowerHistory.ColossalAppendages(body) : null;

        public static void DeclareRefill(Card card, string appendageId, int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            Declare(card, Enumerable.Repeat(appendageId, count).ToArray());
            RefillDeclarations.Add(card.Id);
        }

        internal static bool CanRefill(Minion body) => body[SwapRules.RefillActive] != 0 && RefillDeclarations.Contains(body.Card.Id)
            && body.Zone == body.Controller.BoardZone && !body.ToBeDestroyed && !body.IsSilenced && !body.IsDormant
            && body[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0 && !body.Controller.BoardZone.IsFull;

        internal static bool TryRefill(Minion body)
        {
            if (!CanRefill(body)) return false;
            int remaining = body[GameTag.TAG_SCRIPT_DATA_NUM_1];
            var ids = body.Card.ColossalAppendageIds;
            if (ids == null || !RefillDeclarations.Contains(body.Card.Id)) return false;
            body[GameTag.TAG_SCRIPT_DATA_NUM_1] = remaining - 1;
            using (AppendageBlock(body))
                SummonAppendage(body, ids[ids.Count - remaining], ids.Count - remaining);
            return true;
        }

        internal static void StopRefill(IPlayable body)
        {
            if (body[SwapRules.RefillActive] == 0) return;
            body[SwapRules.RefillActive] = 0;
            body[GameTag.TAG_SCRIPT_DATA_NUM_1] = 0;
        }

        internal static void CopyRefill(IPlayable source, IPlayable copy, bool boardCopy)
        {
            if (!boardCopy || source[SwapRules.RefillActive] == 0) return;
            copy[SwapRules.RefillActive] = 1;
            copy[GameTag.TAG_SCRIPT_DATA_NUM_1] = source[GameTag.TAG_SCRIPT_DATA_NUM_1];
        }

        private static void SummonAppendage(Minion body, string id, int i)
        {
            var board = body.Controller.BoardZone;
            var appendage = (Minion)Entity.FromCard(body.Controller, Cards.FromId(id), new EntityData {
                [GameTag.ZONE] = (int)Zone.PLAY, [GameTag.DISPLAYED_CREATOR] = body.Id, [GameTag.CREATOR] = body.Id
            });
            if (body.Card[(GameTag)4538] != 0) ConfigureAppendage(appendage, HeraldRules.Tier(body.Controller));
            // Lean C3: alternate sides, walking past earlier linked appendages to move outward.
            int position = body.ZonePosition;
            if (Layouts.TryGetValue(body.Card.Id, out var layout))
                position = Math.Min(board.SlotCount, position + layout[i]);
            else if (i % 2 == 0)
            {
                while (position > 0 && IsAppendage(board.GetSlot(position - 1) as Minion, body)) position--;
            }
            else
            {
                position++;
                while (position < board.SlotCount && IsAppendage(board.GetSlot(position) as Minion, body)) position++;
            }
            Generic.SummonBlock(body.Game, appendage, position, body);
        }

        public static void ConfigureAppendage(Minion minion, int tier)
        {
            if (minion.IsSilenced || minion[RealizedFactor] > 0) return;
            int factor = 1 << Math.Max(0, Math.Min(2, tier));
            minion[RealizedFactor] = factor;
            minion[GameTag.TAG_SCRIPT_DATA_NUM_1] = minion.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] * factor;
            minion.AttackDamage += minion.Card.ATK * (factor - 1);
            minion.BaseHealth += minion.Card.Health * (factor - 1);
        }

        private static bool IsAppendage(Minion appendage, Minion body) => appendage != null
            && appendage[GameTag.DISPLAYED_CREATOR] == body.Id
            && (body.Card.ColossalAppendageIds?.Contains(appendage.Card.Id) ?? false);

        public static IEnumerable<Minion> AppendagesOf(Minion body) =>
            body.Controller.BoardZone.Where(m => !m.ToBeDestroyed && IsAppendage(m, body));

        public static Minion BodyOf(Minion appendage)
        {
            if (!appendage.Game.IdEntityDic.TryGetValue(appendage[GameTag.DISPLAYED_CREATOR], out var entity)
                || !(entity is Minion body) || body.Zone != body.Controller.BoardZone || body.ToBeDestroyed
                || !IsAppendage(appendage, body)) return null;
            return body;
        }
    }
}
