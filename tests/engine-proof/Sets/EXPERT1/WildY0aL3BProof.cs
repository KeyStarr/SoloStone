using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Wild wave Y0a, lane l3b: Expert Rogue and Mage cards. The group proof is declared here (group names are unique) and
// calls the Legacy half in Sets/LEGACY/WildY0aL3BProof.cs.
internal static class WildY0aL3BProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case (used by Standard until #87).
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    private const string Yeti = "CS2_182", Squire = "EX1_008", Thalnos = "EX1_012", Shieldmasta = "CS2_179", Fireball = "CS2_029",
        Coin = "GAME_005", Counterspell = "EX1_287";

    [GroupProof("wild-y0a-l3b-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildY0aL3BLegacyProof.Run();
        WildSetsProof.Case("CS2_233", (g, p, id) => {
            var held = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == held.Id), "Blade Flurry unplayable without a weapon");
            var enemy = Summon(p.Opponent, Yeti); var shield = Summon(p.Opponent, Squire); var mine = Summon(p, Yeti);
            Play(g, p, "CS2_080");
            PlayEntity(g, p, held);
            Core2026Proof.Check(enemy.Damage == 2 && shield.Zone.Type == Zone.PLAY && !shield.HasDivineShield && shield.Damage == 0,
                "Blade Flurry deals the weapon's Attack to every enemy minion (Divine Shield absorbs)");
            Core2026Proof.Check(mine.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Blade Flurry spares friendly minions and both heroes");
            Core2026Proof.Check(p.Hero.Weapon == null && p.Hero.AttackDamage == 0, "Blade Flurry destroys the weapon");
            Play(g, p, "CS2_106"); Play(g, p, "CS2_074");
            Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 5, "Fiery War Axe plus Deadly Poison has 5 Attack");
            var big = Summon(p.Opponent, "CS2_186"); // War Golem 7/7: takes the full 5, so Attack 4 or 6 would show
            Play(g, p, id);
            Core2026Proof.Check((enemy.Zone.Type == Zone.GRAVEYARD && shield.Zone.Type == Zone.GRAVEYARD) && big.Damage == 5,
                "Blade Flurry uses the buffed weapon Attack: it kills the 3 remaining Health and the 1/1, and deals exactly 5 to a 7/7");
            Core2026Proof.Check(p.Hero.Weapon == null, "Blade Flurry destroyed the buffed weapon");
            var late = Summon(p.Opponent, Yeti);
            Summon(p, Thalnos); Play(g, p, "CS2_080"); Play(g, p, id);
            Core2026Proof.Check(late.Damage == 3 && mine.Damage == 0, "Blade Flurry scales with Spell Damage (tag 370): 2 Attack plus 1");
            StandardSetsProof.ParkBoard(p.Opponent);
            Play(g, p, "CS2_080"); Play(g, p, id);
            Core2026Proof.Check(p.Hero.Weapon == null && p.Opponent.Hero.Damage == 0, "Blade Flurry on an empty enemy board still destroys the weapon");
            End(g);
            Play(g, p.Opponent, "CS2_106"); Play(g, p.Opponent, id);
            Core2026Proof.Check(mine.Damage == 3 && p.Opponent.Hero.Weapon == null && p.Hero.Damage == 0, "Opponent's Blade Flurry hits only my minions with their weapon");
        });
        WildSetsProof.Case("EX1_124", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti);
            var held = Add(p, id);
            Core2026Proof.Check(WildY0aL3ALegacyProof.Targets(p, held).Count == 4, "Eviscerate targets any character");
            PlayEntity(g, p, held, enemy);
            Core2026Proof.Check(enemy.Damage == 2, "Eviscerate deals 2 without Combo");
            Play(g, p, Coin);
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Eviscerate deals 4 with Combo: 2 + 4 kills a 5 Health minion");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "Eviscerate Combo hits the enemy hero for 4");
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 4, "Eviscerate may hit the own hero");
            Summon(p, Thalnos);
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.Zone.Type == Zone.GRAVEYARD, "Eviscerate Combo scales with Spell Damage (4 + 1 kills a 5 Health minion) dmg=" + mine.Damage + " zone=" + mine.Zone.Type + " sp=" + p.CurrentSpellPower);
            End(g); End(g);
            var fresh = Summon(p.Opponent, Yeti);
            Play(g, p, id, fresh);
            Core2026Proof.Check(fresh.Damage == 3, "Combo ended with the turn: Eviscerate deals 2 plus Spell Damage");
            var squire = Summon(p.Opponent, Squire);
            Play(g, p, id, squire);
            Core2026Proof.Check(squire.Zone.Type == Zone.PLAY && !squire.HasDivineShield && squire.Damage == 0, "Eviscerate only pops Divine Shield");
            End(g);
            var enemyMinion = Summon(p, Yeti);
            Play(g, p.Opponent, Coin); Play(g, p.Opponent, id, enemyMinion);
            Core2026Proof.Check(enemyMinion.Damage == 4, "Opponent's Combo counts their own cards played");
        });
        WildSetsProof.Case("EX1_126", (g, p, id) => {
            var left = Summon(p.Opponent, Yeti); var target = Summon(p.Opponent, Shieldmasta); var right = Summon(p.Opponent, Yeti);
            var mine = Summon(p, Yeti);
            var held = Add(p, id);
            var targets = WildY0aL3ALegacyProof.Targets(p, held);
            Core2026Proof.Check(targets.Count == 3 && targets.All(t => t is Minion m && m.Controller == p.Opponent), "Betrayal targets enemy minions only");
            PlayEntity(g, p, held, target);
            Core2026Proof.Check(left.Damage == 3 && right.Damage == 3 && target.Damage == 0, "Betrayal makes the target deal its Attack to both neighbors");
            Core2026Proof.Check(mine.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Betrayal damages nothing else");
            Summon(p, Thalnos);
            Play(g, p, id, target);
            Core2026Proof.Check(left.Zone.Type == Zone.GRAVEYARD && right.Zone.Type == Zone.GRAVEYARD && target.Damage == 0,
                "Betrayal repeats and kills at exact Health; Spell Damage does not boost it");
            var a = Summon(p.Opponent, Squire); var edge = Summon(p.Opponent, Yeti); var b = Summon(p.Opponent, Yeti);
            Core2026Proof.Check(p.Opponent.BoardZone[0] == target && p.Opponent.BoardZone[1] == a, "setup board order");
            Play(g, p, id, target);
            Core2026Proof.Check(!a.HasDivineShield && a.Damage == 0 && edge.Damage == 0 && a.Zone.Type == Zone.PLAY,
                "Betrayal on the left edge hits only the right neighbor (Divine Shield absorbs)");
            Play(g, p, id, b);
            Core2026Proof.Check(edge.Damage == 4 && b.Damage == 0, "Betrayal on the right edge hits only the left neighbor with the target's 4 Attack");
            WildY0aL3BProof.ParkExcept(p.Opponent, b);
            Play(g, p, id, b);
            Core2026Proof.Check(b.Damage == 0 && p.Opponent.Hero.Damage == 0, "Betrayal on a lone minion damages nothing");
            var image = Summon(p.Opponent, "CS2_mirror"); var other = Summon(p.Opponent, Yeti);
            Play(g, p, id, image);
            Core2026Proof.Check(other.Damage == 0 && b.Damage == 0, "Betrayal with a 0 Attack target deals nothing");
            var buffed = Summon(p.Opponent, Squire); var neighbor = Summon(p.Opponent, Yeti);
            Play(g, p, "CS2_188", buffed);
            Core2026Proof.Check(buffed.AttackDamage == 3, "Abusive Sergeant gives +2 Attack");
            Play(g, p, id, buffed);
            Core2026Proof.Check(neighbor.Damage == 3, "Betrayal uses the target's current Attack, buffs included");
            Core2026Proof.Check(!WildY0aL3ALegacyProof.Targets(p, Add(p, id)).Contains(mine), "Betrayal never targets a friendly minion");
        });
        WildSetsProof.Case("EX1_128", (g, p, id) => {
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Conceal with no minions does nothing");
            var a = Summon(p, Yeti); var b = Summon(p, Squire); var enemy = Summon(p.Opponent, Yeti);
            Core2026Proof.Check(!a.HasStealth && !b.HasStealth, "minions start unstealthed");
            Play(g, p, id);
            Core2026Proof.Check(a.HasStealth && b.HasStealth && !enemy.HasStealth, "Conceal gives Stealth to your minions only");
            var late = Summon(p, Yeti);
            Core2026Proof.Check(!late.HasStealth, "Conceal does not affect later summons");
            WildY0aL3ALegacyProof.Resume(g, next => {
                End(next);
                Core2026Proof.Check(((Minion)next.IdEntityDic[a.Id]).HasStealth && ((Minion)next.IdEntityDic[b.Id]).HasStealth,
                    "Conceal lasts through the opponent's turn");
                End(next);
                Core2026Proof.Check(!((Minion)next.IdEntityDic[a.Id]).HasStealth && !((Minion)next.IdEntityDic[b.Id]).HasStealth,
                    "Conceal expires when the caster's next turn begins");
            });
            Attack(g, b, p.Opponent.Hero);
            Core2026Proof.Check(!b.HasStealth && a.HasStealth, "Attacking breaks Stealth on that minion only");
            a.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(!a.HasStealth && a.IsSilenced, "Silence removes the granted Stealth");
            End(g);
            var theirs = Summon(p.Opponent, Yeti);
            Play(g, p.Opponent, id);
            Core2026Proof.Check(theirs.HasStealth && enemy.HasStealth && !late.HasStealth, "Opponent's Conceal stealths only their minions");
            End(g);
            Core2026Proof.Check(enemy.HasStealth && !late.HasStealth, "The caster's Stealth outlasts my turn start");
            End(g);
            Core2026Proof.Check(!enemy.HasStealth && !theirs.HasStealth, "Opponent's Conceal expires at their next turn start");
        });
        WildSetsProof.Case("EX1_133", (g, p, id) => {
            var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti);
            var held = Add(p, id);
            var targets = WildY0aL3ALegacyProof.Targets(p, held);
            Core2026Proof.Check(targets.Count == 4 && targets.Contains(p.Hero) && targets.Contains(p.Opponent.Hero), "Perdition's Blade Battlecry may target any character");
            PlayEntity(g, p, held, enemy);
            Core2026Proof.Check(enemy.Damage == 1 && p.Hero.Weapon.Card.Id == id && p.Hero.Weapon.AttackDamage == 2 && p.Hero.Weapon.Durability == 2,
                "Perdition's Blade equips a 2/2 weapon and deals 1");
            Play(g, p, Coin);
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Damage == 3, "Perdition's Blade Combo deals 2 instead of 1 dmg=" + enemy.Damage);
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && p.Hero.Weapon.Durability == 2, "Combo hits the enemy hero for 2 and replaces the weapon");
            End(g); End(g);
            Summon(p, Thalnos);
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.Damage == 1, "Without Combo it deals 1 and is not boosted by Spell Damage");
            Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 2, "Perdition's Blade may hit the own hero (Combo is active again after the first play this turn)");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "Perdition's Blade is playable with no minions, targeting heroes");
            End(g);
            Play(g, p.Opponent, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 3 && p.Opponent.Hero.Weapon.Card.Id == id && p.Hero.Weapon.AttackDamage == 2,
                "Opponent's Battlecry hits my hero and equips their own weapon");
        });
        WildSetsProof.Case("EX1_137", (g, p, id) => {
            int hand = p.HandZone.Count;
            var plain = Add(p, id);
            PlayEntity(g, p, plain);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && p.Hero.Damage == 0 && plain.Zone.Type != Zone.HAND, "Headcrack deals 2 to the enemy hero");
            End(g); End(g);
            Core2026Proof.Check(p.HandZone.All(c => c.Card.Id != id), "Without Combo Headcrack is not returned");
            Summon(p, Thalnos);
            Play(g, p, Coin);
            var combo = Add(p, id);
            PlayEntity(g, p, combo);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && combo.Zone.Type != Zone.HAND, "Headcrack Combo deals 2 plus Spell Damage dmg=" + p.Opponent.Hero.Damage);
            Core2026Proof.Check(p.HandZone.All(c => c.Card.Id != id), "Headcrack is not back in hand the turn it is cast");
            WildY0aL3ALegacyProof.Resume(g, next => {
                var mineNext = next.Player1; int own = mineNext.HandZone.Count, opp = next.Player2.HandZone.Count;
                End(next);
                Core2026Proof.Check(mineNext.HandZone.Count(c => c.Card.Id == id) == 1 && next.Player2.HandZone.All(c => c.Card.Id != id),
                    "Headcrack returns to its owner's hand and never to the opponent's");
                End(next);
                Core2026Proof.Check(mineNext.HandZone.Count(c => c.Card.Id == id) == 1, "Headcrack returns exactly once");
            });
            End(g);
            Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == id) == 1 && p.Opponent.HandZone.All(c => c.Card.Id != id), "Headcrack Combo returns Headcrack to the owner's hand");
            End(g);
            var again = p.HandZone.First(c => c.Card.Id == id);
            PlayEntity(g, p, again);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 8 && again.Zone.Type != Zone.HAND, "Returned Headcrack castable again");
            End(g); End(g);
            Core2026Proof.Check(p.HandZone.All(c => c.Card.Id != id), "A Combo-less recast does not return");
            Play(g, p, Coin);
            while (p.HandZone.Count < 9) Add(p, Yeti);
            var full = Add(p, id);
            PlayEntity(g, p, full);
            Add(p, Yeti); Core2026Proof.Check(p.HandZone.Count == 10, "setup full hand");
            End(g);
            Core2026Proof.Check(p.HandZone.All(c => c.Card.Id != id) && p.HandZone.Count == 10, "Headcrack burns when the hand is full at return time");
            Play(g, p.Opponent, Coin);
            var theirs = Add(p.Opponent, id);
            PlayEntity(g, p.Opponent, theirs, null);
            Core2026Proof.Check(p.Hero.Damage == 2, "Opponent's Headcrack hits my hero");
            End(g);
            Core2026Proof.Check(p.Opponent.HandZone.Any(c => c.Card.Id == id) && p.HandZone.All(c => c.Card.Id != id), "Opponent's Headcrack returns to their own hand");
        });
        WildSetsProof.Case("EX1_144", (g, p, id) => {
            var yeti = Summon(p, Yeti); var thalnos = Summon(p, Thalnos); var squire = Summon(p, Squire); var enemy = Summon(p.Opponent, Yeti);
            var held = Add(p, id);
            var targets = WildY0aL3ALegacyProof.Targets(p, held);
            Core2026Proof.Check(targets.Count == 3 && targets.All(t => t is Minion m && m.Controller == p), "Shadowstep targets friendly minions only");
            Play(g, p, "CS2_188", yeti); yeti.Damage = 2;
            PlayEntity(g, p, held, yeti);
            var card = p.HandZone.Single(c => c.Card.Id == Yeti);
            Core2026Proof.Check(card.Cost == 2 && yeti.Zone.Type != Zone.PLAY, "Shadowstep returns the Yeti costing 2 less");
            PlayEntity(g, p, card);
            var replay = (Minion)card;
            Core2026Proof.Check(replay.Zone.Type == Zone.PLAY && replay.AttackDamage == 4 && replay.Damage == 0, "the replayed minion pays 2 and has no leftover buff or damage");
            int deck = p.DeckZone.Count;
            Play(g, p, id, thalnos);
            Core2026Proof.Check(p.HandZone.Single(c => c.Card.Id == Thalnos).Cost == 0 && p.DeckZone.Count == deck, "Shadowstep returns Thalnos at cost 0 and no Deathrattle runs");
            Play(g, p, id, squire);
            Core2026Proof.Check(p.HandZone.Single(c => c.Card.Id == Squire).Cost == 0, "Shadowstep cannot reduce a 1 cost below 0");
            Core2026Proof.Check(enemy.Zone.Type == Zone.PLAY && enemy.Controller == p.Opponent, "Shadowstep leaves the enemy alone");
            WildY0aL3ALegacyProof.Resume(g, next => {
                var back = next.Player1.HandZone.Single(c => c.Card.Id == Thalnos);
                End(next); End(next);
                Core2026Proof.Check(back.Cost == 0, "Shadowstep discount outlasts the turn");
            });
            Play(g, p, id, replay);
            Core2026Proof.Check(replay.Zone.Type == Zone.HAND && replay.Cost == 2, "Shadowstepping the same card again still costs 2");
            End(g);
            var theirs = Summon(p.Opponent, Yeti);
            Play(g, p.Opponent, id, theirs);
            Core2026Proof.Check(p.Opponent.HandZone.Any(c => c.Card.Id == Yeti && c.Cost == 2) && enemy.Zone.Type == Zone.PLAY, "Opponent's Shadowstep bounces their own minion");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_145", (g, p, id) => {
            var spell = Add(p, Fireball); var minionCard = Add(p, Yeti); var oppSpell = Add(p.Opponent, Fireball);
            Core2026Proof.Check(spell.Cost == 4, "Fireball costs 4");
            Play(g, p, id);
            Core2026Proof.Check(spell.Cost == 2 && minionCard.Cost == 4 && oppSpell.Cost == 4, "Preparation discounts only your spells by 2");
            PlayEntity(g, p, minionCard);
            Core2026Proof.Check(spell.Cost == 2, "casting a minion does not use Preparation");
            var cheap = Add(p, "EX1_180"); var weapon = Add(p, "CS2_106"); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(cheap.Cost == 0 && weapon.Cost == 3, "Preparation floors at 0 and ignores weapons cheap=" + cheap.Cost + " weapon=" + weapon.Cost);
            PlayEntity(g, p, spell, p.Opponent.Hero);
            Core2026Proof.Check(p.UsedMana == 2 && p.Opponent.Hero.Damage == 6, "the discounted spell cost 2");
            Core2026Proof.Check(Add(p, Fireball).Cost == 4 && cheap.Cost == 1, "Preparation affects only the next spell");
            Play(g, p, id);
            var held = Add(p, Fireball); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(held.Cost == 2, "second Preparation discounts again");
            WildY0aL3ALegacyProof.Resume(g, next => {
                var inHand = next.IdEntityDic[held.Id];
                Core2026Proof.Check(((IPlayable)inHand).Cost == 2, "Preparation survives clone and snapshot");
                End(next);
                Core2026Proof.Check(((IPlayable)inHand).Cost == 4, "Preparation expires at the end of the turn");
            });
            End(g);
            Core2026Proof.Check(held.Cost == 4, "Preparation does not carry over to the next turn");
            End(g);
            Core2026Proof.Check(held.Cost == 4, "still full price on my next turn");
            Play(g, p, id);
            End(g);
            var theirs = Add(p.Opponent, Fireball);
            Core2026Proof.Check(theirs.Cost == 4, "my Preparation never discounts the opponent");
            Play(g, p.Opponent, id);
            Core2026Proof.Check(theirs.Cost == 2 && held.Cost == 4, "Opponent's Preparation discounts only their own spells");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_274", (g, p, id) => {
            var arc = (Minion)Play(g, p, id);
            Core2026Proof.Check(arc.AttackDamage == 3 && arc.Health == 3, "Ethereal Arcanist is a 3/3");
            End(g); End(g);
            Core2026Proof.Check(arc.AttackDamage == 3 && arc.Health == 3, "No Secret: no growth");
            Play(g, p, Counterspell);
            Core2026Proof.Check(p.SecretZone.Count == 1, "setup own Secret");
            WildY0aL3ALegacyProof.Resume(g, next => {
                End(next);
                var grown = (Minion)next.IdEntityDic[arc.Id];
                Core2026Proof.Check(grown.AttackDamage == 5 && grown.Health == 5, "A Secret at end of turn gives +2/+2 (clone and snapshot)");
            });
            End(g);
            Core2026Proof.Check(arc.AttackDamage == 5 && arc.Health == 5, "A Secret at end of my turn gives +2/+2");
            End(g);
            Core2026Proof.Check(arc.AttackDamage == 5, "The opponent's turn end does not trigger it");
            End(g); End(g);
            Core2026Proof.Check(arc.AttackDamage == 7 && arc.Health == 7, "Growth stacks every turn the Secret stays");
            var secret = p.SecretZone.First();
            p.SetasideZone.Add(p.SecretZone.Remove(secret));
            End(g); End(g);
            Core2026Proof.Check(arc.AttackDamage == 7, "No Secret again: no growth");
            Play(g, p, Counterspell); arc.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(arc.AttackDamage == 3 && arc.Health == 3, "Silence removes the growth");
            End(g); End(g);
            Core2026Proof.Check(arc.AttackDamage == 3, "A Silenced Arcanist no longer grows");
            var second = (Minion)Play(g, p, id); var third = (Minion)Play(g, p, id);
            End(g);
            Core2026Proof.Check(second.AttackDamage == 5 && third.AttackDamage == 5, "Each Arcanist grows independently");
            var theirs = (Minion)Play(g, p.Opponent, id);
            End(g);
            Core2026Proof.Check(theirs.AttackDamage == 3 && second.AttackDamage == 5, "My Secret does not count for the opponent's Arcanist");
            End(g);
            Core2026Proof.Check(second.AttackDamage == 7, "my Arcanist grows again at my turn end");
            foreach (var mySecret in p.SecretZone.ToArray()) p.SetasideZone.Add(p.SecretZone.Remove(mySecret));
            Play(g, p.Opponent, Counterspell);
            Core2026Proof.Check(p.Opponent.SecretZone.Count == 1, "setup opponent Secret");
            End(g);
            Core2026Proof.Check(theirs.AttackDamage == 5 && second.AttackDamage == 7 && third.AttackDamage == 7, "The opponent's Secret grows only their Arcanist, at their turn end");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_275", (g, p, id) => {
            var a = Summon(p.Opponent, Yeti); var b = Summon(p.Opponent, Squire); var c = Summon(p.Opponent, Yeti); var d = Summon(p.Opponent, Yeti);
            var mine = Summon(p, Yeti);
            var held = Add(p, id);
            var targets = WildY0aL3ALegacyProof.Targets(p, held);
            Core2026Proof.Check(targets.Count == 5 && targets.All(t => t is Minion), "Cone of Cold targets any minion, never a hero");
            PlayEntity(g, p, held, b);
            Core2026Proof.Check(a.IsFrozen && b.IsFrozen && c.IsFrozen && !d.IsFrozen && !mine.IsFrozen, "Cone of Cold Freezes the target and both neighbors");
            Core2026Proof.Check(a.Damage == 1 && c.Damage == 1 && !b.HasDivineShield && b.Damage == 0 && d.Damage == 0, "Cone of Cold deals 1 to each (Divine Shield absorbs)");
            Core2026Proof.Check(p.Opponent.Hero.Damage == 0 && !p.Opponent.Hero.IsFrozen, "Cone of Cold leaves heroes alone");
            Play(g, p, id, d);
            Core2026Proof.Check(c.IsFrozen && d.IsFrozen && c.Damage == 2 && d.Damage == 1, "Cone of Cold at the right edge hits the target and its left neighbor only");
            Play(g, p, id, a);
            Core2026Proof.Check(a.Damage == 2 && b.Zone.Type == Zone.GRAVEYARD && c.Damage == 2, "Cone of Cold at the left edge hits the target and its right neighbor only (the 1 Health Squire dies)");
            Summon(p, Thalnos);
            Play(g, p, id, c);
            Core2026Proof.Check(a.Damage == 4 && c.Damage == 4 && d.Damage == 3, "Cone of Cold scales with Spell Damage and its neighbors are re-evaluated after a death");
            Play(g, p, id, d);
            Core2026Proof.Check(d.Zone.Type == Zone.GRAVEYARD && c.Zone.Type == Zone.GRAVEYARD && a.Zone.Type == Zone.PLAY && a.Damage == 4,
                "Cone of Cold kills at exact Health and leaves the far minion alone");
            WildY0aL3ALegacyProof.Resume(g, next => {
                End(next);
                Core2026Proof.Check(((Minion)next.IdEntityDic[a.Id]).IsFrozen, "Freeze lasts through the target's own turn");
                End(next);
                Core2026Proof.Check(!((Minion)next.IdEntityDic[a.Id]).IsFrozen, "Freeze thaws at the end of the frozen side's turn");
            });
            WildY0aL3BProof.ParkExcept(p.Opponent, null);
            var lone = Summon(p.Opponent, Yeti);
            Play(g, p, id, lone);
            Core2026Proof.Check(lone.Damage == 2 && lone.IsFrozen && p.Opponent.Hero.Damage == 0, "Cone of Cold on a lone minion hits only it");
            Play(g, p, id, mine);
            Core2026Proof.Check(mine.IsFrozen && mine.Damage == 2 && p.Hero.Damage == 0, "Cone of Cold may target a friendly minion (Spell Damage +1 from Thalnos)");
            End(g);
            var theirs = Summon(p.Opponent, Yeti); var neighbor = Summon(p.Opponent, Squire);
            Play(g, p.Opponent, id, theirs);
            Core2026Proof.Check(theirs.IsFrozen && neighbor.IsFrozen && p.BoardZone.All(m => !m.IsFrozen || m == mine), "Opponent's Cone of Cold freezes their chosen minions");
        });
    }

    internal static void ParkExcept(Controller owner, Minion keep)
    {
        foreach (var minion in owner.BoardZone.ToArray()) if (minion != keep) owner.SetasideZone.Add(owner.BoardZone.Remove(minion));
        owner.Game.DeathProcessingAndAuraUpdate();
    }

    // EX1_131 Defias Ringleader: BLOCKED-HSDATA. Its English text changed in 37.0 (the card file still has the older
    // text) and the locale files cannot be regenerated without hsdata, so it is not in
    // tests/progression/batches/wild/wild-y0a-l3b.txt. Wire Case("EX1_131", DefiasRingleader) in Run and list the id
    // once tools/gen_card_facts.py --hsdata can run.
    internal static void DefiasRingleader(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, Yeti);
        var plain = (Minion)Play(g, p, id);
        Core2026Proof.Check(plain.AttackDamage == 3 && plain.Health == 2 && p.BoardZone.Count == 1, "Defias Ringleader is a 3/2 and summons nothing without Combo");
        Play(g, p, Coin);
        var combo = (Minion)Play(g, p, id);
        var bandit = p.BoardZone.Single(m => m.Card.Id == "EX1_131t");
        Core2026Proof.Check(bandit.AttackDamage == 2 && bandit.Health == 1 && p.BoardZone.Select(m => m.Id).ToList().IndexOf(bandit.Id) == p.BoardZone.Select(m => m.Id).ToList().IndexOf(combo.Id) + 1 && bandit.Controller == p,
            "Combo summons a 2/1 Defias Bandit to its right");
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && p.Opponent.BoardZone[0] == enemy, "The Bandit appears on my side only");
        WildY0aL3ALegacyProof.Resume(g, next => { End(next); Core2026Proof.Check(next.Player1.BoardZone.Count == 3, "the Bandit stays through the turn change"); });
        combo.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(bandit.Zone.Type == Zone.PLAY, "Silencing the Ringleader keeps the Bandit");
        Summon(p, Yeti); Summon(p, Yeti); Summon(p, Yeti);
        Core2026Proof.Check(p.BoardZone.Count == 6, "setup six minions");
        End(g); End(g);
        Play(g, p, Coin);
        var held = Add(p, id);
        PlayEntity(g, p, held);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "EX1_131t") == 1, "Combo with a free slot for only the Ringleader summons no Bandit");
        End(g);
        Play(g, p.Opponent, Coin);
        Play(g, p.Opponent, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 3 && p.Opponent.BoardZone.Count(m => m.Card.Id == "EX1_131t") == 1, "Opponent's Combo summons for their side");
    }

    // EX1_134 SI:7 Agent: BLOCKED-HSDATA (English text changed in 37.0: Combo deals 3). Same wiring as above.
    // It also needs the script change DamageTask(2 -> 3) in Expert1CardsGen EX1_134, withheld on purpose: the Standard
    // proofs WanderingSeekerProof, TheUnknowableOneProof and AqirGoliathProof use the legacy id as a plain Combo card
    // and WanderingSeekerProof asserts its current 2 damage. With the change this method passes (mutation-checked).
    internal static void SI7Agent(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, Yeti); var mine = Summon(p, Yeti);
        var held = Add(p, id);
        Core2026Proof.Check(!WildY0aL3ALegacyProof.Targets(p, held).Any(), "SI:7 Agent offers no target without Combo");
        PlayEntity(g, p, held);
        var plain = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(plain.AttackDamage == 3 && plain.Health == 3 && enemy.Damage == 0 && p.Opponent.Hero.Damage == 0, "SI:7 Agent is a 3/3 and deals nothing without Combo");
        Play(g, p, Coin);
        var second = Add(p, id);
        var targets = WildY0aL3ALegacyProof.Targets(p, second).Distinct().ToList();
        Core2026Proof.Check(targets.Count == 5 && targets.Contains(p.Hero) && targets.Contains(p.Opponent.Hero), "Combo may target any character");
        PlayEntity(g, p, second, enemy);
        Core2026Proof.Check(enemy.Damage == 3, "SI:7 Agent Combo deals 3");
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3, "Combo hits the enemy hero for 3");
        Summon(p, Thalnos);
        Play(g, p, id, mine);
        Core2026Proof.Check(mine.Damage == 3, "SI:7 Agent Combo is not a spell, Spell Damage never adds");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD, "Combo kills at exact remaining Health (3 + 3 on 5 Health is lethal)");
        End(g);
        Play(g, p.Opponent, Coin); Play(g, p.Opponent, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 3, "Opponent's Combo hits my hero for 3");
    }
}
