using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ShadowflameSuffusionProof
{
    private static void Answer(Game g)
    {
        var ids = g.Player1.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        GiftedDiscoverProof.Choose(g, ids);
    }
    internal static void Run() => StandardSetsProof.Case("FIR_939", (g, p, id) => {
        DecisionChoiceReplayProof.Spell(g.Seed, id, gifted: true);
        var friendly = Summon(p, "EX1_298"); var enemy = Summon(p.Opponent, "EX1_298");
        var spell = Add(p, id);
        var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(targets.Length == 4 && new[] { p.Hero.Id, p.Opponent.Hero.Id, friendly.Id, enemy.Id }
            .All(i => targets.Any(t => t.Target?.Id == i)), "any character target, no untargeted play");
        PlayEntity(g, p, spell, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 2 && p.Choice != null, "damage happens before Discover"); Answer(g);
        Summon(p, "EX1_012"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3, "Spell Damage applies"); Answer(g);
        friendly.HasDivineShield = true; Play(g, p, id, friendly);
        Core2026Proof.Check(!friendly.HasDivineShield && friendly.Damage == 0 && p.Choice != null, "absorbed damage still Discovers"); Answer(g);
        var victim = Summon(p.Opponent, "CS2_231"); Play(g, p, id, victim);
        Core2026Proof.Check(victim.IsDead && victim.Zone == p.Opponent.BoardZone && p.Choice != null, "lethal target still Discovers before cast cleanup"); Answer(g);
        Core2026Proof.Check(victim.Zone == p.Opponent.GraveyardZone, "lethal target cleaned up after answer");
        GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.WARRIOR },
            new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.WARRIOR, type: CardType.MINION));
    });
}
