using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
internal static class B15THELOSTCITYProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("TLC_436", CorpseRushLifesteal);
        StandardSetsProof.Case("TLC_632", Unpinned_SwapSpell);
    }
    private static void CorpseRushLifesteal(Game g, Controller p, string id)
    {
        // Standard harness has Corpses disabled. A dedicated funded game verifies payment and combat.
        var funded = B15HeroPowersProof.Make(); var c = funded.Player1;
        c[GameTag.CORPSES] = 5; c.BaseMana = 0; c.Hero.Damage = 10;
        var enemy = Summon(c.Opponent, "CS2_182"); var card = Add(c, id);
        Core2026Proof.Check(funded.Process(PlayCardTask.Minion(c, card)) && CorpseRules.Available(c) == 0 && c.UsedMana == 0,
            "exact corpse payment and zero mana");
        var body = (Minion)card;
        Core2026Proof.Check(body.IsRush && body.HasLifeSteal && body.AttackableByRush, "printed Rush/Lifesteal ready against minions");
        Core2026Proof.Check(funded.Process(MinionAttackTask.Any(c, body, enemy)) && c.Hero.Damage == 6,
            "combat applies ordinary Lifesteal");
    }
    private static void Unpinned_SwapSpell(Game g, Controller p, string id)
    {
        var original = p.Hero.HeroPower;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id == "TLC_632t" && original.Zone == p.SetasideZone, "paid spell swaps exact two-use identity");
        B15HeroPowersProof.Replay(g, x => B15HeroPowersProof.Use(x, x.Player1.Hero.HeroPower));
        Core2026Proof.Check(p.Opponent.Hero.Damage == 8 && p.Hero.HeroPower.Card.Id == "TLC_632t2", "first power use deals exactly eight");
    }
}
