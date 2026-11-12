using SabberStoneCore.Enums;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static LostCityPoolProof;

internal static class CryptMapProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_435", (g, p, id) =>
        Prove(g, p, id, CardClass.DEATHKNIGHT, c => c[(GameTag)2197] > 0,
            new[] { "RLK_025", "RLK_709", "TIME_611" }));

    private static void PlayChosen(Game g, int chosen)
    {
        var p = g.CurrentPlayer; p.BaseMana = 10; p.UsedMana = 0;
        if (!p.Opponent.BoardZone.IsFull) Summon(p.Opponent, "CS2_182");
        var options = p.Options().Where(t => t.Source?.Id == chosen).ToArray();
        var task = options.Where(t => t.Target?.Controller == p.Opponent).OrderByDescending(t => (t.Target as ICharacter)?.Health ?? 0).FirstOrDefault() ?? options.FirstOrDefault();
        Core2026Proof.Check(task != null && g.Process(task), "chosen reward has a native legal play");
    }
    private static int[] Bonus(Game g, int[] offers, int chosen, int source)
    {
        var expected = offers.Where(i => i != chosen).ToArray();
        // A chosen spell can open its own Discover before the queued map bonus.
        for (int i = 0; i < 10 && g.Player1.Choice != null && !g.Player1.Choice.Choices.SequenceEqual(expected); i++)
            Answer(g, g.Player1.Choice.Choices[0]);
        Core2026Proof.Check(g.Player1.Choice?.SourceId == source && g.Player1.Choice.Choices.SequenceEqual(expected)
            && expected.All(i => g.IdEntityDic[i].Zone == g.Player1.SetasideZone), "bonus retains exact unchosen entities/order/source");
        return expected;
    }
    internal static void Prove(Game g, Controller p, string id, CardClass hero, Func<Card, bool> matches, string[] fixture)
    {
        ReplayAndOrdering(g.Seed, id, hero, matches);
        // Tier D: pinned 37.0.0.254477 text and exact one-turn player enchantment; same recipe as TLC_442.
        p.AddHeroAndPower(Cards.HeroCard(hero));
        var pool = OfflineCardPools.Candidates(g, hero, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(pool.Count >= 3 && pool.All(matches), "printed class domain has at least three admitted matching candidates");
        TinyPools(g, id);
        var source = Start(g, p, id); var offers = Offers(g, p, source, matches);
        foreach (int chosen in offers)
        {
            var branch = g.CloneExact(history: true); Answer(branch, chosen);
            var kept = branch.IdEntityDic[chosen];
            Core2026Proof.Check(kept.Zone == branch.Player1.HandZone && kept.Card.Id == g.IdEntityDic[chosen].Card.Id
                && kept.Controller == branch.Player1, "exact selected entity enters hand");
            if (kept is Minion m) Core2026Proof.Check(m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health, "generated minion stats");
            Core2026Proof.Check(branch.Player1.AppliedEnchantments.Single(e => e.Card.Id == id + "e").IsOneTurnActive,
                "player enchant persists after source spell leaves");
            PlayChosen(branch, chosen); var bonus = Bonus(branch, offers, chosen, source.Id);
            foreach (int pick in bonus)
            {
                var answer = branch.CloneExact(history: true); var random = answer.Random.CaptureState(); Answer(answer, pick);
                Core2026Proof.Check(answer.IdEntityDic[pick].Zone == answer.Player1.HandZone
                    && answer.IdEntityDic[pick].Card.Id == g.IdEntityDic[pick].Card.Id && answer.Player1.Choice == null
                    && answer.Random.CaptureState().Equals(random), "bonus keeps original identity without resampling");
            }
        }
        PoolPilotProof.Resume(g, next => Answer(next, offers[0]));
        PoolPilotProof.Resume(g, next => { PlayChosen(next, offers[0]); var bonus = Bonus(next, offers, offers[0], source.Id); Answer(next, bonus[0]); });
        ParkBoard(p); PoolPilotProof.EmptyHand(p); Empty(g, p, id);
        foreach (int count in new[] { 1, 2, 3 }) WithPool(g, id, fixture.Take(count).ToArray(), () => {
            PoolPilotProof.EmptyHand(p); ParkBoard(p); ParkBoard(p.Opponent);
            source = Play(g, p, id); offers = Offers(g, p, source, matches); Answer(g, offers[0]);
            var armed = p.AppliedEnchantments.SingleOrDefault(e => e.Card.Id == id + "e");
            if (count == 1)
            {
                PlayChosen(g, offers[0]); Core2026Proof.Check(p.Choice == null && armed == null, "singleton leaves no bonus trigger"); return;
            }
            var duplicate = Add(p, g.IdEntityDic[offers[0]].Card.Id); PlayChosen(g, duplicate.Id);
            Core2026Proof.Check(p.Choice == null && p.AppliedEnchantments.Contains(armed), "different instance cannot trigger map");
            PlayChosen(g, offers[0]); var rejected = Bonus(g, offers, offers[0], source.Id);
            Core2026Proof.Check(!p.AppliedEnchantments.Contains(armed), "bonus consumes enchant exactly once");
            Answer(g, rejected[0]);
            var selected = g.IdEntityDic[offers[0]];
            if (selected is Minion minion) minion.Silence();
            selected.Zone.Remove(selected); p.HandZone.Add(selected); PlayChosen(g, selected.Id);
            Core2026Proof.Check(p.Choice == null, "replay cannot repeat consumed bonus");
        });
        WithPool(g, id, fixture, () => {
            PoolPilotProof.EmptyHand(p); ParkBoard(p); ParkBoard(p.Opponent);
            source = Play(g, p, id); offers = Offers(g, p, source, matches); Answer(g, offers[0]);
            End(g); End(g); PlayChosen(g, offers[0]);
            Core2026Proof.Check(p.Choice == null && !p.AppliedEnchantments.Any(e => e.Card.Id == id + "e"), "next-turn play gives no bonus");
            PoolPilotProof.EmptyHand(p); source = Play(g, p, id); offers = Offers(g, p, source, matches); Answer(g, offers[0]);
            End(g); PoolPilotProof.Resolve(g, source);
            var armed = p.AppliedEnchantments.Single(e => e.Card.Id == id + "e");
            var stolen = p.HandZone.Remove(g.IdEntityDic[armed.ScriptTag1]); stolen.Controller = p.Opponent; p.Opponent.HandZone.Add(stolen);
            PlayChosen(g, stolen.Id);
            Core2026Proof.Check(p.Choice == null && p.Opponent.Choice == null && p.AppliedEnchantments.Contains(armed), "opponent exact-entity play cannot trigger owner bonus");
            End(g); PoolPilotProof.EmptyHand(p); ParkBoard(p); FullBoard(p);
            source = Play(g, p, id); offers = Offers(g, p, source, matches); Answer(g, offers[0]);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.HandZone.Single().Id == offers[0], "full mixed board allows Discover without summoning");
            foreach (var e in p.AppliedEnchantments.Where(e => e.Card.Id == id + "e").ToArray()) e.Remove();
            ParkBoard(p); PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9);
            source = Play(g, p, id); offers = Offers(g, p, source, matches); PoolPilotProof.FillHand(p); Answer(g, offers[0]);
            Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[offers[0]].Zone == p.GraveyardZone
                && !p.AppliedEnchantments.Any(e => e.Card.Id == id + "e"), "initial overflow burns selection with no stale trigger");
            PoolPilotProof.EmptyHand(p); source = Play(g, p, id); offers = Offers(g, p, source, matches); Answer(g, offers[0]);
            PlayChosen(g, offers[0]); var rejected = Bonus(g, offers, offers[0], source.Id); PoolPilotProof.FillHand(p); Answer(g, rejected[0]);
            Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[rejected[0]].Zone == p.GraveyardZone && p.Choice == null, "bonus overflow burns reward and completes");
        });
    }

    private static void ReplayAndOrdering(long seed, string id, CardClass hero, Func<Card, bool> matches)
    {
        // Existing selector convention (f1ebace): indirect casts auto-pick; hand-cast tails wait for the answer.
        var tess = GiftedDiscoverProof.New(seed, CardClass.WARLOCK);
        tess.Player1.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        int candidates = OfflineCardPools.Candidates(tess, CardClass.WARLOCK, Filter(id), Cards.FromId(id).AssetId).Count;
        PoolPilotProof.Resume(tess, next => {
            Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " Tess replay leaves no stranded choice or recipe");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1
                && next.Player1.HandZone.Count == (candidates == 0 ? 0 : 1)
                && next.Player1.HandZone.All(c => matches(c.Card)), "Tess completes exactly one cast using its current class pool");
            End(next);
        });
        // Crypt Map has no off-class Frost candidates; exercise nonempty indirect casts in its printed class too.
        foreach (var kind in new[] { HistoryCastKind.Effect, HistoryCastKind.Repeat })
        {
            var indirect = GiftedDiscoverProof.New(seed, hero);
            var source = Entity.FromCard(indirect.Player1, Cards.FromId(id));
            PoolPilotProof.Resume(indirect, next => {
                var owner = next.Player1;
                new EffectContext(next, owner.Id, owner.Hero.Id).CastSpell(source.Id, kind);
                next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(owner.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null
                    && next.IdEntityDic[source.Id].Zone == owner.GraveyardZone && owner.HandZone.Count == 1
                    && matches(owner.HandZone.Single().Card), id + " nonempty indirect cast finishes synchronously");
                var effect = owner.AppliedEnchantments.Single(e => e.Card.Id == id + "e");
                Core2026Proof.Check(effect.ScriptTag1 == owner.HandZone.Single().Id && effect.ScriptTag2 != 0
                    && effect[GameTag.TAG_SCRIPT_DATA_ENT_1] != 0, "indirect selection arms the exact reward and both rejected offers");
                End(next);
            });
        }
        var ordered = GiftedDiscoverProof.New(seed, hero); var player = ordered.Player1;
        var thresher = Summon(player, "TLC_256");
        int spells = player.NumSpellsPlayedThisGame;
        var map = Play(ordered, player, id);
        Core2026Proof.Check(ordered.SpellCompletion?.SourceId == map.Id && map.Zone != player.GraveyardZone
            && !thresher.HasDivineShield && player.NumSpellsPlayedThisGame == spells,
            "map waits for selection before graveyard, after-cast trigger and spell counter");
        int chosen = player.Choice.Choices[0];
        PoolPilotProof.Resume(ordered, next => {
            Answer(next, chosen); var owner = next.Player1;
            Core2026Proof.Check(next.SpellCompletion == null && !next.Resolution.HasSuspendedEffect && owner.Choice == null
                && next.IdEntityDic[map.Id].Zone == owner.GraveyardZone && ((Minion)next.IdEntityDic[thresher.Id]).HasDivineShield
                && owner.NumSpellsPlayedThisGame == spells + 1 && owner.HandZone.Single().Id == chosen,
                "selection finishes effect before after-cast trigger and counters, once across clone/snapshot");
        });
    }
}
