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
internal static class CoreExplosiveTrapProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_610", (g, p, id) => {
        // Secrets cannot be Silenced; consumption/removal must unregister their listener.
        var secret = Play(g, p, id); var friend = Summon(p, "CS2_182"); Attack(g, friend, p.Opponent.Hero);
        Core2026Proof.Check(p.SecretZone.Count == 1, "own turn attack does not trigger secret");
        End(g); Play(g, p.Opponent, "CS2_008", p.Hero); Core2026Proof.Check(p.SecretZone.Count == 1, "direct spell damage is not attack");
        var a = Summon(p.Opponent, "CS2_182"); Attack(g, a, friend); Core2026Proof.Check(p.SecretZone.Count == 1 && p.Opponent.Hero.Damage == 4, "minion defender does not trigger");
        a = Summon(p.Opponent, "CS2_231"); var b = Summon(p.Opponent, "CS2_182");
        int heroDamage = p.Hero.Damage; int friendDamage = friend.Damage; var clone = g.CloneExact(history: true);
        Attack(g, a, p.Hero); Attack(clone, (Minion)clone.IdEntityDic[a.Id], clone.Player1.Hero);
        Core2026Proof.Check(a.Zone == p.Opponent.GraveyardZone && b.Damage == 2 && p.Opponent.Hero.Damage == 6 && p.Hero.Damage == heroDamage && friend.Damage == friendDamage
            && secret.Zone == p.GraveyardZone && secret.ActivatedTrigger == null && Canonical.State(g) == Canonical.State(clone), "two to all enemies before combat; lethal attacker cancels attack, clone preserves secret");
        Attack(g, b, p.Hero); Core2026Proof.Check(p.Hero.Damage == heroDamage + 4 && b.Damage == 2, "spent secret does not trigger again");
        End(g); secret = Play(g, p, id); Summon(p, "CS2_142"); End(g);
        a = Summon(p.Opponent, "CS2_182"); Attack(g, a, p.Hero); Core2026Proof.Check(a.Damage == 3 && p.Opponent.Hero.Damage == 9, "owner Spell Damage modifies secret spell damage");
    }, legacyPoolMember: true);
}
