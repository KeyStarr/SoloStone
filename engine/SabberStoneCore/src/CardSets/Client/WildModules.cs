using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    /// <summary>
    /// Wild cards scripted for content outside the Standard metas, such as Dungeon Run loot. They register as effect modules
    /// only: unlike <see cref="StandardCardAttribute"/> cards they are not recorded in <see cref="StandardSets"/>, so they stay
    /// out of the Standard stress decks and the AI's card frequency tables, which model what Standard opponents hold.
    /// </summary>
    internal static class WildModules
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(Sets.ZephrysTheGreat.DbfId, "ULD_003", Sets.ZephrysTheGreat.Configure);
        }
    }
}
