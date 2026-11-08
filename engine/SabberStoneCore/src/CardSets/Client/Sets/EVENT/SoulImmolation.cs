using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SoulImmolation
    {
        internal const GameTag Upgrades = (GameTag)31293;
        internal static bool UpgradedCondition(IPlayable source) => source.Controller.Hero.HeroPower.Card.Id == "JAIL_EVENT_101hp";
        [StandardCard("JAIL_EVENT_101", 130086)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(UpgradedCondition);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (UpgradedCondition((IPlayable)s)) {
                    var power = c.Hero.HeroPower;
                    power[Upgrades]++;
                    power[(GameTag)2] = 2 + power[Upgrades];
                } else {
                    new ReplaceHeroPower(Cards.FromId("JAIL_EVENT_101hp")).Process(g,c,s,null);
                    c.Hero.HeroPower[(GameTag)2] = 2;
                    BasicHeroPowerRules.EnrollPowerAuras(c, c.Hero.HeroPower);
                }
            }) };
        }
        [StandardCard("JAIL_EVENT_101hp", 132887)]
        internal static void Power(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g,c,s,t,stack) => {
                    var enemies = c.Opponent.BoardZone.GetAll().Where(m => !m.IsDormant && !m.Untouchable && !m.ToBeDestroyed)
                        .Cast<ICharacter>().Append(c.Opponent.Hero).Where(e => !e.ToBeDestroyed).ToArray();
                    if (enemies.Length != 0) Generic.DamageCharFunc((IPlayable)s, enemies[g.Random.Next(enemies.Length)], 2 + s[Upgrades], false);
                }),
                Trigger = new Trigger(TriggerType.SUMMON) { TriggerSource = TriggerSource.FRIENDLY,
                    Condition = new SelfCondition(s => s.Card.IsRace(Race.DEMON)),
                    SingleTask = new CustomTask((g,c,s,t,stack) => ((HeroPower)s).IsExhausted = false)
                }
            };
        }
    }
}
