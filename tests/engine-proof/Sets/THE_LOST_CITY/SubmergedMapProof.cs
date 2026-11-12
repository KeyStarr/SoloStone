using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static LostCityPoolProof;

internal static class SubmergedMapProof
{
    private static bool Matches(Card card) => card.Type == CardType.MINION && card.IsRace(Race.MURLOC);
    private static void EnsureTarget(Controller p)
    {
        if (!p.BoardZone.Any()) Summon(p, "CS2_182");
    }
    private static void PlayChosen(Game g, int chosen)
    {
        var p = g.CurrentPlayer; p.BaseMana = 10; p.UsedMana = 0;
        var task = p.Options().FirstOrDefault(t => t.Source?.Id == chosen);
        Core2026Proof.Check(task != null && g.Process(task), "generated card has legal native play: " + g.IdEntityDic[chosen].Card.Id + "/zone=" + g.IdEntityDic[chosen].Zone.Type + "/cost=" + g.IdEntityDic[chosen].Cost + "/slots=" + p.BoardZone.SlotCount);
        // Finish the selected Murloc's own Battlecry choice before Map's queued rejected offers.
        while (p.Choice?.SourceId == chosen) Answer(g, p.Choice.Choices.First());
    }
    private static int[] Bonus(Game g, int[] original, int chosen, int source)
    {
        var expected = original.Where(i => i != chosen).ToArray();
        Core2026Proof.Check(g.Player1.Choice != null && g.Player1.Choice.SourceId == source &&
            g.Player1.Choice.Choices.SequenceEqual(expected), "bonus offers only exact rejected entities in original order");
        Core2026Proof.Check(expected.All(i => g.IdEntityDic[i].Zone == g.Player1.SetasideZone), "bonus offers remain private setaside entities");
        var bot = FairBotState.Create(g, g.Player1.PlayerId, 901, DeckStyle.Value);
        Core2026Proof.Check(bot.CanExpand && bot.Actions.Length == expected.Length && bot.Actions.All(a => bot.Apply(a) != null), "bot can answer every bonus offer");
        return expected;
    }
    internal static void Run() => StandardSetsProof.Case("TLC_442", Prove);
    private static void Prove(Game g, Controller p, string id)
    {
        var replay = GiftedDiscoverProof.New(g.Seed, CardClass.WARRIOR);
        replay.Player1.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        PoolPilotProof.Resume(replay, next => {
            Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " Tess replay finishes Discover synchronously");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1
                && next.Player1.HandZone.Single().Card.IsRace(Race.MURLOC)
                && next.Player1.AppliedEnchantments.Count(e => e.Card.Id == id + "e") == 1,
                "Tess Map grants one Murloc and arms its rejected-offer bonus once");
            End(next);
        });
        DecisionSpellOrderingProof.Run(g.Seed, id, (next, chosen) =>
            Core2026Proof.Check(chosen.Zone == next.Player1.HandZone
                && next.Player1.AppliedEnchantments.Single(e => e.Card.Id == id + "e").ScriptTag1 == chosen.Id,
                "Map grants selected Murloc and arms bonus before after-cast"));

        p.Hero.Card = Cards.HeroCard(CardClass.PALADIN);
        TinyPools(g, id);
        var source = Start(g, p, id);
        var offers = Offers(g, p, source, c => Matches(c));
        foreach (int chosen in offers)
        {
            var branch = g.CloneExact(history: true); Answer(branch, chosen);
            var owner = branch.Player1; var kept = branch.IdEntityDic[chosen];
            Core2026Proof.Check(kept.Zone == owner.HandZone && kept.Controller == owner && kept.ZonePosition == 0 &&
                kept.Card.Id == g.IdEntityDic[chosen].Card.Id && kept.Cost == kept.Card.Cost, "selected original entity enters hand with printed cost");
            if (kept is Minion minion) Core2026Proof.Check(minion.AttackDamage == minion.Card.ATK && minion.Health == minion.Card.Health, "generated minion keeps pinned stats");
            Core2026Proof.Check(branch.IdEntityDic[source.Id].Zone.Type == Zone.GRAVEYARD && owner.AppliedEnchantments.Single(e => e.Card.Id == id + "e").IsOneTurnActive,
                "exact one-turn player enchant survives source spell leaving play");
            EnsureTarget(owner.Opponent);
            PlayChosen(branch, chosen);
            var bonus = Bonus(branch, offers, chosen, source.Id);
            foreach (int pick in bonus)
            {
                var answer = branch.CloneExact(history: true); var rng = answer.Random.CaptureState(); Answer(answer, pick);
                Core2026Proof.Check(answer.IdEntityDic[pick].Zone == answer.Player1.HandZone && answer.IdEntityDic[pick].Controller == answer.Player1 &&
                    answer.IdEntityDic[pick].Card.Id == g.IdEntityDic[pick].Card.Id && answer.Player1.Choice == null &&
                    answer.Random.CaptureState().Equals(rng), "each bonus answer keeps original card without resampling");
            }
        }
        PoolPilotProof.Resume(g, next => Answer(next, offers[0]));
        var armed = p.AppliedEnchantments.Single(e => e.Card.Id == id + "e");
        EnsureTarget(p.Opponent);
        var duplicate = Add(p, g.IdEntityDic[offers[0]].Card.Id); PlayChosen(g, duplicate.Id);
        Core2026Proof.Check(p.Choice == null && p.AppliedEnchantments.Contains(armed), "same card identity on different entity does not trigger");
        // Generated Murlocs may have Reborn; park fixture actors without death side effects.
        ParkBoard(p); EnsureTarget(p.Opponent);
        PoolPilotProof.Resume(g, next => PlayChosen(next, offers[0]));
        var rejected = Bonus(g, offers, offers[0], source.Id);
        Core2026Proof.Check(!p.AppliedEnchantments.Contains(armed), "bonus trigger consumes player enchant exactly once");
        PoolPilotProof.Resume(g, next => Answer(next, rejected[0]));
        var selected = (Minion)g.IdEntityDic[offers[0]];
        selected.Silence(); g.DeathProcessingAndAuraUpdate();
        p.HandZone.Add(p.BoardZone.Remove(selected)); PlayChosen(g, selected.Id);
        Core2026Proof.Check(p.Choice == null, "silencing and replaying selected entity cannot repeat consumed bonus");
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        Empty(g, p, id);
        const string single = "CORE_EX1_509";
        WithPool(g, id, new[] { single }, () => {
            source = Play(g, p, id); var only = Offers(g, p, source, c => Matches(c)).Single(); Answer(g, only);
            EnsureTarget(p.Opponent); PlayChosen(g, only);
            Core2026Proof.Check(p.Choice == null && !p.AppliedEnchantments.Any(e => e.Card.Id == id + "e"), "singleton keeps card without bonus or stale trigger");
        });
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        WithPool(g, id, new[] { "CORE_EX1_509", "CORE_EX1_506" }, () => {
            source = Play(g, p, id); var pair = Offers(g, p, source, c => Matches(c)); Answer(g, pair[0]);
            EnsureTarget(p.Opponent); PlayChosen(g, pair[0]);
            var last = Bonus(g, pair, pair[0], source.Id).Single(); Answer(g, last);
            Core2026Proof.Check(p.HandZone.Single().Id == last && p.Choice == null, "two-candidate pool gives one exact bonus offer");
        });
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        // Isolate independent Map bindings from generated Battlecries that replace the whole hand.
        WithPool(g, id, new[] { "CORE_EX1_509", "CORE_EX1_506", "CORE_EX1_507" }, () => {
            var firstMap = Play(g, p, id); var firstOffers = Offers(g, p, firstMap, c => Matches(c)); Answer(g, firstOffers[0]);
            var secondMap = Play(g, p, id); var secondOffers = Offers(g, p, secondMap, c => Matches(c)); Answer(g, secondOffers[0]);
            Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == id + "e") == 2, "two maps retain independent player enchantments");
            EnsureTarget(p.Opponent); PlayChosen(g, firstOffers[0]);
            Answer(g, Bonus(g, firstOffers, firstOffers[0], firstMap.Id)[0]);
            Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == id + "e") == 1, "first map does not consume second map");
            EnsureTarget(p.Opponent); PlayChosen(g, secondOffers[0]);
            Answer(g, Bonus(g, secondOffers, secondOffers[0], secondMap.Id)[0]);
        });
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        source = Play(g, p, id); offers = Offers(g, p, source, c => Matches(c)); Answer(g, offers[0]);
        armed = p.AppliedEnchantments.Single(e => e.Card.Id == id + "e");
        End(g);
        EnsureTarget(p);
        var enemyCopy = Add(p.Opponent, g.IdEntityDic[offers[0]].Card.Id); PlayChosen(g, enemyCopy.Id);
        Core2026Proof.Check(p.Choice == null && !p.AppliedEnchantments.Contains(armed), "turn end expires player trigger; opponent copy cannot trigger");
        End(g); EnsureTarget(p.Opponent); PlayChosen(g, offers[0]);
        Core2026Proof.Check(p.Choice == null, "playing selected card next turn gives no bonus");
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        source = Play(g, p, id); offers = Offers(g, p, source, c => Matches(c)); Answer(g, offers[0]);
        armed = p.AppliedEnchantments.Single(e => e.Card.Id == id + "e");
        End(g);
        // Install a fresh map during the opponent turn: the exact selected entity is stolen and played there.
        PoolPilotProof.EmptyHand(p); PoolPilotProof.Resolve(g, source);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "indirect Map resolves synchronously during opponent turn");
        offers = new[] { p.HandZone.Single().Id };
        armed = p.AppliedEnchantments.Single(e => e.Card.Id == id + "e");
        var stolen = p.HandZone.Remove(g.IdEntityDic[offers[0]]); stolen.Controller = p.Opponent; p.Opponent.HandZone.Add(stolen);
        EnsureTarget(p); PlayChosen(g, stolen.Id);
        Core2026Proof.Check(p.Choice == null && p.Opponent.Choice == null && p.AppliedEnchantments.Contains(armed), "opponent playing exact selected entity does not trigger owner's map");
        End(g);
        ClearBoard(g, p); PoolPilotProof.EmptyHand(p); FullBoard(p);
        source = Play(g, p, id); offers = Offers(g, p, source, c => Matches(c)); Answer(g, offers[0]);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.HandZone.Single().Id == offers[0], "full mixed board does not block Discover or summon any offer");
        foreach (var effect in p.AppliedEnchantments.Where(e => e.Card.Id == id + "e").ToArray()) effect.Remove();
        ClearBoard(g, p);
        foreach (var location in p.BoardZone.GetLocations().ToArray()) location.Destroy();
        g.DeathProcessingAndAuraUpdate();
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9);
        source = Play(g, p, id); offers = Offers(g, p, source, c => Matches(c)); PoolPilotProof.FillHand(p); Answer(g, offers[0]);
        Core2026Proof.Check(g.IdEntityDic[offers[0]].Zone == p.GraveyardZone && p.HandZone.Count == 10 &&
            !p.AppliedEnchantments.Any(e => e.Card.Id == id + "e"), "full hand burns initial selection and installs no unplayable trigger");
        PoolPilotProof.EmptyHand(p);
        source = Play(g, p, id); offers = Offers(g, p, source, c => Matches(c)); Answer(g, offers[0]);
        EnsureTarget(p.Opponent); PlayChosen(g, offers[0]);
        rejected = Bonus(g, offers, offers[0], source.Id); PoolPilotProof.FillHand(p); Answer(g, rejected[0]);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[rejected[0]].Zone == p.GraveyardZone && p.Choice == null, "full hand burns bonus selection without repeating trigger");
    }
}
