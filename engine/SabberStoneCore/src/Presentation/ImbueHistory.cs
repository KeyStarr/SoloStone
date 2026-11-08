using System.Collections.Generic;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Presentation
{
    // Pinned 37.0 client asset contract; see docs/reports/2026-11-02-deck-playtest-bugs.md (Imbue upgrade FX).
    // No hero-power CardDef or tag reader owns this effect: the client only plays it from an explicit SUB_SPELL_START.
    public static class ImbueHistory
    {
        public const string Upgrade = "EDRFX_Imbue_HeroPower_Buffimpact_Super:9a395e68d208eee4e920283927ab00cb";

        // SuperSpell places its impact at the subspell source, so the source is the upgraded hero power itself.
        // START/END stay adjacent: the caller's tag rows follow, and PowerProcessor replays a list whose packet ends on END.
        internal static void Pulse(HeroPower power)
        {
            if (!power.Game.History) return;
            power.Game.PowerHistory.Add(new PowerHistorySubSpellStart {
                Source = power.Id, SpellPrefabGuid = Upgrade, Targets = new List<int> { power.Id }
            });
            power.Game.PowerHistory.Add(new PowerHistorySubSpellEnd());
        }
    }
}
