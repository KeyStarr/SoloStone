using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz; current text and printed tags.
internal static class CoreRatTrapProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GIL_577", (g, p, id) => {
        // Secrets cannot be Silenced; spent/destroyed sources must unregister their listeners.
        var secret = Play(g, p, id); Play(g, p, "GAME_005"); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 0, "owner playing three cards never triggers"); End(g);
        Play(g, p.Opponent, "GAME_005"); Play(g, p.Opponent, "GAME_005");
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 0, "first two opponent cards do not trigger");
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var clone = g.CloneExact(history: true);
        Play(g, p.Opponent, "GAME_005"); Play(clone, clone.Player2, "GAME_005"); var rat = p.BoardZone.Last();
        Core2026Proof.Check(rat.Card.Id == "GIL_577t" && rat.AttackDamage == 6 && rat.Health == 6 && rat.Card.IsRace(Race.BEAST)
            && p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, rat.Id }) && secret.Zone == p.GraveyardZone
            && secret.ActivatedTrigger == null && Canonical.State(g) == Canonical.State(clone), "third opponent card summons pinned 6/6 Beast at far right and consumes secret with clone parity");
        Play(g, p.Opponent, "GAME_005"); Core2026Proof.Check(p.BoardZone.Count == 2, "fourth card cannot retrigger spent secret");
        End(g); ParkBoard(p); secret = Play(g, p, id); LocationProofs.Location(p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); End(g);
        for (int i = 0; i < 3; i++) Play(g, p.Opponent, "GAME_005");
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && secret.Zone == p.SecretZone, "full physical board keeps Rat Trap armed without overfilling");
        ParkBoard(p); Play(g, p.Opponent, "GAME_005"); Core2026Proof.Check(p.BoardZone.Count == 0 && secret.Zone == p.SecretZone, "fourth card does not replace third-card threshold");
        Play(g, p.Opponent, "EX1_544"); Core2026Proof.Check(secret.Zone == p.GraveyardZone && secret.ActivatedTrigger == null, "destroyed secret unregisters listener");
        End(g); End(g); for (int i = 0; i < 3; i++) Play(g, p.Opponent, "GAME_005"); Core2026Proof.Check(p.BoardZone.Count == 0, "removed secret never summons");
    }, legacyPoolMember: true);
}
