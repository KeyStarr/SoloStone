using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Snapshots;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B14H3LifecycleProof
{
    internal static void Check(bool value,string message) => Core2026Proof.Check(value,"H3: "+message);
    internal static void Answer(Game g,string id = null)
    {
        var d = g.GetPendingDecisions().Single();
        int selected = id == null ? g.CurrentPlayer.Choice.Choices[0] : g.CurrentPlayer.Choice.Choices.Single(x => g.IdEntityDic[x].Card.Id == id);
        Check(g.TryAnswer(new DecisionAnswer(d.Id,d.ControllerId,new[] { selected }),out var error),"answer: "+error);
    }
    [GroupProof("b14-h3-lifecycle")]
    internal static void Run()
    {
        foreach (bool history in new[] { false,true })
        foreach (bool old in new[] { false,true })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            var m = Summon(p,"CS2_182"); m.HasCharge = true;
            if (old) { Generic.TransformBlock(p,Cards.FromId("CS2_200"),m); m = p.BoardZone.Last(); }
            else m = (Minion)Generic.ChangeEntityBlock(p,m,Cards.FromId("CS2_200"),true);
            Check(!m.HasCharge && m.IsExhausted,"fresh Charge to plain transform removes grant before readiness");
            foreach (var copy in new[] { g.CloneExact(history:history),Game.LoadSnapshot(g.SaveSnapshot()) })
                Check(((Minion)copy.IdEntityDic[m.Id]).IsExhausted,"transformed readiness clone/snapshot");
        }
        foreach (bool history in new[] { false,true })
        {
            var game = HistoryFactsProof.Fixture(history); var p = game.Player1;
            var taka = (Minion)Play(game,p,"DINO_430");
            var chosen = game.IdEntityDic[p.Choice.Choices[0]].Card; Answer(game);
            foreach (var fork in new[] { game.CloneExact(history:history),Game.LoadSnapshot(game.SaveSnapshot()),game })
            {
                var source = (Minion)fork.IdEntityDic[taka.Id]; Kill(fork,source);
                Check(fork.Player1.BoardZone.Single().Card.Id == chosen.Id,"Taka death without presentation history");
            }
            foreach (string id in new[] { "TIME_049","TIME_055","DINO_430","CATA_190h" })
            foreach (string cls in PoolRequirement.PlayableClasses)
            {
                var requirement = SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools.Single();
                var pool = OfflineCardPools.Candidates(game,PoolRequirement.HeroClass(cls),requirement.Filter,Cards.FromId(id).AssetId);
                Check(pool.Count(c => id != "DINO_430" || c.ColossalAppendageIds == null && c[(GameTag)2247] == 0) >= 3,
                    "local pool has three eligible candidates: "+id+"/"+cls);
            }
        }
        var shieldGame = HistoryFactsProof.Fixture(); var shieldOwner = shieldGame.Player1;
        var shielded = Summon(shieldOwner,"CS2_182"); shielded.HasDivineShield = true;
        Play(shieldGame,shieldOwner,"CS2_022",shielded);
        Check(shielded.Card.Id == "CS2_tk1" && !shielded.HasDivineShield,
            "shared Polymorph clearing transform removes granted Divine Shield");
        foreach (string spell in new[] { "CS2_022","EX1_246","CORE_EX1_246" })
        {
            var game = HistoryFactsProof.Fixture(); var p = game.Player1;
            var body = Summon(p,"CS2_182"); Play(game,p,"CAP_804",body);
            Check(body[GameTag.REBORN] == 1,"Specter Specialist actually grants Reborn");
            PoolPilotProof.Resume(game,next => {
                var changed = (Minion)next.IdEntityDic[body.Id];
                Play(next,next.Player1,spell,changed);
                Check(changed[GameTag.REBORN] == 0,"clearing transform removes granted Reborn: "+spell);
                Kill(next,changed);
                Check(!next.Player1.BoardZone.Any(m => m.Card.Id == changed.Card.Id),"transformed body cannot revive: "+spell);
            });
        }
        var printedGame = HistoryFactsProof.Fixture(); var printed = Summon(printedGame.Player1,"CS2_182");
        Generic.ChangeEntityBlock(printedGame.Player1,printed,Cards.FromId("ULD_723"),true);
        Check(printed[GameTag.REBORN] == 1,"clearing transform preserves new printed Reborn");
        B14F1HistoryDefinitionProof.Run();
        var definition = Cards.FromId("TIME_103");
        var bytes = SnapshotCodec.EncodeDefinition(definition,detached:true);
        Check(Convert.FromBase64String(bytes).Take(3).SequenceEqual(new byte[] {255,255,3}),"board-display definition version three");
        Check(SnapshotCodec.EncodeDefinition(SnapshotCodec.DecodeDefinition(bytes)) == bytes,"board-display definition round trip");
        Console.WriteLine("PASS H3 lifecycle: transform Charge/readiness, old definition compatibility, board definition v3");
    }
    [GroupProof("b14-h3-lifecycle-cards",ProofKind.Cards)]
    internal static void CardsRun()
    {
        B14H3TimeProof.Run(); B14H3TakaProof.Run(); B14H3DeathwingProof.Run(); B14H3DivinationProof.Run();
    }
}
