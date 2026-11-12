using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class FrostStrikeProof
{
    internal static void Run() => DeathKnightC1Proof.Case("RLK_025", (g, p, id) => {
        PinnedRule_OffersBeforeDeathCleanup(g, id);
        var source = Add(p, id); Core2026Proof.Check(!HasPlay(p, source), "no minions: no legal play");
        var target = Summon(p.Opponent, "CS2_200"); p.Hero.Damage = 5;
        PlayEntity(g, p, source, target);
        Core2026Proof.Check(target.Damage == 3 && p.Choice == null && p.Hero.Damage == 5, "surviving target: no Discover, no Lifesteal");
        var shield = Summon(p.Opponent, "CS2_101t"); shield.HasDivineShield = true;
        Play(g, p, id, shield); Core2026Proof.Check(!shield.HasDivineShield && p.Choice == null, "shield prevents kill and Discover");
        Pool(g, id, 0, () => {
            Play(g, p, id, shield);
            Core2026Proof.Check(shield.Zone.Type == Zone.GRAVEYARD && p.Choice == null, "lethal with empty pool resolves without choice");
        });
        var friendly = Summon(p, "CS2_101t");
        source = Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.IsDead && friendly.Zone == p.BoardZone && p.Choice != null && p.Choice.Choices.All(x =>
            g.IdEntityDic[x].Card.Tags.GetValueOrDefault((GameTag)2197) > 0), "friendly lethal offers admitted Frost-rune Discover before cleanup");
        Answer(g); Generated(g, source, p.HandZone, 1);
        Core2026Proof.Check(friendly.Zone.Type == Zone.GRAVEYARD, "friendly lethal cleanup completes after the Discover answer");
        var stolen = NewGame(g.Seed, CardClass.MAGE); var mage = stolen.Player1;
        FullBoard(mage); var victim = Summon(mage.Opponent, "CS2_101t");
        source = Play(stolen, mage, id, victim);
        Core2026Proof.Check(mage.Choice != null && mage.Choice.Choices.All(x => stolen.IdEntityDic[x].Card.Tags.GetValueOrDefault((GameTag)2197) > 0), "stolen Frost Strike ignores controller class on mixed full board");
        Answer(stolen); Generated(stolen, source, mage.HandZone, 1);
    });
    private static void PinnedRule_OffersBeforeDeathCleanup(Game seeded, string id)
    {
        var g = NewGame(seeded.Seed); var p = g.Player1;
        var victim = Summon(p.Opponent, "RLK_708");
        int enemyHand = p.Opponent.HandZone.Count;
        var source = Add(p, id);
        // data/rules-evidence/deathknight/frost-strike-timing.yaml (retail block 624).
        source.Card.Power.PowerTask.Process(g, p, source, victim, new TaskStack());
        Core2026Proof.Check(victim.IsDead && victim.Zone == p.Opponent.BoardZone && p.Choice != null
            && p.Opponent.HandZone.Count == enemyHand, "Frost Strike offers precede cleanup and Baron Deathrattle");
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD && p.Opponent.HandZone.Count == enemyHand + 1,
            "ordinary cleanup still resolves Deathrattle once");
    }
}
