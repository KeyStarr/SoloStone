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
internal static class CoreShakuTheCollectorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CFM_781", (g, p, id) => {
        CorePoolProof.CrossClass(g, id, attack: true);
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasStealth, "printed Stealth");
        var ordinary = Summon(p, "CS2_182"); Attack(g, ordinary, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0, "another friendly attacker does not trigger Shaku");
        var clone = g.CloneExact(history: true); Attack(g, source, p.Opponent.Hero); Attack(clone, (Minion)clone.IdEntityDic[source.Id], clone.Player2.Hero);
        var generated = p.HandZone.Single(); Core2026Proof.Check(generated.Card.Class != p.HeroClass && generated.Card.Class != CardClass.NEUTRAL && !generated.Card.IsQuest
            && generated[GameTag.DISPLAYED_CREATOR] == source.Id && !source.HasStealth && Canonical.State(g) == Canonical.State(clone), "Shaku attack generates other-class card, loses Stealth, cloned RNG matches");
        End(g); var enemy = Summon(p.Opponent, "CS2_182"); Attack(g, enemy, source);
        Core2026Proof.Check(p.HandZone.Count == 1, "being attacked does not generate"); End(g);
        source = (Minion)Play(g, p, id); Play(g, p, "CORE_SW_066", source); int count = p.HandZone.Count;
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(p.HandZone.Count == count && !source.HasStealth, "Silence disables attack trigger and Stealth");
        Kill(g, source);
        source = (Minion)Play(g, p, id); var liveTrigger = source.ActivatedTrigger;
        clone = g.CloneExact(history: true); var clonedSource = (Minion)clone.IdEntityDic[source.Id]; var clonedTrigger = clonedSource.ActivatedTrigger;
        Core2026Proof.Check(!source.IsSilenced && !clonedSource.IsSilenced && liveTrigger != null && clonedTrigger != null
            && liveTrigger.IsActive && clonedTrigger.IsActive, "unsilenced departure fixtures have active triggers in original and clone");
        Kill(g, source); Kill(clone, clonedSource);
        Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD && clonedSource.Zone.Type == Zone.GRAVEYARD
            && source.ActivatedTrigger == null && clonedSource.ActivatedTrigger == null
            && !liveTrigger.IsActive && !clonedTrigger.IsActive, "unsilenced Shaku death detaches live trigger in original and clone");
        Attack(g, ordinary, p.Opponent.Hero); Attack(clone, (Minion)clone.IdEntityDic[ordinary.Id], clone.Player2.Hero);
        Core2026Proof.Check(p.HandZone.Count == count && clone.Player1.HandZone.Count == count && Canonical.State(g) == Canonical.State(clone), "departed unsilenced source leaves no callback with clone parity");
        source = (Minion)Play(g, p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var kept = p.HandZone.Select(c => c.Id).ToArray(); Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Select(c => c.Id).SequenceEqual(kept), "full hand burns generated card without replacing existing cards");
    }, legacyPoolMember: true);
}
