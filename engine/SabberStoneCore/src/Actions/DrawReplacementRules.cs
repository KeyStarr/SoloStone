using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Kettle;

namespace SabberStoneCore.Actions
{
    /// <summary>Draw causes and destinations; all retained state belongs to exported entities.</summary>
    public static class DrawReplacementRules
    {
        private const int BacklogKey = 126701;
        public const string ReturnPrefab = "9ecdc1d9a5c5e4c4fb08ce2ca3bf7be9";

        internal static bool TryReplaceNormalDraw(Controller controller)
        {
            if (controller.BoardZone.Any(m => m.Card.Id == "TIME_617" && !m.IsSilenced && !m.IsDormant && !m.ToBeDestroyed))
                return true;
            if (DiscoverRealRules.TryReplaceNormalDraw(controller)) return true;
            if (controller.DeckZone.IsEmpty) return false;
            var effect = controller.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "BE_250e");
            if (effect == null) return false;
            effect.Power.PowerTask.Process(controller.Game, controller, effect, null);
            return true;
        }

        public static void MoveSpellsToTop(Controller controller)
        {
            var spells = controller.DeckZone.Where(p => p.Card.Type == CardType.SPELL).ToList();
            var others = controller.DeckZone.Where(p => p.Card.Type != CardType.SPELL).ToArray();
            if (spells.Count > 1) spells.Shuffle(controller.Game.Random);
            int index = 0;
            foreach (var card in others.Concat(spells)) controller.DeckZone.SetEntity(index++, card);
        }

        internal static bool RetainOverdraw(Controller controller, IPlayable card)
        {
            if (!controller.HandZone.IsFull) return false;
            var effect = controller.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "JAIL_509e");
            if (effect == null) return false;
            if (controller.Game.History)
                controller.Game.PowerHistory.Add(new PowerHistoryRetainedOverdraw {
                    Entity = PowerHistoryBuilder.ShowEntity(card).Entity, Player = controller.Id
                });
            int order = PayloadRules.Entries(effect, BacklogKey).Select(p => p[PayloadRules.Value]).DefaultIfEmpty().Max() + 1;
            PayloadRules.Retain(effect, BacklogKey, card);
            card[PayloadRules.Value] = order;
            controller.SetasideZone.Add(card);
            // Native deck tooltips read this public count from the player enchantment.
            effect[GameTag.TAG_SCRIPT_DATA_NUM_2] = PayloadRules.Entries(effect, BacklogKey).Length;
            return true;
        }

        public static void DeliverBacklog(Controller controller)
        {
            if (controller.HandZone.CardStateDeferred || controller.Game.ResolvingPlayerAction
                || controller.Game.Resolution.Depth != 0 || controller.Game.Resolution.HasSuspendedEffect
                || controller.Game.Player1.Choice != null || controller.Game.Player2.Choice != null) return;
            var effect = controller.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "JAIL_509e");
            if (effect == null) return;
            while (!controller.HandZone.IsFull)
            {
                var entries = PayloadRules.Entries(effect, BacklogKey);
                if (entries.Length == 0) break;
                var card = entries[controller.Game.Random.Next(entries.Length)];
                if (controller.Game.History)
                {
                    // A player lifecycle scope keeps the override ahead of the later spawn task list.
                    controller.Game.PowerHistory.Add(new PowerHistoryBlockStart {
                        BlockType = BlockType.TRIGGER, Source = controller.Id, EffectIndex = -1
                    });
                    controller.Game.PowerHistory.Add(new PowerHistorySubSpellStart {
                        Source = controller.Hero.Id, SpellPrefabGuid = ReturnPrefab,
                        Targets = new System.Collections.Generic.List<int> { card.Id }
                    });
                    controller.Game.PowerHistory.Add(new PowerHistorySubSpellEnd());
                }
                // Detach only the backlog link; the card's own nested payload must survive delivery.
                card[PayloadRules.Owner] = 0;
                card[PayloadRules.Key] = 0;
                effect[GameTag.TAG_SCRIPT_DATA_NUM_2] = entries.Length - 1;
                controller.SetasideZone.Remove(card);
                card[PayloadRules.Stored] = 0;
                // Publish the private destination before cost/SHOW packets, including delayed flushes.
                card[GameTag.ZONE] = (int)Zone.HAND;
                card.Cost = System.Math.Max(0, card.Cost - 1);
                Generic.AddHandPhase(controller, card);
                if (controller.Game.History) controller.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            }
        }

        internal static void Overdrawn(IPlayable card) => OffboardDeathrattle(card);

        internal static void OffboardDeathrattle(IPlayable card)
        {
            if (card.Card.Id != "JAIL_398" || card is not Minion minion || minion.IsSilenced) return;
            card.Game.TaskQueue.StartEvent();
            try
            {
                minion.ActivateTask(PowerActivation.DEATHRATTLE, fromDeath: false);
                card.Game.ProcessTasks();
                card.Game.DeathProcessingAndAuraUpdate();
            }
            finally { card.Game.TaskQueue.EndEvent(); }
        }

        internal static bool DestroyOffboard(IPlayable card)
        {
            if (card.Card.Id != "JAIL_398" || card.Zone != card.Controller.HandZone && card.Zone != card.Controller.DeckZone)
                return false;
            card.Controller.GraveyardZone.Add(card.Zone.Remove(card));
            OffboardDeathrattle(card);
            return true;
        }
    }
}
