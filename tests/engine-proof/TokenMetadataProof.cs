using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class TokenMetadataProof
{
    internal static void Run()
    {
        foreach (var row in new[] { (Id: "RLK_061t", Dbf: 78570, Attack: 1, Health: 3),
                                    (Id: "RLK_008t", Dbf: 79278, Attack: 2, Health: 2) })
        {
            var card = Cards.FromId(row.Id);
            Core2026Proof.Check(card != null && card.AssetId == row.Dbf && card.Implemented,
                "token metadata: exact exported token available " + row.Id);
            Core2026Proof.Check(!card.Collectible && card.Set == CardSet.CORE_MODERN && card[GameTag.CARD_SET] == 1637
                && card.ATK == row.Attack && card.Health == row.Health && card.IsRace(Race.UNDEAD)
                && card[(GameTag)2217] == 1, "token metadata: set correction preserves printed token");
            var game = DeathKnightC1Proof.NewGame(31);
            var token = Summon(game.Player1, row.Id);
            Core2026Proof.Check(token.HasTaunt == (row.Id == "RLK_061t") && token.IsRush == (row.Id == "RLK_008t"),
                "token metadata: native keywords");
            PinnedRule_NoCorpse(game, token.Id);
            token = Summon(game.Player1, row.Id);
            token.Silence();
            Core2026Proof.Check(!token.HasTaunt && !token.IsRush && token[(GameTag)2217] == 1,
                "Unpinned_RisenSilence preserves no-Corpse identity");
            // Definition metadata and intrinsic restriction survive all three game-state paths.
            foreach (var copy in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
            {
                int before = CorpseRules.Available(copy.Player1);
                var risen = (SabberStoneCore.Model.Entities.Minion)copy.IdEntityDic[token.Id];
                new ReturnHandTask(EntityType.TARGET).Process(copy, copy.Player1, copy.Player1.Hero, risen);
                Core2026Proof.Check(risen.Zone == copy.Player1.HandZone && CorpseRules.Available(copy.Player1) == before,
                    "token metadata: leave play is not death");
                copy.Player1.UsedMana = 0;
                Core2026Proof.Check(copy.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Minion(copy.Player1, risen)),
                    "token metadata: replay bounced token");
                End(copy); End(copy);
                Core2026Proof.Check(copy.Player1.BoardZone.Single().Card.Id == row.Id, "token metadata: no expiry");
                PinnedRule_NoCorpse(copy, risen.Id);
            }
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(Game.LoadSnapshot(game.SaveSnapshot())),
                "token metadata: snapshot after death");
        }
        Console.WriteLine("PASS token metadata: exact tokens/set/stats/keywords/no-Corpse/Silence/leave-play/turns/clone/snapshot");
    }

    private static void PinnedRule_NoCorpse(Game game, int id)
    {
        int before = CorpseRules.Available(game.Player1);
        Kill(game, (SabberStoneCore.Model.Entities.Minion)game.IdEntityDic[id]);
        Core2026Proof.Check(CorpseRules.Available(game.Player1) == before, "Risen death leaves no Corpse");
    }
}
