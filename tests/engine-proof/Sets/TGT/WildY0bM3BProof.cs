using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

/// <summary>Wave Y0b lane m3b: Grand Tournament AT_023..AT_044 (Warlock, Rogue, Druid).
/// Registered (literal WildSetsProof.Case, listed in wild-y0b-m3b.txt): AT_024 .. AT_036, AT_038 .. AT_044.
/// BLOCKED-STANDARD, written but never registered: AT_023 (data/pools/pool-eligibility-v1.json), AT_037 (CORE_AT_037 in
/// plugin/LocalCardCatalog.cs, data/pools/pool-eligibility-v1.json, data/progression/core-2026-unlocks.json). Run them with
/// WILD_Y0B_M3B_BLOCKED=1.
/// Mechanics: Inspire is proven in lane m3a (WildY0bM3AProof.InspireMechanic). No card of this lane has Joust (Joust belongs to
/// lanes m3a/m3c cards), so no Joust proof is written here. The upgraded Hero Powers appear through AT_034 (Poisoned Daggers) and
/// AT_027 (Life Tap), installed with SetPower.
/// </summary>
internal static class WildY0bM3BProof
{
    private static bool BlockedRun => Environment.GetEnvironmentVariable("WILD_Y0B_M3B_BLOCKED") == "1";
    private const string Yeti = "CS2_182", Wisp = "CS2_231", Thalnos = "EX1_012", Raptor = "CS2_172", Imp = "EX1_319",
        Coin = "GAME_005", LifeTap = "CS2_056", Dagger = "CS2_083b", Daggers = "AT_132_ROGUE", Axe = "CS2_106";

    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, message);

    [GroupProof("wild-y0b-m3b-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        if (BlockedRun) Blocked();

        WildSetsProof.Case("AT_024", (g, p, id) => {
            var friendly = Summon(p, Imp); var enemy = Summon(p.Opponent, Imp); var yeti = Summon(p, Yeti);
            var spell = Add(p, id);
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
            Check(targets.Length == 2 && targets.All(t => t.Target is Minion m && m.IsRace(Race.DEMON)), "Demonfuse targets Demons on both sides only");
            Check(spell.Cost == 2 && spell.Card.Text == "Give a Demon +3/+3.", "Demonfuse costs 2 and only buffs");
            p.Opponent.BaseMana = 4; p.Opponent.UsedMana = 1;
            PlayEntity(g, p, spell, friendly);
            Check(friendly.AttackDamage == 6 && friendly.Health == 5, "a friendly Demon gets +3/+3");
            Check(p.Opponent.BaseMana == 4 && p.Opponent.UsedMana == 1 && yeti.AttackDamage == 4, "no Mana Crystal for the opponent, other minions untouched");
            Play(g, p, id, enemy);
            Check(enemy.AttackDamage == 6 && enemy.Health == 5, "an enemy Demon can be targeted too");
            Play(g, p, "CS2_004", friendly);
            Check(friendly.AttackDamage == 6 && friendly.Health == 7, "Demonfuse layers with a later +2 Health buff");
            friendly.Damage = 3; End(g); End(g);
            Check(friendly.AttackDamage == 6 && friendly.Health == 4 && friendly.Damage == 3, "the buff is permanent and does not heal: " + friendly.AttackDamage + "/" + friendly.Health + " dmg " + friendly.Damage);
            enemy.HasStealth = true;
            var again = Add(p, id);
            Check(p.Options().Where(t => t.Source?.Id == again.Id).Select(t => t.Target.Id).Distinct().Count() == 1, "a Stealth enemy Demon is not targetable: " + string.Join(",", p.Options().Where(t => t.Source?.Id == again.Id).Select(t => t.Target.Card.Id + "/" + t.Target.Controller.PlayerId + "/" + ((Minion)t.Target).HasStealth)));
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[friendly.Id];
                Play(next, next.Player1, id, n);
                Check(n.AttackDamage == 9 && n.Health == 7, "Demonfuse stacks on a clone (current Health)");
            });
            friendly.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(friendly.AttackDamage == 3 && friendly.Health == 2, "Silence removes the whole buff");
            Kill(g, friendly); Kill(g, enemy);
            Check(!p.Options().Any(t => t.Source?.Id == again.Id), "no Demon on the board: Demonfuse cannot be played");
        });

        WildSetsProof.Case("AT_025", (g, p, id) => {
            var held = Add(p, id);
            Check(!p.Options().Any(t => t.Source?.Id == held.Id), "no enemy minion: Dark Bargain is unplayable");
            Summon(p, Yeti);
            Check(!p.Options().Any(t => t.Source?.Id == held.Id), "a friendly minion does not enable it");
            var only = Summon(p.Opponent, Yeti);
            Check(held.Cost == 4, "Dark Bargain costs 4");
            ParkHand(p);
            var keep = Add(p, Wisp);
            var solo = Add(p, id);
            PlayEntity(g, p, solo);
            Check(only.Zone.Type == Zone.GRAVEYARD, "a lone enemy minion is destroyed");
            Check(keep.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == 0, "the only other card is discarded");
            var survivors = new HashSet<string>(); var discards = new HashSet<string>();
            var ids = new[] { Yeti, Raptor, "CS2_120" };
            for (int i = 0; i < 14; i++)
            {
                StandardSetsProof.ParkBoard(p.Opponent); ParkHand(p);
                var enemies = ids.Select(e => Summon(p.Opponent, e)).ToArray();
                var cards = new[] { Add(p, Yeti), Add(p, Raptor), Add(p, Thalnos), Add(p, Wisp) };
                int oppHand = p.Opponent.HandZone.Count; var friend = p.BoardZone.FirstOrDefault();
                var bargain = Add(p, id);
                PlayEntity(g, p, bargain);
                Check(enemies.Count(e => e.Zone.Type == Zone.GRAVEYARD) == 2 && enemies.Count(e => e.Zone.Type == Zone.PLAY) == 1, "exactly two of three enemy minions die");
                Check(cards.Count(c => c.Zone.Type == Zone.GRAVEYARD) == 2 && cards.Count(c => c.Zone.Type == Zone.HAND) == 2, "exactly two cards are discarded");
                Check(p.Opponent.HandZone.Count == oppHand, "the opponent's hand is untouched: " + oppHand + " -> " + string.Join(",", p.Opponent.HandZone.Select(c => c.Card.Id)));
                if (friend != null) Check(friend.Zone.Type == Zone.PLAY, "friendly minions are untouched");
                survivors.Add(enemies.Single(e => e.Zone.Type == Zone.PLAY).Card.Id);
                discards.Add(string.Join("+", cards.Where(c => c.Zone.Type == Zone.GRAVEYARD).Select(c => c.Card.Id).OrderBy(x => x)));
            }
            Check(survivors.Count >= 2, "the survivor is random: " + string.Join(",", survivors));
            Check(discards.Count >= 3, "the discards are random: " + string.Join(" | ", discards));
            // Discard triggers resolve: two Fists of Jaraxxus discarded deal 4 each to random enemies.
            StandardSetsProof.ParkBoard(p.Opponent); ParkHand(p);
            var foe = p.Opponent.Hero; foe.Armor = 100; foe.Damage = 0;
            var enemyMinions = new[] { Summon(p.Opponent, Yeti), Summon(p.Opponent, Yeti), Summon(p.Opponent, Yeti) };
            foreach (var m in enemyMinions) m.BaseHealth = 80;
            Add(p, "AT_022"); Add(p, "AT_022");
            PlayEntity(g, p, Add(p, id));
            var alive = enemyMinions.Where(m => m.Zone.Type == Zone.PLAY).ToArray();
            Check(alive.Length == 1 && (100 - foe.Armor) + alive[0].Damage == 8, "both discarded Fists hit an enemy for 4: armor lost " + (100 - foe.Armor) + ", minion " + alive[0].Damage);
            Resume(g, next => {
                var owner = next.Player1; StandardSetsProof.ParkBoard(next.Player2); ParkHand(owner);
                Summon(next.Player2, Yeti); Summon(next.Player2, Yeti);
                var c = Add(owner, Wisp);
                PlayEntity(next, owner, Add(owner, id));
                Check(next.Player2.BoardZone.Count == 0 && c.Zone.Type == Zone.GRAVEYARD, "Dark Bargain on a clone destroys both and discards");
            });
        });

        WildSetsProof.Case("AT_026", (g, p, id) => {
            var guard = Summon(p, id);
            Check(guard.AttackDamage == 4 && guard.Health == 3 && guard.IsRace(Race.DEMON), "Wrathguard is a 4/3 Demon");
            Play(g, p, "CS2_024", guard);
            Check(p.Hero.Damage == 3 && guard.Zone.Type == Zone.GRAVEYARD, "3 damage to Wrathguard also hits its hero for 3");
            p.Hero.Damage = 0;
            var big = Summon(p, id);
            Play(g, p, "CS2_029", big);
            Check(p.Hero.Damage == 6, "overkill counts the full amount: " + p.Hero.Damage);
            p.Hero.Damage = 0; p.Hero.Armor = 2;
            var armored = Summon(p, id);
            Play(g, p, "CS2_024", armored);
            Check(p.Hero.Armor == 0 && p.Hero.Damage == 1, "the hero's armor absorbs first");
            p.Hero.Damage = 0;
            var shielded = Summon(p, id); shielded.HasDivineShield = true;
            Play(g, p, "CS2_024", shielded);
            Check(p.Hero.Damage == 0 && !shielded.HasDivineShield && shielded.Damage == 0, "Divine Shield: no damage taken, no damage to the hero");
            var fighter = Summon(p, id); var wall = Summon(p.Opponent, Yeti);
            Attack(g, fighter, wall);
            Check(p.Hero.Damage == 4 && wall.Damage == 4 && fighter.Zone.Type == Zone.GRAVEYARD, "combat damage taken (4) is dealt to the hero, not the damage dealt");
            p.Hero.Damage = 0;
            var destroyed = Summon(p, id); Kill(g, destroyed);
            Check(p.Hero.Damage == 0, "destroying it is no damage");
            var silenced = Summon(p, id); silenced.Silence(); g.DeathProcessingAndAuraUpdate();
            Play(g, p, "CS2_024", silenced);
            Check(p.Hero.Damage == 0, "Silence removes the effect");
            var foeGuard = Summon(p.Opponent, id);
            Play(g, p, "CS2_024", foeGuard);
            Check(p.Opponent.Hero.Damage == 3 && p.Hero.Damage == 0, "an enemy Wrathguard hurts the enemy hero");
            var stolen = Summon(p.Opponent, id);
            Play(g, p, "CS1_113", stolen);
            Check(stolen.Controller == p && stolen.Zone.Type == Zone.PLAY, "Mind Control takes the Wrathguard");
            Play(g, p, "CS2_024", stolen);
            Check(p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3, "a stolen Wrathguard hurts its new controller's hero");
            p.Hero.Damage = 0;
            StandardSetsProof.ParkBoard(p);
            var a = Summon(p, id); var b = Summon(p, id);
            End(g);
            Play(g, p.Opponent, "CS2_032");
            Check(p.Hero.Damage == 8 && a.Zone.Type == Zone.GRAVEYARD && b.Zone.Type == Zone.GRAVEYARD, "two Wrathguards both trigger from one Flamestrike");
            End(g);
            p.Hero.Damage = 0;
            var last = Summon(p, id);
            Resume(g, next => {
                var owner = next.Player1; var n = (Minion)next.IdEntityDic[last.Id];
                owner.Hero.Damage = 28;
                Play(next, owner, "CS2_029", n);
                Check(next.State == State.COMPLETE && owner.PlayState == PlayState.LOST, "lethal damage through Wrathguard kills its own hero (clone)");
            });
            Play(g, p, "CS2_024", last);
            Check(p.Hero.Damage == 3, "Wrathguard on a fresh board");
        });

        WildSetsProof.Case("AT_027", (g, p, id) => {
            SetPower(p, LifeTap); SetPower(p.Opponent, LifeTap);
            Use(g, p);
            Check(p.HandZone.Last().Cost == 4, "without Wilfred a Life Tap card keeps its Cost");
            var enemyWilfred = Summon(p.Opponent, id);
            End(g); End(g);
            Use(g, p);
            Check(p.HandZone.Last().Cost == 4, "an enemy Wilfred does not help me");
            End(g);
            Use(g, p.Opponent);
            Check(p.Opponent.HandZone.Last().Cost == 0, "the enemy Wilfred discounts the enemy's own Hero Power card");
            End(g);
            var wilfred = (Minion)Play(g, p, id);
            Check(wilfred.AttackDamage == 4 && wilfred.Health == 4 && p.HandZone.Last().Cost == 4, "playing Wilfred does not change cards already drawn");
            ParkHand(p);
            End(g); End(g);
            Use(g, p);
            var tapped = p.HandZone.Last();
            Check(tapped.Card.Id == Yeti && tapped.Cost == 0, "a card drawn from the Hero Power costs (0)");
            var normal = Generic.Draw(p);
            Check(normal.Cost == 4, "an ordinary draw is not discounted");
            var made = Entity.FromCard(p, Cards.FromId(Yeti), zone: p.HandZone);
            Check(made.Cost == 4, "a created card is not discounted");
            Play(g, p, "CS2_023");
            Check(p.HandZone.TakeLast(2).All(c => c.Cost == 4), "Arcane Intellect draws are not discounted");
            End(g); End(g);
            Check(p.HandZone.Last().Cost == 4 && tapped.Cost == 0, "the start-of-turn draw is not discounted; the tapped card stays at (0)");
            ParkHand(p);
            for (int i = 0; i < 10; i++) Entity.FromCard(p, Cards.FromId(Wisp), zone: p.HandZone);
            Use(g, p);
            Check(p.HandZone.Count == 10, "a full hand: the tapped card is burned, nothing breaks");
            ParkHand(p); End(g); End(g);
            Resume(g, next => {
                var owner = next.Player1; ParkHand(owner);
                Use(next, owner);
                Check(owner.HandZone.Last().Cost == 0, "Wilfred keeps working on a clone");
            });
            var second = Summon(p, id);
            End(g); End(g);
            Use(g, p);
            Check(p.HandZone.Last().Cost == 0, "two Wilfreds still make it (0)");
            wilfred.Silence(); g.DeathProcessingAndAuraUpdate(); Kill(g, second);
            Check(p.HandZone.Last().Cost == 0, "a card already tapped stays at (0) after Wilfred is gone");
            End(g); End(g);
            Use(g, p);
            Check(p.HandZone.Last().Cost == 4, "Silenced and dead Wilfreds no longer discount");
            Play(g, p, "CS1_113", enemyWilfred);
            ParkHand(p); End(g); End(g);
            Use(g, p);
            Check(p.HandZone.Last().Cost == 0, "a stolen Wilfred discounts its new controller's Hero Power cards");
            Kill(g, enemyWilfred);
            var third = Summon(p, id);
            foreach (var c in p.DeckZone.ToArray()) { p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
            End(g); End(g);
            int hp = p.Hero.Damage;
            Use(g, p);
            Check(p.Hero.Damage > hp, "an empty deck (fatigue) does not break Life Tap");
            Kill(g, third);
        });

        WildSetsProof.Case("AT_028", (g, p, id) => {
            var first = (Minion)Play(g, p, id);
            Check(first.AttackDamage == 3 && first.Health == 7, "no Combo as the first card of the turn");
            Play(g, p, Coin);
            var combo = (Minion)Play(g, p, id);
            Check(combo.AttackDamage == 7 && combo.Health == 7, "Combo gives +4 Attack: " + combo.AttackDamage);
            var summoned = Summon(p, id);
            Check(summoned.AttackDamage == 3, "a summoned (not played) Rider has no Combo");
            Play(g, p, "CS2_188", combo);
            Check(combo.AttackDamage == 9, "layers with a temporary +2 Attack");
            Resume(g, next => {
                var owner = next.Player1;
                var n = (Minion)next.IdEntityDic[combo.Id];
                End(next);
                Check(n.AttackDamage == 7, "the temporary buff expires, Combo stays (clone)");
                End(next);
                Play(next, owner, Coin);
                Check(((Minion)Play(next, owner, id)).AttackDamage == 7, "Combo works again next turn on a clone");
            });
            End(g);
            Check(combo.AttackDamage == 7, "Combo's +4 is permanent");
            End(g);
            var fresh = (Minion)Play(g, p, id);
            Check(fresh.AttackDamage == 3, "a new turn starts without Combo");
            combo.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(combo.AttackDamage == 3, "Silence removes the bonus");
            End(g);
            Play(g, p.Opponent, Coin);
            var theirs = (Minion)Play(g, p.Opponent, id);
            Check(theirs.AttackDamage == 7 && fresh.AttackDamage == 3, "the opponent's Combo works on their side only");
        });

        WildSetsProof.Case("AT_029", (g, p, id) => {
            var plain = (Minion)Play(g, p, id);
            Check(plain.AttackDamage == 2 && plain.Health == 1 && plain.IsRace(Race.PIRATE), "Buccaneer is a 2/1 Pirate");
            Play(g, p, Axe);
            var weapon = p.Hero.Weapon;
            Check(weapon.AttackDamage == 4 && weapon.Durability == 2, "an equipped weapon gets +1 Attack");
            Play(g, p, id);
            Play(g, p, "CS2_091");
            Check(p.Hero.Weapon.Id != weapon.Id && p.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.Durability == 4, "two Buccaneers give +2 to the new weapon: " + p.Hero.Weapon.AttackDamage);
            Use(g, p);
            Check(p.Hero.Weapon.Card.Id == "CS2_082" && p.Hero.Weapon.AttackDamage == 3, "the Hero Power dagger is equipped too (1+2)");
            End(g);
            Play(g, p.Opponent, Axe);
            Check(p.Opponent.Hero.Weapon.AttackDamage == 3, "an enemy weapon gets no bonus");
            End(g);
            plain.Silence(); g.DeathProcessingAndAuraUpdate();
            Play(g, p, Axe);
            Check(p.Hero.Weapon.AttackDamage == 4, "a Silenced Buccaneer no longer helps (one active left)");
            Kill(g, p.BoardZone.First(m => m.Card.Id == id && !m.IsSilenced));
            Play(g, p, Axe);
            Check(p.Hero.Weapon.AttackDamage == 3, "dead Buccaneers do not help");
            var late = Add(p, id);
            Check(p.Hero.Weapon.AttackDamage == 3, "a Buccaneer in hand does nothing");
            PlayEntity(g, p, late);
            Check(p.Hero.Weapon.AttackDamage == 3, "a Buccaneer played after the weapon does not buff it");
            Resume(g, next => {
                Play(next, next.Player1, Axe);
                Check(next.Player1.Hero.Weapon.AttackDamage == 4, "Buccaneer buffs a new weapon on a clone");
            });
            var foeB = Summon(p.Opponent, id);
            Play(g, p, "CS1_113", foeB);
            Play(g, p, Axe);
            Check(p.Hero.Weapon.AttackDamage == 5, "a stolen Buccaneer buffs its new controller's weapon too: " + p.Hero.Weapon.AttackDamage);
        });

        WildSetsProof.Held("AT_030", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var friend = Summon(p, Yeti); var wisp = Summon(p.Opponent, Wisp);
            var stealthy = Summon(p.Opponent, Yeti); stealthy.HasStealth = true;
            var noCombo = Add(p, id);
            Check(p.Options().Any(t => t.Source?.Id == noCombo.Id) && p.Options().Where(t => t.Source?.Id == noCombo.Id).All(t => t.Target == null), "no Combo: no target offered");
            PlayEntity(g, p, noCombo);
            Check(enemy.Damage == 0 && friend.Damage == 0 && p.Opponent.Hero.Damage == 0, "no Combo: no damage");
            var valiant = Add(p, id);
            var offered = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == valiant.Id).ToArray();
            Check(offered.Length > 0 && offered.All(t => t.Target != null), "with Combo a target is required");
            Check(offered.Any(t => t.Target is Hero) && offered.Any(t => t.Target?.Id == friend.Id) && offered.Any(t => t.Target?.Id == enemy.Id), "any character is a legal target");
            Check(!offered.Any(t => t.Target?.Id == stealthy.Id), "an enemy Stealth minion cannot be targeted");
            PlayEntity(g, p, valiant, enemy);
            Check(enemy.Damage == 1, "Combo deals 1 damage");
            Summon(p, Thalnos);
            PlayEntity(g, p, Add(p, id), wisp);
            Check(wisp.Zone.Type == Zone.GRAVEYARD, "it kills a 1-Health minion");
            PlayEntity(g, p, Add(p, id), p.Opponent.Hero);
            Check(p.Opponent.Hero.Damage == 1, "Spell Damage does not apply to a minion's Combo (hero 1)");
            PlayEntity(g, p, Add(p, id), friend);
            Check(friend.Damage == 1, "a friendly minion can be hit");
            foreach (var m in p.BoardZone.Where(m => m.Id != friend.Id).ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(m));
            End(g); End(g);
            var first = Add(p, id);
            Check(p.Options().Any(t => t.Source?.Id == first.Id) && p.Options().Where(t => t.Source?.Id == first.Id).All(t => t.Target == null), "a new turn starts without Combo");
            Resume(g, next => {
                var owner = next.Player1; Play(next, owner, Coin);
                var v = Add(owner, id);
                Check(owner.Options().Any(t => t.Source?.Id == v.Id && t.Target?.Id == enemy.Id), "Combo targets exist on a clone: " + string.Join(",", owner.Options().Where(t => t.Source?.Id == v.Id).Select(t => t.Target?.Id.ToString() ?? "-")) + " enemy " + enemy.Id + " zone " + enemy.Zone.Type + " v " + v.Zone.Type + " hand " + owner.HandZone.Count + " mana " + owner.RemainingMana + " cur " + next.CurrentPlayer.PlayerId + " all " + string.Join(";", owner.Options().Select(t => t.PlayerTaskType + ":" + t.Source?.Card.Id + ">" + t.Target?.Card.Id)));
            });
            End(g);
            Play(g, p.Opponent, Coin);
            PlayEntity(g, p.Opponent, Add(p.Opponent, id), friend);
            Check(friend.Damage == 2, "the opponent's Combo hits my minion");
        });

        WildSetsProof.Held("AT_031", (g, p, id) => {
            var cut = Summon(p, id); var wall = Summon(p.Opponent, Wisp);
            Check(cut.AttackDamage == 2 && cut.Health == 2, "Cutpurse is 2/2");
            int coins = p.HandZone.Count(c => c.Card.Id == Coin);
            Attack(g, cut, wall);
            Check(p.HandZone.Count(c => c.Card.Id == Coin) == coins, "attacking a minion gives no Coin");
            var second = Summon(p, id);
            Attack(g, second, p.Opponent.Hero);
            Check(p.HandZone.Count(c => c.Card.Id == Coin) == coins + 1 && p.Opponent.Hero.Damage == 2, "attacking a hero adds the Coin");
            HiddenFrom(g, p, p.HandZone.Where(c => c.Card.Id == Coin), "the opponent's view of the Coin");
            End(g);
            var theirs = Summon(p.Opponent, id);
            int mine = p.HandZone.Count; int theirsCoins = p.Opponent.HandZone.Count(c => c.Card.Id == Coin);
            Attack(g, theirs, p.Hero);
            Check(p.Opponent.HandZone.Count(c => c.Card.Id == Coin) == theirsCoins + 1 && p.HandZone.Count == mine, "an enemy Cutpurse gives the Coin to the enemy");
            End(g);
            var foeCut = Summon(p.Opponent, id);
            Play(g, p, "CS1_113", foeCut);
            int before = p.HandZone.Count(c => c.Card.Id == Coin); int theirsBefore = p.Opponent.HandZone.Count(c => c.Card.Id == Coin);
            Attack(g, foeCut, p.Opponent.Hero);
            Check(p.HandZone.Count(c => c.Card.Id == Coin) == before + 1 && p.Opponent.HandZone.Count(c => c.Card.Id == Coin) == theirsBefore, "a stolen Cutpurse gives the Coin to its new controller");
            Resume(g, next => {
                var owner = next.Player1; var n = (Minion)next.IdEntityDic[cut.Id];
                int c = owner.HandZone.Count(x => x.Card.Id == Coin);
                Attack(next, n, next.Player2.Hero);
                Check(owner.HandZone.Count(x => x.Card.Id == Coin) == c + 1, "Cutpurse works on a clone");
            });
            ParkHand(p);
            for (int i = 0; i < 10; i++) Entity.FromCard(p, Cards.FromId(Wisp), zone: p.HandZone);
            Attack(g, cut, p.Opponent.Hero);
            Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.Id == Wisp), "a full hand gets no Coin");
            End(g); End(g);
            ParkHand(p);
            cut.Silence(); g.DeathProcessingAndAuraUpdate();
            Attack(g, cut, p.Opponent.Hero);
            Check(!p.HandZone.Any(c => c.Card.Id == Coin), "Silence removes it");
            Kill(g, second);
            Check(second.Zone.Type == Zone.GRAVEYARD, "dead Cutpurse");
        });

        WildSetsProof.Case("AT_032", (g, p, id) => {
            var none = (Minion)Play(g, p, id);
            Check(none.AttackDamage == 4 && none.Health == 3, "no Pirate: 4/3");
            Summon(p.Opponent, "AT_029");
            var enemyPirate = (Minion)Play(g, p, id);
            Check(enemyPirate.AttackDamage == 4, "an enemy Pirate does not count");
            Add(p, "AT_029");
            var inHand = (Minion)Play(g, p, id);
            Check(inHand.AttackDamage == 4, "a Pirate in hand does not count");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var pirate = Summon(p, "AT_029");
            var boosted = (Minion)Play(g, p, id);
            Check(boosted.AttackDamage == 5 && boosted.Health == 4, "a friendly Pirate: +1/+1");
            var viaSummon = Summon(p, id);
            Check(viaSummon.AttackDamage == 4, "a summoned Dealer has no Battlecry");
            pirate.Silence(); g.DeathProcessingAndAuraUpdate();
            StandardSetsProof.ParkBoard(p); pirate = Summon(p, "AT_029");
            pirate.Silence(); g.DeathProcessingAndAuraUpdate();
            var silenced = (Minion)Play(g, p, id);
            Check(silenced.AttackDamage == 5, "a Silenced Pirate is still a Pirate");
            Kill(g, pirate);
            StandardSetsProof.ParkBoard(p);
            var dead = (Minion)Play(g, p, id);
            Check(dead.AttackDamage == 4, "a dead Pirate no longer counts");
            boosted.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(boosted.AttackDamage == 4 && boosted.Health == 3, "Silence removes the buff");
            StandardSetsProof.ParkBoard(p);
            Summon(p, "AT_029");
            Resume(g, next => {
                var m = (Minion)Play(next, next.Player1, id);
                Check(m.AttackDamage == 5 && m.Health == 4, "Shady Dealer on a clone");
            });
            End(g);
            Summon(p.Opponent, "AT_029");
            var theirs = (Minion)Play(g, p.Opponent, id);
            Check(theirs.AttackDamage == 5, "the opponent's Pirate counts for them");
        });

        WildSetsProof.Case("AT_033", (g, p, id) => {
            var classes = new HashSet<string>(); var seen = new HashSet<string>();
            for (int i = 0; i < 12; i++)
            {
                ParkHand(p); ParkHand(p.Opponent);
                int oppHand = 0;
                var spell = Add(p, id);
                PlayEntity(g, p, spell);
                Check(p.HandZone.Count == 3, "three cards are added: " + p.HandZone.Count);
                Check(p.HandZone.All(c => c.Card.Class == CardClass.MAGE && c.Card.Collectible && c.Controller == p), "all collectible cards of the opponent's class");
                Check(p.Opponent.HandZone.Count == oppHand, "the opponent gets nothing");
                foreach (var c in p.HandZone) seen.Add(c.Card.Id);
                if (i == 0)
                {
                    var made = p.HandZone.ToArray();
                    HiddenFrom(g, p, made, "the opponent's view of the Burgle cards");
                    Resume(g, next => Check(next.Player1.HandZone.Count == 3, "the cards survive clone and snapshot"));
                }
            }
            Check(seen.Count >= 10, "the cards are random: " + seen.Count + " distinct");
            ParkHand(p);
            for (int i = 0; i < 8; i++) Entity.FromCard(p, Cards.FromId(Wisp), zone: p.HandZone);
            PlayEntity(g, p, Add(p, id));
            Check(p.HandZone.Count == 10, "a nearly full hand takes only what fits");
            End(g);
            ParkHand(p); ParkHand(p.Opponent);
            PlayEntity(g, p.Opponent, Add(p.Opponent, id));
            Check(p.Opponent.HandZone.Count == 3 && p.Opponent.HandZone.All(c => c.Card.Class == CardClass.ROGUE), "the enemy's Burgle draws from my class (Rogue): " + string.Join(",", p.Opponent.HandZone.Select(c => c.Card.Id + "/" + c.Card.Class)));
        });

        WildSetsProof.Case("AT_034", (g, p, id) => {
            var blade = (Weapon)Play(g, p, id);
            Check(blade.AttackDamage == 1 && blade.Durability == 3 && p.Hero.Weapon.Id == blade.Id, "Poisoned Blade is a 1/3 weapon");
            Use(g, p);
            Check(p.Hero.Weapon.Id == blade.Id && blade.AttackDamage == 2 && blade.Durability == 3, "the Hero Power gives +1 Attack instead of equipping a dagger");
            Check(!CanUse(p), "the Hero Power is used");
            End(g); End(g);
            Use(g, p);
            Check(blade.AttackDamage == 3 && p.Hero.Weapon.Id == blade.Id, "it stacks every use");
            Resume(g, next => {
                var owner = next.Player1; var w = owner.Hero.Weapon;
                Check(w.AttackDamage == 3, "the bonus survives a clone");
                End(next); End(next); Use(next, owner);
                Check(w.AttackDamage == 4 && owner.Hero.Weapon.Id == w.Id, "and keeps stacking");
            });
            Check(p.Hero.AttackDamage == 3, "the hero attacks with the bonus");
            Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.Opponent.Hero.Damage == 3 && blade.Durability == 2, "attacking uses it: 3 damage, durability 2");
            SetPower(p, Daggers);
            End(g); End(g);
            Use(g, p);
            Check(blade.AttackDamage == 4 && p.Hero.Weapon.Id == blade.Id, "Poisoned Daggers also gives +1 instead of equipping");
            Summon(p, "AT_029");
            End(g); End(g);
            Use(g, p);
            Check(blade.AttackDamage == 5, "no weapon is equipped, so Buccaneer does not trigger");
            Play(g, p, Axe);
            Check(p.Hero.Weapon.Card.Id == Axe && p.Hero.Weapon.AttackDamage == 4, "another weapon replaces the Blade normally (Buccaneer applies)");
            End(g); End(g);
            Use(g, p);
            Check(p.Hero.Weapon.Card.Id == "AT_132_ROGUEt" && p.Hero.Weapon.AttackDamage == 3, "Poisoned Daggers equips its own dagger over another weapon (2 + Buccaneer 1)");
            SetPower(p, Dagger); p.Hero.RemoveWeapon();
            End(g); End(g);
            Use(g, p);
            Check(p.Hero.Weapon.Card.Id == "CS2_082", "with no Blade the dagger is equipped");
            var again = (Weapon)Play(g, p, id);
            End(g); End(g);
            Use(g, p);
            Check(p.Hero.Weapon.Id == again.Id && again.AttackDamage == 3, "a Blade with Buccaneer (2) + Hero Power (1)");
            // opponent side: a Blade on my side is not affected by the enemy Hero Power
            End(g);
            SetPower(p.Opponent, Dagger);
            var theirs = (Weapon)Play(g, p.Opponent, id);
            Use(g, p.Opponent);
            Check(theirs.AttackDamage == 2 && again.AttackDamage == 3, "each side's Hero Power affects its own Blade only");
        });

        WildSetsProof.Held("AT_035", (g, p, id) => {
            var seen = new HashSet<int>();
            for (int round = 0; round < 6; round++)
            {
                StandardSetsProof.ParkBoard(p); ParkHand(p.Opponent);
                foreach (var c in p.Opponent.DeckZone.Where(c => c.Card.Id == "AT_035t").ToArray()) p.Opponent.DeckZone.Remove(c);
                int mine = p.DeckZone.Count, theirs = p.Opponent.DeckZone.Count;
                Play(g, p, id);
                var ambushes = p.Opponent.DeckZone.Where(c => c.Card.Id == "AT_035t").ToArray();
                Check(ambushes.Length == 3 && p.Opponent.DeckZone.Count == theirs + 3 && p.DeckZone.Count == mine, "three Ambushes are shuffled into the opponent's deck only");
                Check(p.HandZone.All(c => c.Card.Id != "AT_035t") && p.Opponent.HandZone.Count == 0, "no Ambush in either hand");
                seen.Add(p.Opponent.DeckZone.ToList().FindIndex(c => c.Card.Id == "AT_035t"));
                if (round == 0)
                {
                    HiddenSeat(g, p.Opponent.PlayerId, ambushes, "the opponent's own view of the shuffled Ambushes");
                    HiddenSeat(g, p.PlayerId, ambushes, "my view of the opponent's deck");
                    Resume(g, next => Check(next.Player2.DeckZone.Count(c => c.Card.Id == "AT_035t") == 3, "the Ambushes survive clone and snapshot"));
                }
            }
            Check(seen.Count >= 3, "the Ambushes are shuffled to random positions: " + string.Join(",", seen.OrderBy(x => x)));
            // drawing an Ambush
            StandardSetsProof.ParkBoard(p); ParkHand(p.Opponent);
            var opp = p.Opponent;
            var first = opp.DeckZone.First(c => c.Card.Id == "AT_035t");
            int deck = opp.DeckZone.Count;
            Generic.Draw(opp, first); g.DeathProcessingAndAuraUpdate();
            Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "AT_036t" && p.BoardZone.Single().AttackDamage == 4 && p.BoardZone.Single().Health == 4
                && p.BoardZone.Single().Controller == p, "drawing an Ambush summons a 4/4 Nerubian for me");
            Check(opp.DeckZone.Count == deck - 2 && opp.HandZone.Count == 1 && opp.HandZone.All(c => c.Card.Id != "AT_035t"), "the Ambush draws a card and is not kept: deck " + opp.DeckZone.Count + " hand " + opp.HandZone.Count);
            Check(opp.BoardZone.Count == 0, "nothing is summoned for the drawer");
            Kill(g, p.BoardZone.Single());
            Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "AT_036", "that Nerubian's Deathrattle summons Anub'arak (shared token): " + string.Join(",", p.BoardZone.Select(m => m.Card.Id)));
            // draw out the rest: two more Nerubians, never above seven minions
            StandardSetsProof.ParkBoard(p);
            while (opp.DeckZone.Count > 0 && g.State != State.COMPLETE) { ParkHand(opp); Generic.Draw(opp); g.DeathProcessingAndAuraUpdate(); }
            Check(p.BoardZone.Count(m => m.Card.Id == "AT_036t") == 2, "the other two Ambushes summon two more Nerubians: " + p.BoardZone.Count);
            // full board: no Nerubian, no failure
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 7; i++) Summon(p, Yeti);
            var packed = Entity.FromCard(opp, Cards.FromId("AT_035t"), zone: opp.DeckZone);
            Entity.FromCard(opp, Cards.FromId(Yeti), zone: opp.DeckZone);
            Generic.Draw(opp, packed); g.DeathProcessingAndAuraUpdate();
            Check(p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id == Yeti), "a full board gets no Nerubian");
            // the Ambush is drawn on the opponent's own turn too
            StandardSetsProof.ParkBoard(p);
            End(g);
            var turnAmbush = Entity.FromCard(opp, Cards.FromId("AT_035t"), zone: opp.DeckZone);
            Generic.Draw(opp, turnAmbush); g.DeathProcessingAndAuraUpdate();
            Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "AT_036t", "an Ambush drawn on the opponent's turn works the same");
            // a nearly full deck (99 cards at most) takes only what fits
            foreach (var c in opp.DeckZone.Where(c => c.Card.Id == "AT_035t").ToArray()) opp.DeckZone.Remove(c);
            while (opp.DeckZone.Count < 98) Entity.FromCard(opp, Cards.FromId(Wisp), zone: opp.DeckZone);
            End(g);
            Play(g, p, id);
            Check(opp.DeckZone.Count == 99 && opp.DeckZone.Count(c => c.Card.Id == "AT_035t") == 1, "a nearly full deck (99 max) takes only what fits: " + opp.DeckZone.Count + "/" + opp.DeckZone.Count(c => c.Card.Id == "AT_035t"));
        });

        WildSetsProof.Held("AT_036", (g, p, id) => {
            var anub = Summon(p, id);
            Check(anub.AttackDamage == 8 && anub.Health == 4 && anub.Card.Cost == 8 && anub.IsRace(Race.UNDEAD), "Anub'arak is an 8/4 Undead for 8");
            int hand = p.HandZone.Count;
            Kill(g, anub);
            Check(p.HandZone.Count == hand && p.HandZone.All(c => c.Card.Id != id), "he no longer returns to the hand");
            var nerub = p.BoardZone.Single();
            Check(nerub.Card.Id == "AT_036t" && nerub.AttackDamage == 4 && nerub.Health == 4 && nerub.HasDeathrattle, "a 4/4 Nerubian with a Deathrattle appears: " + nerub.Card.Id + " " + nerub.AttackDamage + "/" + nerub.Health + " dr " + nerub.HasDeathrattle + " task " + (nerub.Card.Power?.DeathrattleTask != null) + " tag " + nerub.Card[GameTag.DEATHRATTLE] + " ent " + nerub[GameTag.DEATHRATTLE]);
            Kill(g, nerub);
            var back = p.BoardZone.Single();
            Check(back.Card.Id == id && back.Id != anub.Id && back.AttackDamage == 8 && back.Health == 4 && back.Damage == 0, "the Nerubian's Deathrattle summons a fresh Anub'arak");
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[back.Id];
                Kill(next, n); var nb = next.Player1.BoardZone.Single();
                Check(nb.Card.Id == "AT_036t", "the chain continues on a clone");
                Kill(next, nb);
                Check(next.Player1.BoardZone.Single().Card.Id == id, "and back to Anub'arak");
            });
            Kill(g, back); var again = p.BoardZone.Single();
            Check(again.Card.Id == "AT_036t", "the chain goes round again");
            again.Silence(); g.DeathProcessingAndAuraUpdate();
            Kill(g, again);
            Check(p.BoardZone.Count == 0, "a Silenced Nerubian has no Deathrattle");
            var silenced = Summon(p, id); silenced.Silence(); g.DeathProcessingAndAuraUpdate();
            Kill(g, silenced);
            Check(p.BoardZone.Count == 0, "a Silenced Anub'arak has no Deathrattle");
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            var packed = Summon(p, id);
            Check(p.BoardZone.Count == 7, "full board");
            Kill(g, packed);
            Check(p.BoardZone.Count == 7 && p.BoardZone.Any(m => m.Card.Id == "AT_036t"), "the Nerubian takes the freed slot on a full board");
            Kill(g, p.BoardZone.First(m => m.Card.Id == "AT_036t"));
            Check(p.BoardZone.Count == 7 && p.BoardZone.Any(m => m.Card.Id == id), "and so does Anub'arak again");
            StandardSetsProof.ParkBoard(p);
            var foe = Summon(p.Opponent, id);
            Kill(g, foe);
            Check(p.Opponent.BoardZone.Single().Card.Id == "AT_036t" && p.BoardZone.Count == 0, "an enemy Anub'arak summons for the enemy");
        });

        WildSetsProof.Case("AT_038", (g, p, id) => {
            var asp = (Minion)PlayAt(g, p, id, 5);
            Check(asp.AttackDamage == 2 && asp.Health == 3, "Darnassus Aspirant is 2/3");
            Check(p.BaseMana == 6 && p.RemainingMana == 3, "Battlecry: an empty crystal (6 total, still 3 available): " + p.BaseMana + "/" + p.RemainingMana);
            Kill(g, asp);
            Check(p.BaseMana == 5, "Deathrattle: lose a Mana Crystal");
            var full = (Minion)PlayAt(g, p, id, 10);
            Check(p.BaseMana == 10, "at 10 crystals the Battlecry gains nothing");
            Kill(g, full);
            Check(p.BaseMana == 9, "the Deathrattle still loses one");
            var low = (Minion)PlayAt(g, p, id, 2);
            Check(p.BaseMana == 3, "gain at 2 crystals");
            p.UsedMana = 0;
            var silenced = (Minion)PlayAt(g, p, id, 4); silenced.Silence(); g.DeathProcessingAndAuraUpdate();
            int before = p.BaseMana;
            Kill(g, silenced);
            Check(p.BaseMana == before, "Silence removes the Deathrattle");
            Resume(g, next => {
                var owner = next.Player1; var n = (Minion)next.IdEntityDic[low.Id];
                int b = owner.BaseMana; Kill(next, n);
                Check(owner.BaseMana == b - 1, "the Deathrattle works on a clone");
            });
            p.BaseMana = 3; p.UsedMana = 3;
            Kill(g, low);
            Check(p.BaseMana == 2 && p.UsedMana <= 2, "losing a crystal never leaves more used than owned: " + p.UsedMana + "/" + p.BaseMana);
            End(g);
            p.Opponent.BaseMana = 5;
            var theirs = (Minion)PlayAt(g, p.Opponent, id, 5);
            Check(p.Opponent.BaseMana == 6 && p.BaseMana == 2, "the opponent's Aspirant affects the opponent only");
            Kill(g, theirs);
            Check(p.Opponent.BaseMana == 5 && p.BaseMana == 2, "and so does its Deathrattle");
        });

        WildSetsProof.Case("AT_039", (g, p, id) => {
            var combatant = (Minion)Play(g, p, id);
            Check(combatant.AttackDamage == 5 && combatant.Health == 4 && combatant.IsRace(Race.BEAST), "Savage Combatant is a 5/4 Beast");
            Use(g, p);
            Check(p.Hero.Weapon.Card.Id == "CS2_082" && p.Hero.AttackDamage == 3, "Inspire: +2 hero Attack this turn (dagger 1 + 2)");
            Check(!CanUse(p), "one Inspire per use");
            Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)) && p.Opponent.Hero.Damage == 3, "the hero hits for 3");
            End(g);
            Check(p.Hero.AttackDamage == 0, "the bonus expires at the end of the turn");
            Use(g, p.Opponent, p.Hero);
            Check(p.Hero.AttackDamage == 0 && p.Opponent.Hero.AttackDamage == 0, "the opponent's hero power does not Inspire");
            End(g);
            Resume(g, next => {
                var owner = next.Player1; Use(next, owner);
                Check(owner.Hero.AttackDamage == 3, "Inspire on a clone (the new dagger replaced the old one)");
            });
            var second = (Minion)Play(g, p, id);
            Use(g, p);
            Check(p.Hero.AttackDamage == 1 + 4, "two Combatants give +4");
            End(g); End(g);
            second.Silence(); g.DeathProcessingAndAuraUpdate();
            Use(g, p);
            Check(p.Hero.AttackDamage == 3, "a Silenced Combatant no longer Inspires");
            Kill(g, combatant);
            End(g); End(g);
            Use(g, p);
            Check(p.Hero.AttackDamage == 1, "no Combatant left");
            // a hero without a weapon: the enemy Mage
            End(g);
            var theirs = (Minion)Play(g, p.Opponent, id);
            Use(g, p.Opponent, p.Hero);
            Check(p.Opponent.Hero.AttackDamage == 2 && p.Opponent.Hero.Weapon == null, "a weaponless hero attacks for 2");
            int hurt = p.Hero.Damage;
            Check(g.Process(HeroAttackTask.Any(p.Opponent, p.Hero)) && p.Hero.Damage == hurt + 2, "and can attack for 2");
        });

        WildSetsProof.Case("AT_040", (g, p, id) => {
            var none = (Minion)Play(g, p, id);
            Check(none.AttackDamage == 4 && none.Health == 4, "playable without a Beast target");
            var beast = Summon(p, Raptor); var other = Summon(p, Yeti); var enemyBeast = Summon(p.Opponent, Raptor);
            var held = Add(p, id);
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id && t.Target != null).ToArray();
            Check(targets.Select(t => t.Target.Id).Distinct().SequenceEqual(new[] { beast.Id }), "only a friendly Beast is a legal target: " + string.Join(",", targets.Select(t => t.Target.Card.Id + "/" + t.Target.Controller.PlayerId)));
            Check(!p.Options().Any(t => t.Source?.Id == held.Id && t.Target is Hero), "no hero target");
            beast.Damage = 1;
            PlayEntity(g, p, held, beast);
            Check(beast.Health == 4 && beast.Damage == 1 && beast.AttackDamage == 3, "+3 Health on a damaged Beast keeps its damage: " + beast.Health);
            End(g); End(g);
            Check(beast.Health == 4, "the buff is permanent");
            Resume(g, next => {
                var n = (Minion)next.IdEntityDic[beast.Id];
                Play(next, next.Player1, id, n);
                Check(n.Health == 7, "stacks on a clone");
            });
            beast.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(beast.Health == 2 && beast.Damage == 0, "Silence removes the buff and the damage it absorbed");
            Check(other.Health == 5 && enemyBeast.Health == 2, "non-targets are untouched");
            Kill(g, beast);
            var again = Add(p, id);
            Check(p.Options().Any(t => t.Source?.Id == again.Id && t.Target == null) && !p.Options().Any(t => t.Source?.Id == again.Id && t.Target != null), "no Beast left: it is played without a target");
        });

        WildSetsProof.Case("AT_041", (g, p, id) => {
            var knight = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
            Summon(p, Raptor); Summon(p, Raptor);
            Summon(p.Opponent, Raptor);
            Summon(p, Yeti);
            var drawn = Generic.Draw(p, knight);
            Check(drawn.Card.Id == id && drawn.Cost == 5, "Beasts summoned before it was drawn count (2): cost " + drawn.Cost);
            StandardSetsProof.ParkBoard(p);
            Summon(p, Raptor);
            Check(drawn.Cost == 4, "another Beast makes it cheaper while in hand");
            Summon(p.Opponent, Raptor); Summon(p, Yeti);
            Check(drawn.Cost == 4, "enemy Beasts and non-Beasts do not count");
            var raptor = Play(g, p, Raptor);
            Check(drawn.Cost == 3, "a played Beast counts too");
            foreach (var m in p.BoardZone.Where(m => m.Card.Id == Raptor).ToArray()) Kill(g, m);
            Check(drawn.Cost == 3, "dead Beasts still count (summoned this game)");
            Resume(g, next => {
                var n = next.IdEntityDic[drawn.Id];
                Check(n.Cost == 3, "the count survives clone and snapshot");
                Summon(next.Player1, Raptor);
                Check(n.Cost == 2, "and keeps counting");
            });
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, Raptor);
            StandardSetsProof.ParkBoard(p);
            Check(drawn.Cost == 0, "the Cost never goes below 0");
            PlayEntity(g, p, drawn);
            Check(p.BoardZone.Any(m => m.Card.Id == id && m.AttackDamage == 6 && m.Health == 6), "it is played as a 6/6 for the reduced Cost");
            var second = Add(p, id);
            Check(second.Cost == 0, "a Knight created later sees the same count");
            // a Knight on the other side: only their own Beasts count
            End(g);
            var foe = Entity.FromCard(p.Opponent, Cards.FromId(id), zone: p.Opponent.HandZone);
            Check(foe.Cost == 7 - 2, "the enemy Knight counts the enemy's two summoned Beasts: " + foe.Cost);
            // a Knight in the starting deck, with and without the presentation history (simulations run without it)
            foreach (bool history in new[] { true, false })
            {
                var deck = Enumerable.Repeat(Cards.FromId(Yeti), 29).Append(Cards.FromId(id)).ToList();
                var game = new Game(new GameConfig { RandomSeed = 5, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                    Player1HeroClass = CardClass.DRUID, Player2HeroClass = CardClass.MAGE, History = history, Logging = false,
                    Player1Deck = deck, Player2Deck = Enumerable.Repeat(Cards.FromId(Yeti), 30).ToList(), FormatType = FormatType.FT_WILD });
                game.StartGame();
                var owner = game.Player1;
                var starting = owner.DeckZone.Single(c => c.Card.Id == id);
                Summon(owner, Raptor); Summon(owner, Raptor); Summon(owner, Raptor);
                var held = Generic.Draw(owner, starting);
                Check(held.Cost == 4, "a starting-deck Knight sees the Beasts summoned before it was drawn (history " + history + "): " + held.Cost);
                Summon(owner, Raptor);
                Check(held.Cost == 3, "and keeps counting (history " + history + ")");
                var copy = game.CloneExact(history: history);
                Check(copy.IdEntityDic[held.Id].Cost == 3, "the clone agrees (history " + history + ")");
            }
        });

        WildSetsProof.Held("AT_042", "held: FX-MISSING live (choose:AT_042a, choose:AT_042b)", (g, p, id) => {
            var lion = (Minion)PlayChoice(g, p, id, 1);
            lion = p.BoardZone.Single();
            Check(lion.Card.Id == "AT_042t" && lion.AttackDamage == 2 && lion.Health == 1 && lion.HasCharge && !lion.HasStealth, "Lion form: a 2/1 with Charge");
            Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == lion.Id), "Charge: it can attack at once");
            var panther = (Minion)PlayChoice(g, p, id, 2);
            panther = p.BoardZone.Single(m => m.Id != lion.Id);
            Check(panther.Card.Id == "AT_042t2" && panther.AttackDamage == 3 && panther.Health == 2 && panther.HasStealth && !panther.HasCharge, "Panther form: a 3/2 with Stealth");
            Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == panther.Id), "no Charge: it cannot attack at once");
            Check(p.BoardZone.Count == 2, "the Druid transforms in place");
            Resume(g, next => {
                var n = PlayChoice(next, next.Player1, id, 1);
                Check(next.Player1.BoardZone.Count == 3 && next.Player1.BoardZone.Last().Card.Id == "AT_042t", "Choose One works on a clone");
            });
            lion.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(!lion.HasCharge && lion.AttackDamage == 2, "Silence removes Charge");
            StandardSetsProof.ParkBoard(p);
            Summon(p, "OG_044");
            var both = (Minion)Play(g, p, id);
            var made = p.BoardZone.Single(m => m.Card.Id != "OG_044");
            Check(made.Card.Id == "OG_044c" && made.AttackDamage == 3 && made.Health == 2 && made.HasCharge && made.HasStealth, "with Fandral Staghelm: a 3/2 with Charge and Stealth");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            PlayChoice(g, p, id, 1);
            Check(p.BoardZone.Count == 7 && p.BoardZone.Any(m => m.Card.Id == "AT_042t"), "it can be played into the last slot");
            End(g);
            PlayChoice(g, p.Opponent, id, 2);
            Check(p.Opponent.BoardZone.Single().Card.Id == "AT_042t2" && p.Opponent.BoardZone.Single().Controller == p.Opponent, "the opponent's Druid transforms on their side");
        });

        WildSetsProof.Case("AT_043", (g, p, id) => {
            var held = Add(p, id);
            p.BaseMana = 4; p.UsedMana = 0;
            Check(held.Cost == 5 && !p.Options().Any(t => t.Source?.Id == held.Id), "Astral Communion costs 5 and is unplayable with 4");
            ParkHand(p);
            var others = new[] { Add(p, Yeti), Add(p, Wisp), Add(p, Raptor) };
            var hidden = PlayAt(g, p, id, 5);
            Check(p.BaseMana == 10 && p.RemainingMana == 10 && p.UsedMana == 0, "10 full Mana Crystals: " + p.BaseMana + "/" + p.RemainingMana);
            Check(p.HandZone.Count == 0 && others.All(c => c.Zone.Type == Zone.GRAVEYARD), "the hand is discarded");
            Check(!p.HandZone.Any(c => c.Card.Id == "CS2_013t"), "no Excess Mana below 10 crystals");
            Play(g, p, "CS2_182");
            Check(p.BoardZone.Count == 1, "everything is playable at 10 mana");
            ParkHand(p);
            var full = new[] { Add(p, Yeti), Add(p, Wisp) };
            PlayAt(g, p, id, 10);
            Check(p.BaseMana == 10 && p.HandZone.Count == 1 && p.HandZone.Single().Card.Id == "CS2_013t", "at 10 crystals the hand is discarded and Excess Mana is added");
            Check(full.All(c => c.Zone.Type == Zone.GRAVEYARD), "the cards in hand were discarded first");
            ParkHand(p);
            Entity.FromCard(p, Cards.FromId("AT_022"), zone: p.HandZone);
            p.Opponent.Hero.Armor = 100;
            PlayAt(g, p, id, 7);
            Check(p.Opponent.Hero.Armor == 96, "a discarded Fist of Jaraxxus still triggers (4 to a random enemy)");
            ParkHand(p);
            var theirsHand = Entity.FromCard(p.Opponent, Cards.FromId(Yeti), zone: p.Opponent.HandZone);
            PlayAt(g, p, id, 6);
            Check(theirsHand.Zone.Type == Zone.HAND && p.Opponent.BaseMana == 10, "the opponent's hand and crystals are untouched");
            Resume(g, next => {
                var owner = next.Player1; ParkHand(owner);
                Entity.FromCard(owner, Cards.FromId(Wisp), zone: owner.HandZone);
                PlayAt(next, owner, id, 6);
                Check(owner.BaseMana == 10 && owner.RemainingMana == 10 && owner.HandZone.Count == 0, "Astral Communion on a clone");
            });
            End(g);
            ParkHand(p.Opponent);
            Entity.FromCard(p.Opponent, Cards.FromId(Wisp), zone: p.Opponent.HandZone);
            PlayAt(g, p.Opponent, id, 5);
            Check(p.Opponent.BaseMana == 10 && p.Opponent.HandZone.Count == 0 && p.HandZone.Count == 0, "the opponent's Astral Communion is their own");
        });

        WildSetsProof.Case("AT_044", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var friend = Summon(p, Yeti);
            var held = Add(p, id);
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
            Check(targets.Length == 2 && targets.All(t => t.Target is Minion), "any minion, never a hero");
            ParkHand(p.Opponent);
            var seen = new HashSet<string>(); var classes = new HashSet<CardClass>();
            PlayEntity(g, p, held, enemy);
            Check(enemy.Zone.Type == Zone.GRAVEYARD, "Mulch destroys an enemy minion");
            var got = p.Opponent.HandZone.ToArray();
            Check(got.Length == 1 && got[0].Card.Type == CardType.MINION && got[0].Card.Collectible && got[0].Controller == p.Opponent, "a random collectible minion goes to the opponent's hand");
            HiddenFrom(g, p.Opponent, got, "my view of the opponent's new minion");
            for (int i = 0; i < 20; i++)
            {
                ParkHand(p.Opponent);
                var victim = Summon(p.Opponent, Wisp);
                Play(g, p, id, victim);
                var card = p.Opponent.HandZone.Single();
                Check(card.Card.Type == CardType.MINION, "always a minion");
                seen.Add(card.Card.Id); classes.Add(card.Card.Class);
            }
            Check(seen.Count >= 8 && classes.Count >= 3, "random across the pool: " + seen.Count + " minions, " + classes.Count + " classes");
            ParkHand(p.Opponent);
            Play(g, p, id, friend);
            Check(friend.Zone.Type == Zone.GRAVEYARD && p.Opponent.HandZone.Count == 1, "a friendly minion can be destroyed, the opponent still gets the card");
            ParkHand(p.Opponent);
            for (int i = 0; i < 10; i++) Entity.FromCard(p.Opponent, Cards.FromId(Wisp), zone: p.Opponent.HandZone);
            var again = Summon(p.Opponent, Yeti);
            Play(g, p, id, again);
            Check(again.Zone.Type == Zone.GRAVEYARD && p.Opponent.HandZone.Count == 10 && p.Opponent.HandZone.All(c => c.Card.Id == Wisp), "a full opponent hand gets nothing: " + again.Zone.Type + " " + p.Opponent.HandZone.Count + " " + string.Join(",", p.Opponent.HandZone.Select(c => c.Card.Id).Distinct()));
            ParkHand(p.Opponent);
            var bystander = Summon(p.Opponent, Yeti);
            var rattle = Summon(p.Opponent, "FP1_007");
            Play(g, p, id, rattle);
            Check(p.Opponent.BoardZone.Any(m => m.Card.Id == "FP1_007t") && p.Opponent.HandZone.Count == 1, "the destroyed minion's Deathrattle resolves");
            Resume(g, next => {
                var owner = next.Player1; ParkHand(next.Player2);
                var victim = Summon(next.Player2, Yeti);
                Play(next, owner, id, victim);
                Check(next.Player2.HandZone.Count == 1 && victim.Zone.Type == Zone.GRAVEYARD, "Mulch on a clone");
            });
            Check(bystander.Zone.Type == Zone.PLAY, "other minions are untouched");
        });
    }

    // ---- BLOCKED-STANDARD proofs: Standard uses these ids; written, never registered ---------------------------------

    private static void Blocked()
    {
        Unregistered("AT_023", (g, p, id) => {
            var crusher = (Minion)Play(g, p, id);
            Check(crusher.AttackDamage == 5 && crusher.Health == 4 && crusher.IsRace(Race.DEMON), "Void Crusher is a 5/4 Demon");
            StandardSetsProof.ParkBoard(p);
            var a = Summon(p, Yeti); var b = Summon(p, Wisp); var c = Summon(p, Raptor);
            var e1 = Summon(p.Opponent, Yeti); var e2 = Summon(p.Opponent, Raptor); var e3 = Summon(p.Opponent, Wisp);
            var vc = Summon(p, id);
            Use(g, p);
            var mine = new[] { a, b, c, vc }; var theirs = new[] { e1, e2, e3 };
            Check(mine.Count(m => m.Zone.Type == Zone.GRAVEYARD) == 1 && theirs.Count(m => m.Zone.Type == Zone.GRAVEYARD) == 1, "Inspire destroys one random minion of each player");
            var victims = new HashSet<string>(); var enemyVictims = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
                var m1 = Summon(p, Yeti); var m2 = Summon(p, Raptor); var self = Summon(p, id);
                var f1 = Summon(p.Opponent, Yeti); var f2 = Summon(p.Opponent, Raptor); var f3 = Summon(p.Opponent, Wisp);
                p.Hero.HeroPower.IsExhausted = false;
                Use(g, p);
                foreach (var m in new[] { m1, m2, self }.Where(m => m.Zone.Type == Zone.GRAVEYARD)) victims.Add(m.Card.Id);
                foreach (var m in new[] { f1, f2, f3 }.Where(m => m.Zone.Type == Zone.GRAVEYARD)) enemyVictims.Add(m.Card.Id);
            }
            Check(victims.Count >= 3 && enemyVictims.Count >= 3, "both picks are random and can include Void Crusher itself: " + string.Join(",", victims) + " | " + string.Join(",", enemyVictims));
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var alone = Summon(p, id);
            End(g); End(g);
            Use(g, p);
            Check(alone.Zone.Type == Zone.GRAVEYARD, "with no other minion it destroys itself; an empty enemy board is fine");
            StandardSetsProof.ParkBoard(p);
            var silenced = Summon(p, id); silenced.Silence(); g.DeathProcessingAndAuraUpdate();
            var spare = Summon(p.Opponent, Yeti);
            End(g); End(g);
            Use(g, p);
            Check(silenced.Zone.Type == Zone.PLAY && spare.Zone.Type == Zone.PLAY, "Silence removes Inspire");
            Resume(g, next => {
                var owner = next.Player1; Summon(owner, id);
                End(next); End(next); Use(next, owner);
                Check(next.Player2.BoardZone.Count == 0, "Inspire on a clone");
            });
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            var mineVc = Summon(p, id); var bystander = Summon(p.Opponent, Yeti);
            End(g);
            Use(g, p.Opponent, p.Hero);
            Check(mineVc.Zone.Type == Zone.PLAY && bystander.Zone.Type == Zone.PLAY, "an enemy Hero Power does not Inspire my Void Crusher");
            End(g); End(g);
            var theirsVc = Summon(p.Opponent, id);
            Use(g, p.Opponent, p.Hero);
            Check(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 1, "an enemy Void Crusher Inspires for the enemy: one of theirs, one of mine");
        });

        Unregistered("AT_037", (g, p, id) => {
            var foe = Summon(p.Opponent, Yeti);
            var spell = Add(p, id);
            Check(spell.Cost == 1, "Living Roots costs 1");
            var damage = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id && t.Target?.Id == foe.Id && t.ChooseOne == 1);
            Check(g.Process(damage) && foe.Damage == 2, "option 1 deals 2 damage");
            var saplings = Add(p, id);
            var summon = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == saplings.Id && t.ChooseOne == 2);
            Check(summon != null && g.Process(summon), "option 2 is playable");
            Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "AT_037t" && m.AttackDamage == 1 && m.Health == 1), "two 1/1 Saplings");
            Summon(p, Thalnos);
            Play(g, p, id, foe);
            Check(foe.Damage == 5, "Spell Damage applies to the damage branch (2+1)");
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

    /// <summary>Runs a scenario for a card the suite must not register or change; prints PASS or FAIL, never throws.</summary>
    private static void Unregistered(string id, Action<Game, Controller, string> scenario, string reason = "BLOCKED-STANDARD")
    {
        try
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var g = Fixture(seed);
                scenario(g, g.Player1, id);
                Check(Canonical.State(g) == Canonical.State(g.CloneExact(history: true)), id + " exact clone");
                Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshotWithPools(g.SaveSnapshot(), g.OfflinePools)), id + " snapshot");
            }
            Console.WriteLine("PASS (unregistered, " + reason + ") " + id);
        }
        catch (Exception error) { Console.WriteLine("FAIL (unregistered, " + reason + ") " + id + ": " + error.Message); }
    }

    private static void Use(Game g, Controller c, ICharacter target = null)
    {
        c.BaseMana = 10; c.UsedMana = 0; c.TemporaryMana = 0;
        Check(g.Process(HeroPowerTask.Any(c, target)), "hero power accepted");
    }

    private static bool CanUse(Controller c) => c.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    /// <summary>Installs another Hero Power for <paramref name="c"/> (the proof games are Rogue vs Mage).</summary>
    private static void SetPower(Controller c, string id)
    {
        var power = (HeroPower)Entity.FromCard(c, Cards.FromId(id));
        c.SetasideZone.Add(c.Hero.HeroPower);
        c.Hero.HeroPower = power;
        BasicHeroPowerRules.EnrollPowerAuras(c, power);
        c.Game.AuraUpdate();
    }

    /// <summary>Kills a minion and flushes the summons its Deathrattle made (the next cleanup pass), as a real turn does.</summary>
    private static void Kill(Game g, Minion minion)
    {
        Core2026Proof.Kill(g, minion);
        g.DeathProcessingAndAuraUpdate();
    }

    private static void ParkHand(Controller p) { foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); } }

    /// <summary>Plays a card with the mana set as given (Play always refills to 10).</summary>
    private static IPlayable PlayAt(Game g, Controller c, string id, int baseMana, ICharacter target = null)
    {
        var card = Add(c, id);
        c.BaseMana = baseMana; c.UsedMana = 0; c.TemporaryMana = 0;
        var task = c.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.Target?.Id == target?.Id);
        Check(task != null, "legal play missing: " + id);
        Check(g.Process(task), "legal play rejected: " + id);
        return card;
    }

    private static IPlayable PlayChoice(Game g, Controller c, string id, int mode)
    {
        var card = Add(c, id);
        c.BaseMana = 10; c.UsedMana = 0; c.TemporaryMana = 0;
        var task = c.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id && t.ChooseOne == mode);
        Check(task != null, "choose-one option " + mode + " missing for " + id);
        Check(g.Process(task), "choose-one option rejected");
        return card;
    }

    /// <summary>No card of <paramref name="hidden"/> identifies itself in the fair view of the opponent of <paramref name="owner"/>.</summary>
    private static void HiddenFrom(Game g, Controller owner, IEnumerable<IPlayable> hidden, string why)
    {
        var ids = hidden.Select(e => e.Card.Id).ToHashSet();
        var before = Canonical.State(g); string history = g.HistoryFacts.CanonicalText(); var random = g.Random.CaptureState();
        var fair = FairBotState.Create(g, owner.Opponent.PlayerId, 83, DeckStyle.Value);
        var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        Check(!view.IdEntityDic.Values.Any(e => e.Controller.PlayerId == owner.PlayerId
            && (e.Zone?.Type is Zone.HAND or Zone.DECK or Zone.SETASIDE or Zone.SECRET) && ids.Contains(e.Card.Id)), why + ": identity visible in the fair view");
        Check(before == Canonical.State(g) && history == g.HistoryFacts.CanonicalText() && random.Equals(g.Random.CaptureState()),
            why + ": the projection leaves the game untouched");
    }

    /// <summary>No card of <paramref name="hidden"/> sits in a hidden zone (hand, deck, set aside, secret) of either player in the fair view of seat <paramref name="viewer"/>.</summary>
    private static void HiddenSeat(Game g, int viewer, IEnumerable<IPlayable> hidden, string why)
    {
        var ids = hidden.Select(e => e.Card.Id).ToHashSet();
        var before = Canonical.State(g); string history = g.HistoryFacts.CanonicalText(); var random = g.Random.CaptureState();
        var fair = FairBotState.Create(g, viewer, 83, DeckStyle.Value);
        var view = (Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        Check(!view.IdEntityDic.Values.Any(e => (e.Zone?.Type is Zone.HAND or Zone.DECK or Zone.SETASIDE or Zone.SECRET) && ids.Contains(e.Card.Id)),
            why + ": identity visible in the fair view");
        Check(before == Canonical.State(g) && history == g.HistoryFacts.CanonicalText() && random.Equals(g.Random.CaptureState()),
            why + ": the projection leaves the game untouched");
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        Game[] copies;
        try { copies = new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) }; }
        catch (Exception error) { throw new Exception("clone/snapshot inside the scenario: " + error.Message, error); }
        foreach (var next in copies)
        {
            Check(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }
}
