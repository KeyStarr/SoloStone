using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Presentation
{
    // Pinned 37.0 client prefab/FSM contract; see docs/design/shatter-presentation.md.
    public static class ShatterHistory
    {
        public const string Combine = "2795f3f54446b6c45a96f1981cda0df8";
        public const string Separate = "0384874a085f04440af9f66618dfb8bc";
        public const string CombinedSpawn = "83e833360c0c11a4285a07390ded073d";
        public const string SeparatedSpawn = "ad8ed082d78aad5478b1d0e682159ab4";
        public const int IndexTag = 1436; // FX_DATANUM_1, physical left/right, never half identity.

        public static bool IsConceal(string prefab) => prefab == Combine || prefab == Separate;
        public static bool IsSpawn(string prefab) => prefab == CombinedSpawn || prefab == SeparatedSpawn;

        internal static void Begin(Controller c)
        {
            if (c.Game.History) c.Game.PowerHistory.Add(new PowerHistoryBlockStart {
                BlockType = BlockType.TRIGGER, Source = c.Id, EffectIndex = -1
            });
        }
        internal static void End(Controller c)
        {
            if (c.Game.History) c.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        }
        internal static void Pulse(Controller c, string prefab, params IPlayable[] targets)
        {
            if (!c.Game.History) return;
            void Index(int ordinal, int value) => c.Game.PowerHistory.Add(new PowerHistoryTagChange {
                EntityId = targets[ordinal].Id, Tag = (GameTag)IndexTag, Value = value
            });
            if (prefab == Combine) { Index(0, 0); Index(1, 1); }
            c.Game.PowerHistory.Add(new PowerHistorySubSpellStart {
                Source = c.Hero.Id, SpellPrefabGuid = prefab, Targets = targets.Select(t => t.Id).ToList()
            });
            c.Game.PowerHistory.Add(new PowerHistorySubSpellEnd());
            // Presentation only: no entity state, snapshot, copy or gameplay modifier retains the index.
            if (prefab == Combine) { Index(0, 0); Index(1, 0); }
        }
    }
}
