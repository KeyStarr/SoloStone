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
internal static class B15GameSetupVioletHoldProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_384", B15GameSetupCases.Card);
        StandardSetsProof.Case("JAIL_443", B15GameSetupCases.Card);
    }
}
internal static class B15ESCAPEFROMVIOLETHOLDProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_446", Unpinned_PersistentSecondary);
    private static void Unpinned_PersistentSecondary(Game g, Controller p, string id)
    {
        var original = p.Hero.HeroPower; var body = (Minion)Play(g, p, id);
        var second = p.SecondaryHeroPower;
        Core2026Proof.Check(second?.Card.Id == "JAIL_446hp" && !second.IsExhausted && original == p.Hero.HeroPower, "Battlecry installs exact secondary");
        second.IsExhausted = true;
        body.Silence(); Kill(g, body); Play(g, p, id);
        Core2026Proof.Check(p.SecondaryHeroPower.Id == second.Id && second.IsExhausted && p.HeroPowers.Count() == 2,
            "repeat after source Silence/death never refreshes or adds third power");
    }
}
