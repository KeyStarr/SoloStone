using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static B15DrawReplacementProof;

internal static class B15DrawReplacementCoreProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SCH_717", Alabaster);
    private static void Alabaster(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g, p, id);
        var drawn = Entity.FromCard(p.Opponent, Cards.FromId("CS2_029"), zone: p.Opponent.DeckZone);
        Generic.Draw(p.Opponent);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "CS2_029" && p.HandZone[0].Cost == 1,
            "opponent draw produces a one-Cost copy");
        Generic.Draw(p);
        Core2026Proof.Check(p.HandZone.Count == 2, "own draws do not trigger Alabaster");
        while (!p.Opponent.HandZone.IsFull) Add(p.Opponent, "CS2_182");
        drawn = Entity.FromCard(p.Opponent, Cards.FromId("CS2_200"), zone: p.Opponent.DeckZone);
        Generic.Draw(p.Opponent, drawn);
        Core2026Proof.Check(drawn.Zone == p.Opponent.GraveyardZone
            && p.HandZone.Last().Card.Id == "CS2_200" && p.HandZone.Last().Cost == 1, "opponent burn also copied at one");
        body.Silence(); int hand = p.HandZone.Count;
        Generic.Draw(p.Opponent);
        Core2026Proof.Check(p.HandZone.Count == hand, "Silence disables copy trigger");
        var second = Summon(p, id);
        p.SetasideZone.Add(p.BoardZone.Remove(second));
        Generic.Draw(p.Opponent);
        Core2026Proof.Check(p.HandZone.Count == hand, "leave-play removes copy trigger");
        Summon(p, id);
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        Generic.Draw(p.Opponent);
        Core2026Proof.Check(p.HandZone.Count == 10, "copy overflow uses ordinary hand capacity");
    }
}
