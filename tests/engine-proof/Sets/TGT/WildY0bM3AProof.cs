using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>Wave Y0b lane m3a: Grand Tournament AT_001..AT_022 (Mage, Hunter, Priest, Warlock, Neutral).
/// Registered (literal WildSetsProof.Case, listed in wild-y0b-m3a.txt): AT_002, AT_008, AT_009, AT_010, AT_014, AT_016, AT_019, AT_022.
/// BLOCKED-STANDARD, written but never registered: AT_004, AT_005, AT_017, AT_020 (data/pools/pool-eligibility-v1.json),
/// (AT_009 and AT_019 were held for plugin/Npc/DeathValueData.cs; ValueData does not hold an id, so they are registered.)
/// BLOCKED-HSDATA, written but never registered: AT_001, AT_003, AT_006, AT_007, AT_011, AT_012, AT_013, AT_015, AT_018 and
/// AT_021 changed their English text in 37.0, so their identity rows need the pinned hsdata locale source
/// (tools/gen_card_facts.py --hsdata). Once that runs: list the ids in the manifest, rerun the generator and change
/// Pending( to WildSetsProof.Case( for each. Their scenarios assume the 37.0 identity (stats, tags) that the rows give.
/// Run both groups with WILD_Y0B_M3A_BLOCKED=1.
/// Fixed script lines held back with the BLOCKED-HSDATA cards (TgtCardsGen.cs stays at HEAD for them so text and behaviour agree).
/// Apply them together with the identity rows; the Pending scenarios below expect this behaviour:
///   AT_001  DamageTask(25, EntityType.TARGET, true)                          (was 8)
///   AT_007  after the first AddStackTo(HAND): new AddAuraEffect(Effects.ReduceCost(2), EntityType.STACK)
///   AT_011  Trigger = new Trigger(TriggerType.HEAL_INTENT) { TriggerSource = TriggerSource.SELF,
///             Condition = new SelfCondition(p => p is ICharacter c && p.Game.CurrentEventData != null
///               && p.Game.CurrentEventData.EventNumber > c.Damage), SingleTask = new AddEnchantmentTask("AT_011e", EntityType.SOURCE) }
///   AT_012  PowerTask = ComplexTask.Create(new DamageTask(4, EntityType.OP_HERO), new DamageTask(4, EntityType.HERO))  (Battlecry, as the Inspire)
///   AT_015  PowerTask = ComplexTask.Create(new CopyTask(EntityType.TARGET, Zone.HAND, addToStack: true),
///             new AddAuraEffect(Effects.SetCost(1), EntityType.STACK))
///   AT_018  PowerTask = ComplexTask.Create(new RandomCardTask(CardType.MINION, CardClass.INVALID, rarity: Rarity.LEGENDARY), new SummonTask())
///   AT_021  AT_021e Enchant = new OngoingEnchant(Effects.Attack_N(2), Effects.Health_N(1)); trigger adds .SetSource(TriggerSource.FRIENDLY)
/// </summary>
internal static class WildY0bM3AProof
{
    // Set WILD_Y0B_M3A_BLOCKED=1 to run the proofs that are written but not registered (see the class summary).
    private static bool BlockedRun => Environment.GetEnvironmentVariable("WILD_Y0B_M3A_BLOCKED") == "1";
    private const string Yeti = "CS2_182", Wisp = "CS2_231", Thalnos = "EX1_012", Squire = "EX1_008";

    [GroupProof("wild-y0b-m3a-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        InspireMechanic();
        if (BlockedRun) Blocked();

        WildSetsProof.Case("AT_009", (g, p, id) => {
            var rhonin = Summon(p, id);
            int hand = p.HandZone.Count;
            Kill(g, rhonin);
            Core2026Proof.Check(p.HandZone.Count == hand + 3 && p.HandZone.TakeLast(3).All(c => c.Card.Id == "EX1_277"), "three Arcane Missiles");
            for (int i = p.HandZone.Count; i < 8; i++) Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone);
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.HandZone.Count == 10, "hand cap 10");
            var silenced = Summon(p, id); silenced.Silence(); int h = p.HandZone.Count; Kill(g, silenced);
            Core2026Proof.Check(p.HandZone.Count == h, "Silence removes Deathrattle");
            End(g);
            int oh = p.Opponent.HandZone.Count; Kill(g, Summon(p.Opponent, id));
            Core2026Proof.Check(p.Opponent.HandZone.Count == oh + 3 && p.HandZone.Count == h, "enemy Rhonin feeds the enemy");
        });
        WildSetsProof.Case("AT_019", (g, p, id) => {
            var steed = Summon(p, id); Kill(g, steed);
            Core2026Proof.Check(p.BoardZone.Count == 0, "no instant replacement");
            End(g);
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == id, "a new Dreadsteed at the end of the turn (owner's turn)");
            Core2026Proof.Check(g.CurrentPlayer == p.Opponent, "now the opponent's turn");
            Kill(g, p.BoardZone.Single());
            Core2026Proof.Check(p.BoardZone.Count == 0, "died on the opponent's turn");
            End(g);
            Core2026Proof.Check(p.BoardZone.Count == 1, "'At the end of the turn' also covers the opponent's turn end");
        });
        Pending("AT_001", (g, p, id) => {
            var big = Summon(p.Opponent, Yeti); big.BaseHealth = 60;
            var hand = Add(p, id);
            Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == hand.Id).All(t => t.Target is Minion) && p.Options().Any(t => t.Source?.Id == hand.Id),
                "Flame Lance targets minions only");
            PlayEntity(g, p, hand, big);
            Core2026Proof.Check(big.Damage == 25 && big.Zone.Type == Zone.PLAY, "Flame Lance deals 25 to a minion");
            Summon(p, Thalnos); Play(g, p, id, big);
            Core2026Proof.Check(big.Damage == 51, "Flame Lance scales with Spell Damage (25+1)");
            var squire = Summon(p.Opponent, Squire);
            Core2026Proof.Check(squire.HasDivineShield, "Argent Squire has Divine Shield");
            Play(g, p, id, squire);
            Core2026Proof.Check(!squire.HasDivineShield && squire.Damage == 0 && squire.Zone.Type == Zone.PLAY, "Flame Lance is absorbed by Divine Shield");
            var friend = Summon(p, Yeti); Play(g, p, id, friend);
            Core2026Proof.Check(friend.Zone.Type == Zone.GRAVEYARD, "Flame Lance kills a friendly minion at exact lethal (overkill)");
            Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Flame Lance never touches heroes");
        });

        WildSetsProof.Case("AT_002", (g, p, id) => {
            var own = Summon(p, Yeti);
            var secret = Add(p, id); PlayEntity(g, p, secret);
            Core2026Proof.Check(p.SecretZone.Count == 1, "Effigy is played as a Secret");
            Kill(g, own);
            Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 0, "Effigy does not trigger on the owner's own turn");
            End(g);
            var enemyMinion = Summon(p.Opponent, Yeti); Kill(g, enemyMinion);
            Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0, "Effigy ignores enemy minion deaths");
            var dying = Summon(p, Yeti);
            Resume(g, next => {
                var d = (Minion)next.IdEntityDic[dying.Id];
                Kill(next, d);
                var born = next.Player1.BoardZone.Single();
                Core2026Proof.Check(born.Card.Cost == 4 && born.Card.Type == CardType.MINION && born.Card.Collectible && next.Player1.SecretZone.Count == 0,
                    "Effigy summons a collectible minion of the dead minion's Cost and is consumed (clone)");
            });
            Kill(g, dying);
            var summoned = p.BoardZone.Single();
            Core2026Proof.Check(summoned.Card.Cost == 4 && summoned.Card.Type == CardType.MINION && summoned.Card.Collectible && p.SecretZone.Count == 0
                && summoned.Controller == p, "Effigy summons a minion of the same Cost for the owner and is consumed");
            End(g);
            var seen = new HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                StandardSetsProof.ParkBoard(p);
                var again = Add(p, id); PlayEntity(g, p, again); End(g);
                var wisp = Summon(p, Wisp); Kill(g, wisp);
                var token = p.BoardZone.Single();
                Core2026Proof.Check(token.Card.Cost == 0 && token.Card.Type == CardType.MINION, "Effigy follows a Cost 0 minion to another Cost 0 minion");
                seen.Add(token.Card.Id); End(g);
            }
            Core2026Proof.Check(seen.Count >= 2, "Effigy picks randomly among several candidates");
            StandardSetsProof.ParkBoard(p);
            var huge = Summon(p, Yeti); huge[GameTag.COST] = 99;
            var unlucky = Add(p, id); PlayEntity(g, p, unlucky); End(g);
            Kill(g, huge);
            Core2026Proof.Check(p.BoardZone.Count == 0 && p.SecretZone.Count == 0, "no minion has that Cost: Effigy summons nothing and is spent: board " + string.Join(",", p.BoardZone.Select(m => m.Card.Id + "/" + m.Card.Cost)) + " secrets " + p.SecretZone.Count);
            End(g);
            for (int i = 0; i < 7; i++) Summon(p, Yeti);
            var full = Add(p, id); PlayEntity(g, p, full); End(g);
            Kill(g, p.BoardZone.First());
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.SecretZone.Count == 0, "Effigy fills the freed slot on a full board");
            g.DeathProcessingAndAuraUpdate();
        });

        Pending("AT_003", (g, p, id) => {
            var hero = Summon(p.Opponent, id);
            End(g);
            var mage = p.Opponent; var target = Summon(p, Yeti);
            Use(g, mage, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 2, "Fallen Hero adds 1 to Fireblast (1+1)");
            End(g); End(g);
            Use(g, mage, target);
            Core2026Proof.Check(target.Damage == 2, "Fallen Hero bonus also applies to a minion target");
            var second = Summon(mage, id); End(g); End(g);
            Use(g, mage, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 5, "two Fallen Heroes stack (1+2)");
            End(g); End(g);
            hero.Silence(); g.DeathProcessingAndAuraUpdate();
            Use(g, mage, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 7, "Silence removes one bonus (1+1)");
            End(g); End(g);
            Kill(g, second);
            Use(g, mage, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 8, "death removes the bonus (1)");
            End(g); End(g);
            var foe = Summon(p, id); // the opponent's hero power is not boosted by the other side's Fallen Hero
            Use(g, mage, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 9, "an enemy Fallen Hero does not boost the mage's power");
        });

        Pending("AT_006", (g, p, id) => {
            var asp = (Minion)Play(g, p, id);
            Core2026Proof.Check(p.CurrentSpellPower == 1, "Aspirant has printed Spell Damage +1");
            Use(g, p);
            Core2026Proof.Check(p.CurrentSpellPower == 2, "Inspire grants Spell Damage +1 on hero power");
            Core2026Proof.Check(!CanUse(p), "hero power exhausted: no second Inspire this turn");
            End(g);
            Use(g, p.Opponent, p.Hero);
            Core2026Proof.Check(p.CurrentSpellPower == 2, "opponent hero power does not Inspire");
            End(g);
            Resume(g, next => { Use(next, next.Player1); Core2026Proof.Check(next.Player1.CurrentSpellPower == 3, "Inspire stacks next turn (clone)"); });
            Use(g, p);
            Core2026Proof.Check(p.CurrentSpellPower == 3, "Inspire stacks again next turn");
            var target = Summon(p.Opponent, Yeti); target.BaseHealth = 60;
            Play(g, p, "CS2_029", target);
            Core2026Proof.Check(target.Damage == 9, "Fireball scales with gained Spell Damage (6+3)");
            asp.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.CurrentSpellPower == 0, "Silence removes printed and gained Spell Damage");
            End(g); End(g); Use(g, p);
            Core2026Proof.Check(p.CurrentSpellPower == 0, "Silenced Aspirant no longer Inspires");
            var second = (Minion)Play(g, p, id); End(g); End(g);
            Use(g, p); Core2026Proof.Check(p.CurrentSpellPower == 2, "fresh Aspirant: printed +1 and one Inspire");
            Kill(g, second); Core2026Proof.Check(p.CurrentSpellPower == 0, "death removes Spell Damage");
        });

        Pending("AT_007", (g, p, id) => {
            var seen = new HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                ParkBoardHand(p); ParkBoardHand(p.Opponent); StandardSetsProof.ParkBoard(p);
                var before = p.HandZone.ToArray(); var oppBefore = p.Opponent.HandZone.ToArray();
                Play(g, p, id);
                var mine = p.HandZone.Except(before).ToArray(); var theirs = p.Opponent.HandZone.Except(oppBefore).ToArray();
                Core2026Proof.Check(mine.Length == 1 && theirs.Length == 1, "Spellslinger gives each player exactly one card");
                var mineCard = mine[0]; var theirCard = theirs[0];
                Core2026Proof.Check(mineCard.Card.Type == CardType.SPELL && theirCard.Card.Type == CardType.SPELL
                    && mineCard.Card.Collectible && theirCard.Card.Collectible, "both cards are collectible spells");
                Core2026Proof.Check(mineCard.Cost == Math.Max(0, mineCard.Card.Cost - 2), "your spell costs (2) less: " + mineCard.Card.Id);
                Core2026Proof.Check(theirCard.Cost == theirCard.Card.Cost, "the opponent's spell keeps its Cost: " + theirCard.Card.Id);
                seen.Add(mineCard.Card.Id); seen.Add(theirCard.Card.Id);
                if (i == 0)
                {
                    HiddenFrom(g, p, new[] { mineCard }, "the opponent's fair view of the owner's spell");
                    HiddenFrom(g, p.Opponent, new[] { theirCard }, "the owner's fair view of the opponent's spell");
                    Resume(g, next => Core2026Proof.Check(next.IdEntityDic[mineCard.Id].Cost == Math.Max(0, mineCard.Card.Cost - 2), "the cost reduction survives clone and snapshot"));
                }
                End(g); Core2026Proof.Check(mineCard.Cost == Math.Max(0, mineCard.Card.Cost - 2), "the reduction persists past turn end"); End(g);
            }
            Core2026Proof.Check(seen.Count >= 4, "Spellslinger spells are random");
            ParkBoardHand(p); ParkBoardHand(p.Opponent);
            while (p.Opponent.HandZone.Count < 10) Entity.FromCard(p.Opponent, Cards.FromId(Wisp), zone: p.Opponent.HandZone);
            int own = p.HandZone.Count;
            Play(g, p, id);
            Core2026Proof.Check(p.Opponent.HandZone.Count == 10 && p.Opponent.HandZone.All(c => c.Card.Id == Wisp), "a full opponent hand receives nothing");
            Core2026Proof.Check(p.HandZone.Count == own + 1 && p.HandZone.Last().Card.Type == CardType.SPELL, "the owner still gets a spell");
        });

        WildSetsProof.Held("AT_008", (g, p, id) => {
            Play(g, p, id);
            Use(g, p); Core2026Proof.Check(CanUse(p), "hero power is usable again");
            Use(g, p); Use(g, p);
            Core2026Proof.Check(p.HeroPowerActivationsThisTurn == 3, "Coldarra Drake allows any number of uses");
            p.BaseMana = 10; p.UsedMana = 9;
            Core2026Proof.Check(!CanUse(p), "mana still limits the hero power");
            End(g); End(g);
            Resume(g, next => { Use(next, next.Player1); Use(next, next.Player1); Core2026Proof.Check(next.Player1.HeroPowerActivationsThisTurn == 2, "still repeatable on a clone"); });
            var drake = p.BoardZone.Single();
            drake.Silence(); g.DeathProcessingAndAuraUpdate();
            Use(g, p);
            Core2026Proof.Check(!CanUse(p), "Silence ends the repeatable hero power");
            End(g); End(g);
            var late = Add(p, id);
            Use(g, p);
            Core2026Proof.Check(!CanUse(p), "hero power exhausted before the Drake arrives");
            PlayEntity(g, p, late);
            Core2026Proof.Check(CanUse(p), "playing Coldarra Drake after using the hero power frees it again");
            Use(g, p); Core2026Proof.Check(p.HeroPowerActivationsThisTurn == 2, "and it can be used again");
            Kill(g, (Minion)late);
            Use(g, p);
            Core2026Proof.Check(!CanUse(p), "the effect ends when the Drake dies");
            End(g);
            Use(g, p.Opponent, p.Hero);
            Core2026Proof.Check(!CanUse(p.Opponent), "the opponent's hero power is not repeatable");
        });

        WildSetsProof.Case("AT_010", (g, p, id) => {
            int enemyBoard = p.Opponent.BoardZone.Count;
            var plain = (Minion)Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == enemyBoard, "no Beast: Ram Wrangler summons nothing");
            Summon(p.Opponent, "CS2_172");
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 2, "an enemy Beast does not count");
            Add(p, "CS2_172");
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 3, "a Beast in hand does not count");
            var seen = new HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                StandardSetsProof.ParkBoard(p);
                var beast = Summon(p, "CS2_172");
                var wrangler = (Minion)Play(g, p, id);
                Core2026Proof.Check(p.BoardZone.Count == 3, "Beast: Ram Wrangler summons one more minion");
                var summoned = p.BoardZone.Single(m => m.Id != beast.Id && m.Id != wrangler.Id);
                Core2026Proof.Check(summoned.IsRace(Race.BEAST) && summoned.Card.Collectible, "the summoned minion is a collectible Beast: " + summoned.Card.Id);
                seen.Add(summoned.Card.Id);
            }
            Core2026Proof.Check(seen.Count >= 3, "the Beast is random");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, "CS2_172");
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 7, "full board: nothing extra is summoned");
            StandardSetsProof.ParkBoard(p);
            var silenced = Summon(p, "CS2_172"); silenced.Silence();
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 3, "a Silenced Beast is still a Beast");
        });

        Pending("AT_011", (g, p, id) => {
            var champ = (Minion)Play(g, p, id);
            Core2026Proof.Check(champ.AttackDamage == 1 && champ.Health == 4, "Holy Champion is 1/4");
            Play(g, p, "CS2_007", champ);
            Core2026Proof.Check(champ.AttackDamage == 3, "healing an undamaged Champion overheals: +2 Attack");
            champ.Damage = 3;
            HealingRules.Heal(Add(p, "CS2_007"), new ICharacter[] { champ }, 3);
            Core2026Proof.Check(champ.Damage == 0 && champ.AttackDamage == 3, "healing exactly the missing Health is not an Overheal");
            champ.Damage = 1;
            Play(g, p, "AT_055", champ);
            Core2026Proof.Check(champ.Damage == 0 && champ.AttackDamage == 5, "healing more than the missing Health overheals once");
            var other = Summon(p, Yeti); other.Damage = 1;
            Play(g, p, "CS2_007", other);
            Core2026Proof.Check(champ.AttackDamage == 5, "healing another character is not an Overheal of the Champion");
            Play(g, p, "CS2_007", p.Hero);
            Core2026Proof.Check(champ.AttackDamage == 5, "healing a hero does not Overheal the Champion");
            Resume(g, next => {
                var c = (Minion)next.IdEntityDic[champ.Id];
                Play(next, next.Player1, "CS2_007", c);
                Core2026Proof.Check(c.AttackDamage == 7, "Overheal continues on a clone");
            });
            End(g);
            Core2026Proof.Check(champ.AttackDamage == 5, "Overheal buff persists past turn end");
            End(g);
            var second = (Minion)Play(g, p, id);
            Play(g, p, "CS2_007", second);
            Core2026Proof.Check(second.AttackDamage == 3 && champ.AttackDamage == 5, "only the healed Champion triggers");
            champ.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(champ.AttackDamage == 1, "Silence removes the gained Attack");
            Play(g, p, "CS2_007", champ);
            Core2026Proof.Check(champ.AttackDamage == 1, "a Silenced Champion no longer overheals");
            End(g);
            var theirs = Add(p.Opponent, "CS2_007");
            PlayEntity(g, p.Opponent, theirs, second);
            Core2026Proof.Check(second.AttackDamage == 5, "an enemy heal also overheals the Champion");
            Kill(g, second);
            Core2026Proof.Check(second.Zone.Type == Zone.GRAVEYARD, "Champion dies normally");
        });

        Pending("AT_012", (g, p, id) => {
            p.Opponent.Hero.Armor = 3;
            var spawn = (Minion)Play(g, p, id);
            Core2026Proof.Check(p.Hero.Damage == 4 && p.Opponent.Hero.Damage == 1 && p.Opponent.Hero.Armor == 0, "Battlecry deals 4 to each hero (armor absorbs)");
            Use(g, p);
            Core2026Proof.Check(p.Hero.Damage == 8 && p.Opponent.Hero.Damage == 5, "Inspire deals 4 to each hero");
            Core2026Proof.Check(!CanUse(p), "one Inspire per hero power use");
            End(g);
            Use(g, p.Opponent, p.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "the opponent's hero power does not Inspire");
            End(g);
            int mineBefore = p.Hero.Damage, theirsBefore = p.Opponent.Hero.Damage;
            Resume(g, next => {
                Use(next, next.Player1);
                Core2026Proof.Check(next.Player1.Hero.Damage == mineBefore + 4 && next.Player2.Hero.Damage == theirsBefore + 4, "Inspire on a clone");
                next.Player2.Hero.Damage = 28; next.Player1.Hero.Damage = 0;
                Play(next, next.Player1, id);
                Core2026Proof.Check(next.State == State.COMPLETE && next.Player2.PlayState == PlayState.LOST && next.Player1.PlayState != PlayState.LOST,
                    "lethal Battlecry damage ends the game for the enemy hero only");
            });
            var second = Summon(p, id);
            Core2026Proof.Check(p.Hero.Damage == mineBefore && p.Opponent.Hero.Damage == theirsBefore, "a summoned (not played) Spawn has no Battlecry");
            int hurt = mineBefore, enemyHurt = theirsBefore;
            spawn.Silence(); g.DeathProcessingAndAuraUpdate();
            Use(g, p);
            Core2026Proof.Check(p.Hero.Damage == hurt + 4 && p.Opponent.Hero.Damage == enemyHurt + 4, "Silence removes only that Spawn's Inspire");
            Kill(g, second); End(g); End(g);
            Use(g, p);
            Core2026Proof.Check(p.Hero.Damage == hurt + 4, "no Inspire once both are gone");
        });

        Pending("AT_013", (g, p, id) => {
            p.Hero.Damage = 20;
            var held = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == held.Id), "no legal play without a minion");
            var friend = Summon(p, Yeti); friend.BaseHealth = 60;
            var enemy = Summon(p.Opponent, Yeti); enemy.BaseHealth = 60;
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Core2026Proof.Check(targets.Length == 2 && targets.All(t => t.Target is Minion), "Glory targets any minion and no hero");
            PlayEntity(g, p, held, friend);
            Core2026Proof.Check(p.Hero.Damage == 20, "casting Glory heals nothing");
            Attack(g, friend, enemy);
            Core2026Proof.Check(p.Hero.Damage == 16, "the buffed minion's attack restores 4 to your hero");
            Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "the enemy hero is not healed");
            Resume(g, next => {
                End(next); End(next);
                Attack(next, (Minion)next.IdEntityDic[friend.Id], next.Player2.Hero);
                Core2026Proof.Check(next.Player1.Hero.Damage == 12, "the enchantment survives a clone and works on face attacks");
            });
            End(g); End(g);
            Play(g, p, id, friend);
            Attack(g, friend, enemy);
            Core2026Proof.Check(p.Hero.Damage == 8, "two Glory enchantments restore 4 each");
            End(g); End(g);
            Play(g, p, id, enemy);
            p.Opponent.Hero.Damage = 10; End(g);
            var bystander = Summon(p, Yeti);
            Attack(g, enemy, bystander);
            Core2026Proof.Check(p.Hero.Damage == 4 && p.Opponent.Hero.Damage == 10, "Glory on an enemy minion restores the caster's hero, not its own");
            End(g);
            var fresh = Summon(p, Yeti); fresh.BaseHealth = 60;
            Play(g, p, id, fresh);
            fresh.Silence(); g.DeathProcessingAndAuraUpdate();
            int before = p.Hero.Damage;
            Attack(g, fresh, enemy);
            Core2026Proof.Check(p.Hero.Damage == before, "Silence removes Glory");
            End(g);
            p.Hero.Damage = 0;
            Attack(g, enemy, bystander);
            Core2026Proof.Check(p.Hero.Damage == 0, "healing at full Health restores nothing");
        });

        WildSetsProof.Case("AT_014", (g, p, id) => {
            var fiend = Summon(p, id);
            var drawn = Generic.Draw(p);
            Core2026Proof.Check(drawn.Cost == drawn.Card.Cost - 1, "a drawn card costs (1) less");
            var made = Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone);
            Core2026Proof.Check(made.Cost == made.Card.Cost, "a created card is not drawn");
            var theirs = Generic.Draw(p.Opponent);
            Core2026Proof.Check(theirs.Cost == theirs.Card.Cost, "the opponent's draw is not reduced");
            var second = Summon(p, id);
            var twice = Generic.Draw(p);
            Core2026Proof.Check(twice.Cost == twice.Card.Cost - 2, "two Shadowfiends stack");
            var wisp = Entity.FromCard(p, Cards.FromId(Wisp), zone: p.DeckZone);
            var free = Generic.Draw(p, wisp);
            Core2026Proof.Check(free.Cost == 0, "Cost never drops below 0");
            Kill(g, second);
            Resume(g, next => {
                var d = Generic.Draw(next.Player1);
                Core2026Proof.Check(d.Cost == d.Card.Cost - 1, "reduction continues on a clone");
            });
            End(g); End(g);
            Core2026Proof.Check(p.HandZone.Last().Cost == p.HandZone.Last().Card.Cost - 1, "the start-of-turn draw is reduced too");
            fiend.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(drawn.Cost == drawn.Card.Cost - 1, "Silence leaves cards already drawn reduced");
            var after = Generic.Draw(p);
            Core2026Proof.Check(after.Cost == after.Card.Cost, "a Silenced Shadowfiend no longer reduces");
        });

        Pending("AT_015", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var friend = Summon(p, Yeti);
            var held = Add(p, id);
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Core2026Proof.Check(targets.Length == 1 && targets[0].Target.Id == enemy.Id, "Convert targets enemy minions only");
            int before = p.HandZone.Count;
            PlayEntity(g, p, held, enemy);
            var copy = p.HandZone.Last();
            Core2026Proof.Check(p.HandZone.Count == before && copy.Card.Id == Yeti && copy.Id != enemy.Id && copy.Cost == 1, "Convert puts a copy in your hand costing (1)");
            Core2026Proof.Check(enemy.Zone.Type == Zone.PLAY && p.Opponent.BoardZone.Count == 1, "the original stays on the enemy board");
            var cheap = Summon(p.Opponent, Wisp);
            Play(g, p, id, cheap);
            Core2026Proof.Check(p.HandZone.Last().Card.Id == Wisp && p.HandZone.Last().Cost == 1, "a Cost 0 minion's copy costs (1), not 0");
            var giant = Summon(p.Opponent, "EX1_572");
            Play(g, p, id, giant);
            Core2026Proof.Check(p.HandZone.Last().Card.Id == "EX1_572" && p.HandZone.Last().Cost == 1, "an expensive minion's copy costs (1)");
            Resume(g, next => {
                var c = next.Player1.HandZone.Last();
                Core2026Proof.Check(c.Cost == 1, "the Cost survives a clone");
            });
            End(g); End(g);
            Core2026Proof.Check(p.HandZone.Last().Cost == 1 || p.HandZone.Any(c => c.Card.Id == "EX1_572" && c.Cost == 1), "the Cost persists across turns");
            PlayEntity(g, p, p.HandZone.First(c => c.Card.Id == Yeti && c.Cost == 1));
            Core2026Proof.Check(p.UsedMana >= 0, "the discounted copy is playable");
            var lowMana = Add(p, id); StandardSetsProof.ParkBoard(p.Opponent);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == lowMana.Id), "Convert is unplayable without an enemy minion");
        });

        WildSetsProof.Case("AT_016", (g, p, id) => {
            var a = Summon(p, Yeti); var b = Summon(p.Opponent, Yeti); b.Damage = 2;
            var mirror = Summon(p, "CS2_mirror");
            Play(g, p, id);
            Core2026Proof.Check(a.AttackDamage == 5 && a.Health == 4 && a.Damage == 0, "Confuse swaps Attack and Health of a friendly minion");
            Core2026Proof.Check(mirror.Zone.Type == Zone.GRAVEYARD, "a 0-Attack minion dies (Health 0)");
            Core2026Proof.Check(b.Health == 4 && b.AttackDamage == 3 && b.Damage == 0, "enemy damaged minion: Attack = current Health, Health = Attack, damage cleared");
            Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "heroes are not minions");
            Play(g, p, id);
            Core2026Proof.Check(a.AttackDamage == 4 && a.Health == 5, "a second Confuse swaps back");
            a.Silence(); g.DeathProcessingAndAuraUpdate();
            Play(g, p, id);
            Core2026Proof.Check(a.AttackDamage == 5 && a.Health == 4, "swap again before Silence");
            a.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(a.AttackDamage == 4 && a.Health == 5, "Silence restores the printed stats");
            var buffed = Summon(p, Yeti); Play(g, p, "CS2_004", buffed); Play(g, p, "CS2_188", buffed);
            Core2026Proof.Check(buffed.AttackDamage == 6 && buffed.Health == 7, "Power Word: Shield (+2 Health) and Abusive Sergeant (+2 Attack) layer up");
            Play(g, p, id);
            Core2026Proof.Check(buffed.AttackDamage == 7 && buffed.Health == 6, "Confuse swaps the buffed stats: " + buffed.AttackDamage + "/" + buffed.Health);
            Play(g, p, "CS2_188", buffed);
            Core2026Proof.Check(buffed.AttackDamage == 9 && buffed.Health == 6, "a later buff applies on top of the swap");
            Resume(g, next => {
                End(next);
                var n = (Minion)next.IdEntityDic[buffed.Id];
                Core2026Proof.Check(n.AttackDamage == 7 && n.Health == 6, "the temporary buffs expire after the swap (clone): " + n.AttackDamage + "/" + n.Health);
            });
        });

        Pending("AT_018", (g, p, id) => {
            var seen = new HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                StandardSetsProof.ParkBoard(p);
                var pal = (Minion)Play(g, p, id);
                Core2026Proof.Check(p.BoardZone.Count == 2, "Battlecry summons one minion");
                var other = p.BoardZone.Single(m => m.Id != pal.Id);
                Core2026Proof.Check(other.Card.Rarity == Rarity.LEGENDARY && other.Card.Type == CardType.MINION && other.Card.Collectible, "a collectible Legendary minion: " + other.Card.Id);
                seen.Add(other.Card.Id);
            }
            Core2026Proof.Check(seen.Count >= 3, "the Legendary is random");
            StandardSetsProof.ParkBoard(p);
            var spirit = (Minion)Play(g, p, id);
            int count = p.BoardZone.Count;
            Use(g, p);
            Core2026Proof.Check(p.BoardZone.Count == count + 1 && p.BoardZone.Last().Card.Rarity == Rarity.LEGENDARY, "Inspire summons a Legendary");
            Core2026Proof.Check(!CanUse(p), "one Inspire per use");
            End(g);
            Use(g, p.Opponent, p.Hero);
            Core2026Proof.Check(p.BoardZone.Count == count + 1, "the opponent's hero power does not Inspire");
            End(g);
            Resume(g, next => {
                int c = next.Player1.BoardZone.Count; Use(next, next.Player1);
                Core2026Proof.Check(next.Player1.BoardZone.Count == c + 1, "Inspire on a clone");
            });
            StandardSetsProof.ParkBoard(p);
            var full = (Minion)Play(g, p, id);
            while (p.BoardZone.Count < 7) Summon(p, Yeti);
            Use(g, p);
            Core2026Proof.Check(p.BoardZone.Count == 7, "a full board prevents the Inspire summon");
            spirit.Silence(); full.Silence(); g.DeathProcessingAndAuraUpdate();
            End(g); End(g);
            ParkBoardOnly(p);
            Summon(p, id).Silence();
            Use(g, p);
            Core2026Proof.Check(p.BoardZone.Count == 1, "Silence removes Inspire");
        });

        Pending("AT_021", (g, p, id) => {
            var knight = (Minion)Play(g, p, id);
            Core2026Proof.Check(knight.AttackDamage == 3 && knight.Health == 2, "Tiny Knight of Evil is 3/2");
            var one = Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone);
            Discard(g, p, one);
            Core2026Proof.Check(knight.AttackDamage == 5 && knight.Health == 3, "a discard grants +2/+1");
            var two = Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone);
            var three = Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone);
            Discard(g, p, two); Discard(g, p, three);
            Core2026Proof.Check(knight.AttackDamage == 9 && knight.Health == 5, "each discard triggers: " + knight.AttackDamage + "/" + knight.Health);
            var foe = Entity.FromCard(p.Opponent, Cards.FromId(Yeti), zone: p.Opponent.HandZone);
            Discard(g, p.Opponent, foe);
            Core2026Proof.Check(knight.AttackDamage == 9 && knight.Health == 5, "the opponent's discard does not count");
            Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone);
            Play(g, p, "EX1_306");
            Core2026Proof.Check(knight.AttackDamage == 11 && knight.Health == 6, "a Felstalker discard counts: " + knight.AttackDamage + "/" + knight.Health);
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[knight.Id];
                Discard(next, next.Player1, Entity.FromCard(next.Player1, Cards.FromId(Yeti), zone: next.Player1.HandZone));
                Core2026Proof.Check(n.AttackDamage == 13 && n.Health == 7, "discards keep counting on a clone");
            });
            knight.Damage = 1;
            Discard(g, p, Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone));
            Core2026Proof.Check(knight.Damage == 1 && knight.Health == 6 && knight.AttackDamage == 13, "the buff adds Health without healing: " + knight.AttackDamage + "/" + knight.Health + " dmg " + knight.Damage);
            knight.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(knight.AttackDamage == 3 && knight.Health == 2, "Silence removes the buff");
            Discard(g, p, Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone));
            Core2026Proof.Check(knight.AttackDamage == 3 && knight.Health == 2, "a Silenced Knight ignores discards");
        });

        WildSetsProof.Case("AT_022", (g, p, id) => {
            var foeHero = p.Opponent.Hero; foeHero.Armor = 100;
            var m1 = Summon(p.Opponent, Yeti); m1.BaseHealth = 80; var m2 = Summon(p.Opponent, Yeti); m2.BaseHealth = 80;
            var friend = Summon(p, Yeti); friend.BaseHealth = 80;
            var hits = new Dictionary<string, int>();
            for (int i = 0; i < 9; i++)
            {
                int h = foeHero.Armor, d1 = m1.Damage, d2 = m2.Damage;
                Play(g, p, id);
                if (foeHero.Armor != h) Count(hits, "hero", h - foeHero.Armor);
                if (m1.Damage != d1) Count(hits, "m1", m1.Damage - d1);
                if (m2.Damage != d2) Count(hits, "m2", m2.Damage - d2);
            }
            Core2026Proof.Check(hits.Values.Sum() == 36 && hits.Values.All(v => v % 4 == 0) && hits.Count >= 2, "Fist deals 4 to one random enemy each cast: " + string.Join(",", hits));
            Core2026Proof.Check(friend.Damage == 0 && p.Hero.Damage == 0, "Fist never hits friendly characters");
            int armor = foeHero.Armor, dm1 = m1.Damage, dm2 = m2.Damage;
            var held = Entity.FromCard(p, Cards.FromId(id), zone: p.HandZone);
            Discard(g, p, held);
            Core2026Proof.Check((armor - foeHero.Armor) + (m1.Damage - dm1) + (m2.Damage - dm2) == 4, "discarding Fist deals 4 to a random enemy");
            var thalnos = Summon(p, Thalnos);
            armor = foeHero.Armor; dm1 = m1.Damage; dm2 = m2.Damage;
            Play(g, p, id);
            Core2026Proof.Check((armor - foeHero.Armor) + (m1.Damage - dm1) + (m2.Damage - dm2) == 5, "Fist scales with Spell Damage");
            StandardSetsProof.ParkBoard(p.Opponent);
            foeHero.Armor = 0; foeHero.Damage = 0;
            Resume(g, next => {
                var h2 = next.Player2.Hero;
                Play(next, next.Player1, id);
                Core2026Proof.Check(h2.Damage == 5, "with no enemy minions Fist hits the hero (clone)");
            });
            Discard(g, p, Entity.FromCard(p, Cards.FromId(id), zone: p.HandZone));
            Core2026Proof.Check(foeHero.Damage == 5, "a discarded Fist hits the hero when no minion is left");
            Kill(g, thalnos);
            int mine = p.Hero.Damage + friend.Damage + p.BoardZone.Where(m => m.Id != friend.Id).Sum(m => m.Damage);
            Discard(g, p.Opponent, Entity.FromCard(p.Opponent, Cards.FromId(id), zone: p.Opponent.HandZone));
            Core2026Proof.Check(p.Hero.Damage + friend.Damage + p.BoardZone.Where(m => m.Id != friend.Id).Sum(m => m.Damage) == mine + 4
                && foeHero.Damage == 5, "a Fist discarded by the opponent hits my side for 4, not theirs");
            g.DeathProcessingAndAuraUpdate();
        });
    }

    private static void Count(Dictionary<string, int> hits, string key, int amount) => hits[key] = hits.GetValueOrDefault(key) + amount;

    private static void ParkBoardOnly(Controller p) => StandardSetsProof.ParkBoard(p);

    // ---- facilities -------------------------------------------------------------------------------------------------

    /// <summary>Inspire: after its owner uses the Hero Power, on the owner's turn only, once per use. Uses Lowly Squire (AT_082,
    /// Inspire: +1 Attack), which no lane of this wave changes.</summary>
    private static void InspireMechanic()
    {
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = Fixture(seed); var p = g.Player1;
            var first = (Minion)Play(g, p, "AT_082"); var second = (Minion)Play(g, p, "AT_082");
            Use(g, p);
            Core2026Proof.Check(first.AttackDamage == 2 && second.AttackDamage == 2, "two Inspire minions both trigger on one hero power use");
            Core2026Proof.Check(!CanUse(p), "the hero power cannot be used twice");
            var late = (Minion)Play(g, p, "AT_082");
            Core2026Proof.Check(late.AttackDamage == 1, "an Inspire minion played afterwards does not trigger retroactively");
            End(g);
            var theirs = (Minion)Play(g, p.Opponent, "AT_082");
            Use(g, p.Opponent, p.Hero);
            Core2026Proof.Check(theirs.AttackDamage == 2 && first.AttackDamage == 2 && late.AttackDamage == 1,
                "each side's Inspire follows its own hero power only");
            End(g);
            first.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(first.AttackDamage == 1, "Silence removes the gained Attack");
            Use(g, p);
            Core2026Proof.Check(first.AttackDamage == 1 && second.AttackDamage == 3 && late.AttackDamage == 2 && theirs.AttackDamage == 2,
                "Silence removes the Inspire trigger; only the owner's other minions still trigger");
            Resume(g, next => {
                End(next); End(next);
                var n = (Minion)next.IdEntityDic[second.Id]; Use(next, next.Player1);
                Core2026Proof.Check(n.AttackDamage == 4, "Inspire continues on a clone and snapshot");
            });
            Kill(g, late); Core2026Proof.Check(late.Zone.Type == Zone.GRAVEYARD, "dead Inspire minion");
        }
    }

    // ---- blocked cards: Standard uses them (pool eligibility / NPC death values); never registered ----------------------

    private static void Blocked()
    {
        Unregistered("AT_004", (g, p, id) => {
            var big = Summon(p.Opponent, Yeti); big.BaseHealth = 60;
            var held = Add(p, id);
            Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == held.Id).All(t => t.Target is Minion) && p.Options().Any(t => t.Source?.Id == held.Id), "minion targets only");
            PlayEntity(g, p, held, big);
            Core2026Proof.Check(big.Damage == 2, "Arcane Blast deals 2");
            Summon(p, Thalnos); Play(g, p, id, big);
            Core2026Proof.Check(big.Damage == 6, "double bonus from Spell Damage (2+2)");
            Summon(p, Thalnos); Play(g, p, id, big);
            Core2026Proof.Check(big.Damage == 12, "two Spell Damage sources: 2+2*2 = 6 more");
        });
        Unregistered("AT_005", (g, p, id) => {
            var target = Summon(p.Opponent, Yeti); target.Damage = 2; Play(g, p, "CS2_009", target);
            Play(g, p, id, target);
            var boar = p.Opponent.BoardZone.Single();
            Core2026Proof.Check(boar.Card.Id == "AT_005t" && boar.AttackDamage == 4 && boar.Health == 2 && boar.Damage == 0 && boar.HasCharge && boar.IsRace(Race.BEAST), "4/2 Boar with Charge, no buffs or damage");
            Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Target is Hero), "no hero targets");
            boar.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(boar.Card.Id == "AT_005t" && !boar.HasCharge, "Silence leaves a Boar without Charge");
            var friend = Summon(p, Yeti); Play(g, p, id, friend);
            Core2026Proof.Check(p.BoardZone.Single().Card.Id == "AT_005t" && p.BoardZone.Single().Controller == p, "friendly transform stays friendly");
        });
        Unregistered("AT_017", (g, p, id) => {
            var plain = (Minion)Play(g, p, id);
            Core2026Proof.Check(plain.AttackDamage == 2 && !plain.HasTaunt, "no Dragon in hand");
            Summon(p, "AT_008");
            var onBoard = (Minion)Play(g, p, id);
            Core2026Proof.Check(onBoard.AttackDamage == 2 && !onBoard.HasTaunt, "a Dragon on the board does not count");
            Entity.FromCard(p, Cards.FromId("AT_008"), zone: p.HandZone);
            var boosted = (Minion)Play(g, p, id);
            Core2026Proof.Check(boosted.AttackDamage == 3 && boosted.HasTaunt, "Dragon in hand: +1 Attack and Taunt");
            ParkBoardHand(p);
            Entity.FromCard(p.Opponent, Cards.FromId("AT_008"), zone: p.Opponent.HandZone);
            var enemyDragon = (Minion)Play(g, p, id);
            Core2026Proof.Check(enemyDragon.AttackDamage == 2, "the opponent's hand does not count");
            boosted.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(boosted.AttackDamage == 2 && !boosted.HasTaunt, "Silence removes the bonus");
        });
        Unregistered("AT_020", (g, p, id) => {
            var doom = (Minion)Play(g, p, id);
            Core2026Proof.Check(doom.AttackDamage == 6 && doom.Health == 8 && doom.Card.Cost == 7 && doom.IsRace(Race.DEMON), "6/8 Demon for 7");
            Core2026Proof.Check(!doom.HasTaunt && !doom.HasCharge && !doom.HasDivineShield && !doom.HasStealth && !doom.HasWindfury && !doom.HasDeathrattle, "vanilla");
            Core2026Proof.Check(doom.IsExhausted, "no Charge: cannot attack immediately");
        });
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------

    private static Game Fixture(int seed)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = CardClass.ROGUE, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId(Yeti), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId(Yeti), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
            owner.BaseMana = 10;
        }
        return game;
    }

    private static void Pending(string id, Action<Game, Controller, string> scenario)
    {
        if (BlockedRun) Unregistered(id, scenario, "BLOCKED-HSDATA");
    }

    /// <summary>Runs a scenario for a card the suite must not register or change; prints PASS or FAIL, never throws.</summary>
    private static void Unregistered(string id, Action<Game, Controller, string> scenario, string reason = "BLOCKED-STANDARD")
    {
        try
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = Fixture(seed);
                scenario(g, g.Player1, id);
                Core2026Proof.Check(Canonical.State(g) == Canonical.State(g.CloneExact(history: true)), id + " exact clone");
                Core2026Proof.Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools)), id + " snapshot");
            }
            Console.WriteLine("PASS (unregistered, " + reason + ") " + id);
        }
        catch (Exception error) { Console.WriteLine("FAIL (unregistered, " + reason + ") " + id + ": " + error.Message); }
    }

    private static void Use(Game g, Controller c, ICharacter target = null)
    {
        c.BaseMana = 10; c.UsedMana = 0; c.TemporaryMana = 0;
        Core2026Proof.Check(g.Process(HeroPowerTask.Any(c, target)), "hero power accepted");
    }

    private static bool CanUse(Controller c) => c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    /// <summary>A discard outside a play, followed by the death and aura processing a real turn always performs.</summary>
    private static void Discard(Game g, Controller owner, IPlayable card) { Generic.DiscardBlock(owner, card); g.DeathProcessingAndAuraUpdate(); }

    private static void ParkBoardHand(Controller p) { foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); } }

    /// <summary>The fair (information-limited) view for the opponent of <paramref name="owner"/> contains none of the owner's hidden cards.</summary>
    private static void HiddenFrom(Game g, Controller owner, IEnumerable<IPlayable> hidden, string why)
    {
        var ids = hidden.Select(e => e.Card.Id).ToHashSet();
        var before = Canonical.State(g); string history = g.HistoryFacts.CanonicalText(); var random = g.Random.CaptureState();
        var fair = FairBotState.Create(g, owner.Opponent.PlayerId, 83, DeckStyle.Value);
        var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        Core2026Proof.Check(!view.IdEntityDic.Values.Any(e => e.Controller.PlayerId == owner.PlayerId
            && (e.Zone?.Type is Zone.HAND or Zone.DECK or Zone.SETASIDE or Zone.SECRET) && ids.Contains(e.Card.Id)), why + ": identity visible in the fair view");
        Core2026Proof.Check(before == Canonical.State(g) && history == g.HistoryFacts.CanonicalText() && random.Equals(g.Random.CaptureState()),
            why + ": the projection leaves the game untouched");
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }
}
