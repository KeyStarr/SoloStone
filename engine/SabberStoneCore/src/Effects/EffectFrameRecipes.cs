using System;
using System.Collections.Concurrent;
using System.Linq;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Model.Effects
{
    // Definitions live in the installed module, never in a saved frame. Keys must include module/version.
    internal static class EffectFrameRecipes
    {
        private static readonly ConcurrentDictionary<string, ISimpleTask[]> Recipes =
            new ConcurrentDictionary<string, ISimpleTask[]>(StringComparer.Ordinal);

        internal static void Register(string key, ISimpleTask[] tasks)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A versioned effect recipe key is required.", nameof(key));
            if (tasks == null || tasks.Any(t => t == null)) throw new ArgumentException("Effect steps cannot be null.", nameof(tasks));
            if (!Recipes.TryAdd(key, tasks.ToArray())) throw new InvalidOperationException("Duplicate effect recipe: " + key);
        }

        internal static ISimpleTask[] Resolve(string key) => key != null && Recipes.TryGetValue(key, out var tasks) ? tasks :
            Actions.GeneratedDefinitionRules.ResolveRecipe(key) ??
            throw new NotSupportedException("Unregistered effect recipe: " + key);
    }
}
