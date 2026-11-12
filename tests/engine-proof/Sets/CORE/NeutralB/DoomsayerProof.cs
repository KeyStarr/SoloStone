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
internal static class DoomsayerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NEW1_021", (g, p, id) => {
        var location = LocationProofs.Location(p); var friend = Summon(p, "GVG_085"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id); End(g);
        Core2026Proof.Check(source.Zone == p.BoardZone && friend.Zone == p.BoardZone && enemy.Zone == p.Opponent.BoardZone, "opponent turn start does not trigger");
        PoolPilotProof.Resume(g, next => {
            End(next);
            Core2026Proof.Check(next.Player1.BoardZone.Count == 0 && next.Player2.BoardZone.Count == 0
                && next.IdEntityDic[source.Id].Zone.Type == Zone.GRAVEYARD && next.IdEntityDic[friend.Id].Zone.Type == Zone.GRAVEYARD
                && next.IdEntityDic[location.Id].Zone.Type == Zone.PLAY, "own turn destroys self and all minions through shield; location survives");
        });
        source = (Minion)Play(g, p, id); enemy = Summon(p.Opponent, "CS2_182"); source.Silence(); End(g); End(g);
        Core2026Proof.Check(source.Zone == p.BoardZone && enemy.Zone == p.Opponent.BoardZone, "Silence removes turn-start destruction");
        var departed = Summon(p, id); Kill(g, departed); End(g); End(g);
        Core2026Proof.Check(source.Zone == p.BoardZone && enemy.Zone == p.Opponent.BoardZone, "dead source has no turn-start listener");
    }, legacyPoolMember: true);
}
