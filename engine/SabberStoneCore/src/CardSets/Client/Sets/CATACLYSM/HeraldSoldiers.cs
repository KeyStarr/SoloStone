using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HeraldSoldiers
    {
        internal static void ConfigureAzshara(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new CustomTask((g, c, s, t, stack) =>
                    Effects.Attack_N(HeraldRules.Magnitude((IPlayable)s)).ApplyTo(c.Hero, true))
            } };
        }
        internal static void ConfigureAlAkir(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new AdjacentAura(GameTag.TAG_SCRIPT_DATA_NUM_1) };
        }
        internal static void ConfigureChoGall(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    HeraldRules.ConsumeNeighbor((Minion)s);
                })
            } };
        }
        internal static void ConfigureRagnaros(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var selected = new TaskStack();
                new RandomTask(1, EntityType.ENEMIES).Process(g, c, s, t, selected);
                new DamageTask(HeraldRules.Magnitude((IPlayable)s), EntityType.STACK).Process(g, c, s, t, selected);
            }) };
        }
    }
}
