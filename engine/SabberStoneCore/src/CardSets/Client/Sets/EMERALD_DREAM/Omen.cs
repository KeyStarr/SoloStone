using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Omen
    {
        internal const int DbfId = 113606;
        [StandardCard("EDR_421", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Damage);
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                    TriggerSource = TriggerSource.SELF,
                    SingleTask = new CustomTask((g, c, s, t, stack) => {
                        var progress = PayloadRules.Entries(s, DbfId).SingleOrDefault();
                        if (progress == null) PayloadRules.StoreValue(s, DbfId, 1);
                        else progress[PayloadRules.Value]++;
                        s[GameTag.TAG_SCRIPT_DATA_NUM_1] = Damage((IPlayable)s);
                    })
                },
                DeathrattleTask = new CustomTask((g, c, s, t, stack) =>
                    new EffectContext(g, c.Id, s.Id, stack: stack).DamageBatch(
                        c.Opponent.BoardZone.Where(m => !m.Untouchable).Select(m => m.Id)
                            .Append(c.Opponent.Hero.Id), Damage((IPlayable)s)))
            };
        }

        private static int Damage(IPlayable source) =>
            1 + PayloadRules.Entries(source, DbfId).Sum(e => e[PayloadRules.Value]);
    }
}
