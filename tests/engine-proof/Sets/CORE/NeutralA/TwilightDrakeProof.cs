using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class TwilightDrakeProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_043", (g, p, id) => {
        var empty = (Minion)Play(g, p, id); Core2026Proof.Check(empty.Health == 1 && empty.AttackDamage == 4, "empty remaining hand gives no Health");
        Add(p, "CS2_182"); Add(p, "CS2_029"); Add(p, "EX1_506");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Health == 4 && source.AttackDamage == 4 && source.AppliedEnchantments.Any(e => e.Card.Id == "EX1_043e"), "counts all three remaining cards, excluding itself");
        PoolPilotProof.EmptyHand(p); Core2026Proof.Check(source.Health == 4, "Health is a Battlecry snapshot, not hand aura");
        PoolPilotProof.Resume(g, next => { ((Minion)next.IdEntityDic[source.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[source.Id]).Health == 1, "Silence removes granted Health"); });
        ParkBoard(p); var held = Add(p, id); PoolPilotProof.FillHand(p);
        PlayEntity(g, p, held); Core2026Proof.Check(((Minion)held).Health == 10, "full starting hand leaves nine cards for +9 Health");
    }, legacyPoolMember: true);
}
