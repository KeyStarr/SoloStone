using SabberStoneCore.Conditions;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheFoodChain
    {
        [StandardCard("TLC_830", 117672)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(117672, 4, QuestRewardKind.CardToHand, "TLC_830t"));
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                OwnerCondition = new SelfCondition(owner => PayloadRules.Entries(owner, owner.Game.CurrentEventData?.EventSource?.Id ?? 0).Any()),
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    var entry = PayloadRules.Entries(s, g.CurrentEventData.EventSource.Id).SingleOrDefault();
                    if (entry == null) return;
                    int bit = entry[PayloadRules.Value];
                    PayloadRules.Remove(entry);
                    if ((s[PayloadRules.Value] & bit) != 0) return;
                    s[PayloadRules.Value] |= bit;
                    QuestRules.Advance((Spell)s);
                })
            } };
        }
        internal static void Capture(Minion minion)
        {
            var quest = minion.Controller.SecretZone.ModernQuests.FirstOrDefault(q => q.Card.Id == "TLC_830");
            if (quest == null || !minion.Card.IsRace(Race.BEAST)) return;
            minion.Game.AuraUpdate();
            int attack = minion.AttackDamage;
            if (attack != 1 && attack != 3 && attack != 5 && attack != 7) return;
            PayloadRules.StoreValue(quest, minion.Id, 1 << (attack / 2), (PayloadPolicy)0);
        }
    }
}
