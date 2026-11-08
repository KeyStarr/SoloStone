using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class StandardCardAttribute : Attribute
    {
        internal string Id { get; }
        internal int DbfId { get; }
        internal StandardCardAttribute(string id, int dbfId) { Id = id; DbfId = dbfId; }

        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            var entries = typeof(StandardCardAttribute).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Instance
                    | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .SelectMany(method => method.GetCustomAttributes<StandardCardAttribute>()
                    .Select(spec => new { Method = method, Spec = spec }))
                .OrderBy(entry => entry.Spec.Id, StringComparer.Ordinal).ToArray();
            foreach (var entry in entries)
            {
                var method = entry.Method;
                if (string.IsNullOrWhiteSpace(entry.Spec.Id) || entry.Spec.DbfId <= 0
                    || !method.IsStatic || method.ContainsGenericParameters || method.ReturnType != typeof(void)
                    || method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(Card))
                    throw new InvalidOperationException("Invalid Standard card module: " + method);
                register(entry.Spec.DbfId, entry.Spec.Id, (Action<Card>)method.CreateDelegate(typeof(Action<Card>)));
            }
        }
    }
}
