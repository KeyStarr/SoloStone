using SabberStoneCore.Enums;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;

internal static class ChronoDaggersProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_001", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6 && p.UsedMana == 3 && g.Rewind != null, "three sequential two-damage knives and offer");
        Keep(g);
        Summon(p, "CS2_052");
        int damage = p.Opponent.Hero.Damage;
        Play(g, p, id); Keep(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 9, "spell power applied to every knife");
        var enemy = Summon(p.Opponent, "CS2_231");
        long seed = 1;
        while (new SabberStoneCore.Model.Util.DeepCloneableRandom(seed).Next(2) != 1) seed++;
        g.SetRandomSeed(seed);
        damage = p.Opponent.Hero.Damage; Play(g, p, id); Keep(g);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == damage + 6,
            $"lethal knife removed before next random selection; remaining knives hit living hero: zone={enemy.Zone.Type}, damage={p.Opponent.Hero.Damage - damage}");
    });
    internal static void Keep(SabberStoneCore.Model.Game g)
    {
        var t = g.Rewind;
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(t.OfferChoiceId, t.ControllerId, new[] { t.KeepEntityId }), out _), "Keep probe outcome");
    }
}
