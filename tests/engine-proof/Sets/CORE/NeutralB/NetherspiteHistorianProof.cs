using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags.
// Tier A: Blizzard 15.2 / 18.0.1 pool rules; source URLs in CorePoolProof.
internal static class NetherspiteHistorianProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_KAR_062", (g, p, id) => {
        CorePoolProof.CrossClass(g, id, player => Add(player, "EX1_043"));
        Play(g, p, id); Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0, "no held Dragon means no Discover");
        var boardDragon = Summon(p, "EX1_043"); Play(g, p, id);
        Core2026Proof.Check(p.Choice == null, "Dragon on board is not held");
        Add(p.Opponent, "EX1_043"); Play(g, p, id); Core2026Proof.Check(p.Choice == null, "opposing held Dragon does not qualify");
        Add(p, "EX1_043"); Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3
            && p.Choice.Choices.All(key => g.IdEntityDic[key].Card.IsRace(Race.DRAGON)), "held friendly Dragon offers three Dragons");
        int chosen = p.Choice.Choices[0]; var offered = g.IdEntityDic[chosen].Card; string chosenId = offered.Id;
        PoolPilotProof.Resume(g, next => {
            Core2026Proof.Check(next.Process(ChooseTask.Pick(next.Player1, chosen)), "Dragon pick accepted");
            var generated = (Minion)next.IdEntityDic[chosen];
            Core2026Proof.Check(generated.Zone == next.Player1.HandZone && generated.Card.Id == chosenId
                && generated.Card.Cost == offered.Cost && generated.AttackDamage == offered.ATK && generated.Health == offered.Health,
                "chosen entity retains offered identity and printed stats");
            Core2026Proof.Check(next.Player1.HandZone.Count == 2 && next.Player1.HandZone.Any(c => c.Card.Id == chosenId), "picked Dragon added in original/clone/snapshot");
        });
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 8); Add(p, "EX1_043"); Play(g, p, id);
        Core2026Proof.Check(p.Choice != null, "Discover at one free hand slot");
        chosen = p.Choice.Choices[0]; PoolPilotProof.FillHand(p);
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, chosen)) && p.HandZone.Count == 10, "choice resolving into full hand does not overflow");
    }, legacyPoolMember: true);
}
