using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Existing DecisionSpell/DecisionBattlecry completion contract (docs/roadmap/2026-10-20-selectors.md).
internal static class DecisionChoiceReplayProof
{
    internal static void Spell(long seed, string id, bool gifted = false)
    {
        var normal = GiftedDiscoverProof.New(seed, CardClass.ROGUE);
        var p = normal.Player1;
        var pyro = Summon(p, "CORE_NEW1_020");
        var target = p.Opponent.Hero;
        int spells = p.NumSpellsPlayedThisGame;
        var reference = normal.CloneExact(history: true);
        var expected = OfflineCardPools.SampleDistinct(reference, PoolPilotProof.Filter(id), p.HeroClass, Cards.FromId(id).AssetId);
        IPlayable source;
        if (id == "TIME_612")
        {
            source = Add(p, id);
            Core2026Proof.Check(normal.Process(PlayCardTask.Any(p, source)) && p.Hero.Damage == 3,
                "Blood Draw pays Health before choosing");
        }
        else source = Play(normal, p, id, id == "FIR_939" ? target : null);
        Core2026Proof.Check(p.Choice != null && normal.SpellCompletion?.SourceId == source.Id && pyro.Damage == 0
            && p.NumSpellsPlayedThisGame == spells && source.Zone != p.GraveyardZone,
            id + " normal Discover defers after-cast and completion");
        Core2026Proof.Check(p.Choice.Choices.Select(key => normal.IdEntityDic[key].Card.Id).SequenceEqual(expected.Select(c => c.Id)),
            id + " unchanged declared-pool offer sampling and order");
        int selected = p.Choice.Choices[0];
        PoolPilotProof.Resume(normal, next => {
            CoreCostProof.Answer(next, selected);
            var chosen = next.IdEntityDic[selected];
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null
                && next.IdEntityDic[source.Id].Zone == next.Player1.GraveyardZone
                && next.Player1.NumSpellsPlayedThisGame == spells + 1 && ((Minion)next.IdEntityDic[pyro.Id]).Damage == 1,
                id + " normal answer completes once before after-cast");
            Core2026Proof.Check(gifted ? DarkGifts.Has(chosen) && (chosen.Zone == next.Player1.HandZone || chosen.Zone == next.Player1.DeckZone)
                : chosen.Zone == next.Player1.HandZone, id + " exact chosen offer delivered");
            End(next);
        });

        // Direct effect cast must return only after delivering its compulsory choice.
        var indirect = GiftedDiscoverProof.New(seed, CardClass.ROGUE);
        var victim = indirect.Player2.Hero;
        var spell = (Spell)Entity.FromCard(indirect.Player1, Cards.FromId(id), zone: indirect.Player1.SetasideZone);
        PoolPilotProof.Resume(indirect, next => {
            ICharacter castTarget = id == "FIR_939"
                ? (ICharacter)next.IdEntityDic[victim.Id] : null;
            Generic.CastSpell.Invoke(next.Player1, next, (Spell)next.IdEntityDic[spell.Id], castTarget, 0);
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null
                && next.Player1.HandZone.Concat(next.Player1.DeckZone).Count(c => c[GameTag.DISPLAYED_CREATOR] == spell.Id) == 1
                && next.IdEntityDic[spell.Id].Zone == next.Player1.GraveyardZone,
                id + " indirect cast delivers one offer and completes inline");
            next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
            End(next);
        });
        var replay = GiftedDiscoverProof.New(seed, CardClass.ROGUE);
        replay.Player1.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        PoolPilotProof.Resume(replay, next => {
            Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null
                && next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1,
                id + " Tess completes one cast without a stranded choice");
            var outputs = next.Player1.HandZone.Concat(next.Player1.DeckZone).Where(c => c[GameTag.DISPLAYED_CREATOR] != 0).ToArray();
            Core2026Proof.Check(outputs.Length == 1 && (!gifted || DarkGifts.Has(outputs[0])), id + " Tess delivers one chosen offer");
            End(next);
        });
    }

    internal static void Battlecry(long seed, string id, CardClass hero)
    {
        var normal = DeathKnightC1Proof.NewGame(seed, hero);
        var p = normal.Player1;
        Summon(p, "LOE_077");
        if (id == "CORE_RLK_066") CorpseRules.Gain(p, 2);
        var reference = normal.CloneExact(history: true);
        var expected = OfflineCardPools.SampleDistinct(reference, PoolPilotProof.Filter(id), p.HeroClass, Cards.FromId(id).AssetId);
        Play(normal, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Select(key => normal.IdEntityDic[key].Card.Id).SequenceEqual(expected.Select(c => c.Id)),
            id + " unchanged declared-pool first offers with Brann");
        Core2026Proof.Check(Generic.HasDecisionPlay(p) && normal.Resolution.HasSuspendedEffect && p.Choice?.NextChoice == null,
            id + " Brann suspends first Battlecry before rebuilding second offers");
        int selected = p.Choice.Choices[0];
        PoolPilotProof.Resume(normal, next => {
            CoreCostProof.Answer(next, selected);
            Core2026Proof.Check(next.Player1.HandZone.Single().Id == selected && next.Player1.Choice != null
                && Generic.HasDecisionPlay(next.Player1), id + " first result delivered before second choice");
            int second = next.Player1.Choice.Choices[0];
            CoreCostProof.Answer(next, second);
            Core2026Proof.Check(next.Player1.HandZone.Select(c => c.Id).SequenceEqual(new[] { selected, second })
                && next.Player1.Choice == null && !Generic.HasDecisionPlay(next.Player1) && !next.Resolution.HasSuspendedEffect,
                id + " Brann delivers two independently chosen entities and completes");
            if (id == "CORE_RLK_066") Core2026Proof.Check(CorpseRules.Available(next.Player1) == 0, "Hematurge spends once per activation");
            End(next);
        });
        var automatic = DeathKnightC1Proof.NewGame(seed, hero);
        var actor = Summon(automatic.Player1, id);
        if (id == "CORE_RLK_066") CorpseRules.Gain(automatic.Player1, 1);
        PoolPilotProof.Resume(automatic, next => {
            PoolPilotProof.Resolve(next, next.IdEntityDic[actor.Id]);
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect,
                id + " indirect Battlecry completes before returning to replay queue");
            PoolPilotProof.Generated(next, next.IdEntityDic[actor.Id], next.Player1.HandZone, 1,
                filter: PoolPilotProof.Filter(id));
            End(next);
        });
        foreach (bool brann in new[] { false, true })
        {
            var replay = DeathKnightC1Proof.NewGame(seed, hero);
            var owner = replay.Player1;
            if (brann) Summon(owner, "LOE_077");
            if (id == "CORE_RLK_066") CorpseRules.Gain(owner, 2);
            owner.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
            PoolPilotProof.Resume(replay, next => {
                var shudder = Play(next, next.Player1, "GIL_820");
                Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && !Generic.HasDecisionPlay(next.Player1)
                    && next.Player1.HandZone.Count == (brann ? 2 : 1),
                    id + " Shudderwock completes each automatic Battlecry, including Brann");
                PoolPilotProof.Generated(next, shudder, next.Player1.HandZone, brann ? 2 : 1,
                    filter: PoolPilotProof.Filter(id));
                End(next);
            });
        }
    }
}
