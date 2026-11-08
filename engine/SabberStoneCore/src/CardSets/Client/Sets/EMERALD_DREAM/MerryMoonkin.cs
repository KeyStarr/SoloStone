using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MerryMoonkin
    {
        internal const int DbfId = 115805;
        private static int Armor(IPlayable source) => 1 + source.Controller.BoardZone.Count(m => m.Card.Name == "Wisp");
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: base one Armor plus one per currently controlled Wisp of any printing, evaluated at owner end (32.0.3 wording).
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Armor);
            card.Power = new Power { Aura = new WispDisplay(), Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => c.Hero.GainArmor((IPlayable)s, Armor((IPlayable)s)))
            } };
        }
        // Public board text follows the aura lifecycle; hidden hand text uses HistoryDisplay.
        private sealed class WispDisplay : IAura
        {
            private IPlayable _owner;
            public IPlayable Owner => _owner;
            public void Activate(IPlayable owner)
            {
                var aura = new WispDisplay { _owner = owner };
                owner.Game.Auras.Add(aura); owner.OngoingEffect = aura;
            }
            public void Update() => _owner[GameTag.TAG_SCRIPT_DATA_NUM_1] = Armor(_owner);
            public void Remove()
            {
                _owner.OngoingEffect = null;
                _owner.Game.Auras.Remove(this);
            }
            public void Clone(IPlayable clone) => Activate(clone);
        }
    }
}
