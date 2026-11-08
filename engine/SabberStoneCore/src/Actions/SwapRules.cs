using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    public static class SwapRules
    {
        private static readonly Dictionary<string, int> DamageSummons = new Dictionary<string, int>(StringComparer.Ordinal);
        public const GameTag PendingSummon = (GameTag)32710;
        internal const GameTag ResolvingSummons = (GameTag)32711;
        internal const GameTag RefillActive = (GameTag)32712;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 32710 && (int)tag <= 32712;

        public static void DeclareDamageSummon(Card card, int distinctCharacters)
        {
            if (distinctCharacters <= 0) throw new ArgumentOutOfRangeException(nameof(distinctCharacters));
            card.ConsumesHistory = true;
            DamageSummons[card.Id] = distinctCharacters;
        }

        internal static void DamageResolved(Character victim, int damage)
        {
            var owner = victim.Controller; var game = victim.Game;
            if (damage <= 0 || game.CurrentPlayer != owner) return;
            var eligible = owner.HandZone.Concat(owner.DeckZone)
                .Where(card => card[PendingSummon] == 0 && DamageSummons.ContainsKey(card.Card.Id)).ToArray();
            if (eligible.Length == 0) return;
            int count = game.HistoryFacts.Query.Damage(game.HistoryFacts.Query.ThisTurn)
                .Where(fact => fact.Amount > 0 && fact.VictimControllerId == owner.Id)
                .Select(fact => fact.VictimId).Distinct().Count();
            foreach (var card in eligible)
                if (count >= DamageSummons[card.Card.Id]) DeferSummon(card);
        }

        public static void DeferSummon(IPlayable card)
        {
            if (card is Minion && InHandOrDeck(card) && card[PendingSummon] == 0)
                card[PendingSummon] = card.Game.NextOop;
        }

        internal static bool InHandOrDeck(IPlayable card) => card.Zone == card.Controller.HandZone || card.Zone == card.Controller.DeckZone;

        internal static void ZoneChanged(IPlayable card, IZone destination)
        {
            // The transient null between Remove/Add must not cancel a deck-to-hand request.
            if (card[PendingSummon] != 0 && destination != null && destination.Type != Zone.HAND && destination.Type != Zone.DECK)
                card[PendingSummon] = 0;
            if (card[RefillActive] != 0 && destination != null && destination.Type != Zone.PLAY)
                ColossalRules.StopRefill(card);
        }

        internal static void ChangingDefinition(IPlayable card)
        {
            if (card[PendingSummon] != 0) card[PendingSummon] = 0;
            bool refill = card[RefillActive] != 0;
            ColossalRules.StopRefill(card);
            if (refill) card.NativeTags.Remove(GameTag.TAG_SCRIPT_DATA_NUM_1);
        }

        public static bool ExchangeBoardAndHand(Minion outgoing, Minion incoming)
        {
            if (outgoing == null || incoming == null || outgoing.Game != incoming.Game || outgoing.Controller == incoming.Controller
                || outgoing.Zone != outgoing.Controller.BoardZone || incoming.Zone != incoming.Controller.HandZone
                || outgoing.ToBeDestroyed || outgoing.IsDormant) return false;
            var game = outgoing.Game; var owner = outgoing.Controller; var opponent = incoming.Controller;
            int boardPosition = outgoing.ZonePosition, handPosition = incoming.ZonePosition;
            using var mutation = opponent.HandZone.BeginCardStateMutation();
            opponent.HandZone.Remove(incoming);
            owner.BoardZone.Remove(outgoing);
            foreach (var attachment in outgoing.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>()) attachment.Remove();
            outgoing.Reset();
            TransferController(outgoing, opponent);
            TransferController(incoming, owner);
            opponent.HandZone.Add(outgoing, handPosition);
            if (game.History) game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(outgoing));
            Generic.SummonBlock(game, incoming, boardPosition, outgoing);
            game.AuraUpdate();
            return true;
        }

        public static void ShuffleHands(Controller owner)
        {
            var opponent = owner.Opponent; var game = owner.Game;
            int count = owner.HandZone.Count;
            var pool = owner.HandZone.Concat(opponent.HandZone).ToArray();
            if (count == 0 || opponent.HandZone.Count == 0) return;
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int j = game.Random.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            game.OnRandomHappened(true);
            var selected = pool.Take(count).ToArray();
            var outgoing = owner.HandZone.Where(card => !selected.Contains(card)).ToArray();
            var incoming = selected.Where(card => card.Controller == opponent).ToArray();
            var transfers = outgoing.Concat(incoming).ToArray();
            var ownAuras = transfers.Where(card => card.OngoingEffect != null).ToArray();
            using var first = owner.HandZone.BeginCardStateMutation();
            using var second = opponent.HandZone.BeginCardStateMutation();
            foreach (var card in transfers)
            {
                card.ActivatedTrigger?.Remove();
                card.OngoingEffect?.Remove();
                foreach (var aura in card.Controller.HandZone.Auras.ToArray()) aura.DeApply(card);
            }
            for (int i = 0; i < outgoing.Length; i++) owner.HandZone.ExchangeSlots(opponent.HandZone, outgoing[i], incoming[i]);
            foreach (var card in transfers)
            {
                TransferController(card, ((HandZone)card.Zone).Controller);
                foreach (var aura in card.Controller.HandZone.Auras.ToArray()) aura.EntityAdded(card);
                card.Power?.Trigger?.Activate(card, TriggerActivation.HAND);
                if (ownAuras.Contains(card)) card.Power.Aura.Activate(card);
            }
            CostRules.RefreshHandAuras(game);
            game.AuraUpdate();
            if (game.History)
                foreach (var card in transfers) game.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(card));
            // Publish all recipient facts before observers can react to the committed hands.
            foreach (var card in transfers) game.Facts.HandEntered(card, Zone.HAND);
            foreach (var card in transfers) game.TriggerManager.OnZoneTrigger(card);
            foreach (var hand in new[] { owner.HandZone, opponent.HandZone })
                if (hand.Any(card => card.Card.Shatter != null)) ShatterRules.StabilizeHand(hand.Controller);
        }

        private static void TransferController(IPlayable card, Controller receiver)
        {
            card.Controller = receiver;
            card[GameTag.CONTROLLER] = receiver.PlayerId;
            foreach (var actor in card.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>())
                actor.RebindController(receiver, refreshAuras: false);
            if (card.ChooseOnePlayables != null)
                foreach (var option in card.ChooseOnePlayables)
                {
                    option.Zone?.Remove(option);
                    option.Controller = receiver;
                    option[GameTag.CONTROLLER] = receiver.PlayerId;
                    receiver.SetasideZone.Add(option);
                }
        }
    }
}
