using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
internal static class B15GameSetupEmeraldDreamProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_000", B15GameSetupCases.Card);
        StandardSetsProof.Case("EDR_260", B15GameSetupCases.Card);
        StandardSetsProof.Case("EDR_845", B15GameSetupCases.Card);
    }
}
internal static class B15EMERALDDREAMProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_264", Unpinned_AegisFullBoard);
        StandardSetsProof.Case("EDR_451", Unpinned_DrakeProgression);
    }
    private static void Paladin(Controller p) => p.Hero.Card = Cards.FromId("HERO_04");
    private static void Unpinned_AegisFullBoard(Game g, Controller p, string id)
    {
        Paladin(p); Play(g, p, id);
        var summoned = p.BoardZone.Single();
        Core2026Proof.Check(summoned.Card.Cost == 2 && summoned.HasTaunt && ImbueRules.Count(p) == 1
            && p.Hero.HeroPower.Card.Id == "EDR_445p", "exact-cost summon with Taunt then Paladin Imbue");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && ImbueRules.Count(p) == 2, "full board still permits spell and Imbue");
    }
    private static void Unpinned_DrakeProgression(Game g, Controller p, string id)
    {
        Paladin(p); var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(ImbueRules.Count(p) == 1, "Battlecry Imbues once");
        Kill(g, body); Core2026Proof.Check(ImbueRules.Count(p) == 2, "Deathrattle Imbues once");
        body = (Minion)Play(g, p, id); body.Silence(); Kill(g, body);
        Core2026Proof.Check(ImbueRules.Count(p) == 3, "Silenced Deathrattle gives no fourth Imbue");
        B15HeroPowersProof.Replay(g, x => {
            B15HeroPowersProof.Use(x, x.Player1.Hero.HeroPower);
            var portals = x.Player1.DeckZone.Where(c => c.Card.Id == "EDR_445pt3").ToArray();
            Core2026Proof.Check(portals.Length == 2 && portals.All(c => c[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3), "power shuffles two exact live-magnitude Portals");
            ImbueRules.Imbue(x.Player1, x.Player1.Hero); x.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(portals.All(c => c[GameTag.TAG_SCRIPT_DATA_NUM_1] == 4), "existing deck Portals track later Imbue");
        });
        var portal = p.DeckZone.First(c => c.Card.Id == "EDR_445pt3");
        p.DeckZone.Remove(portal); p.HandZone.Add(portal);
        int hand = p.HandZone.Count;
        PlayEntity(g, p, portal);
        Core2026Proof.Check(p.HandZone.Count == hand - 1 && p.BoardZone.Single().Card.Cost == 4
            && p.BoardZone.Single().Card.IsRace(Race.DRAGON), "manual Portal summons exact live cost without replacement draw");
    }
}
