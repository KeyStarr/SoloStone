using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class OfflinePoolAttribute : Attribute
    {
        internal string CardId { get; }
        internal int DbfId { get; }
        internal OfflinePoolAttribute(string cardId, int dbfId) { CardId = cardId; DbfId = dbfId; }

        internal static IReadOnlyList<OfflinePoolDeclaration> Collect(IEnumerable<OfflinePoolDeclaration> legacy,
            IEnumerable<Type> types)
        {
            var entries = types.SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Instance
                    | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Select(method => new { Method = method, Spec = method.GetCustomAttribute<OfflinePoolAttribute>() })
                .Where(entry => entry.Spec != null)
                .OrderBy(entry => entry.Spec.CardId, StringComparer.Ordinal).ThenBy(entry => entry.Spec.DbfId).ToArray();
            var cards = legacy.ToList();
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            var dbfIds = new HashSet<int>();
            void CheckIdentity(string cardId, int dbfId)
            {
                if (!cardIds.Add(cardId) || !dbfIds.Add(dbfId))
                    throw new InvalidOperationException($"Duplicate offline pool declaration: {cardId}/{dbfId}");
            }
            foreach (var card in cards) CheckIdentity(card.CardId, card.DbfId);
            foreach (var entry in entries)
            {
                var method = entry.Method;
                if (string.IsNullOrWhiteSpace(entry.Spec.CardId) || entry.Spec.DbfId <= 0
                    || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(PoolRequirement[])
                    || method.GetParameters().Length != 0)
                    throw new InvalidOperationException("Invalid offline pool provider: " + method);
                CheckIdentity(entry.Spec.CardId, entry.Spec.DbfId);
                var pools = ((Func<PoolRequirement[]>)method.CreateDelegate(typeof(Func<PoolRequirement[]>)))();
                if (pools == null || pools.Length == 0 || pools.Any(pool => pool == null)
                    || pools.Select(pool => pool.Name).Distinct(StringComparer.Ordinal).Count() != pools.Length)
                    throw new InvalidOperationException("Invalid offline pool requirements: " + method);
                cards.Add(new OfflinePoolDeclaration(entry.Spec.CardId, entry.Spec.DbfId, pools));
            }
            return Array.AsReadOnly(cards.ToArray());
        }
    }
}
