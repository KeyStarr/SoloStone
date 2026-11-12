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
internal static class CoreWagglePickProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DAL_720", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); friend.Damage = 2; var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Play(g, p, "CS2_082");
        Core2026Proof.Check(friend.Zone == p.HandZone && friend.Cost == 2 && ((Minion)friend).Damage == 0 && enemy.Zone == p.Opponent.BoardZone, "destroyed weapon bounces friendly entity, resets damage and discounts by two");
        PlayEntity(g, p, friend); Core2026Proof.Check(friend.Cost == 4, "hand discount is reset when minion enters play");
        ParkBoard(p); Play(g, p, id); Play(g, p, "CS2_082"); Core2026Proof.Check(p.BoardZone.Count == 0, "no friendly minion means no bounce");
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_200"); Play(g, p, id);
        var clone = g.CloneExact(history: true); Play(g, p, "CS2_082"); Play(clone, clone.Player1, "CS2_082");
        Core2026Proof.Check(new[] { a, b }.Count(m => m.Zone == p.HandZone) == 1 && new[] { a, b }.Where(m => m.Zone == p.HandZone).All(m => m.Cost == m.Card.Cost - 2)
            && Canonical.State(g) == Canonical.State(clone), "one random friendly return with cloned RNG parity");
        ParkBoard(p); var cheap = Summon(p, "CS2_231"); Play(g, p, id); Play(g, p, "CS2_082"); Core2026Proof.Check(cheap.Cost == 0, "discount floors at zero");
        ParkBoard(p); var burned = Summon(p, "CS2_182"); Play(g, p, id);
        var replacement = Add(p, "CS2_082"); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        // Use enemy weapon destruction so owner's hand remains full during the Deathrattle.
        End(g); Play(g, p.Opponent, "EX1_066"); Core2026Proof.Check(burned.Zone == p.GraveyardZone && p.HandZone.Count == 10, "full-hand bounce destroys minion without overflow");
    }, legacyPoolMember: true);
}
