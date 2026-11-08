using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ForgeOfOrigination
    {
        internal const int DbfId = 129947;
        [StandardCard("BE_561", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.None, RequiresFreeSlot = true,
                Task = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id);
                    int golem = ctx.SummonRightOfLocation("BE_561t");
                    if (golem != 0 && c.Hero.Armor >= 3)
                    {
                        c.Hero.Armor -= 3;
                        ctx.GiveBuff(golem, "BE_561e");
                    }
                    s[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
                })
            } };
        }

        [StandardCard("BE_561e", 131344)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(3)) };
        }
    }
}
