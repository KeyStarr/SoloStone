using System;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Decimation
    {
        internal const int DbfId = 123056;
        private static int DamageDisplay(IPlayable p) => 1 +
            p.Controller.BoardZone.GetAll().Length + p.Controller.Opponent.BoardZone.GetAll().Length;

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, DamageDisplay);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var minions = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).Select(m => m.Id).ToArray();
                int spellPower = SabberStoneCore.Actions.SpellDamageRules.Bonus((SabberStoneCore.Model.Entities.IPlayable)s);
                int damage = (1 + minions.Length + spellPower) * (int)Math.Pow(2, c.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
                new EffectContext(g, c.Id, s.Id).DamageBatch(minions, damage);
            }) };
        }
    }
}
