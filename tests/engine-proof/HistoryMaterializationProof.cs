using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HistoryMaterializationProof
{
    internal static void Run()
    {
        var g = HistoryFactsProof.Fixture();
        var minion = Summon(g.Player1, "JAIL_974");
        Kill(g, minion);
        Core2026Proof.Check(g.HistoryFacts.Query.Deaths(g.HistoryFacts.Query.ThisTurn).Single().EntityId == minion.Id,
            "history materialization: registered trigger minion death recorded once");
        Verify(g);
        Verify(g.CloneExact());
        var bytes = g.SaveSnapshot();
        var loaded = Game.LoadSnapshot(bytes);
        Verify(loaded);
        Core2026Proof.Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "history materialization: snapshot bytes round trip");
        int count=0;
        foreach(var registered in Cards.All.Where(card=>card.Implemented && !card.Id.StartsWith("HISTORY_PROOF_",StringComparison.Ordinal)))
        {
            Card detached;
            try { detached=new HistoryDefinition(registered).Materialize(); }
            catch(Exception error) { throw new Exception("Installed definition failed: "+registered.Id,error); }
            Core2026Proof.Check(detached.Id==registered.Id && detached.AssetId==registered.AssetId
                && !ReferenceEquals(detached,registered),"history materialization: installed definition "+registered.Id);
            count++;
        }
        var custom=Cards.FromId("JAIL_974").Clone();
        custom.Tags[SabberStoneCore.Enums.GameTag.ATK]=7;
        var frozen=new HistoryDefinition(custom);
        custom.Tags[SabberStoneCore.Enums.GameTag.ATK]=99;
        Core2026Proof.Check(frozen.Materialize().Tags[SabberStoneCore.Enums.GameTag.ATK]==7,
            "history materialization: noncatalogue trigger graph frozen at capture");
        Console.WriteLine($"PASS history materialization: CapturedArchmage death, detached trigger, clone, snapshot; {count} installed definitions");
    }
    private static void Verify(Game game)
    {
        var death = game.HistoryFacts.Query.Deaths(game.HistoryFacts.Query.ThisTurn).Single();
        var card = death.Definition.Materialize();
        Core2026Proof.Check(card.Id == "JAIL_974" && card.Power.Trigger.GetType() == Cards.FromId("JAIL_974").Power.Trigger.GetType(),
            "history materialization: registered trigger recipe retained");
        Core2026Proof.Check(!ReferenceEquals(card.Power.Trigger, Cards.FromId("JAIL_974").Power.Trigger),
            "history materialization: returned trigger detached from catalogue");
        Core2026Proof.Check(((SabberStoneCore.Triggers.MultiTrigger)card.Power.Trigger)[0].Condition.Eval(game.IdEntityDic[death.EntityId]),
            "history materialization: nested registered trigger callback retained");
        card.Tags[SabberStoneCore.Enums.GameTag.ATK] = 999;
        Core2026Proof.Check(death.Definition.Materialize().ATK != 999 && Cards.FromId("JAIL_974").ATK != 999,
            "history materialization: query edits cannot change stored or registered definition");
    }
}
