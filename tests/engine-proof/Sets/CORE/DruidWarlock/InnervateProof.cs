using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier A: Blizzard, Update 9.1, https://news.blizzard.com/en-gb/article/21029448/upcoming-balance-changes-update-9-1.
// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class InnervateProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_169", (g, p, id) => {
        p.BaseMana = 3; p.UsedMana = 3; p.TemporaryMana = 0;
        var source = Add(p, id);
        Core2026Proof.Check(g.Process(p.Options().Single(t => t.Source?.Id == source.Id)), "Innervate accepted at zero available mana");
        Core2026Proof.Check(p.BaseMana == 3 && p.UsedMana == 3 && p.TemporaryMana == 1 && p.RemainingMana == 1 && p.Opponent.TemporaryMana == 0, "one temporary available mana; no permanent or opponent crystal");
        source = Add(p, "EX1_302"); var target = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(g.Process(p.Options().Single(t => t.Source?.Id == source.Id && t.Target?.Id == target.Id)), "temporary crystal can pay for a one-cost spell");
        Core2026Proof.Check(p.RemainingMana == 0 && p.TemporaryMana == 0 && p.BaseMana == 3, "temporary mana consumed before permanent mana");
        source = Add(p, id); Core2026Proof.Check(g.Process(p.Options().Single(t => t.Source?.Id == source.Id)), "second Innervate accepted");
        PoolPilotProof.Resume(g, next => {
            End(next);
            Core2026Proof.Check(next.Player1.BaseMana == 3 && next.Player2.TemporaryMana == 0, "temporary mana neither becomes permanent nor transfers to opponent");
            End(next);
            Core2026Proof.Check(next.Player1.TemporaryMana == 0 && next.Player1.BaseMana == 4 && next.Player1.RemainingMana == 4,
                "unused temporary mana unavailable on next owner turn in clone and snapshot");
        });
        p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        source = Add(p, id); Core2026Proof.Check(g.Process(p.Options().Single(t => t.Source?.Id == source.Id)), "Innervate accepted at cap");
        Core2026Proof.Check(p.RemainingMana == 10 && p.TemporaryMana == 0 && p.BaseMana == 10, "ten-mana cap gives no extra crystal");
    }, legacyPoolMember: true);
}
