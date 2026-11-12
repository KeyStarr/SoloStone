using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class GallagioGoonProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_802", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.Card.GetRawRace() == Race.ELEMENTAL, "Elemental identity");
        var ordinary = (Minion)Play(g, p, "CS2_182"); Core2026Proof.Check(ordinary.AttackDamage == 4 && ordinary.Health == 5, "non-Battlecry unchanged");
        var summoned = Summon(p, "JAIL_456"); Core2026Proof.Check(summoned.AttackDamage == 2 && summoned.Health == 3, "summoning Battlecry minion does not count as playing");
        var clone = g.Clone(); var cloneTarget = (Minion)Play(clone, clone.Player1, "CATA_612");
        Core2026Proof.Check(cloneTarget.AttackDamage == 6 && cloneTarget.Health == 4 && p.BoardZone.Count == 3, "clone trigger independent");
        var target = (Minion)Play(g, p, "CATA_612");
        Core2026Proof.Check(target.IsFrozen && target.AttackDamage == 6 && target.Health == 4 && target.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_802e"), "frozen Battlecry minion receives exact +1/+1 buff");
        int bearsBefore = p.BoardZone.Count(m => m.Card.Id == "EDR_481");
        var bear = (Minion)Play(g, p, "EDR_481");
        Core2026Proof.Check(bear.AttackDamage == 4 && bear.Health == 5
            && p.BoardZone.Count(m => m.Card.Id == "EDR_481") == bearsBefore + 1,
            "Runebear checks three Attack before Goon buff; no copy despite free board slot");
        Kill(g, bear);
        var cranium = (Minion)Play(g, p, "JAIL_513");
        Core2026Proof.Check(cranium.Health == 2 && cranium.AttackDamage == 4, "post-Battlecry buff can rescue a one-Health minion");
        End(g); var enemy = (Minion)Play(g, p.Opponent, "CATA_612");
        Core2026Proof.Check(enemy.AttackDamage == 5 && enemy.Health == 3, "opponent Battlecry not buffed"); End(g);
        Core2026Proof.Check(target.AttackDamage == 6, "buff persists past turn"); target.Silence();
        Core2026Proof.Check(target.AttackDamage == 5 && target.Health == 3, "silence removes received buff");
        source.Silence(); var noBuff = (Minion)Play(g, p, "CATA_612");
        Core2026Proof.Check(noBuff.AttackDamage == 5 && noBuff.Health == 3, "silenced Goon does not trigger");
        Kill(g, source); Kill(g, ordinary);
        var fresh = (Minion)Play(g, p, id);
        Core2026Proof.Check(!fresh.IsSilenced && fresh.ActivatedTrigger != null, "fresh Goon trigger active before removal");
        Kill(g, fresh); var afterDeath = (Minion)Play(g, p, "CATA_612");
        Core2026Proof.Check(afterDeath.AttackDamage == 5 && afterDeath.Health == 3, "removed Goon does not trigger");
    });
}
