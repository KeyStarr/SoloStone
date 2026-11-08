using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model
{
    public partial class Game
    {
        internal void RetireBoardObject(BoardObject location, bool queueDeathrattle = true)
        {
            if (location.Zone == location.Controller.BoardZone)
            {
                location.Controller.BoardZone.Remove(location);
                location.ZonePosition = -1;
                // Free the slot and emit GRAVEYARD now; enchantment removal queues granted Deathrattles.
                location.Controller.GraveyardZone.MoveTo(location);
                location[GameTag.DAMAGE] = 0;
            }
            // Final charges free their slot now; GraveYard queues their Deathrattle after the recipe.
            if (!queueDeathrattle) return;
            if (!DeadBoardObjects.Remove(location)) return;
            if (location.Zone == location.Controller.GraveyardZone)
            {
                if (location.HasDeathrattle) location.ActivateTask(PowerActivation.DEATHRATTLE);
                location.Controller.GraveyardZone.RemoveEnchantments(location);
            }
        }

        private void ResolveMixedBoardDeaths()
        {
            var dead = DeadMinions.Cast<IPlayable>().Concat(DeadBoardObjects).OrderBy(p => p.OrderOfPlay).ToArray();
            foreach (Minion minion in DeadMinions) minion.LastBoardPosition = minion.ZonePosition;
            foreach (BoardObject location in DeadBoardObjects)
                if (location.Zone == location.Controller.BoardZone) location.LastBoardPosition = location.ZonePosition;
            if (History) PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.DEATHS, 1, "", 0, 0));
            var deathCosts = DeadMinions.ToDictionary(m => m.Id, m => m.Cost);
            foreach (IPlayable entity in dead)
            {
                if (entity is BoardObject location) { RetireBoardObject(location); continue; }
                var minion = (Minion)entity;
                Actions.CorpseRules.RecordDeath(minion);
                Facts.Died(minion, deathCosts[minion.Id]);
                TriggerManager.OnDeathTrigger(minion);
                bool reborn = minion[GameTag.REBORN] == 1 && minion[GameTag.HAS_BEEN_REBORN] == 0;
                minion.Zone.Remove(minion);
                if (minion.HasDeathrattle) minion.ActivateTask(PowerActivation.DEATHRATTLE);
                minion.Controller.GraveyardZone.Add(minion);
                minion.Controller.NumFriendlyMinionsThatDiedThisTurn++;
                CurrentPlayer.NumMinionsPlayerKilledThisTurn++;
                NumMinionsKilledThisTurn++;
                if (reborn) _rebornMinions.Add(minion);
                DeadMinions.Remove(minion);
            }
            if (History) PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
        }
    }
}
