using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class WildPyromancerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NEW1_020", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var source = (Minion)Play(g, p, id);
        Play(g, p, "CS2_182"); Core2026Proof.Check(source.Damage == 0 && enemy.Damage == 0, "minion play does not trigger");
        End(g); Play(g, p.Opponent, "GAME_005");
        Core2026Proof.Check(source.Damage == 0 && enemy.Damage == 0, "opponent spell does not trigger"); End(g);
        PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, "EX1_371", (Minion)next.IdEntityDic[source.Id]);
            var pyro = (Minion)next.IdEntityDic[source.Id];
            Core2026Proof.Check(!pyro.HasDivineShield && pyro.Damage == 0 && ((Minion)next.IdEntityDic[friend.Id]).Damage == 1
                && ((Minion)next.IdEntityDic[enemy.Id]).Damage == 1 && next.Player1.Hero.Damage == 0 && next.Player2.Hero.Damage == 0,
                "spell resolves shield before wave; all minions, no heroes; clone/snapshot");
        });
        Play(g, p, "GAME_005"); Core2026Proof.Check(source.Damage == 1 && friend.Damage == 2 && enemy.Damage == 2, "wave also damages source");
        source.Silence(); Play(g, p, "GAME_005");
        Core2026Proof.Check(source.Damage == 1 && friend.Damage == 2 && enemy.Damage == 2, "Silence removes spell listener");
        var departed = Summon(p, id); Kill(g, departed); Play(g, p, "GAME_005");
        Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 2, "source death removes listener");
    }, legacyPoolMember: true);
}
