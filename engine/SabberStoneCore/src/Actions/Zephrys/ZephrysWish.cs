using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>Public facts the pool gates read. Locations are never minions here (patch 30.4).</summary>
    internal sealed class WishView
    {
        internal int MyMinions, EnemyMinions, UnfrozenEnemyMinions, MyHand, MyDeck, EnemyDeck, EnemySecrets;
        internal int MyArmor, MyHeroAttack, MyWeaponAttack, FriendlyDiedThisTurn;
        internal bool EnemyWeapon, EnemyStealth, Murlocs, Demons, DivineShields;

        internal static WishView Of(Controller me)
        {
            var op = me.Opponent;
            var mine = Minions(me).ToArray();
            var theirs = Minions(op).ToArray();
            var all = mine.Concat(theirs).ToArray();
            return new WishView {
                MyMinions = mine.Length, EnemyMinions = theirs.Length,
                UnfrozenEnemyMinions = theirs.Count(m => !m.IsFrozen),
                MyHand = me.HandZone.Count, MyDeck = me.DeckZone.Count, EnemyDeck = op.DeckZone.Count,
                EnemySecrets = op.SecretZone.Count,
                MyArmor = me.Hero.Armor, MyHeroAttack = me.Hero.AttackDamage,
                MyWeaponAttack = me.Hero.Weapon?.AttackDamage ?? 0,
                FriendlyDiedThisTurn = me.NumFriendlyMinionsThatDiedThisTurn,
                EnemyWeapon = op.Hero.Weapon != null,
                EnemyStealth = theirs.Any(m => m.HasStealth),
                Murlocs = all.Any(m => m.IsRace(Race.MURLOC)),
                Demons = all.Any(m => m.IsRace(Race.DEMON)),
                DivineShields = all.Any(m => m.HasDivineShield),
            };
        }

        internal static IEnumerable<Minion> Minions(Controller c) => c.BoardZone.Where(m => m != null && !m.IsDormant);
    }

    /// <summary>
    /// Zephrys the Great: "wish for the perfect card". Retail runs a server-side rule set whose scoring was never published; this
    /// rebuilds it from Blizzard's description and the wiki's documented offers:
    /// - only public information: hands and decks become blank cards of the same count, enemy Secrets are removed (only their
    ///   number is known), Deathrattle text is unread (valued from stats);
    /// - lethal first, never lethal that depends on randomness;
    /// - otherwise every candidate is played on every sensible target on a copy and the resulting board is scored, random cards
    ///   pessimistically; the best card of each of the three strongest categories is offered;
    /// - cards playable next turn count for less; the choice itself is deterministic and never draws from the live RNG.
    /// </summary>
    internal static class ZephrysWish
    {
        internal const int Offered = 3;
        private const int RandomSamples = 8;
        private const string Placeholder = "CS2_182"; // vanilla 4/5: stands in for every hidden card
        private const string Pyroblast = "EX1_279";
        // Competitive Spirit only acts when its owner's turn starts, so it can never fire during the wish's own turn.
        private const string SecretStandIn = "AT_073";
        private static readonly HashSet<string> SilenceFirst = new HashSet<string>(StringComparer.Ordinal) { "EX1_332", "EX1_245", "CS2_203" };

        private sealed class Option { internal int ChooseOne, TargetId; }

        private sealed class Result
        {
            internal WishEntry Entry;
            internal int Index, Cost;
            internal double Delta;
            internal bool Lethal, Now;
        }

        /// <summary>The offered cards, best first. Same state, same offers.</summary>
        internal static Card[] Offers(Game game, Controller controller, IEntity source)
        {
            long seed = Mix(game.Seed, game.Turn, source.Id, controller.Id);
            Game basis;
            try { basis = PublicCopy(game, controller, seed); }
            catch (Exception e) when (!(e is OutOfMemoryException)) { basis = null; }
            if (basis == null || basis.CurrentPlayer.Id != controller.Id) return Fallback(controller);

            var me = basis.ControllerById(controller.Id);
            var view = WishView.Of(me);
            int mana = me.RemainingMana;
            int next = Math.Min(10, me.BaseMana + 1) - me.OverloadOwed;
            double baseline = Score(me);
            bool alreadyLethal;
            try { alreadyLethal = ZephrysLethal.Can(basis.CloneForSimulation(Mix(seed, -1, 0, 0)), controller.Id); }
            catch (Exception e) when (!(e is OutOfMemoryException)) { alreadyLethal = false; }

            var results = new List<Result>();
            for (int i = 0; i < ZephrysPool.Entries.Length; i++)
            {
                var entry = ZephrysPool.Entries[i];
                if (!ZephrysPool.Eligible(entry, view)) continue;
                var card = Cards.FromId(entry.Id);
                int cost = WishCost(card, me);
                bool now = cost <= mana;
                if (!now && (cost > next || entry.Flags.HasFlag(WishFlags.ThisTurnOnly))) continue;
                var result = Evaluate(basis, controller.Id, entry, card, cost, now, baseline, Mix(seed, i, 0, 0));
                if (result == null) continue;
                result.Index = i;
                if (!now)
                {
                    result.Delta *= entry.Category == WishCategory.Removal ? 0.3 : 0.45;
                    result.Lethal = false;
                }
                if (alreadyLethal) result.Lethal = false;
                results.Add(result);
            }

            // Pyroblast next turn: the one lethal Zephrys plans a turn ahead (wiki, Nervig 2020-02-03).
            if (!alreadyLethal && !results.Any(r => r.Lethal))
            {
                var pyro = results.FirstOrDefault(r => r.Entry.Id == Pyroblast && !r.Now);
                var op = me.Opponent;
                if (pyro != null && op.Hero.Health + op.Hero.Armor - FaceNow(me) <= 10
                    && !WishView.Minions(op).Any(m => m.HasLifeSteal && m.AttackDamage > 0))
                    pyro.Lethal = true;
            }
            return Select(results).Select(r => Cards.FromId(r.Entry.Id)).ToArray();
        }

        private static IEnumerable<Result> Select(List<Result> results)
        {
            var chosen = new List<Result>();
            foreach (var lethal in results.Where(r => r.Lethal)
                         .OrderByDescending(r => SilenceFirst.Contains(r.Entry.Id))
                         .ThenBy(r => r.Now ? 0 : 1).ThenBy(r => r.Cost).ThenBy(r => r.Index))
            {
                if (chosen.Count == Offered) break;
                chosen.Add(lethal);
            }

            // The best card of each strongest category; a category may take another slot when its runner-up clearly beats
            // the next category's best (three board clears against a wide board).
            var queues = results.Where(r => !chosen.Contains(r) && r.Delta > 0)
                .GroupBy(r => r.Entry.Category)
                .Select(g => new Queue<Result>(g.OrderByDescending(r => r.Delta).ThenBy(r => r.Index)))
                .OrderByDescending(q => q.Peek().Delta).ThenBy(q => q.Peek().Index).ToList();
            var used = new HashSet<WishCategory>(chosen.Select(r => r.Entry.Category));
            while (chosen.Count < Offered && queues.Any(q => q.Count > 0))
            {
                var fresh = queues.FirstOrDefault(q => q.Count > 0 && !used.Contains(q.Peek().Entry.Category));
                var repeat = queues.Where(q => q.Count > 0 && used.Contains(q.Peek().Entry.Category))
                    .OrderByDescending(q => q.Peek().Delta).ThenBy(q => q.Peek().Index).FirstOrDefault();
                var take = fresh == null || repeat != null && repeat.Peek().Delta >= 1.5 * fresh.Peek().Delta ? repeat : fresh;
                var pick = take.Dequeue();
                used.Add(pick.Entry.Category);
                chosen.Add(pick);
            }

            // Too few useful wishes: fill with the best remaining, proactive cards first.
            foreach (var filler in results.Where(r => !chosen.Contains(r))
                         .OrderByDescending(r => r.Entry.Category == WishCategory.Proactive)
                         .ThenByDescending(r => r.Delta).ThenBy(r => r.Index))
            {
                if (chosen.Count == Offered) break;
                chosen.Add(filler);
            }
            return chosen;
        }

        private static Result Evaluate(Game basis, int controllerId, WishEntry entry, Card card, int cost, bool now,
            double baseline, long seed)
        {
            List<Option> options;
            try { options = Options(basis, controllerId, entry, card, cost, now, seed); }
            catch (Exception e) when (!(e is OutOfMemoryException)) { return null; }
            if (options.Count == 0) return null;
            bool random = entry.Flags.HasFlag(WishFlags.Random);
            bool lethalAllowed = now && !entry.Flags.HasFlag(WishFlags.NeverForLethal);
            Result best = null;
            for (int o = 0; o < options.Count; o++)
            {
                var deltas = new List<double>();
                bool lethal = lethalAllowed;
                for (int s = 0; s < (random ? RandomSamples : 1); s++)
                {
                    // A copy is thrown away: a rule that fails on an unusual copied state skips the line, never the live game.
                    try
                    {
                        var sim = basis.CloneForSimulation(Mix(seed, o, s, 1));
                        var me = sim.ControllerById(controllerId);
                        if (!Play(sim, me, card, cost, now, options[o])) { lethal = false; continue; }
                        bool won = me.Opponent.Hero.IsDead || sim.State == State.COMPLETE && me.PlayState == PlayState.WINNING;
                        if (!won && me.Hero.IsDead) { deltas.Add(-1000); lethal = false; continue; }
                        deltas.Add(won ? 1000 : Score(me) - baseline);
                        if (lethal && !won) lethal = ZephrysLethal.Can(sim, controllerId);
                    }
                    catch (Exception e) when (!(e is OutOfMemoryException)) { lethal = false; }
                }
                if (deltas.Count == 0) continue;
                deltas.Sort();
                // Pessimistic for random outcomes: the second-worst run, not the average (Brawl: assume the median survivor).
                double delta = deltas[Math.Min(1, deltas.Count - 1)];
                if (best == null || delta > best.Delta || lethal && !best.Lethal)
                    best = new Result { Entry = entry, Cost = cost, Now = now, Delta = Math.Max(delta, best?.Delta ?? delta),
                        Lethal = lethal || (best?.Lethal ?? false) };
            }
            return best;
        }

        /// <summary>Legal ways to play the card (Choose One branches and targets), keeping only sensible targets.</summary>
        private static List<Option> Options(Game basis, int controllerId, WishEntry entry, Card card, int cost, bool now, long seed)
        {
            var probe = basis.CloneForSimulation(seed);
            var me = probe.ControllerById(controllerId);
            var playable = Prepare(me, card, cost, now);
            var options = new List<Option>();
            foreach (var task in me.Options().OfType<PlayCardTask>())
            {
                if (task.Source?.Id != playable.Id) continue;
                if (!ZephrysPool.TargetAllowed(entry, me, task.Target)) continue;
                options.Add(new Option { ChooseOne = task.ChooseOne, TargetId = task.Target?.Id ?? 0 });
            }
            return options;
        }

        private static IPlayable Prepare(Controller me, Card card, int cost, bool now)
        {
            if (me.HandZone.IsFull) me.SetasideZone.Add(me.HandZone.Remove(me.HandZone[me.HandZone.Count - 1]));
            var playable = Entity.FromCard(me, card, null, me.HandZone);
            // Pay the wish cost (base cost, own-text reductions only), whatever reducers say, so the mana left afterwards is
            // what retail's model expects; a next-turn card is played with exactly enough mana.
            int adjust = playable.Cost - cost + (now ? 0 : Math.Max(0, cost - me.RemainingMana));
            if (adjust > 0) me.TemporaryMana += adjust;
            else if (adjust < 0) me.UsedMana -= adjust;
            return playable;
        }

        private static bool Play(Game sim, Controller me, Card card, int cost, bool now, Option option)
        {
            var playable = Prepare(me, card, cost, now);
            var target = option.TargetId == 0 ? null : sim.IdEntityDic[option.TargetId];
            if (!sim.Process(PlayCardTask.Any(me, playable, target, -1, option.ChooseOne))) return false;
            for (int guard = 0; guard < 8 && me.Choice != null && sim.State != State.COMPLETE; guard++)
                if (!sim.Process(ChooseTask.Pick(me, me.Choice.Choices[0]))) return false;
            return true;
        }

        /// <summary>A copy that holds only what Zephrys may see.</summary>
        private static Game PublicCopy(Game game, Controller controller, long seed)
        {
            var copy = game.CloneDetached(seed);
            var blank = Cards.FromId(Placeholder);
            foreach (var player in new[] { copy.Player1, copy.Player2 })
            {
                int hand = player.HandZone.Count, deck = player.DeckZone.Count;
                foreach (var held in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(held));
                foreach (var drawn in player.DeckZone.ToArray()) player.SetasideZone.Add(player.DeckZone.Remove(drawn));
                for (int i = 0; i < hand; i++) Entity.FromCard(player, blank, null, player.HandZone);
                for (int i = 0; i < deck; i++) Entity.FromCard(player, blank, null, player.DeckZone);
            }
            // Enemy Secrets: Zephrys knows how many, not which (Whalen 2019-08-01). Inert stand-ins keep the count, so
            // Flare and SI:7 Infiltrator still have something to remove, and nothing can trigger.
            var op = copy.ControllerById(controller.Id).Opponent;
            var secrets = op.SecretZone.Where(s => s.IsSecret).ToArray();
            foreach (var secret in secrets) op.SetasideZone.Add(op.SecretZone.Remove(secret));
            var standIn = Cards.FromId(SecretStandIn);
            foreach (var _ in secrets) Entity.FromCard(op, standIn, null, op.SecretZone);
            copy.AuraUpdate();
            return copy;
        }

        /// <summary>Base cost; only a card's own text changes it (Sea Giant, Dread Corsair), never outside reducers.</summary>
        internal static int WishCost(Card card, Controller me)
        {
            switch (card.Id)
            {
                case "EX1_586": return Math.Max(0, card.Cost - WishView.Minions(me).Count() - WishView.Minions(me.Opponent).Count());
                case "NEW1_022": return Math.Max(0, card.Cost - (me.Hero.Weapon?.AttackDamage ?? 0));
                default: return card.Cost;
            }
        }

        private static Card[] Fallback(Controller controller)
        {
            var view = WishView.Of(controller);
            return ZephrysPool.Entries.Where(e => ZephrysPool.Eligible(e, view))
                .Select(e => Cards.FromId(e.Id)).Where(c => c.Cost <= Math.Max(controller.RemainingMana, 1))
                .Take(Offered).ToArray();
        }

        // ---- Scoring: public features only. Units are roughly "stat points". ----

        internal static double Score(Controller me)
        {
            var op = me.Opponent;
            // A Taunt only matters to the side that can be attacked.
            double mine = WishView.Minions(me).Sum(m => Value(m, Threatens(op)));
            double theirs = WishView.Minions(op).Sum(m => Value(m, Threatens(me)));
            // Doomsayer is the one text Zephrys was taught (2019-08-08 hotfix): an enemy one threatens your whole board.
            if (WishView.Minions(op).Any(m => ZephrysPool.IsDoomsayer(m) && !m.IsSilenced)) theirs += mine;
            double score = mine - 1.5 * theirs; // removing a threat beats adding a body
            score += Life(me.Hero.Health + me.Hero.Armor - Danger(me));
            score -= 0.6 * Life(op.Hero.Health + op.Hero.Armor - FaceNow(me));
            score += 1.5 * Math.Min(me.HandZone.Count, 10) - 1.0 * Math.Min(op.HandZone.Count, 10);
            score += WeaponValue(me.Hero.Weapon) - WeaponValue(op.Hero.Weapon);
            score += 4 * Math.Min(me.BaseMana, 10) - 0.7 * me.OverloadOwed;
            score -= 6 * op.SecretZone.Count(e => e.IsSecret); // an unknown Secret: about a card and its tempo
            return score;
        }

        private static double Life(int health) => health <= 0 ? -40 : 10 * Math.Log(1 + health);

        private static double WeaponValue(Weapon weapon) =>
            weapon == null ? 0 : 0.8 * weapon.AttackDamage * Math.Min(Math.Max(weapon.Durability, 0), 3);

        private static bool Threatens(Controller c) =>
            WishView.Minions(c).Any(m => m.AttackDamage > 0) || (c.Hero.Weapon?.AttackDamage ?? 0) > 0;

        internal static double Value(Minion m, bool threatened)
        {
            int atk = m.AttackDamage, hp = m.Health;
            double v = atk + 0.8 * hp;
            if (m.HasTaunt && threatened) v += 1;
            if (m.HasDivineShield) v += 0.8 * atk + 1;
            if (m.HasWindfury) v += 0.6 * atk;
            if (m.Poisonous) v += 3;
            if (m.HasLifeSteal) v += 0.5 * atk;
            if (m.HasStealth) v += 1;
            if (m[GameTag.REBORN] == 1) v += 1 + 0.5 * (m.Card.ATK + 1);
            if (m.SpellPower > 0) v += 1.5 * m.SpellPower;
            if (m.IsFrozen) v -= 0.4 * atk;
            if (m.CantAttack) v -= 0.8 * atk;
            if (m.CantBeTargetedBySpells && m.CantBeTargetedByHeroPowers) v += 1;
            // Deathrattle text is unread: worth the gap to a vanilla minion of its cost, slightly more (Nervig 2019-12-27).
            if (m.HasDeathrattle && !m.IsSilenced)
                v += Math.Max(1, 1.2 * (2 * m.Card.Cost + 1 - (m.Card.ATK + m.Card.Health)));
            return Math.Max(v, 0.5);
        }

        /// <summary>Damage the opponent can deal to your hero next turn, after your Taunts soak attacks.</summary>
        internal static int Danger(Controller me)
        {
            var op = me.Opponent;
            var hits = new List<int>();
            foreach (var m in WishView.Minions(op))
            {
                if (m.AttackDamage <= 0 || m.IsFrozen || m.CantAttack || m.CantAttackHeroes) continue;
                for (int i = 0; i < (m.HasWindfury ? 2 : 1); i++) hits.Add(m.AttackDamage);
            }
            var weapon = op.Hero.Weapon;
            if (weapon != null && weapon.AttackDamage > 0 && !op.Hero.IsFrozen)
                for (int i = 0; i < (weapon.IsWindfury ? 2 : 1); i++) hits.Add(weapon.AttackDamage);
            hits.Sort();
            foreach (var taunt in WishView.Minions(me).Where(m => m.HasTaunt && !m.HasStealth).OrderBy(m => m.Health))
            {
                int soak = taunt.Health + (taunt.HasDivineShield ? 1 : 0), dealt = 0;
                while (dealt < soak && hits.Count > 0) { dealt += hits[0]; hits.RemoveAt(0); }
            }
            return hits.Sum() + HeroPowerFace(op, nextTurn: true);
        }

        /// <summary>Face damage available this turn when no Taunt is in the way (no trades are simulated).</summary>
        internal static int FaceNow(Controller me)
        {
            var op = me.Opponent;
            if (WishView.Minions(op).Any(m => m.HasTaunt && !m.HasStealth)) return 0;
            int face = 0;
            foreach (var m in WishView.Minions(me))
            {
                if (!m.CanAttack || m.CantAttackHeroes || m.AttackableByRush && !m.HasCharge) continue;
                face += m.AttackDamage * Math.Max(0, (m.HasWindfury ? 2 : 1) - m.NumAttacksThisTurn);
            }
            if (me.Hero.CanAttack && !me.Hero.CantAttackHeroes)
                face += me.Hero.AttackDamage * Math.Max(0, (me.Hero.Weapon?.IsWindfury == true ? 2 : 1) - me.Hero.NumAttacksThisTurn);
            return face + HeroPowerFace(me, nextTurn: false);
        }

        private static int HeroPowerFace(Controller c, bool nextTurn)
        {
            var power = c.Hero.HeroPower;
            if (power == null || !ZephrysLethal.IsBasicPower(power.Card)) return 0;
            if (!nextTurn && (power.IsExhausted || c.RemainingMana < power.Cost)) return 0;
            switch (power.Card.Name)
            {
                case "Fireblast": case "Shapeshift": case "Dagger Mastery": case "Demon Claws": return 1;
                case "Steady Shot": case "Fireblast Rank 2": case "Dire Shapeshift": case "Poisoned Daggers": return 2;
                case "Ballista Shot": return 3;
                default: return 0;
            }
        }

        /// <summary>Stable seed mixing (SplitMix64 finaliser); never touches the live RNG.</summary>
        internal static long Mix(long a, long b, long c, long d)
        {
            ulong x = (ulong)a;
            foreach (long part in new[] { b, c, d })
            {
                x += 0x9E3779B97F4A7C15UL + (ulong)part;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                x ^= x >> 31;
            }
            return (long)(x & 0x7FFFFFFFFFFFFFFFUL);
        }
    }
}
