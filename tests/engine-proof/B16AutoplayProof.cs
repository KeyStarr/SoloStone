using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using L = LocationProofs;

internal static class B16AutoplayProof
{
    internal static Game[] Futures(Game game) => new[] { game.CloneExact(true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools), game };
    internal static void AssertSaved(Game game)
    {
        foreach (var next in Futures(game)) L.Check(Canonical.State(next) == Canonical.State(game), "clone/snapshot state");
    }
    internal static void Answer(Game game, int id = 0)
    {
        var choice = game.CurrentPlayer.Choice;
        var decision = game.GetPendingDecisions().Single();
        L.Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { id == 0 ? choice.Choices[0] : id }), out var error), "answer: " + error);
    }
    internal static void Ready(BoardObject source)
    {
        source.IsExhausted = false; source[GameTag.EXHAUSTED] = 0; source[GameTag.LOCATION_ACTION_COOLDOWN] = 0;
    }
    internal static void EmptyDeck(Controller p)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
    }
    [GroupProof("b16-autoplay")]
    internal static void Run()
    {
        var game = L.Game(); var p = game.Player1;
        p.UsedMana = p.BaseMana;
        var body = Entity.FromCard(p, Cards.FromId("CS2_189"), zone: p.DeckZone);
        int damage = p.Opponent.Hero.Damage, plays = p.NumCardsPlayedThisTurn;
        new PlayTask((PlayType)1, true).Process(game, p, p.Hero, null,
            new TaskStack { Playables = new List<IPlayable> { body } });
        L.Check(body.Zone == p.BoardZone && p.Opponent.Hero.Damage == damage,
            "mixed autoplay summons Elven Archer without Battlecry");
        L.Check(p.RemainingMana == 0 && p.NumCardsPlayedThisTurn == plays,
            "automatic placement pays nothing and is not a hand play");
        RandomTargetsAndBattlecries();
        CapacityAndDecisions();
        HeroBattlecryPolicy();
        var counts = Enumerable.Range(1, 3).Select(cost => OfflineCardPools.Candidates(game, CardClass.DRUID,
            OfflinePoolFilter.Random(CardType.MINION, minCost: cost, maxCost: cost), 115631).Count).ToArray();
        int past = OfflineCardPools.Candidates(game, CardClass.PRIEST,
            OfflinePoolFilter.Random(CardType.MINION, minCost: 5, race: Race.DRAGON), 120544).Count;
        int dragons = OfflineCardPools.Candidates(game, CardClass.PRIEST,
            OfflinePoolFilter.Discover(CardType.MINION, minCost: 5, race: Race.DRAGON), 122262).Count;
        int naga = OfflineCardPools.Candidates(game, CardClass.DEMONHUNTER,
            OfflinePoolFilter.Random(CardType.MINION, race: Race.NAGA), 122845)
            .Count(card => card.ColossalAppendageIds == null || card.ColossalAppendageIds.Count == 0);
        Console.WriteLine("POOLS Amirdrassil 1/2/3=" + string.Join("/", counts) + "; Past Conflux=" + past + "; Conflux Priest=" + dragons + "; Nespirah non-Colossal=" + naga);
        Console.WriteLine("PASS b16-autoplay mixed dispatch, seeded targets, Battlecry opt-in, capacity, automatic decisions");
    }

    private static void RandomTargetsAndBattlecries()
    {
        foreach (int seed in new[] { 1, 8, 19 })
        {
            var game = L.Game(seed); var p = game.Player1;
            L.Minion(p); var first = L.Minion(p.Opponent); var second = L.Minion(p.Opponent);
            first.BaseHealth = second.BaseHealth = 20;
            game.DeathProcessingAndAuraUpdate();
            var spell = (Spell)Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.SetasideZone);
            var legal = spell.ValidPlayTargets.Where(spell.IsValidPlayTarget).Where(t => t.Controller == p.Opponent).ToArray();
            var rng = game.Random.Clone(); int expected = legal[rng.Next(legal.Length)].Id;
            foreach (var next in Futures(game))
            {
                AutoPlayRules.Play(next.IdEntityDic[spell.Id], next.Player1.Hero, preferEnemies: true);
                L.Check(((ICharacter)next.IdEntityDic[expected]).Damage == 6 && next.Random.CaptureState().Equals(rng.CaptureState()),
                    "seeded enemy-first target and exact RNG, seed " + seed);
                L.Check(next.Player1.Hero.Damage == 0 && next.Player1.BoardZone.All(m => m.Damage == 0), "enemy preference never targets friend");
            }
        }
        var battlecryGame = L.Game(); var owner = battlecryGame.Player1;
        var archer = Entity.FromCard(owner, Cards.FromId("CS2_189"), zone: owner.SetasideZone);
        AutoPlayRules.Play(archer, owner.Hero, preferEnemies: true, battlecry: true);
        L.Check(owner.Opponent.Hero.Damage == 1 && owner.NumCardsPlayedThisTurn == 0, "explicit Battlecry opt-in without hand-play accounting");
    }
    private static void HeroBattlecryPolicy()
    {
        foreach (bool battlecry in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1;
            var enemy = L.Minion(p.Opponent); p.Hero.Damage = 3; p.Hero.Armor = 2;
            game.DeathProcessingAndAuraUpdate();
            var hero = (Hero)Entity.FromCard(p, Cards.FromId("ICC_828"), zone: p.SetasideZone);
            AutoPlayRules.Play(hero, p.Hero, battlecry: battlecry);
            L.Check(p.Hero == hero && enemy.Damage == (battlecry ? 2 : 0), "hero replacement respects explicit Battlecry policy");
            L.Check(hero.Damage == 3 && hero.Armor == 7 && p.NumCardsPlayedThisTurn == 0, "automatic hero carries old health/armor and printed armor without paid play");
            AssertSaved(game);
        }
    }
    private static void CapacityAndDecisions()
    {
        var game = L.Game(); var p = game.Player1;
        while (!p.BoardZone.IsFull) L.Minion(p);
        while (!p.HandZone.IsFull) Core2026Proof.Add(p, "CS2_182");
        var blocked = Entity.FromCard(p, Cards.FromId("CORE_REV_990"), zone: p.DeckZone);
        int deck = p.DeckZone.Count; var rng = game.Random.CaptureState();
        AutoPlayRules.Play(blocked, p.Hero, drawWhenFull: true);
        L.Check(blocked.Zone == p.GraveyardZone && p.DeckZone.Count == deck - 1 && game.Random.CaptureState().Equals(rng), "full board plus full hand burns once with no random retry");
        var failed = Entity.FromCard(p, Cards.FromId("CS2_189"), zone: p.SetasideZone);
        AutoPlayRules.Play(failed, p.Hero);
        L.Check(failed.Zone == p.GraveyardZone, "failed replay consumes minion without hand fallback");
        var choiceGame = L.Game(); var c = choiceGame.Player1;
        var spell = Entity.FromCard(c, Cards.FromId("MEND_307"), zone: c.SetasideZone);
        AutoPlayRules.Play(spell, c.Hero);
        L.Check(c.BoardZone.Count == 1 && c.Choice == null && !choiceGame.Resolution.HasSuspendedEffect, "registered Discover completes automatically");
        AssertSaved(choiceGame);
    }
}
