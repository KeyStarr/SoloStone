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
internal static class CoreFreezingTrapProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_611", (g, p, id) => {
        // Secrets cannot be Silenced; spent secret removal is the listener-lifetime check.
        var secret = Play(g, p, id); var friend = Summon(p, "CS2_182"); Attack(g, friend, p.Opponent.Hero);
        Core2026Proof.Check(p.SecretZone.Count == 1, "own minion attack does not trigger"); End(g);
        Play(g, p.Opponent, "CS2_008", p.Hero); Core2026Proof.Check(p.SecretZone.Count == 1, "spell does not trigger");
        Play(g, p.Opponent, "CS2_082"); p.Opponent.Hero.IsExhausted = false;
        var heroAttack = p.Opponent.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Hero);
        Core2026Proof.Check(g.Process(heroAttack) && p.SecretZone.Count == 1, "enemy hero attack does not trigger");
        var attacker = Summon(p.Opponent, "CS2_182"); attacker.Damage = 1; int health = friend.Health; var clone = g.CloneExact(history: true);
        Attack(g, attacker, friend); Attack(clone, (Minion)clone.IdEntityDic[attacker.Id], (Minion)clone.IdEntityDic[friend.Id]);
        Core2026Proof.Check(attacker.Zone == p.Opponent.HandZone && attacker.Cost == 6 && attacker.Damage == 0 && friend.Health == health
            && secret.Zone == p.GraveyardZone && secret.ActivatedTrigger == null && Canonical.State(g) == Canonical.State(clone), "enemy minion attacking any defender bounces before combat, resets damage, gains +2 cost; clone matches");
        PlayEntity(g, p.Opponent, attacker); Core2026Proof.Check(attacker.Cost == 4, "surcharge ends after replay");
        End(g); secret = Play(g, p, id); End(g); while (!p.Opponent.HandZone.IsFull) Add(p.Opponent, "CS2_182");
        attacker = Summon(p.Opponent, "CS2_182"); int damage = p.Hero.Damage; Attack(g, attacker, p.Hero);
        Core2026Proof.Check(attacker.Zone == p.Opponent.GraveyardZone && p.Opponent.HandZone.Count == 10 && p.Hero.Damage == damage && secret.Zone == p.GraveyardZone, "full-hand return destroys attacker and still prevents combat");
    }, legacyPoolMember: true);
}
