using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StoneTroggProof
{
    internal static void Run() => StandardSetsProof.Case("BE_994", (g, p, id) => {
        var lethal = Summon(p.Opponent, "CS2_182"); var source = (Minion)Play(g, p, id, lethal);
        Core2026Proof.Check(lethal.Zone.Type == Zone.GRAVEYARD && source.Health == 8 && !source.HasTaunt, "lethal damage grants no buff");
        ClearBoard(g, p); var survivor = Summon(p.Opponent, "CS2_182"); survivor.BaseHealth = 8; source = (Minion)Play(g, p, id, survivor);
        Core2026Proof.Check(survivor.Health == 2 && source.Health == 14 && source.HasTaunt && source.AttackDamage == 8, "surviving target grants six Health and Taunt to source");
        Play(g, p, "BE_228", source); Core2026Proof.Check(source.Health == 8 && !source.HasTaunt, "silence removes Health and Taunt");
        ClearBoard(g, p); var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        source = (Minion)Play(g, p, id, shield); Core2026Proof.Check(shield.Health == 1 && source.Health == 14 && source.HasTaunt, "shield target survives");
        TargetRules(g, p, id, shield, source, false); ClearBoard(g, p); ClearBoard(g, p.Opponent);
        source = (Minion)Play(g, p, id); Core2026Proof.Check(source.Health == 8 && !source.HasTaunt, "no target gives no buff");
    });
}
