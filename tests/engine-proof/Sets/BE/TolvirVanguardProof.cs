using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TolvirVanguardProof
{
    internal static void Run() => StandardSetsProof.Case("BE_572", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(p.Hero.Armor == 3 && source.HasTaunt, "battlecry gains 3 Armor");
        Kill(g, source); Core2026Proof.Check(p.Hero.Armor == 6 && source.Zone.Type == Zone.GRAVEYARD, "kill triggers another 3 Armor");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); int armor = p.Hero.Armor;
        Kill(g, source); Core2026Proof.Check(p.Hero.Armor == armor, "silence removes deathrattle");
    });
}
