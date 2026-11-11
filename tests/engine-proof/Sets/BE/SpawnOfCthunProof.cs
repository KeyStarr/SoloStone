using SabberStoneCore.Model;
using static Core2026Proof;
internal static class SpawnOfCthunProof
{
    internal static void Run() => StandardSetsProof.Case("BE_014",Unpinned_InitialDeckGate);
    private static void Unpinned_InitialDeckGate(Game g,SabberStoneCore.Model.Entities.Controller p,string id)
    {
        Add(p,"BE_013");
        Play(g,p,id);
        Core2026Proof.Check(p.Choice == null,"generated C'Thun cannot satisfy starting-deck gate");
        var started = CthunRulesProof.Fixture(initialCthun:true); var owner = started.Player1;
        foreach(var original in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(original));
        Play(started,owner,id);
        Core2026Proof.Check(owner.Choice.Choices.Count == 4,"original C'Thun outside deck still satisfies gate");
        CthunRulesProof.Pick(started,"BE_013t4");
        Core2026Proof.Check(owner.BoardZone.Count == 3,"Spawn triggers external base Servants");
    }
}
