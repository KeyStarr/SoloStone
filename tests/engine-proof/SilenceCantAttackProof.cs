using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// Minion.Silence clears a card's own printed "Can't attack" (tag 227 on the base row: Ragnaros the Firelord EX1_298, Ancient
// Watcher EX1_045), and nothing else. The positive cases are retail behaviour (Silence removes card text). The effect-ban, Sleep
// and Cursed Chains cases are ENGINE REGRESSION GUARDS, not retail pins: they record what the engine did before and after the
// Silence line, and the source for "a ban another effect set stays" is B16TimersBoundaryProof.Unpinned_Control, an unpinned
// assumption. Where retail differs, change the engine and these expectations together.
internal static class SilenceCantAttackProof
{
    private const string Silence = "BE_228", Yeti = "CS2_182";

    private static Game New(bool history)
    {
        var game = new Game(new GameConfig { RandomSeed = 5, StartPlayer = 1, SkipMulligan = true, History = history,
            Logging = false, FormatType = FormatType.FT_WILD });
        game.StartGame();
        return game;
    }

    private static Game[] Branches(Game g, bool history) => new[] { g, g.CloneExact(history: history), Game.LoadSnapshot(g.SaveSnapshot()) };

    internal static void Run()
    {
        foreach (bool history in new[] { true, false })
        foreach (string id in new[] { "EX1_298", "EX1_045" })
        {
            var game = New(history);
            var minion = Summon(game.Player1, id);
            var foe = Summon(game.Player2, Yeti);
            End(game); End(game); // ready next turn: only the printed ban stops the attack
            Core2026Proof.Check(minion.CantAttack && minion.Card.CantAttack && !SilenceKeywordsProof.CanAttack(minion, foe), id + " cannot attack before Silence");
            foreach (var copy in Branches(game, history))
            {
                var target = (Minion)copy.IdEntityDic[minion.Id]; var enemy = (Minion)copy.IdEntityDic[foe.Id];
                Core2026Proof.Check(target.CantAttack && !SilenceKeywordsProof.CanAttack(target, enemy), id + " ban survives the copy");
                Play(copy, copy.Player1, Silence, target);
                Core2026Proof.Check(target.IsSilenced && !target.CantAttack && SilenceKeywordsProof.CanAttack(target, enemy)
                    && SilenceKeywordsProof.CanAttack(target, copy.Player2.Hero), id + " can attack after Silence");
                foreach (var again in Branches(copy, history))
                {
                    var after = (Minion)again.IdEntityDic[minion.Id];
                    Core2026Proof.Check(!after.CantAttack && SilenceKeywordsProof.CanAttack(after, (Minion)again.IdEntityDic[foe.Id]),
                        id + " keeps attacking after CloneExact and a snapshot round trip");
                }
            }
        }

        foreach (bool history in new[] { true, false })
        {
            // A minion without its own 227 whose ban another effect set: Silence leaves the tag alone.
            var game = New(history);
            var yeti = Summon(game.Player1, Yeti);
            End(game); End(game);
            yeti[GameTag.CANT_ATTACK] = 1;
            Core2026Proof.Check(!yeti.Card.CantAttack && yeti.CantAttack, "fixture: an effect ban on a card without its own");
            yeti.Silence();
            Core2026Proof.Check(yeti.CantAttack, "Silence does not clear a ban another effect set");

            // ENGINE REGRESSION GUARD, not a retail pin: Sleep (Tranquil Clearing) is an enchantment; Silence removes it as any enchantment.
            var sleeper = Summon(game.Player1, Yeti);
            var source = Summon(game.Player1, Yeti);
            PlayLockRules.Sleep(sleeper, source, "MEND_044e3");
            Core2026Proof.Check(sleeper.CantAttack && !sleeper.Card.CantAttack, "fixture: asleep");
            bool before = sleeper.CantAttack;
            sleeper.Silence();
            Core2026Proof.Check(sleeper.CantAttack == PlayLockRules.IsSleeping(sleeper) && PlayLockRules.IsSleeping(sleeper) == SLEEP_AFTER_SILENCE,
                "Sleep after Silence follows the enchantment rule only (was " + before + ")");

            // ENGINE REGRESSION GUARD, not a retail pin: the Cursed Chains lease ban is a turn-scoped enchantment; Silence returns the minion and
            // removes it (the printed-ban line is not involved: Yeti has none).
            var g = HeraldProof.New(CardClass.WARLOCK); var p = g.CurrentPlayer;
            var victim = Summon(p.Opponent, Yeti);
            Play(g, p, "CATA_496", victim);
            foreach (var branch in Branches(g, true))
            {
                var v = (Minion)branch.IdEntityDic[victim.Id];
                Core2026Proof.Check(v.CantAttack && !v.Card.CantAttack, "Cursed Chains: can't attack this turn");
                v.Silence();
                Core2026Proof.Check(v.Controller == branch.ControllerById(p.Opponent.Id) && v.CantAttack == CHAINS_AFTER_SILENCE,
                    "Cursed Chains after Silence: control returns, the turn ban " + (CHAINS_AFTER_SILENCE ? "stays" : "ends"));
            }
        }
        // ENGINE REGRESSION GUARD, not a retail pin: a card with its own 227 AND a ban from another effect (Ragnaros under Sleep, and
        // under Cursed Chains). Silence removes the enchantment and the printed ban, so he can attack afterwards.
        foreach (bool history in new[] { true, false })
        {
            var game = New(history);
            var rag = Summon(game.Player1, "EX1_298"); var anchor = Summon(game.Player1, Yeti);
            PlayLockRules.Sleep(rag, anchor, "MEND_044e3");
            Core2026Proof.Check(rag.CantAttack && PlayLockRules.IsSleeping(rag) && rag.Card.CantAttack, "fixture: Ragnaros asleep with his own ban");
            rag.Silence();
            Core2026Proof.Check(!PlayLockRules.IsSleeping(rag) && !rag.CantAttack, "Ragnaros under Sleep, after Silence: both bans gone");

            var g = HeraldProof.New(CardClass.WARLOCK); var p = g.CurrentPlayer;
            var enemyRag = Summon(p.Opponent, "EX1_298");
            Play(g, p, "CATA_496", enemyRag);
            foreach (var branch in Branches(g, true))
            {
                var r = (Minion)branch.IdEntityDic[enemyRag.Id];
                Core2026Proof.Check(r.CantAttack && r.Card.CantAttack, "fixture: leased Ragnaros has both bans");
                r.Silence();
                Core2026Proof.Check(r.Controller == branch.ControllerById(p.Opponent.Id) && !r.CantAttack,
                    "Ragnaros under Cursed Chains, after Silence: control returns, both bans gone");
            }
        }
        Console.WriteLine("PASS Silence and printed Can't attack: Ragnaros and Ancient Watcher attack after Silence, clone and snapshot; effect ban, Sleep and Cursed Chains unchanged");
    }

    private const bool SLEEP_AFTER_SILENCE = false, CHAINS_AFTER_SILENCE = false;
}
