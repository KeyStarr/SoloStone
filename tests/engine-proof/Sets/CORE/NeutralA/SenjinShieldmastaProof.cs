using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class SenjinShieldmastaProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_179", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var friend = Summon(p, "CS2_182");
        Core2026Proof.Check(source.HasTaunt && source.AttackDamage == 3 && source.Health == 5, "printed 3/5 Taunt");
        End(g); var attacker = Summon(p.Opponent, "CS2_182"); attacker.IsExhausted = false;
        var attacks = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id).ToArray();
        Core2026Proof.Check(attacks.Length == 1 && attacks[0].Target?.Id == source.Id, "Taunt blocks hero and other minion attacks");
        source.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!source.HasTaunt && p.Opponent.Options().Any(t => t.Source?.Id == attacker.Id && t.Target?.Id == p.Hero.Id), "Silence removes Taunt targeting restriction");
        End(g); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); var blocked = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source?.Id == blocked.Id), "cannot play eighth physical board slot");
    }, legacyPoolMember: true);
}
