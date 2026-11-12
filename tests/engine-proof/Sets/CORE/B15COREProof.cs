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
internal static class B15GameSetupCoreProof
{
    [GroupProof("b15-game-setup-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CORE_ETC_111", B15GameSetupCases.Card);
        StandardSetsProof.Case("CORE_REV_946", B15GameSetupCases.Card);
        B15GameSetupEmeraldDreamProof.Run(); B15GameSetupVioletHoldProof.Run(); B15TIMETRAVELProof.Run();
    }
}
internal static class B15COREProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CFM_670", RandomTargets);
    private static void RandomTargets(Game g, Controller p, string id)
    {
        p.Hero.Card = Cards.FromId("HERO_08");
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(g, p, p.Hero, null);
        var mayor = (Minion)Play(g, p, id); Summon(p.Opponent, "CS2_182");
        var power = p.Hero.HeroPower;
        p.UsedMana = 0; g.SetRandomSeed(2);
        var candidates = power.ValidPlayTargets.ToArray();
        var oracle = g.CloneExact(history: true);
        int chosen = candidates[oracle.Random.Next(candidates.Length)].Id;
        int before = ((ICharacter)g.IdEntityDic[chosen]).Health;
        B15HeroPowersProof.Replay(g, x => B15HeroPowersProof.Use(x, x.Player1.Hero.HeroPower, x.Player2.Hero));
        Core2026Proof.Check(((ICharacter)g.IdEntityDic[chosen]).Health == before - 1, "seeded target order redirects actual Hero Power effect");
        mayor.Silence(); power.IsExhausted = false; p.UsedMana = 0;
        int damage = p.Opponent.Hero.Damage;
        B15HeroPowersProof.Use(g, power, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 1, "Silence removes random-target trigger");
        Kill(g, mayor);
    }
}
