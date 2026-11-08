using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.PlayerTasks
{
    public sealed class PrepareCardTask : PlayerTask
    {
        private PrepareCardTask(Controller controller, IPlayable source)
        {
            PlayerTaskType = PlayerTaskType.PREPARE_CARD;
            Game = controller.Game;
            Controller = controller;
            Source = source;
        }

        public static PrepareCardTask Any(Controller controller, IPlayable source) => new PrepareCardTask(controller, source);

        public static bool IsValid(Controller controller, IPlayable source) => controller != null && source != null
            && controller.Game.State == State.RUNNING && controller.Game.Step == Step.MAIN_ACTION
            && controller.Choice == null && controller.Game.CurrentPlayer == controller
            && source.Game == controller.Game && source.Controller == controller && source.Zone == controller.HandZone
            && controller.HandZone.Contains(source)
            && source[GameTag.PREPARE] != 0 && source[GameTag.PREPARED] == 0
            && source.Cost >= 1 && controller.RemainingMana >= 1;

        public override bool Process()
        {
            if (!IsValid(Controller, Source)) return false;
            int before = Source.Cost, spent = PrepareRules.ManaToSpend(Source), discount = spent + 1;
            if (Game.History)
                Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.DECK_ACTION, Source.Id, "", 0, 0));
            Source[GameTag.IS_USING_PREPARE_OPTION] = 1;
            int temporary = Math.Min(Controller.TemporaryMana, spent);
            Controller.TemporaryMana -= temporary;
            Controller.UsedMana += spent - temporary;
            Controller.TotalManaSpentThisGame += spent;
            Game.Facts.Paid(Controller,Source,spent,action:SabberStoneCore.Model.HistoryPaymentKind.Prepare);
            PrepareRules.Prepare(Source, discount);
            Source[GameTag.IS_USING_PREPARE_OPTION] = 0;
            Game.TriggerManager.OnCardPreparedTrigger(Source, spent, discount, before - Source.Cost);
            Game.DeathProcessingAndAuraUpdate();
            if (Game.History) Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            return true;
        }

        public override string FullPrint() => $"PrepareCardTask => [{Controller.Name}] prepare {Source}";
    }
}
