using System;
using System.Runtime.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Ordered future-turn owners in game tags; no suppressed turn start, draw or refill.</summary>
    public static class TurnQueueRules
    {
        private const GameTag Count = (GameTag)32200;
        private static GameTag Slot(int index) => (GameTag)(40000 + index);
        public static bool IsInternalTag(GameTag tag) => tag == Count || (int)tag >= 40000 || (int)tag >= 32800 && (int)tag <= 32819;
        public static bool Active(Game game) => game[Count] > 0;

        public const GameTag TomeSelection = (GameTag)32805;

        private const GameTag ClockOwner = (GameTag)32800, Remaining = (GameTag)32801,
            LastTick = (GameTag)32802, EndPhase = (GameTag)32803;

        public static void Arm(IEntity actor, Controller owner, int boundaries, bool atEnd = false)
        {
            if (actor.Game != owner.Game || boundaries <= 0) throw new ArgumentException("Invalid owner clock.");
            actor[ClockOwner] = owner.Id; actor[Remaining] = boundaries;
            actor[EndPhase] = atEnd ? 1 : 0;
            actor[LastTick] = atEnd ? -1 : actor.Game.Turn;
        }

        public static bool Tick(IEntity actor, bool atEnd = false)
        {
            if (actor[Remaining] <= 0 || actor[ClockOwner] != actor.Game.CurrentPlayer.Id ||
                actor[EndPhase] != (atEnd ? 1 : 0) || actor[LastTick] == actor.Game.Turn) return false;
            actor[LastTick] = actor.Game.Turn;
            return --actor[Remaining] == 0;
        }

        // Store definitions on controllers: no extra entities, triggers, RNG or snapshot schema.
        private const GameTag OpeningCount = (GameTag)32804;
        private static GameTag OpeningSlot(int index) => (GameTag)(60000 + index);
        internal static void CaptureOpening(Game game)
        {
            if (!CardSets.Client.Sets.TheFinsBeyondTime.RequiresOpening(game)) return;
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                owner[OpeningCount] = owner.HandZone.Count;
                for (int i = 0; i < owner.HandZone.Count; i++) owner[OpeningSlot(i)] = owner.HandZone[i].Card.AssetId;
            }
        }
        public static Card[] StartingHand(Controller owner)
        {
            var cards = new Card[owner[OpeningCount]];
            for (int i = 0; i < cards.Length; i++) cards[i] = Cards.FromAssetId(owner[OpeningSlot(i)]);
            return cards;
        }
        public static void RedactOpening(Controller owner)
        {
            for (int i = 0; i < owner[OpeningCount]; i++) owner.NativeTags.Remove(OpeningSlot(i));
            owner.NativeTags.Remove(OpeningCount);
        }

        public static void Prepend(Game game, params Controller[] owners)
            =>Prepend(game,owners,new long[owners.Length]);

        internal static void PrependGranted(Game game,Controller owner,long operation)
            =>Prepend(game,new[]{owner},new[]{operation});

        private static void Prepend(Game game,Controller[] owners,long[] operations)
        {
            if(owners.Length!=operations.Length) throw new ArgumentException("Turn provenance length mismatch.");
            foreach (var owner in owners)
                if (owner != game.Player1 && owner != game.Player2) throw new ArgumentException("Turn owner belongs to another game.");
            int count = game[Count];
            // Import already granted legacy extra turns after the new segment exactly once.
            if (count == 0 && game.CurrentPlayer.NumTurnsLeft > 1)
            {
                count = game.CurrentPlayer.NumTurnsLeft - 1;
                for (int i = 0; i < count; i++) game[Slot(i)] = game.CurrentPlayer.Id;
                game.HistoryFacts.PrependFutureTurnOperations(game.HistoryFacts.MoveCurrentExtraTurnOperations(count));
                game.CurrentPlayer.NumTurnsLeft = 1;
            }
            for (int i = count - 1; i >= 0; i--) game[Slot(i + owners.Length)] = game[Slot(i)];
            for (int i = 0; i < owners.Length; i++) game[Slot(i)] = owners[i].Id;
            game.HistoryFacts.PrependFutureTurnOperations(operations);
            game[Count] = count + owners.Length;
        }

        internal static Controller Take(Game game,out long operation)
        {
            int count = game[Count];
            if (count == 0) {operation=0;return null;}
            var next = game.ControllerById(game[Slot(0)]);
            operation=game.HistoryFacts.TakeFutureTurnOperation();
            for (int i = 1; i < count; i++) game[Slot(i-1)] = game[Slot(i)];
            game[Slot(count-1)] = 0;
            game[Count] = count-1;
            return next;
        }
        internal static void MarkStarted(Game game,long operation)
        { game.HistoryFacts.MarkStartedTurnOperation(operation); }
        internal static long ConsumeStarted(Game game)
        { return game.HistoryFacts.ConsumeStartedTurnOperation(); }

        internal static void Validate(Game game, bool strictEvidence = true)
        {
            int count = game[Count];
            if (count < 0 || count > game.NativeTags.Count) throw new SerializationException("Invalid future-turn queue length.");
            for (int i = 0; i < count; i++)
                if (game[Slot(i)] != game.Player1.Id && game[Slot(i)] != game.Player2.Id)
                    throw new SerializationException("Invalid future-turn queue owner.");
            game.HistoryFacts.ValidateFutureTurnOperations(game,count,strictEvidence);
        }
    }
}
