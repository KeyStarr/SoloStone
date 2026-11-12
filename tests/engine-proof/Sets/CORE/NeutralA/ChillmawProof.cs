using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class ChillmawProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_AT_123", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = Summon(p, id); Add(p.Opponent, "EX1_043"); Kill(g, source);
        Core2026Proof.Check(friend.Damage == 0 && enemy.Damage == 0, "opponent Dragon does not satisfy holding condition");
        Add(p, "CS2_182"); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(friend.Damage == 0 && enemy.Damage == 0, "friendly non-Dragon does not satisfy condition");
        var dragon = Add(p, "EX1_043"); source = Summon(p, id);
        Core2026Proof.Check(source.HasTaunt && source.HasDeathrattle, "printed Taunt and Deathrattle");
        PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).Damage == 3 && ((Minion)next.IdEntityDic[enemy.Id]).Damage == 3
                && next.Player1.Hero.Damage == 0 && next.Player2.Hero.Damage == 0, "death damages both minion sides only in clone/load/original");
        });
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(friend.Damage == 3 && enemy.Damage == 3, "Silence suppresses Deathrattle");
        ParkBoard(p); ParkBoard(p.Opponent); PoolPilotProof.EmptyHand(p);
        source = Summon(p, id); Summon(p, "EX1_043"); enemy = Summon(p.Opponent, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(enemy.Damage == 0, "Dragon on board is not held");
    }, legacyPoolMember: true);
}
