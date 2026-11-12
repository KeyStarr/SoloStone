using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AvatarofDestructionProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_778", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var lethal = Summon(p.Opponent, "CS2_182");
        var survivor = Summon(p.Opponent, "CS2_182"); survivor.BaseHealth = 12;
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt, "intrinsic Taunt"); Kill(g, source);
        Core2026Proof.Check(lethal.Zone.Type == Zone.GRAVEYARD && survivor.Damage == 9 && survivor.Health == 3 && shield.Damage == 0 && !shield.HasDivineShield
            && friend.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "kill deals 9 only to enemy minions; shield survives");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(survivor.Damage == 9 && shield.Zone.Type == Zone.PLAY, "silence removes deathrattle");
    });
}
