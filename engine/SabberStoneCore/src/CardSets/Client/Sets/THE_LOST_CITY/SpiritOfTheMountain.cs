using System;
using System.Collections.Generic;
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
    internal static class SpiritOfTheMountain
    {
        internal static readonly Race[] Types = { Race.BEAST, Race.DEMON, Race.DRAGON, Race.ELEMENTAL, Race.MECHANICAL,
            Race.MURLOC, Race.PIRATE, Race.TOTEM, Race.UNDEAD, Race.NAGA, Race.QUILBOAR, Race.DRAENEI };
        [StandardCard("TLC_229", 117538)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            QuestDefinition.Register(new QuestDefinition(117538, 6, QuestRewardKind.CardToHand, "TLC_229t14"));
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                OwnerCondition = new SelfCondition(owner => NextMask((Spell)owner, PlayedRaces(owner.Game.CurrentEventData.EventSource)) != 0),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    Count((Spell)s, PlayedRaces(g.CurrentEventData.EventSource));
                })
            } };
        }

        private static IReadOnlyList<Race> PlayedRaces(IPlayable played) =>
            played.Game.HistoryFacts.Query.Plays().LastOrDefault(f => f.EntityId == played.Id)?.Races ?? played.Card.Races;

        private static void Count(Spell quest, IReadOnlyList<Race> races)
        {
            int mask = NextMask(quest, races);
            if (mask == 0) return;
            quest[(GameTag)(33060 + quest.QuestProgress)] = mask;
            QuestRules.Advance(quest);
        }

        private static int NextMask(Spell quest, IReadOnlyList<Race> races)
        {
            if (quest.Zone != quest.Controller.SecretZone || quest.QuestProgress >= 6) return 0;
            int mask = 0;
            for (int i = 0; i < Types.Length; i++) if (races.Contains(Race.ALL) || races.Contains(Types[i])) mask |= 1 << i;
            if (mask == 0) return 0;
            int count = quest.QuestProgress;
            var masks = Enumerable.Range(0, count).Select(i => quest[(GameTag)(33060 + i)]).Append(mask).ToArray();
            var assigned = Enumerable.Repeat(-1, Types.Length).ToArray();
            bool Match(int entry, bool[] seen)
            {
                for (int type = 0; type < Types.Length; type++)
                {
                    if ((masks[entry] & (1 << type)) == 0 || seen[type]) continue;
                    seen[type] = true;
                    if (assigned[type] < 0 || Match(assigned[type], seen)) { assigned[type] = entry; return true; }
                }
                return false;
            }
            for (int i = 0; i < masks.Length; i++) if (!Match(i, new bool[Types.Length])) return 0;
            // A maximum independent basis suffices: rejected dependent plays cannot improve later matching.
            return mask;
        }
    }
}
