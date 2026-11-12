using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class BloodmageThalnosProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_012", (g, p, id) => {
        var source = Summon(p, id); var enemy = Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(p.CurrentSpellPower == 1 && p.Opponent.CurrentSpellPower == 0, "Spell Damage applies only to owner");
        Play(g, p, "CS2_008", enemy); Core2026Proof.Check(enemy.Damage == 2, "one-damage spell deals two");
        int top = p.DeckZone.TopCard.Id; PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(next.Player1.HandZone.Single().Id == top && next.Player1.CurrentSpellPower == 0, "death draws exact top and removes Spell Damage");
        });
        PoolPilotProof.EmptyHand(p); source = Summon(p, id); source.Silence();
        Core2026Proof.Check(p.CurrentSpellPower == 0 && !source.HasDeathrattle, "Silence removes both powers");
        Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 0, "silenced death does not draw");
        source = Summon(p, id); Play(g, p, "EX1_049", source);
        Core2026Proof.Check(p.CurrentSpellPower == 0 && p.HandZone.Single().Id == source.Id, "bounce removes Spell Damage without drawing");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p); source = Summon(p, id); int deck = p.DeckZone.Count; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "Deathrattle overdraw burns one card");
    }, legacyPoolMember: true);
}
