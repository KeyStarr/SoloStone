using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch9Proof;

internal static class TwilightInfluenceProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_463", (g, p, id) => {
        var small = Summon(p.Opponent, "CS2_171"); small.AttackDamage = 3;
        var large = Summon(p.Opponent, "CS2_182");
        var friendly = Summon(p, "CS2_171"); friendly.AttackDamage = 0;
        var card = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == card.Id && t.ChooseOne == 1).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion m && m.AttackDamage <= 3)
            && options.Any(t => t.Target?.Id == friendly.Id) && options.Any(t => t.Target?.Id == small.Id)
            && options.All(t => t.Target?.Id != large.Id), "destroy branch targets either side, Attack <=3, excludes heroes and four Attack");
        Choose(g, p, id, 1, small);
        Core2026Proof.Check(small.Zone == p.Opponent.GraveyardZone && large.Zone == p.Opponent.BoardZone && p.BoardZone.SlotCount == 1, "only selected minion destroyed, summon branch absent");
        Choose(g, p, id, 1, friendly);
        Core2026Proof.Check(friendly.Zone == p.GraveyardZone, "zero Attack friendly target destroyed");
        LocationProofs.Location(p, position: 0);
        var source = Choose(g, p, id, 2);
        GeneratedBoard(g, source, p.BoardZone, 2, 1);
        Core2026Proof.Check(p.BoardZone.Single().ZonePosition == 1, "random summon appends after location");
        foreach (int size in new[] { 0, 1, 2 })
        {
            foreach (var m in p.BoardZone.ToArray()) m.Silence(); ClearBoard(g, p);
            EmeraldDreamPoolProof.WithPool(g, id, size, () => {
                long before = g.Random.DrawCount;
                var spell = Choose(g, p, id, 2, resume: false);
                GeneratedBoard(g, spell, p.BoardZone, 2, size == 0 ? 0 : 1);
                Core2026Proof.Check(g.Random.DrawCount - before == (size == 0 ? 0 : 1), "empty/tiny pool exact RNG");
            });
        }
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var blocked = Add(p, id); p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        long draws = g.Random.DrawCount;
        // Tier C: https://hearthstone.wiki.gg/wiki/Choose_One — unavailable summon options are disabled.
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id && t.ChooseOne == 2), "full physical board disables summon branch");
        Core2026Proof.Check(!g.Process(PlayCardTask.Any(p, blocked, chooseOne: 2)) && blocked.Zone == p.HandZone
            && p.UsedMana == 0 && p.BoardZone.SlotCount == 7 && g.Random.DrawCount == draws, "full-board summon rejected without spending card, mana or RNG");
        small = Summon(p.Opponent, "CS2_171");
        var destroy = p.Options().Single(t => t.Source?.Id == blocked.Id && t.ChooseOne == 1 && t.Target?.Id == small.Id);
        Core2026Proof.Check(g.Process(destroy) && small.Zone == p.Opponent.GraveyardZone && p.BoardZone.SlotCount == 7
            && blocked.Zone == p.GraveyardZone && p.UsedMana == blocked.Cost, "destroy branch remains playable on full physical board");
        foreach (var m in p.BoardZone.ToArray()) m.Silence(); ClearBoard(g, p);
        small = Summon(p.Opponent, "CS2_171"); p.ChooseBoth = true;
        source = Choose(g, p, id, 0, small);
        Core2026Proof.Check(small.Zone == p.Opponent.GraveyardZone, "Choose Both destroys selected small minion");
        GeneratedBoard(g, source, p.BoardZone, 2, 1);
        p.ChooseBoth = false;
    });
}
