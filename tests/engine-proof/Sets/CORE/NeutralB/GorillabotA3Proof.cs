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
internal static class GorillabotA3Proof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOE_039", (g, p, id) => {
        CorePoolProof.CrossClass(g, id, player => Summon(player, "GVG_085"));
        Play(g, p, id); Core2026Proof.Check(p.Choice == null, "source Mech does not enable itself");
        ParkBoard(p); Summon(p, "CS2_182"); Summon(p.Opponent, "GVG_085"); Play(g, p, id);
        Core2026Proof.Check(p.Choice == null, "non-Mech friend and opposing Mech do not enable Discover");
        Summon(p, "GVG_085"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.MECHANICAL) && source.IsRace(Race.BEAST) && source.AttackDamage == 3 && source.Health == 4, "current dual-type 3/4");
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3
            && p.Choice.Choices.All(key => g.IdEntityDic[key].Card.IsRace(Race.MECHANICAL)), "another friendly Mech offers three Mechs");
        int chosen = p.Choice.Choices[0]; var offered = g.IdEntityDic[chosen].Card; string chosenId = offered.Id;
        PoolPilotProof.Resume(g, next => {
            Core2026Proof.Check(next.Process(ChooseTask.Pick(next.Player1, chosen)), "Mech pick accepted");
            var generated = (Minion)next.IdEntityDic[chosen];
            Core2026Proof.Check(generated.Zone == next.Player1.HandZone && generated.Card.Id == chosenId
                && generated.Card.Cost == offered.Cost && generated.AttackDamage == offered.ATK && generated.Health == offered.Health,
                "chosen entity retains offered identity and printed stats");
            Core2026Proof.Check(next.Player1.HandZone.Single().Card.Id == chosenId, "picked Mech enters friendly hand");
        });
        ParkBoard(p); Summon(p, "GVG_085"); PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9); Play(g, p, id);
        chosen = p.Choice.Choices[0]; PoolPilotProof.FillHand(p);
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, chosen)) && p.HandZone.Count == 10, "full hand at choice resolution cannot overflow");
    }, legacyPoolMember: true);
}
