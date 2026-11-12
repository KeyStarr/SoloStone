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
internal static class DreadCorsairProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NEW1_022", (g, p, id) => {
        var source = Add(p, id); Core2026Proof.Check(source.Cost == 4, "no weapon means printed cost");
        End(g); Play(g, p.Opponent, "CS2_106"); End(g);
        Core2026Proof.Check(source.Cost == 4, "opposing weapon does not discount");
        Play(g, p, "CS2_106"); Core2026Proof.Check(source.Cost == 1, "three Attack weapon reduces cost by three");
        PoolPilotProof.Resume(g, next => {
            next.Player1.Hero.RemoveWeapon(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(next.IdEntityDic[source.Id].Cost == 4, "discount disappears with weapon, including clone/snapshot");
        });
        Play(g, p, "EX1_567"); Core2026Proof.Check(source.Cost == 2, "different weapon recomputes discount");
        p.Hero.Weapon.AttackDamage = 7; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Cost == 0, "discount floors at zero");
        PlayEntity(g, p, source); var corsair = (Minion)source;
        Core2026Proof.Check(p.UsedMana == 0 && corsair.AttackDamage == 3 && corsair.Health == 3 && corsair.HasTaunt && corsair.IsRace(Race.PIRATE), "zero-Mana play retains 3/3 Taunt Pirate");
        corsair.Silence(); Core2026Proof.Check(!corsair.HasTaunt, "Silence removes Taunt");
    }, legacyPoolMember: true);
}
