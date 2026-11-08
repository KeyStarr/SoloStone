using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScorchingRavager
    {
        internal const int DbfId = 123552;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // The export has no CATA_160e: Rush is the Soldier's own RUSH tag, which Silence clears.
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int id = ctx.Herald();
                if (id != 0 && g.IdEntityDic[id] is Minion soldier && soldier.Zone == c.BoardZone)
                    GrantRush(soldier);
            }) };
        }

        internal static void GrantRush(Minion minion)
        {
            minion.IsRush = true;
            var game = minion.Game;
            // Same readiness as printed Rush on entry: a fresh minion of the active player may attack minions now.
            if (!minion.HasCharge && minion.Controller == game.CurrentPlayer && minion.BoardEntryTurn == game.Turn
                && minion.NumAttacksThisTurn == 0 && !minion.AttackableByRush)
            {
                minion.IsExhausted = false;
                minion.AttackableByRush = true;
                game.RushMinions.Add(minion.Id);
            }
        }
    }
}
