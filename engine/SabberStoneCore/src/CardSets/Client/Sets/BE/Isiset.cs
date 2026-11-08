using System;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Isiset
    {
        [StandardCard("BE_259", 129851)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new SummonTask("BE_254"), new SummonTask("BE_254")) };
        }
        [StandardCard("BE_254", 129790)]
        internal static void ConfigureConstruct(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }

        internal static bool FreezeGate(Minion m) => m.Card.Id == "BE_254" && !m.IsSilenced && !m.IsDormant &&
            m.Zone == m.Controller.BoardZone && m[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0;

        internal static void SpellManaPaid(Controller controller, IPlayable source, int amount)
        {
            if (!(source is Spell) || amount == 0) return;
            foreach (var construct in controller.BoardZone.Where(FreezeGate).ToArray())
            {
                construct[GameTag.TAG_SCRIPT_DATA_NUM_1] = Math.Max(0, construct[GameTag.TAG_SCRIPT_DATA_NUM_1] - amount);
                if (construct[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0) construct.IsFrozen = false;
            }
        }

        internal static void Refresh(Game game)
        {
            // Every aura update: indexed scan; the Dragonbreath check only runs once a construct is found.
            for (int side = 0; side < 2; side++)
            {
                var board = (side == 0 ? game.Player1 : game.Player2).BoardZone;
                bool? dragonbreath = null;
                for (int i = 0; i < board.Count; i++)
                {
                    var m = board[i];
                    if (!FreezeGate(m)) continue;
                    dragonbreath ??= board.Any(b => b.Card.Id == "DRG_068" && !b.IsSilenced && !b.IsDormant && !b.ToBeDestroyed);
                    bool frozen = !dragonbreath.Value && m[GameTag.CANT_BE_FROZEN] == 0;
                    if (m.IsFrozen != frozen) m.IsFrozen = frozen;
                }
            }
        }
    }
}
