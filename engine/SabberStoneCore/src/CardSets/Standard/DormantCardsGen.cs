using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Standard
{
    public static class DormantCardsGen
    {
        public static void AddAll(Dictionary<string, CardDef> cards)
        {
            cards.Add("EDR_469", new CardDef());
            cards.Add("EDR_469e", new CardDef(new Power
            {
                Trigger = new Trigger(TriggerType.INSPIRE)
                {
                    TriggerSource = TriggerSource.FRIENDLY,
                    SingleTask = new CustomTask((game, controller, source, target, stack) =>
                        ((Minion)((Enchantment)source).Target).Awaken())
                }
            }));
        }
    }
}
