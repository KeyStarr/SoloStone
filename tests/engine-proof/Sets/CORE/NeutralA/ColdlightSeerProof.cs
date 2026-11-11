using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
// Tier A: https://hearthstone.blizzard.com/en-gb/news/20328179 (2016-10-20): other friendly Murlocs only; current export agrees.
internal static class ColdlightSeerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_103", (g, p, id) => {
        var friend = Summon(p, "EX1_506a"); var plain = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "EX1_506a");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(friend.Health == 3 && friend.AttackDamage == 1 && friend.AppliedEnchantments.Any(e => e.Card.Id == "EX1_103e")
            && plain.Health == 5 && enemy.Health == 1 && source.Health == 3, "+2 Health only to other friendly Murlocs");
        var later = Summon(p, "EX1_506a"); Core2026Proof.Check(later.Health == 1, "later summons receive no Battlecry buff");
        PoolPilotProof.Resume(g, next => { Kill(next, (Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).Health == 3, "buff survives source death");
            ((Minion)next.IdEntityDic[friend.Id]).Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).Health == 1, "recipient Silence removes permanent buff"); });
        ParkBoard(p); source = (Minion)Play(g, p, id); Core2026Proof.Check(source.Health == 3, "alone does not buff itself");
    }, legacyPoolMember: true);
}
