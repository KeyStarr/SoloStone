using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WebofDeceptionProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_523", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        source.AttackDamage = 5; source.Damage = 1; Play(g, p, id, source);
        var spider = p.BoardZone.Single(m => m.Card.Id == "EDR_523t");
        Core2026Proof.Check(source.Zone == p.HandZone && source.AttackDamage == 4 && source.Damage == 0, "returned minion resets buffs and damage");
        Core2026Proof.Check(spider.AttackDamage == 4 && spider.Health == 4 && spider.HasStealth && spider.Card.Implemented && spider.Card.GetRawRace() == Race.BEAST, "exact pinned 4/4 Stealth Spider");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, right.Id, spider.Id }), "removed source appends spider");
        var enemy = Summon(p.Opponent, "CS2_182"); var spell = Add(p, id); p.UsedMana = 0;
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(targets.Length > 0 && targets.All(t => t.Target is Minion m && m.Controller == p), "only friendly minion targets"); PlayEntity(g, p, spell, left);
        ClearBoard(g, p); for (int i = 0; i < 7; i++) Summon(p, "CS2_182"); source = p.BoardZone[3]; Play(g, p, id, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_523t") == 1 && source.Zone == p.HandZone, "bounce creates space on full board");
        ClearBoard(g, p); spell = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "no friendly minion means no legal play");
        source = Summon(p, "CS2_182"); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        spell.Card.Power.PowerTask.Process(g, p, spell, source, new TaskStack()); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 0 && p.HandZone.Count == 10, "failed full-hand return does not summon");
    });
}
