using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Judgment
    {
        internal const int DbfId = 126616;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion chosen)) return;
                int attack = chosen.AttackDamage, health = chosen.Health;
                foreach (var minion in c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).Where(m => !m.Untouchable).ToArray())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_326e"), (IPlayable)s, minion, attack, health, 0);
            }) };
        }

        internal static void ConfigureStats(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(0), Effects.SetMaxHealth(0)) { UseScriptTag = true } };
        }
    }
}
