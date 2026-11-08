using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DissolvingOoze
    {
        internal const int DbfId = 118252;
        [StandardCard("TLC_252", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion victim)) return;
                int attack = victim.AttackDamage, health = victim.Health;
                new DestroyTask(EntityType.TARGET).Process(g, c, s, victim, stack);
                var bones = Entity.FromCard(c, Cards.FromId("TLC_829t"), new EntityData {
                    [GameTag.DISPLAYED_CREATOR] = s.Id,
                    [GameTag.TAG_SCRIPT_DATA_NUM_1] = attack, [GameTag.TAG_SCRIPT_DATA_NUM_2] = health
                });
                Generic.AddHandPhase(c, bones);
            }) };
        }

        [StandardCard("TLC_829t", 117614)]
        internal static void ConfigureBones(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t is Minion) Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_829te"), (IPlayable)s, t,
                    s[GameTag.TAG_SCRIPT_DATA_NUM_1], s[GameTag.TAG_SCRIPT_DATA_NUM_2]);
            }) };
        }

        [StandardCard("TLC_829te", 117621)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
