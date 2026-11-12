using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GladeEcologistProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_820", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Kill(g, source); var spell = p.HandZone.Single();
        Core2026Proof.Check(spell.Card.Id == "TLC_813" && spell.Cost == 1 && spell.Card[GameTag.SPELL_SCHOOL] == 5, "pinned 1-Cost Holy spell");
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, "TLC_813", enemy, friend, false); PlayEntity(g, p, spell, friend);
        Core2026Proof.Check(friend.Health == 7 && friend.AppliedEnchantments.Any(e => e.Card.Id == "TLC_813e"), "friendly target gains two Health");
        enemy.Damage = 1; enemy.HasDivineShield = true;
        Play(g, p, "TLC_813", enemy); Core2026Proof.Check(enemy.BaseHealth == 3 && enemy.Health == 2 && enemy.Damage == 1 && enemy.HasDivineShield && enemy.AppliedEnchantments.Any(e => e.Card.Id == "TLC_813e2"), "enemy loses two maximum Health without damage or consuming Divine Shield");
        enemy = Summon(p.Opponent, "CS2_168"); Play(g, p, "TLC_813", enemy); Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Health reduction kills 1-Health enemy");
        Play(g, p, "BE_228", friend); Core2026Proof.Check(friend.Health == 5, "silence removes Health buff");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); int hand = p.HandZone.Count; Kill(g, source); Core2026Proof.Check(p.HandZone.Count == hand, "silenced source gives no spell");
        source = (Minion)Play(g, p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182"); Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 10, "full hand gives no spell");
    });
}
