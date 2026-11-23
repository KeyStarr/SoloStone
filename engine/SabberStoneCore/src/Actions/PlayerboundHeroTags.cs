using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Hero tags that belong to the player, not the hero entity: they follow the player's current hero on every
    /// hero swap (a played hero card, or a replacement such as Lord Jaraxxus). Today only Godfrey the Betrayer's portal.</summary>
    internal static class PlayerboundHeroTags
    {
        private static readonly GameTag[] Tags = { (GameTag)4717 };

        /// <summary>Clears the tags on the outgoing hero and returns their values, before the swap writes any history.</summary>
        internal static int[] Take(Hero from)
        {
            var values = new int[Tags.Length];
            for (int i = 0; i < Tags.Length; i++)
            {
                values[i] = from[Tags[i]];
                if (values[i] != 0) from[Tags[i]] = 0;
            }
            return values;
        }

        /// <summary>Sets the taken values on the new hero.</summary>
        internal static void Give(Hero to, int[] values)
        {
            for (int i = 0; i < Tags.Length; i++)
                if (values[i] != 0) to[Tags[i]] = values[i];
        }
    }
}
