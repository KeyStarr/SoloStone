using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static class TemporaryRules
    {
        public const GameTag Temporary = (GameTag)3630;
        // Private tags: residence, captured boundary/residence, last processed controller boundary, scoped hand transfer.
        private const GameTag Residence = (GameTag)32020, CapturedTurn = (GameTag)32021,
            CapturedResidence = (GameTag)32022, LastBoundary = (GameTag)32023, Moving = (GameTag)32025;
        public const GameTag DiscardReveal = (GameTag)32024;
        public static bool IsPrivateTag(GameTag tag) => (int)tag >= 32020 && (int)tag <= 32025 || (int)tag >= 32810 && (int)tag <= 32819;
        private const GameTag PendingMana = (GameTag)32810, GrantedMana = (GameTag)32811,
            PreviousOwner = (GameTag)32812, AttackBanTurn = (GameTag)32813,
            FinsCopy = (GameTag)32814, SavedOrder = (GameTag)32815;

        public static void NextTurnMana(Controller owner) => owner[PendingMana]++;
        internal static void ControllerStart(Controller owner)
        {
            if (owner[PendingMana] == 0) return;
            int granted = Math.Min(owner[PendingMana], Math.Max(0, owner[GameTag.MAXRESOURCES] - owner.BaseMana - owner.TemporaryMana));
            owner[PendingMana] = 0; owner[GrantedMana] = granted;
            owner.TemporaryMana += granted;
        }
        public static bool BlocksAttack(IEntity target) => target.AppliedEnchantments?.Any(e =>
            e[PreviousOwner] != 0 && e[AttackBanTurn] == target.Game.Turn) == true;

        public static void Borrow(Minion target, IPlayable source)
        {
            if (target == null || target.Zone != target.Controller.BoardZone || target.IsDormant) return;
            var previous = target.Controller;
            new ControlTask(EntityType.TARGET).Process(source.Game, source.Controller, source, target);
            if (target.Controller != source.Controller || target.ToBeDestroyed) return;
            var actor = Enchantment.GetInstance(source.Controller, source, target, Cards.FromId("CATA_496e"));
            actor[PreviousOwner] = previous.Id; actor[AttackBanTurn] = source.Game.Turn;
            TurnQueueRules.Arm(actor, previous, 1, atEnd:true);
            actor.Power.Trigger.Activate(actor);
            PublishAttack(target);
        }
        public static void ControlEnd(Enchantment actor)
        {
            if (actor.Target is not Minion target || target.Zone != target.Controller.BoardZone || target.ToBeDestroyed)
            { actor[PreviousOwner] = 0; actor.Remove(); return; }
            if (actor[AttackBanTurn] == actor.Game.Turn)
            { actor[AttackBanTurn] = 0; PublishAttack(target); }
            if (TurnQueueRules.Tick(actor, atEnd:true)) actor.Remove();
        }
        internal static void RemovingControl(Enchantment actor)
        {
            int previous = actor[PreviousOwner];
            if (previous == 0) return;
            actor[PreviousOwner] = 0; actor[AttackBanTurn] = 0;
            if (actor.Target is not Minion target) return;
            PublishAttack(target);
            if (target.Zone == target.Controller.BoardZone && !target.ToBeDestroyed && target.Controller.Id != previous)
                new ControlTask(EntityType.TARGET).Process(actor.Game, actor.Game.ControllerById(previous), actor, target);
        }
        private static void PublishAttack(IPlayable target)
        {
            if (target.Game.History) target.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(target.Id,GameTag.CANT_ATTACK,target[GameTag.CANT_ATTACK]));
        }

        public static void SwapStartingHand(IPlayable source)
        {
            var owner=source.Controller;
            var actor=Enchantment.GetInstance(owner,source,owner,Cards.FromId("TIME_706e3"));
            using (owner.HandZone.BeginCardStateMutation())
            {
                int order=0;
                foreach(var card in owner.HandZone.ToArray())
                {
                    CardStateBoundary.EnrollAttachments(card);
                    owner.HandZone.DetachForEscrow(card);
                    PayloadRules.Retain(actor,120774,card);card[SavedOrder]=++order;
                    owner.SetasideZone.Add(card);
                }
                foreach(var card in TurnQueueRules.StartingHand(owner))
                {
                    var copy=Entity.FromCard(owner,card,new EntityData { [FinsCopy]=actor.Id, [GameTag.CREATOR]=source.Id });
                    CardStateBoundary.EnrollAttachments(copy);
                    Generic.AddHandPhase(owner,copy);
                }
            }
        }
        private static void RestoreHand(Enchantment actor)
        {
            var owner=actor.Controller;
            using (owner.HandZone.BeginCardStateMutation())
            {
                foreach(var card in owner.HandZone.Where(c=>c[FinsCopy]==actor.Id).ToArray())
                    owner.SetasideZone.Add(owner.HandZone.Remove(card));
                foreach(var card in PayloadRules.Entries(actor,120774).OrderBy(c=>c[SavedOrder]))
                {
                    card[PayloadRules.Owner]=0;card[PayloadRules.Key]=0;card[PayloadRules.Stored]=0;
                    owner.SetasideZone.Remove(card);
                    card[GameTag.ZONE]=(int)Zone.HAND;
                    Generic.AddHandPhase(owner,card);
                    card[SavedOrder]=0;
                }
            }
            actor.Remove();
        }
        internal static bool IsReturningSavedHand(IPlayable card) => card[SavedOrder] > 0 && card[PayloadRules.Stored] == 0 || HandTimelineRules.IsReturning(card);

        public static bool IsTemporary(IPlayable card) => card != null && card[Temporary] != 0;
        private static Enchantment Helper(IPlayable card) => card.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "GBL_999e");

        public static void Make(IPlayable card)
        {
            // Hero cards are ordinary hand cards (Cursed Catacombs can pick Deathwing, Worldbreaker); only the in-play hero is not.
            if (card == null || card is Enchantment || card is HeroPower || card == card.Controller.Hero
                || (card.Zone != null && card.Zone != card.Controller.HandZone && card.Zone.Type != Zone.SETASIDE))
                throw new ArgumentException("Temporary requires a hand card or an ungranted card.", nameof(card));
            if (IsTemporary(card)) return;
            int before = card.Cost;
            card[Temporary] = 1;
            CardStateBoundary.EnrollAttachments(card);
            if (card[Residence] == 0) card[Residence] = 1;
            if (Helper(card) == null) Enchantment.GetInstance(card.Controller, card, card, Cards.FromId("GBL_999e"));
            if (card.Zone == card.Controller.HandZone) PublishCost(card, before);
        }
        private static void PublishCost(IPlayable card, int before)
        {
            int cost = card.Cost;
            if (card.Game.History && before != cost)
                card.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(card.Id, GameTag.COST, cost));
        }
        internal static void OnHandEntry(IPlayable card)
        {
            if (!IsTemporary(card)) return;
            var helper = Helper(card);
            if (helper != null && helper.Controller != card.Controller) helper.RebindController(card.Controller);
            if (helper != null && card[Moving] != 0 && card.Game.History)
            {
                // Capture transfer visibility now; Dark Bribe may already have rebound the helper.
                card.Game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(helper));
                card.Game.PowerHistory.Add(new PowerHistoryShowEntity { Entity = new PowerHistoryEntity {
                    Id = helper.Id, Name = helper.Card.Id,
                    Tags = helper.NativeTags.ToDictionary(pair => pair.Key, pair => pair.Value)
                } });
            }
            // Matches requires HAND; the pre-entry SHOW still contains the undiscounted quote.
            if (card.Game.History) card.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(card.Id, GameTag.COST, card.Cost));
        }
        internal static void OnHandExit(IPlayable card)
        {
            if (!IsTemporary(card) || card[Moving] != 0) return;
            int before = card.Cost;
            card[Temporary] = 0;
            card[Residence] = checked(card[Residence] + 1);
            Helper(card)?.Remove();
            PublishCost(card, before);
        }
        internal static IDisposable PreserveHandTransfer(IPlayable card)
        {
            if (!IsTemporary(card)) return null;
            card[Residence] = checked(card[Residence] + 1);
            card[Moving] = 1;
            return new HandTransfer(card);
        }
        private sealed class HandTransfer : IDisposable
        {
            private readonly IPlayable card;
            internal HandTransfer(IPlayable card) { this.card = card; }
            public void Dispose()
            {
                card[Moving] = 0;
                if (card.Zone != card.Controller.HandZone) OnHandExit(card);
            }
        }
        internal static void BeginControllerEnd(Controller controller)
        {
            if (controller[LastBoundary] == controller.Game.Turn) return;
            foreach (var card in controller.HandZone.Where(IsTemporary))
            {
                var helper = Helper(card);
                if (helper == null) continue;
                helper[CapturedTurn] = controller.Game.Turn;
                helper[CapturedResidence] = card[Residence];
            }
        }
        public static void CaptureControllerEnd(Controller controller)
        {
            HandTimelineRules.ControllerEnd(controller);
            if (controller[GrantedMana] > 0)
            {
                controller.TemporaryMana = Math.Max(0,controller.TemporaryMana-controller[GrantedMana]);
                controller[GrantedMana] = 0;
            }
            foreach(var actor in controller.AppliedEnchantments?.Where(e=>e.Card.Id=="TIME_706e3").Reverse().ToArray()
                ?? Array.Empty<Enchantment>()) RestoreHand(actor);
            if (controller[LastBoundary] == controller.Game.Turn) return;
            if (controller.HandZone.Any(IsTemporary)) controller[LastBoundary] = controller.Game.Turn;
            // Drain only frozen boundary stamps, including split/join successors; new marks have no stamp.
            while (true)
            {
                var card = controller.HandZone.FirstOrDefault(c => IsTemporary(c) && Helper(c) is Enchantment helper
                    && helper[CapturedTurn] == controller.Game.Turn && helper[CapturedResidence] == c[Residence]);
                if (card == null) break;
                Generic.DiscardBlock(controller, card);
                controller.Game.DeathProcessingAndAuraUpdate();
            }
        }
        internal static void PublishDiscard(IPlayable card)
        {
            if (!card.Game.History) return;
            var show = PowerHistoryBuilder.ShowEntity(card);
            show.Entity.Tags[DiscardReveal] = 1;
            card.Game.PowerHistory.Add(show);
        }
        internal static void TransferTransform(IPlayable source, IPlayable target)
        {
            var helper = Helper(source);
            if (helper == null) return;
            source.AppliedEnchantments.Remove(helper);
            if (target.AppliedEnchantments == null) target.AppliedEnchantments = new System.Collections.Generic.List<Enchantment>();
            target.AppliedEnchantments.Add(helper);
            helper.RebindTarget(target);
        }
        internal static void CopyState(IPlayable source, IPlayable copy, bool preserveExpiry = false)
        {
            if (!IsTemporary(source)) return;
            Make(copy);
            var original = Helper(source);
            if (preserveExpiry && original != null && original[CapturedTurn] == source.Game.Turn
                && original[CapturedResidence] == source[Residence])
            {
                var helper = Helper(copy);
                helper[CapturedTurn] = original[CapturedTurn];
                helper[CapturedResidence] = copy[Residence];
            }
        }
    }
}
