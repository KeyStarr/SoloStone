using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KronntheImpaler
    {
        internal const int DbfId = 130765;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var minion in c.BoardZone.GetAll())
                {
                    if (minion.IsDead || minion.Zone != c.BoardZone || minion.AttackDamage == 0) continue;
                    var targets = new TaskStack();
                    new IncludeTask(EntityType.ENEMIES).Process(g, c, minion, null, targets);
                    new FilterStackTask(SabberStoneCore.Conditions.SelfCondition.IsNotDead).Process(g, c, minion, null, targets);
                    if (targets.Playables.Count == 0) continue;
                    new RandomTask(1, EntityType.STACK).Process(g, c, minion, null, targets);
                    var enemy = (ICharacter)targets.Playables[0];
                    if (g.History)
                    {
                        // Attribute each hit without replaying the contributor card's trigger effect.
                        g.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, minion.Id, "", -1, enemy.Id, effectIndex: -1));
                        g.PowerHistory.SelectResolvedTarget(enemy);
                        g.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.TARGET,
                            Data = minion.Id, Info = new List<int> { enemy.Id } });
                    }
                    Generic.DamageCharFunc(minion, enemy, minion.AttackDamage, false);
                    if (g.History) g.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
                }
            }) };
        }

    }
}
