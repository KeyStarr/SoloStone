using HsSim;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;

internal static class B15AssembleProof
{
    internal static readonly string[] Ids = { "BE_044", "BE_045", "BE_087", "BE_114", "BE_229", "BE_257",
        "BE_262", "BE_553", "BE_571", "BE_702", "BE_740", "BE_851" };

    [GroupProof("b15-assemble-cards", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (string id in Ids) StandardSetsProof.Case(id, Unpinned_Recipe);
    }

    private static void Unpinned_Recipe(Game game, Controller player, string id)
    {
        if (id == "BE_553") KnownMetaProofFixture.UseBlackEmpire(game);
        var template = Cards.FromId(id);
        if (template.Class != CardClass.NEUTRAL) player.AddHeroAndPower(Cards.HeroCard(template.Class));
        if (id == "BE_702") Play(game, player, "GAME_005");
        var held = Add(player, "CS2_182");
        var source = Play(game, player, id);
        var candidates = AssembleRules.Candidates(game, player, id, template.AssetId);
        var viable = candidates.Where(c => AssembleRules.PrimaryMatches(c, id, player.HeroClass)
            && candidates.Any(d => AssembleRules.DonorMatches(c, d, id, player.HeroClass))).ToArray();
        Console.WriteLine($"assemble bucket {id}/{player.HeroClass}: primary={viable.Length}; catalog={candidates.Length}");
        Core2026Proof.Check(viable.Length > 0 && player.Choice != null, "Unpinned_Assemble opens a compatible donor decision");
        Core2026Proof.Check(player.HandZone.Count == (id is "BE_553" or "BE_702" or "BE_851" ? 1 : 2),
            "core grant precedes offers; newer source-specific final-grant assumptions remain isolated");
        var offers = player.Choice.Choices.Select(i => game.IdEntityDic[i].Card).ToArray();
        Core2026Proof.Check(offers.Length is > 0 and <= 2 && offers.Select(c => c.Id).Distinct().Count() == offers.Length,
            "donor identities distinct, no padded offers");
        var fair = FairBotState.Create(game, player.Opponent.PlayerId, 77, DeckStyle.Value);
        var hiddenView = (Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fair);
        var bodyId = hiddenView.Resolution.CurrentEffect.Restore(hiddenView).Number;
        Core2026Proof.Check(hiddenView.IdEntityDic[bodyId] is Unknown
            && hiddenView.IdEntityDic[bodyId].Zone.Type == (id is "BE_553" or "BE_702" or "BE_851" ? Zone.SETASIDE : Zone.HAND),
            "opponent pending frame binds a redacted entity, never a hidden dbfId");
        var clone = game.CloneExact(history: true); var loaded = Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools);
        foreach (var next in new[] { game, clone, loaded })
        {
            var p = next.Player1;
            PlayerDecisionProof.Answer(next, p.Choice.Choices[0]);
            Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count is > 0 and <= 2, "choose exactly one changed half");
            var pending = next.GetPendingDecisions().Single(); var before = next.SaveSnapshot();
            Core2026Proof.Check(!next.TryAnswer(new DecisionAnswer(pending.Id, p.Opponent.Id, new[] { p.Choice.Choices[0] }), out _)
                && before.SequenceEqual(next.SaveSnapshot()), "invalid composite answer is atomic");
            var halfClone = next.CloneExact(history: true); var halfLoaded = Game.LoadSnapshotWithPools(before, next.OfflinePools);
            PlayerDecisionProof.Answer(next, p.Choice.Choices[0]);
            foreach (var resumed in new[] { halfClone, halfLoaded })
            {
                PlayerDecisionProof.Answer(resumed, resumed.Player1.Choice.Choices[0]);
                Core2026Proof.Check(Canonical.State(next) == Canonical.State(resumed), "half-choice clone/snapshot resumes exact composite definition");
            }
            Core2026Proof.Check(p.Choice == null && !next.Resolution.HasSuspendedEffect, "composition cannot stall decision");
            var result = p.HandZone.Single(c => c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]);
            var body = Cards.FromId(result.Card.Id);
            Core2026Proof.Check(result.Card.Cost == body.Cost && result.Card.ATK == body.ATK && result.Card.Health == body.Health
                && result.Card.Name == body.Name && result[GameTag.DISPLAYED_CREATOR] == source.Id,
                "primary identity, stats, cost and creator retained");
            Core2026Proof.Check(AssembleRules.PrimaryMatches(body, id, p.HeroClass), "source-specific primary predicate");
            if (id == "BE_114") Core2026Proof.Check(result.Cost == Math.Max(0, body.Cost - 2), "Shadow Sculpt persistent reduction");
            if (id == "BE_740") Core2026Proof.Check(((Minion)result).AttackDamage == body.ATK + 2 && ((Minion)result).Health == body.Health + 3,
                "Chisel uses removable +2/+3 layer");
            if (id == "BE_571") Core2026Proof.Check(((Minion)next.IdEntityDic[held.Id]).AttackDamage == held.Card.ATK + 2
                && ((Minion)result).AttackDamage == body.ATK + 2, "Fleshshape includes new result in hand buff");
            if (id == "BE_087") Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "BE_087t" && m.HasTaunt && m.Health == 3),
                "Darkest Depths summons exact 0/3 Taunt Guards before composition");
            if (id == "BE_553") Core2026Proof.Check(p.HandZone.Last().Card.Id == "BE_553t", "Overdrive granted after result");
        }
        Core2026Proof.Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(loaded),
            "open donor choice clone/snapshot resumes identically");
    }
}
