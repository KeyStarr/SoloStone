using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class B16AutoplayConfluxProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_436", Unpinned_Conflux);
    private static void Unpinned_Conflux(Game game, Controller p, string id)
    {
        var location = LocationProofs.Play(p, id);
        LocationProofs.Use(location);
        Core2026Proof.Check(location.Card.Id == "TIME_436t1" && location.Durability == 2 && p.BoardZone.Single().IsRace(Race.DRAGON), "Past advances with one spent charge");
        B16AutoplayProof.Ready(location); LocationProofs.Use(location);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == p.Choice.Choices.Distinct().Count() && location.Durability == 2, "Present pauses before charge, distinct offers");
        foreach (var next in B16AutoplayProof.Futures(game))
        {
            var loc = (BoardObject)next.IdEntityDic[location.Id]; var owner = next.Player1;
            B16AutoplayProof.Answer(next);
            Core2026Proof.Check(loc.Card.Id == "TIME_436t2" && loc.Durability == 1, "Present continuation advances once");
            B16AutoplayProof.Ready(loc); LocationProofs.Use(loc);
            Core2026Proof.Check(loc.Zone == owner.GraveyardZone && owner.Choice != null, "final Conflux clears before decision");
            var blocked = next.CloneExact(true);
            var blockedOwner = blocked.Player1;
            var blockedChoice = blocked.IdEntityDic[blockedOwner.Choice.Choices[0]].Card;
            while (!blockedOwner.BoardZone.IsFull) Summon(blockedOwner, "CS2_182");
            B16AutoplayProof.Answer(blocked);
            Core2026Proof.Check(blockedOwner.BoardZone.SlotCount == 7 && blockedOwner.HandZone.Any(c => c.Card.Id == blockedChoice.Id),
                "Future retains chosen definition for hand even when summon blocked at completion");
            var fullHand = next.CloneExact(true);
            while (!fullHand.Player1.HandZone.IsFull) Add(fullHand.Player1, "CS2_182");
            int before = fullHand.Player1.BoardZone.Count;
            B16AutoplayProof.Answer(fullHand);
            Core2026Proof.Check(fullHand.Player1.HandZone.Count == 10 && fullHand.Player1.BoardZone.Count == before + 1,
                "full hand burns copy without preventing final summon/completion");
            foreach (var final in B16AutoplayProof.Futures(next))
            {
                var chosen = final.IdEntityDic[final.Player1.Choice.Choices[0]].Card;
                B16AutoplayProof.Answer(final);
                Core2026Proof.Check(final.Player1.HandZone.Any(c => c.Card.Id == chosen.Id) && final.Player1.BoardZone.Any(m => m.Card.Id == chosen.Id), "Future summons and grants chosen definition");
                Core2026Proof.Check(((BoardObject)final.IdEntityDic[loc.Id]).Location.UsesCompleted == 3, "final activation completes once");
            }
        }
    }
}
