using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Auras;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Adventures.NefarianRises
{
    // Nefarian, the boss of Nefarian Rises! (https://hearthstone.wiki.gg/wiki/Nefarian_Rises!): a 0/200 Taunt minion that can't be
    // targeted by spells. Every turn he stands on the side of the player whose turn it is NOT, so the active player can attack
    // him (and his Taunt keeps the teammates from attacking each other).
    //
    // At the end of each turn he switches to the side of the player who just ended it (destroying every minion there first when
    // that board is full), then casts a spell from his schedule, cast by the player he now belongs to, so his Explosive Rune
    // lands on that side for the teammate to clear. His spells deal no spell damage: the side he stands on lends him nothing.
    //
    // Phases (health): at 160 and at 60 he can't be damaged further for the rest of that turn ("Enough!", Immune). At 160 he casts
    // Don't Push Me! and Elemental Eruption; at 60 Glorious Finale and Twisted Light, and from then on the stronger forms of his
    // spells. Attack: 0, then +5 ("Getting Angry....") and +5 ("WHY WON'T YOU DIE!?"); each step comes with its health phase, or
    // on its own when the team takes too long (AttackAdvanceTurns of his end-of-turn casts in one attack phase). Bamboozle stops
    // once he has 10 Attack or is past 60 Health.
    //
    // The retail spell schedule is not public: his choice is a seeded random pick from the phase's pool, with a turn of rest
    // possible, never the same choice twice in a row. Losing or winning is CoopRules: his death ends the game with the team's win.
    public static class NefarianBoss
    {
        public const string BossId = "TB_CoOpv3_Boss";
        private static readonly string[] BossIds = { BossId, "TB_CoOpv3_Boss_FB", "TB_CoOpv3_Boss_NewClasses" };

        public const int FirstThreshold = 160, SecondThreshold = 60, AttackStep = 5;
        // His end-of-turn casts in one attack phase before the next one starts without the team reaching the health threshold.
        public const int AttackAdvanceTurns = 12;

        // Boss state on Nefarian himself (CoopRules' private range, never sent to the client).
        internal const GameTag HealthPhase = (GameTag)34712;   // 0: above 160, 1: 160 reached, 2: 60 reached
        internal const GameTag AttackPhase = (GameTag)34713;   // 0, 1, 2: +0, +5, +10 Attack
        internal const GameTag TurnsInPhase = (GameTag)34714;  // end-of-turn casts since the attack phase began
        internal const GameTag LastChoice = (GameTag)34715;    // dbf of his last scheduled spell (-1: he rested)
        internal const GameTag Pending = (GameTag)34716;       // 1: a health threshold was just reached, the phase change follows

        public const string Glorious = "TB_CoOpv3_001", TwistedLight = "TB_CoOpv3_002", Bamboozle = "TB_CoOpv3_003",
            Cleave7 = "TB_CoOpv3_004", Cleave4 = "TB_CoOpv3_005", Eruption = "TB_CoOpv3_006", Missiles5 = "TB_CoOpv3_007",
            Missiles10 = "TB_CoOpv3_008", Rune = "TB_CoOpv3_009", Runes = "TB_CoOpv3_010", DontPush = "TB_CoOpv3_011",
            Immolate4 = "TB_CoOpv3_012", Immolate7 = "TB_CoOpv3_013", RuneMinion = "TB_Coopv3_009t";
        internal const string GettingAngry = "TB_CoOpv3_BOSS2e", WhyWontYouDie = "TB_CoOpv3_BOSSe", Enough = "TB_CoOpv3_BOSS3e";

        public static bool IsBoss(IPlayable p) => p != null && Array.IndexOf(BossIds, p.Card.Id) >= 0;

        // Nefarian in play, on either side, or null.
        public static Minion Find(Game game) =>
            // untouchable-ok: the boss is looked up by card id, not chosen by an effect.
            game.Player1.BoardZone.Concat(game.Player2.BoardZone).FirstOrDefault(m => IsBoss(m) && !m.IsDead);

        // The pool his schedule picks from in his current phase (public for the proofs).
        public static IReadOnlyList<string> Pool(int healthPhase, int attackPhase)
        {
            if (healthPhase >= 2) return new[] { Cleave7, Runes, Missiles10, Immolate7 };
            var pool = new List<string> { Cleave4, Rune, Missiles5, Immolate4 };
            if (attackPhase < 2) pool.Add(Bamboozle);
            return pool;
        }

        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            Action<Card> boss = card => {
                card.Power = new Power {
                    Aura = new Aura(AuraType.SELF, Effects.CantBeTargetedBySpellsAndHeroPowers),
                    Trigger = new MultiTrigger(
                        new Trigger(TriggerType.PREDAMAGE) {
                            TriggerSource = TriggerSource.SELF,
                            FastExecution = true,
                            SingleTask = new CustomTask((game, controller, source, target, stack) => CapDamage(game, (Minion)source))
                        },
                        new Trigger(TriggerType.TAKE_DAMAGE) {
                            TriggerSource = TriggerSource.SELF,
                            Condition = new SelfCondition(p => p[Pending] == 1),
                            SingleTask = new CustomTask((game, controller, source, target, stack) => ReachThreshold(game, (Minion)source))
                        },
                        new Trigger(TriggerType.TURN_END) {
                            EitherTurn = true,
                            SingleTask = new CustomTask((game, controller, source, target, stack) => EndOfTurn(game, (Minion)source))
                        })
                };
                // His death wins the game for the team (CoopRules.CheckBoss after each death phase). No Deathrattle: the client
                // would show its skull under him.
                CoopRules.MarkBoss(card);
                NefarianRisesCards.Plain(card);
            };
            register(40259, BossId, boss);
            register(45369, "TB_CoOpv3_Boss_FB", boss);
            register(41609, "TB_CoOpv3_Boss_NewClasses", boss);

            register(40533, GettingAngry, card => NefarianRisesCards.Enchantment(card, Effects.Attack_N(AttackStep)));
            register(40356, WhyWontYouDie, card => NefarianRisesCards.Enchantment(card, Effects.Attack_N(AttackStep)));
            register(40585, Enough, card => {
                card.Power = new Power { Enchant = new Enchant(Effects.Immune) { IsOneTurnEffect = true } };
                NefarianRisesCards.Plain(card);
            });

            // Explosive Rune: At the start of your turn, this explodes, dealing 9 damage to your hero.
            register(40499, RuneMinion, card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_START) {
                        SingleTask = ComplexTask.Create(new DamageTask(9, EntityType.HERO), new DestroyTask(EntityType.SOURCE))
                    }
                };
                CoopRules.MarkBossSide(card);
                NefarianRisesCards.Plain(card);
            });

            // His spells. Each finds Nefarian itself; the side he stands on is the casting player.
            Spell(register, 40358, Bamboozle, SwapHands);
            Spell(register, 40341, Cleave4, (game, ctx) => Cleave(game, ctx, 4));
            Spell(register, 40352, Cleave7, (game, ctx) => Cleave(game, ctx, 7));
            Spell(register, 40315, Eruption, (game, ctx) =>
                new DamageTask(4, 2, EntityType.STACK).Process(game, ctx.Caster, ctx.Spell, null,
                    new TaskStack { Playables = OtherMinions(game).Cast<IPlayable>().ToList() }));
            Spell(register, 40316, Missiles5, (game, ctx) => Missiles(game, ctx, 5));
            Spell(register, 40317, Missiles10, (game, ctx) => Missiles(game, ctx, 10));
            Spell(register, 40500, Rune, (game, ctx) => ctx.Effect.Summon(RuneMinion));
            Spell(register, 40498, Runes, (game, ctx) => {
                ctx.Effect.Summon(RuneMinion);
                new EffectContext(game, ctx.Caster.Opponent.Id, ctx.Spell.Id).Summon(RuneMinion);
            });
            Spell(register, 40545, Immolate4, (game, ctx) => ctx.Effect.DamageBatch(Heroes(game), 4));
            Spell(register, 40546, Immolate7, (game, ctx) => ctx.Effect.DamageBatch(Heroes(game), 7));
            Spell(register, 40386, TwistedLight, (game, ctx) => {
                var nefarian = Find(game);
                if (nefarian != null) ctx.Effect.Heal(nefarian.Id, 30);
                ctx.Effect.DamageBatch(OtherMinions(game).Select(m => m.Id), 30);
            });
            Spell(register, 40532, DontPush, (game, ctx) => { });
            Spell(register, 40353, Glorious, (game, ctx) => { });
        }

        private static void Spell(Action<int, string, Action<Card>> register, int dbf, string id, Action<Game, SpellContext> effect) =>
            register(dbf, id, card => {
                card.Power = new Power {
                    PowerTask = new CustomTask((game, controller, source, target, stack) =>
                        effect(game, new SpellContext(new EffectContext(game, controller.Id, source.Id, stack: stack), controller, (IPlayable)source)))
                };
                NefarianRisesCards.Plain(card);
            });

        // A boss spell's resolution: the effect helpers plus the casting player and the spell itself.
        private sealed class SpellContext
        {
            internal readonly EffectContext Effect; internal readonly Controller Caster; internal readonly IPlayable Spell;
            internal SpellContext(EffectContext effect, Controller caster, IPlayable spell) { Effect = effect; Caster = caster; Spell = spell; }
        }

        // PREDAMAGE: the hit that would take him to or past the next threshold stops at it; a hit after that, before the phase
        // change resolves, does nothing (he is about to be Immune).
        private static void CapDamage(Game game, Minion nefarian)
        {
            var data = game.CurrentEventData;
            if (data == null || data.EventNumber <= 0 || nefarian.IsImmune) return;
            if (nefarian[Pending] == 1) { data.EventNumber = 0; return; }
            int threshold = Threshold(nefarian[HealthPhase]);
            if (threshold <= 0 || nefarian.Health <= threshold || nefarian.Health - data.EventNumber > threshold) return;
            data.EventNumber = nefarian.Health - threshold;
            nefarian[Pending] = 1;
        }

        private static int Threshold(int healthPhase) => healthPhase == 0 ? FirstThreshold : healthPhase == 1 ? SecondThreshold : 0;

        // TAKE_DAMAGE after a capped hit: the phase change.
        private static void ReachThreshold(Game game, Minion nefarian)
        {
            nefarian[Pending] = 0;
            if (nefarian.IsDead) return;
            int phase = nefarian[HealthPhase] + 1;
            nefarian[HealthPhase] = phase;
            new EffectContext(game, nefarian.Controller.Id, nefarian.Id).GiveBuff(nefarian.Id, Enough);
            while (nefarian[AttackPhase] < phase) AdvanceAttack(game, nefarian);
            if (phase == 1)
            {
                Cast(game, nefarian, DontPush);
                Cast(game, nefarian, Eruption);
            }
            else
            {
                Cast(game, nefarian, Glorious);
                Cast(game, nefarian, TwistedLight);
            }
        }

        private static void AdvanceAttack(Game game, Minion nefarian)
        {
            int next = nefarian[AttackPhase] + 1;
            if (next > 2) return;
            nefarian[AttackPhase] = next;
            nefarian[TurnsInPhase] = 0;
            Say(game, next == 1 ? VoiceTruePower : VoiceDestroyYou);
            new EffectContext(game, nefarian.Controller.Id, nefarian.Id).GiveBuff(nefarian.Id, next == 1 ? GettingAngry : WhyWontYouDie);
        }

        private static bool Over(Game game) =>
            game.State == State.COMPLETE || game.Player1.PlayState != PlayState.PLAYING || game.Player2.PlayState != PlayState.PLAYING
            || CoopRules.IsBossDefeated(game);

        // TURN_END (either player's): switch sides, then cast.
        private static void EndOfTurn(Game game, Minion nefarian)
        {
            if (Over(game) || nefarian.IsDead || nefarian.Zone?.Type != Zone.PLAY) return;
            SwitchSides(game, nefarian);
            if (Over(game) || nefarian.IsDead || nefarian.Zone?.Type != Zone.PLAY) return;

            nefarian[TurnsInPhase] = nefarian[TurnsInPhase] + 1;
            if (nefarian[AttackPhase] < 2 && nefarian[TurnsInPhase] >= AttackAdvanceTurns)
            {
                AdvanceAttack(game, nefarian);
                Cast(game, nefarian, DontPush);
                if (Over(game) || nefarian.IsDead) return;
            }

            string choice = Choose(game, nefarian);
            if (choice != null) Cast(game, nefarian, choice);
        }

        private static void SwitchSides(Game game, Minion nefarian)
        {
            Controller to = game.CurrentPlayer;
            if (nefarian.Controller == to) return;
            if (to.BoardZone.IsFull)
            {
                // A full side: he destroys every minion on it, then takes his place there.
                foreach (var minion in to.BoardZone.Interactable.ToList()) minion.Destroy();
                game.DeathProcessingAndAuraUpdate();
                if (Over(game) || nefarian.IsDead || to.BoardZone.IsFull) return;
            }
            new ControlTask(EntityType.SOURCE, opposite: true).Process(game, nefarian.Controller, nefarian, null);
        }

        // The scheduled spell, or null for a turn of rest. Never the same choice twice in a row.
        private static string Choose(Game game, Minion nefarian)
        {
            var options = Pool(nefarian[HealthPhase], nefarian[AttackPhase]).Cast<string>().ToList();
            options.Add(null); // rest
            int last = nefarian[LastChoice];
            options.RemoveAll(id => (id == null ? -1 : Cards.FromId(id).AssetId) == last && last != 0);
            string pick = options[DungeonRunCards.Pick(game, options.Count)];
            nefarian[LastChoice] = pick == null ? -1 : Cards.FromId(pick).AssetId;
            return pick;
        }

        // His voice: MISSION_EVENT numbers the client's brawl script (TB11_CoOpv3) answers with a line of his (the lines stay in
        // the client). The script names no moment for them; each pairing follows what the line is about.
        internal const int VoiceManipulated = 2;     // minds manipulated: Bamboozle
        internal const int VoicePriest = 6;          // the Priest's light: Twisted Light
        internal const int VoiceShaman = 7;          // the Shaman's elements: Elemental Eruption
        internal const int VoiceDestroyYou = 97;     // a threat: his second Attack gain
        internal const int VoiceTruePower = 98;      // his true power: his first Attack gain
        internal const int VoiceFinale = 99;         // the finale: Glorious Finale

        private static int VoiceFor(string spell) =>
            spell == Bamboozle ? VoiceManipulated : spell == Eruption ? VoiceShaman : spell == Glorious ? VoiceFinale
            : spell == TwistedLight ? VoicePriest : 0;

        // The client acts on a change of the tag, so it goes back to 0 at once (the script ignores 0): the same line can come
        // again (two Bamboozles), and a hot-seat swap, which sends the game entity again with all its tags, carries no line.
        private static void Say(Game game, int missionEvent)
        {
            if (missionEvent == 0 || Over(game)) return;
            game[GameTag.MISSION_EVENT] = missionEvent;
            game[GameTag.MISSION_EVENT] = 0;
        }

        // Cast by the player whose side he stands on, created by him (the client shows the spell coming from Nefarian).
        private static void Cast(Game game, Minion nefarian, string id)
        {
            if (Over(game)) return;
            Say(game, VoiceFor(id));
            Controller caster = nefarian.Controller;
            var spell = (Spell)Entity.FromCard(caster, Cards.FromId(id), new Dictionary<GameTag, int> {
                { GameTag.CREATOR, nefarian.Id }, { GameTag.DISPLAYED_CREATOR, nefarian.Id } });
            game.TaskQueue.StartEvent();
            new EffectContext(game, caster.Id, nefarian.Id).CastSpell(spell.Id, HistoryCastKind.Effect);
            game.DeathProcessingAndAuraUpdate();
            game.TaskQueue.EndEvent();
        }

        // Every minion in play except Nefarian (both sides).
        private static List<Minion> OtherMinions(Game game) =>
            game.Player1.BoardZone.Interactable.Concat(game.Player2.BoardZone.Interactable).Where(m => !IsBoss(m)).ToList();

        private static IEnumerable<int> Heroes(Game game) => new[] { game.Player1.Hero.Id, game.Player2.Hero.Id };

        // Bamboozle: the players swap hands (cards keep their costs and their order, as a hand-to-hand theft does). Cards move one
        // at a time, always one whose new hand has room, so neither hand ever goes past its limit; two full hands trade one card
        // while another crosses (live 2026-11-22: a one-way pass kept 3 of 6 cards when the other hand reached 10).
        private static void SwapHands(Game game, SpellContext ctx)
        {
            var toSecond = new Queue<IPlayable>(game.Player1.HandZone.ToList());
            var toFirst = new Queue<IPlayable>(game.Player2.HandZone.ToList());
            while (toSecond.Count > 0 || toFirst.Count > 0)
            {
                if (toFirst.Count > 0 && !game.Player1.HandZone.IsFull) Move(game, toFirst.Dequeue(), game.Player1, ctx.Spell);
                else if (toSecond.Count > 0 && !game.Player2.HandZone.IsFull) Move(game, toSecond.Dequeue(), game.Player2, ctx.Spell);
                else if (toSecond.Count > 0 && toFirst.Count > 0)
                    // Both hands full: one card leaves Player 1's hand, one of Player 2's takes its place, then the first lands.
                    Move(game, toSecond.Dequeue(), game.Player2, ctx.Spell, inTransit: () => Move(game, toFirst.Dequeue(), game.Player1, ctx.Spell));
                else break;
            }
        }

        private static void Move(Game game, IPlayable card, Controller to, IPlayable source, Action inTransit = null)
        {
            if (card.Zone?.Type != Zone.HAND || card.Controller == to) return;
            Controller from = card.Controller;
            using var historyTransfer = game.Facts.CardTransfer(card, from.Id, Zone.HAND, source);
            using var temporaryTransfer = TemporaryRules.PreserveHandTransfer(card);
            if (card.OngoingEffect is AdaptiveCostEffect costEffect) costEffect.Remove();
            from.HandZone.RemoveForTrade(card);
            inTransit?.Invoke();
            card.Controller = to;
            card[GameTag.CONTROLLER] = to.PlayerId;
            Generic.AddHandPhase(to, card);
        }

        // Cleave: N damage to a random minion (not Nefarian) and to its owner's hero.
        private static void Cleave(Game game, SpellContext ctx, int amount)
        {
            var minions = OtherMinions(game).Where(m => !m.IsDead).ToList();
            if (minions.Count == 0) return;
            var victim = minions[DungeonRunCards.Pick(game, minions.Count)];
            ctx.Effect.DamageBatch(new[] { victim.Id, victim.Controller.Hero.Id }, amount);
        }

        // Flame Missiles: N damage randomly split among all other characters (Explosive Runes are never hit). Like the engine's
        // other split damage (RandomTask.Harmful), a missile skips a character already dead (a hero at 0, a minion at 0).
        private static void Missiles(Game game, SpellContext ctx, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (Over(game)) return;
                var targets = new List<ICharacter> { game.Player1.Hero, game.Player2.Hero };
                targets.AddRange(OtherMinions(game).Where(m => m.Card.Id != RuneMinion));
                targets.RemoveAll(c => c.IsDead);
                if (targets.Count == 0) return;
                var hit = targets[DungeonRunCards.Pick(game, targets.Count)];
                ctx.Effect.Damage(hit.Id, 1);
            }
        }
    }
}
