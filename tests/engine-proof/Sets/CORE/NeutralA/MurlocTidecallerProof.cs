using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class MurlocTidecallerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_509", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.AttackDamage == 1, "own summon does not self-trigger");
        Summon(p, "CS2_182"); Summon(p.Opponent, "EX1_506a");
        Core2026Proof.Check(source.AttackDamage == 1, "non-Murloc and opponent Murloc do not trigger");
        Summon(p, "EX1_506a"); Core2026Proof.Check(source.AttackDamage == 2 && source.AppliedEnchantments.Any(e => e.Card.Id == "EX1_509e"), "friendly summoned Murloc grants exact +1 Attack enchantment");
        PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, "EX1_506");
            Core2026Proof.Check(((Minion)next.IdEntityDic[source.Id]).AttackDamage == 4, "played Murloc and Battlecry Scout each trigger once");
            ((Minion)next.IdEntityDic[source.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Summon(next.Player1, "EX1_506a"); Core2026Proof.Check(((Minion)next.IdEntityDic[source.Id]).AttackDamage == 1, "Silence resets buff and removes listener");
        });
        ParkBoard(p); source = Summon(p, id); Play(g, p, "EX1_049", source); Summon(p, "EX1_506a");
        Core2026Proof.Check(source.Zone == p.HandZone && source.AttackDamage == 1, "source in hand no longer listens");
    }, legacyPoolMember: true);
}
