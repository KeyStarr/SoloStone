using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using SabberStoneCore.Actions;
using static ChooseOneProof;

internal static class TwilightTimereaverProof
{
    internal static void Run() => StandardSetsProof.Case("END_010", (g, p, id) => {
        var batch9 = g.CloneExact(history: true);
        RunBatch9(batch9, batch9.Player1, id);
        Core2026Proof.Check(HsSim.Canonical.State(batch9) == HsSim.Canonical.State(batch9.CloneExact(history: true)) &&
            HsSim.Canonical.State(batch9) == HsSim.Canonical.State(SabberStoneCore.Model.Game.LoadSnapshot(batch9.SaveSnapshot())), "batch-9 proof clone/snapshot future");
        RunSelectors(g, p, id);
    });

    private static void RunBatch9(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        for (int branch = 1; branch <= 2; branch++)
        {
            ClearBoard(g, p); ClearBoard(g, p.Opponent);
            var location = LocationBoardProof.Object(p.Opponent, 0);
            var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            friendly.Damage = 2; enemy.Damage = 1;
            var hand = (Minion)Add(p, "CS2_182");
            var source = (Minion)Add(p, id);
            p.UsedMana = 0;
            var options = p.Options().OfType<PlayCardTask>().Where(t => t.Source?.Id == source.Id).ToArray();
            Core2026Proof.Check(options.Select(t => t.ChooseOne).Distinct().OrderBy(n => n).SequenceEqual(new[] { 1, 2 }),
                "bot exposes both exact Choose One branches");
            Core2026Proof.Check(source.ChooseOnePlayables.Select(o => o.Card.Id).SequenceEqual(new[] { "END_010a", "END_010b" }),
                "pinned option identities");
            var selected = options.First(t => t.ChooseOne == branch && t.ZonePosition == 0);
            var clone = g.CloneExact(history: true);
            Core2026Proof.Check(clone.Process(PlayCardTask.Any(clone.Player1, clone.IdEntityDic[source.Id], zonePosition: 0, chooseOne: branch)), "clone plays branch");
            Core2026Proof.Check(g.Process(selected), "bot branch accepted");
            foreach (var candidate in new[] { g, clone })
            {
                var owner = candidate.Player1;
                foreach (int targetId in new[] { friendly.Id, enemy.Id })
                {
                    var target = (Minion)candidate.IdEntityDic[targetId];
                    Core2026Proof.Check(target.AttackDamage == (branch == 1 ? 1 : 4) &&
                        target.Health == (branch == 2 ? 1 : targetId == friendly.Id ? 3 : 4),
                        "only selected stat set, both sides, damaged minions survive");
                    Core2026Proof.Check(target.AppliedEnchantments.Any(e => e.Card.Id == (branch == 1 ? "END_010ae" : "END_010be")), "exact enchantment");
                }
                var played = (Minion)candidate.IdEntityDic[source.Id];
                Core2026Proof.Check(played.AttackDamage == 5 && played.Health == 5 && played.ZonePosition == 0 &&
                    played.Card.IsRace(Race.DRAGON) && owner.UsedMana == 6 && owner.BoardZone.SlotCount == 2,
                    "source excluded, Dragon, position and payment");
                Core2026Proof.Check(((Minion)candidate.IdEntityDic[hand.Id]).AttackDamage == 4 &&
                    ((Minion)candidate.IdEntityDic[hand.Id]).Health == 5 && owner.Hero.Health == 30, "hand and heroes excluded");
                Core2026Proof.Check(candidate.IdEntityDic[location.Id].Zone == owner.Opponent.BoardZone &&
                    candidate.IdEntityDic[location.Id].AppliedEnchantments?.Any() != true, "location excluded from minion stat sets");
                played.Silence(); candidate.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(((Minion)candidate.IdEntityDic[enemy.Id]).AttackDamage == (branch == 1 ? 1 : 4) &&
                    ((Minion)candidate.IdEntityDic[enemy.Id]).Health == (branch == 2 ? 1 : 4), "source Silence does not undo resolved stat sets");
                Kill(candidate, played);
                Core2026Proof.Check(((Minion)candidate.IdEntityDic[enemy.Id]).Health == (branch == 2 ? 1 : 4), "stat set persists after source leaves");
                End(candidate); End(candidate);
                Core2026Proof.Check(((Minion)candidate.IdEntityDic[enemy.Id]).AttackDamage == (branch == 1 ? 1 : 4) &&
                    ((Minion)candidate.IdEntityDic[enemy.Id]).Health == (branch == 2 ? 1 : 4), "stat set persists through both turn endings");
                var affected = (Minion)candidate.IdEntityDic[friendly.Id];
                affected.Silence(); candidate.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(affected.AttackDamage == 4 && affected.Health == (branch == 1 ? 3 : 5), "Silence removes stat enchantment");
            }
            p.HandZone.Remove(hand); p.SetasideZone.Add(hand);
            p.Opponent.BoardZone.Remove(location); p.Opponent.SetasideZone.Add(location);
        }
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var bothTarget = Summon(p.Opponent, "CS2_182");
        p.ChooseBoth = true;
        var bothSource = (Minion)Play(g, p, id);
        Core2026Proof.Check(bothTarget.AttackDamage == 1 && bothTarget.Health == 1 &&
            bothSource.AttackDamage == 5 && bothSource.Health == 5, "existing Choose Both resolves both options and excludes source");
        p.ChooseBoth = false;
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var emptySource = (Minion)Play(g, p, id);
        Core2026Proof.Check(emptySource.AttackDamage == 5 && emptySource.Health == 5, "empty board excludes source");
        LocationBoardProof.Object(p);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var blocked = Add(p, id);
        Core2026Proof.Check(p.BoardZone.Count == 6 && p.BoardZone.SlotCount == 7 &&
            !p.Options().Any(t => t.Source?.Id == blocked.Id), "seven physical slots including location block play");
    }

    private static void RunSelectors(SabberStoneCore.Model.Game g, SabberStoneCore.Model.Entities.Controller p, string id) {
        OptionsSurvive(g, p, id, "END_010a", "END_010b");
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 2;
        var source = (Minion)PlayOption(g, p, id, 1);
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 5 && friend.AttackDamage == 1 && enemy.AttackDamage == 1
            && enemy.Health == 3 && enemy.AppliedEnchantments.Any(e => e.Card.Id == "END_010ae"), "Attack choice affects both boards, excludes self, preserves Health");
        var second = (Minion)PlayOption(g, p, id, 2);
        Core2026Proof.Check(second.AttackDamage == 5 && second.Health == 5 && source.Health == 1 && source.AttackDamage == 5
            && enemy.Health == 1 && enemy.BaseHealth == 1 && enemy.AppliedEnchantments.Any(e => e.Card.Id == "END_010be"), "Health choice sets damaged minion to one, excludes self, preserves Attack");
        Both(p); var both = (Minion)PlayOption(g, p, id, -1);
        Core2026Proof.Check(both.AttackDamage == 5 && both.Health == 5 && p.BoardZone.Where(m => m.Id != both.Id).Concat(p.Opponent.BoardZone)
            .All(m => m.AttackDamage == 1 && m.Health == 1 && m.AppliedEnchantments.Any(e => e.Card.Id == "END_010ae") && m.AppliedEnchantments.Any(e => e.Card.Id == "END_010be")),
            "Fandral applies ae then be to all other minions");
    }
}
