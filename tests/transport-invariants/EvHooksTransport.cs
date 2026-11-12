using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace TransportInvariants;

internal static class EvHooksTransport
{
    internal static void WatfinIdentity()
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "mage", "mage", 0);
        session = new(match); match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var game = match.Game; var owner = game.Player1; owner.BaseMana = 10; owner.UsedMana = 0;
        var watfin = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("JAIL_EVENT_100"));
        session.Flush(); session.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(owner, watfin));
        int chosen = owner.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == "JAIL_EVENT_100t");
        session.Process(SabberStoneCore.Tasks.PlayerTasks.ChooseTask.Pick(owner, chosen));
        var actor = game.IdEntityDic[chosen]; var shown = session.Client.Entities[chosen];
        string expected = Cards.FromAssetId(actor.Card[GameTag.MODULAR_ENTITY_PART_1]).Id;
        SelfChecks.Check(shown.CardId == expected && shown.Get(GameTag.MODULAR_ENTITY_PART_1) == 0
            && !Invariants.Compare(game, session.Client).Any(v => v.Entity == chosen), "Watfin publishes the original identity and visible mutation");
        try
        {
            shown.CardId = "JAIL_EVENT_100t";
            SelfChecks.Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == chosen && v.Tag == "CARD_ID"),
                "Watfin oracle rejects leaked suspicious token identity");
            shown.CardId = expected == "CS2_182" ? "CS2_231" : "CS2_182";
            SelfChecks.Check(Invariants.Compare(game, session.Client).Any(v => v.Entity == chosen && v.Tag == "CARD_ID"),
                "Watfin oracle rejects an unrelated visible identity");
        }
        finally { shown.CardId = expected; }
    }

    internal static void Arrange(SabberStoneCore.Model.Game game, string card)
    {
        if (card != "TIME_EVENT_997") return;
        var location = (BoardObject)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CORE_REV_990"), zone: game.Player1.BoardZone);
        location.IsExhausted = true;
        location[GameTag.EXHAUSTED] = 1;
        location[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
    }
}
