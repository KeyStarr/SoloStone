using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_AqirSwarmerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_722", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "Deathrattle waits for death");
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(x => x.Card.Id.StartsWith("BE_720t")
            && x.Card.Id != "BE_720t3" && x.Card.Id != "BE_720t12" && x.Card.Cost == 1
            && x is Minion m && m.AttackDamage == 2 && m.Health == 1 && x.Card.IsRace(Race.BEAST)
            && x[GameTag.DISPLAYED_CREATOR] == source.Id), "Unpinned_ClassPoolAndTeal: exact count, fixed other-class tokens, stats and creator");
        
        ParkBoard(p);
        var hand = p.HandZone.Select(x => x.Id).ToArray();
        var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
        Core2026Proof.Check(p.HandZone.Select(x => x.Id).SequenceEqual(hand), "silenced Deathrattle grants no Larvae with free hand space");

    });
}
