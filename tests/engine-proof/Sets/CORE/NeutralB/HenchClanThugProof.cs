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
internal static class HenchClanThugProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GIL_534", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var ally = Summon(p, "CS2_182");
        Core2026Proof.Check(source.IsRace(Race.QUILBOAR), "current Quilboar type");
        Attack(g, ally, p.Opponent.Hero);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3, "friendly minion attack does not buff");
        Play(g, p, "CS2_106");
        PoolPilotProof.Resume(g, next => {
            Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1, next.Player2.Hero)), "friendly hero attack accepted");
            var thug = (Minion)next.IdEntityDic[source.Id];
            Core2026Proof.Check(thug.AttackDamage == 4 && thug.Health == 4 && thug.AppliedEnchantments.Single().Card.Id == "GIL_534t", "hero attack grants exact +1/+1 enchantment");
        });
        End(g); Play(g, p.Opponent, "CS2_106");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)), "opposing hero attack accepted");
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 4, "opposing hero does not trigger");
        End(g); source.Silence();
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attacks after Silence");
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3, "Silence removes buff and listener");
        var departed = Summon(p, id); Kill(g, departed); Play(g, p, "CS2_106"); End(g); End(g);
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attacks after source death");
        Core2026Proof.Check(departed.Zone == p.GraveyardZone && departed.AttackDamage == 3, "departed source has no listener");
    }, legacyPoolMember: true);
}
