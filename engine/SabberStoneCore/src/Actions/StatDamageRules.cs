using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.Actions
{
    internal static class StatDamageRules
    {
        internal static void Resolved(IPlayable source, int amount)
        {
            if (!(source is Spell) || amount <= 0) return;
            foreach (var minion in source.Controller.BoardZone.Where(m => m.Card.Id == "CATA_487" && !m.IsSilenced && !m.Untouchable).ToArray())
            {
                if (minion.AppliedEnchantments?.Any(e => e.Card.Id == "CATA_487e1") == true) continue;
                // Consume before victim reactions or another missile can observe this instance.
                Enchantment.GetInstance(minion.Controller, minion, minion, Cards.FromId("CATA_487e1")).ExpireAtEndOfTurn();
                source.Game.TaskQueue.Enqueue(new AddEnchantmentTask("CATA_487e", EntityType.SOURCE), minion.Controller, minion, null);
            }
        }
    }
}
