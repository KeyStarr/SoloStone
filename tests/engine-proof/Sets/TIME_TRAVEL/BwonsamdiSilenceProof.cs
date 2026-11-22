using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;

// #189. UNPINNED (no retail trace), lean from hearthstone.wiki.gg Talanji of the Graves (tier C, 2026-11-19): Boon buffs count as
// Auras, so Silence cannot remove them. A Boon is a player enchantment (TIME_619e/e2/e3) whose aura gives Bwonsamdi (TIME_619t) the
// keyword: a silenced Bwonsamdi keeps it, but loses his printed Deathrattle like any silenced minion.
internal static class BwonsamdiSilenceProof
{
    [GroupProof("bwonsamdi-silence", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (var (option, keyword) in new (string, Func<Minion, bool>)[] { ("TIME_619t3", m => m.HasTaunt), ("TIME_619t4", m => m.HasLifeSteal), ("TIME_619t5", m => m.IsRush) })
        {
            var game = DeathKnightC1Proof.NewGame(7); var p = game.Player1;
            Boon(game, p, option);
            var bwonsamdi = (Minion)Core2026Proof.Add(p, "TIME_619t");
            Core2026Proof.PlayEntity(game, p, bwonsamdi);
            Core2026Proof.Check(keyword(bwonsamdi) && bwonsamdi.HasDeathrattle, option + ": the Boon's keyword and the Deathrattle before Silence");
            bwonsamdi.Silence(); game.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(bwonsamdi.IsSilenced && keyword(bwonsamdi), "Unpinned lean: a silenced Bwonsamdi keeps the keyword of " + option);
            Core2026Proof.Check(!bwonsamdi.HasDeathrattle, "Unpinned lean: a silenced Bwonsamdi loses his Deathrattle");
            int before = p.BoardZone.Count;
            Core2026Proof.Kill(game, bwonsamdi);
            Core2026Proof.Check(p.BoardZone.Count == before - 1, option + ": a silenced Bwonsamdi summons nothing when he dies");
        }
        Console.WriteLine("PASS Bwonsamdi Silence (#189, Unpinned lean): Boon keywords stay, Deathrattle goes");
    }

    // What Befell Zandalar (TIME_619t2): deal 2 to all enemies, then choose a Boon.
    private static void Boon(Game game, Controller p, string option)
    {
        Core2026Proof.Play(game, p, "TIME_619t2");
        var decision = game.GetPendingDecisions().Single();
        int entity = p.Choice.Choices.Single(id => game.IdEntityDic[id].Card.Id == option);
        Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { entity }), out var error), "boon answer: " + error);
    }
}
