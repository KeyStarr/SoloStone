using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static WildY0bM1BProof;

/// <summary>Wave Y0b lane m1c: Curse of Naxxramas scripts (Kel'Thuzad to Baron Rivendare) proven against the 37.0 text.
/// FP1_018 Duplicate is BLOCKED-STANDARD (data/pools/pool-eligibility-v1.json names it): its proof runs but is not in the manifest.</summary>
internal static class WildY0bM1CProof
{
    private static string Describe(Controller c) => string.Join(",", c.BoardZone.Select(m => m.Card.Id + ":" + m.AttackDamage + "/" + m.Health));

    /// <summary>Kills a minion the way a game does: a Fireball cast by its controller's turn owner, so Secrets and triggers run inside a real action.</summary>
    private static void Slay(Game g, Controller caster, Minion m)
    {
        for (int i = 0; i < 3 && m.Zone.Type == Zone.PLAY; i++) { if (caster.HandZone.Count >= 9) foreach (var x in caster.HandZone.Where(c => c.Card.Id != "CS2_029").Take(3).ToArray()) { caster.HandZone.Remove(x); caster.SetasideZone.Add(x); } var fireball = Add(caster, "CS2_029"); try { PlayEntity(g, caster, fireball, m); } catch (Exception e) { throw new Exception(e.Message + " | turn=" + g.CurrentPlayer.PlayerId + " caster=" + caster.PlayerId + " target=" + m.Card.Id + "/" + m.Controller.PlayerId + "/" + m.Zone?.Type + " hand=" + caster.HandZone.Count); } }
    }

    private static void HeroHit(Game g, Controller p, ICharacter target)
    {
        var task = p.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == target.Id);
        Core2026Proof.Check(task != null && g.Process(task), "hero attack accepted");
    }

    [GroupProof("wild-y0b-m1c-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Held("FP1_013", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 8 && card.ATK == 6 && card.Health == 8 && card.IsRace(Race.UNDEAD) && card.Rarity == Rarity.LEGENDARY, "Kel'Thuzad is an 8-mana 6/8 Undead Legendary");
            var kt = Summon(p, id);
            var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_168");
            Play(g, p, "CS2_092", a);
            var enemy = Summon(opp, "CS2_179");
            Kill(g, b); Kill(g, a); Kill(g, enemy);
            Core2026Proof.Check(Describe(p) == id + ":6/8", "fixture: both of mine died and nothing came back yet: " + Describe(p));
            Resume(g, next => {
                End(next);
                Core2026Proof.Check(Describe(next.Player1) == id + ":6/8,CS2_168:2/1,CS2_182:4/5", "the end of turn returns them in death order, as fresh copies, on a clone and a snapshot restore: " + Describe(next.Player1));
                Core2026Proof.Check(next.Player2.BoardZone.Count == 0, "the enemy's dead minion does not come back for me");
            });
            End(g);
            Core2026Proof.Check(Describe(p) == id + ":6/8,CS2_168:2/1,CS2_182:4/5" && opp.BoardZone.Count == 0, "back in death order, buffs gone, nothing for the enemy: " + Describe(p));
            // Either turn: a minion of mine that dies on the opponent's turn comes back at the end of that turn.
            var again = p.BoardZone[2]; Kill(g, again);
            Core2026Proof.Check(p.BoardZone.Count == 2, "fixture: it died on the opponent's turn");
            End(g);
            Core2026Proof.Check(Describe(p) == id + ":6/8,CS2_168:2/1,CS2_182:4/5" && p.BoardZone[2].Id != again.Id, "the end of the opponent's turn also summons it: " + Describe(p));
            End(g);
            Core2026Proof.Check(p.BoardZone.Count == 3, "minions that died on an earlier turn do not return again");
            // Full board: only as many as fit, in death order.
            StandardSetsProof.ParkBoard(p);
            Summon(p, id); for (int i = 0; i < 4; i++) Summon(p, "CS2_182");
            var first = Summon(p, "CS2_168"); var second = Summon(p, "CS2_179");
            Kill(g, first); Kill(g, second); Summon(p, "CS2_182");
            End(g);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_168") == 1 && p.BoardZone.Count(m => m.Card.Id == "CS2_179") == 0,
                "one free slot: the first to die returns, the second does not: " + Describe(p));
            End(g);
            // Silence, and dying together with the minions.
            StandardSetsProof.ParkBoard(p);
            var quiet = Summon(p, id); var lost = Summon(p, "CS2_182"); quiet.Silence(); Kill(g, lost); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 1, "Silence removes the effect");
            End(g);
            StandardSetsProof.ParkBoard(p);
            var doomed = Summon(p, id); var withIt = Summon(p, "CS2_182");
            doomed.ToBeDestroyed = true; withIt.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate(); End(g);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Kel'Thuzad dying together with them: no return");
            End(g);
            // The opponent's Kel'Thuzad serves the opponent only.
            StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p);
            Summon(opp, id); var theirs = Summon(opp, "CS2_182"); var mine = Summon(p, "CS2_182");
            Kill(g, theirs); Kill(g, mine); End(g);
            Core2026Proof.Check(Describe(opp) == id + ":6/8,CS2_182:4/5" && p.BoardZone.Count == 0, "the opponent's Kel'Thuzad returns only the opponent's minions at my turn's end: " + Describe(opp) + " | " + Describe(p));
            End(g);
        });
        WildSetsProof.Held("FP1_014", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 5 && card.ATK == 7 && card.Health == 4 && card.IsRace(Race.UNDEAD) && card.Rarity == Rarity.LEGENDARY, "Stalagg is a 5-mana 7/4 Undead Legendary");
            Resume(g, next => { // Feugen died first, then Stalagg.
                var q = next.Player1; Kill(next, Summon(q, "FP1_015"));
                Core2026Proof.Check(q.BoardZone.Count == 0, "Feugen's own death summons nothing, Stalagg is alive: " + Describe(q));
                Kill(next, Summon(q, id));
                Core2026Proof.Check(q.BoardZone.Count == 1 && q.BoardZone[0].Card.Id == "FP1_014t" && q.BoardZone[0].AttackDamage == 11 && q.BoardZone[0].Health == 11,
                    "Feugen died earlier: Stalagg's Deathrattle summons the 11/11 Thaddius: " + Describe(q));
            });
            Resume(g, next => { // a pair on the opponent's side serves the opponent only.
                var o = next.Player2;
                Kill(next, Summon(o, "FP1_015")); Kill(next, Summon(o, id));
                Core2026Proof.Check(o.BoardZone.Count == 1 && o.BoardZone[0].Card.Id == "FP1_014t" && next.Player1.BoardZone.Count == 0, "the opponent's pair gives the opponent Thaddius");
            });
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 0, "Feugen has not died: nothing");
            var feugen = Summon(p, "FP1_015"); Play(g, p, "EX1_144", feugen);
            Core2026Proof.Check(feugen.Zone.Type == Zone.HAND && p.HandZone.Contains(feugen), "fixture: Feugen was returned to the hand, not killed");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 0, "a bounced Feugen has not died: nothing");
            Kill(g, Summon(p, "FP1_015"));
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == "FP1_014t", "Stalagg died earlier: Feugen's death summons Thaddius");
            StandardSetsProof.ParkBoard(p);
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes the Deathrattle");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
            Summon(p, "FP1_031"); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "FP1_014t") == 1, "Baron Rivendare doubles it, only one Thaddius fits: " + Describe(p));
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
            var last = Summon(p, id); last.ToBeDestroyed = true; Summon(p, "CS2_182"); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "FP1_014t") == 1, "a board with a free slot after Stalagg's death gets Thaddius: " + Describe(p));
            End(g);
        });
        WildSetsProof.Held("FP1_015", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 5 && card.ATK == 4 && card.Health == 7 && card.IsRace(Race.UNDEAD) && card.Rarity == Rarity.LEGENDARY, "Feugen is a 5-mana 4/7 Undead Legendary");
            Resume(g, next => { // Stalagg died first, then Feugen.
                var q = next.Player1; Kill(next, Summon(q, "FP1_014"));
                Core2026Proof.Check(q.BoardZone.Count == 0, "Stalagg's own death summons nothing, Feugen is not dead: " + Describe(q));
                Kill(next, Summon(q, id));
                Core2026Proof.Check(q.BoardZone.Count == 1 && q.BoardZone[0].Card.Id == "FP1_014t" && q.BoardZone[0].AttackDamage == 11 && q.BoardZone[0].Health == 11,
                    "Stalagg died earlier: Feugen's Deathrattle summons Thaddius: " + Describe(q));
            });
            Resume(g, next => {
                var o = next.Player2;
                Kill(next, Summon(o, "FP1_014")); Kill(next, Summon(o, id));
                Core2026Proof.Check(o.BoardZone.Count == 1 && o.BoardZone[0].Card.Id == "FP1_014t" && next.Player1.BoardZone.Count == 0, "the opponent's pair gives the opponent Thaddius");
            });
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 0, "Stalagg has not died: nothing");
            var stalagg = Summon(p, "FP1_014"); Play(g, p, "EX1_144", stalagg);
            Core2026Proof.Check(p.HandZone.Contains(stalagg), "fixture: Stalagg was returned to the hand");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 0, "a bounced Stalagg has not died: nothing");
            Kill(g, Summon(p, "FP1_014"));
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == "FP1_014t", "Feugen died earlier: Stalagg's death summons Thaddius");
            StandardSetsProof.ParkBoard(p);
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes the Deathrattle");
            Summon(p, "FP1_031"); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_014t") == 2, "Baron Rivendare doubles it: two Thaddius");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            var last = Summon(p, id); last.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "FP1_014t") == 1, "the free slot of the dead Feugen takes Thaddius: " + Describe(p));
            End(g);
        });
        WildSetsProof.Held("FP1_016", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 4 && card.ATK == 3 && card.Health == 5 && card.IsRace(Race.UNDEAD), "Wailing Soul is a 4-mana 3/5 Undead");
            var kings = Summon(p, "CS2_182"); Play(g, p, "CS2_092", kings);
            var taunt = Summon(p, "CS2_179"); var swords = Summon(p, "FP1_029");
            var enemy = Summon(opp, "CS2_182"); Play(g, p, "CS2_092", enemy); var enemyTaunt = Summon(opp, "CS2_179");
            Core2026Proof.Check(kings.AttackDamage == 8 && swords.Card.Tags.GetValueOrDefault(GameTag.DEATHRATTLE) == 1, "fixture: buffed Yeti and a Deathrattle minion");
            var soul = (Minion)Add(p, id);
            Twin(g, p, soul, null, "Wailing Soul");
            Core2026Proof.Check(kings.AttackDamage == 4 && kings.Health == 5 && !taunt.HasTaunt && p.BoardZone.Contains(soul), "my other minions are silenced (buff and Taunt gone), the Soul stays");
            Core2026Proof.Check(enemy.AttackDamage == 8 && enemyTaunt.HasTaunt, "enemy minions are untouched");
            int hand = opp.HandZone.Count; Kill(g, swords);
            Core2026Proof.Check(opp.HandZone.Count == hand, "a silenced Dancing Swords draws nothing");
            Core2026Proof.Check(soul.AttackDamage == 3 && soul.Health == 5, "the Soul keeps its stats");
            StandardSetsProof.ParkBoard(p);
            var lone = (Minion)Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 1 && lone.AttackDamage == 3, "no other minion: nothing to silence, still playable");
            var held = Summon(p, "CS2_182"); Play(g, p, "CS2_092", held); Summon(p, id);
            Core2026Proof.Check(held.AttackDamage == 8, "summoning the Soul has no Battlecry");
            End(g);
            var theirs = (Minion)Play(g, opp, id);
            Core2026Proof.Check(enemy.AttackDamage == 4 && enemyTaunt.HasTaunt == false && held.AttackDamage == 8, "the opponent's Soul silences the opponent's minions only");
            End(g);
        });
        WildSetsProof.Held("FP1_017", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 2 && card.ATK == 1 && card.Health == 4 && card.IsRace(Race.UNDEAD), "Nerub'ar Weblord is a 2-mana 1/4 Undead");
            var bc = Add(p, "CS2_188"); var plain = Add(p, "CS2_182"); var spell = Add(p, "CS2_029"); var oppBc = Add(opp, "CS2_188");
            p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_188")));
            Core2026Proof.Check(bc.Cost == 1 && plain.Cost == 4 && oppBc.Cost == 1, "fixture costs");
            var weblord = Summon(p, id);
            Core2026Proof.Check(bc.Cost == 3 && plain.Cost == 4 && spell.Cost == 4, "my Battlecry minions cost 2 more; others do not");
            Core2026Proof.Check(oppBc.Cost == 3, "the opponent's Battlecry minions cost 2 more as well");
            Resume(g, next => {
                var held = next.Player1.HandZone.First(c => c.Card.Id == "CS2_188");
                Core2026Proof.Check(held.Cost == 3, "the cost is restored on a clone and a snapshot restore");
                Kill(next, (Minion)next.IdEntityDic[weblord.Id]);
                Core2026Proof.Check(held.Cost == 1, "and ends with the Weblord's death");
            });
            var drawn = p.DeckZone.First(c => c.Card.Id == "CS2_188");
            Core2026Proof.Check(drawn.Cost == 1, "a card still in the deck is not changed");
            var second = Summon(p, id);
            Core2026Proof.Check(bc.Cost == 5, "two Weblords stack");
            Kill(g, second);
            Core2026Proof.Check(bc.Cost == 3, "one Weblord left");
            Play(g, p, "EX1_332", weblord);
            Core2026Proof.Check(bc.Cost == 1 && oppBc.Cost == 1, "Silence ends it: " + bc.Cost + "," + oppBc.Cost);
            var soul = Add(p, "FP1_016");
            var theirs = Summon(opp, id);
            Core2026Proof.Check(bc.Cost == 3 && soul.Cost == 6 && plain.Cost == 4, "the opponent's Weblord raises my Battlecry minions too");
            var bcMinion = (Minion)Summon(p, "CS2_188");
            Core2026Proof.Check(bcMinion.Cost == 1, "a Battlecry minion on the board is not a hand card");
            Kill(g, theirs);
            Core2026Proof.Check(bc.Cost == 1, "back to normal");
            // Playable cost is really paid.
            var w = Summon(p, id); p.BaseMana = 10; p.UsedMana = 0;
            var played = (Minion)bc; PlayEntity(g, p, bc, w);
            Core2026Proof.Check(p.UsedMana == 3 && p.BoardZone.Contains(played), "the raised cost is the cost paid");
            Kill(g, w);
            End(g);
        });
        WildSetsProof.Case("FP1_019", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 4 && card.Type == CardType.SPELL && card.Class == CardClass.DRUID, "Poison Seeds is a 4-mana Druid Nature spell");
            Core2026Proof.Check(Cards.FromId("FP1_019t").ATK == 2 && Cards.FromId("FP1_019t").Health == 2, "the Treant is 2/2");
            var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_168"); var c = Summon(p, "CS2_179");
            Play(g, p, "CS2_092", a);
            var x = Summon(opp, "CS2_182"); var y = Summon(opp, "CS2_168");
            var seeds = Add(p, id);
            Twin(g, p, seeds, null, "Poison Seeds");
            Core2026Proof.Check(string.Join(",", p.BoardZone.Select(m => m.Card.Id + ":" + m.AttackDamage + "/" + m.Health)) == "FP1_019t:2/2,FP1_019t:2/2,FP1_019t:2/2" && opp.BoardZone.Count == 2
                && opp.BoardZone.All(m => m.Card.Id == "FP1_019t" && m.AttackDamage == 2 && m.Health == 2 && m.Controller == opp), "every minion is replaced by a fresh Treant on its own side: " + Describe(p) + " | " + Describe(opp));
            Core2026Proof.Check(!p.BoardZone.Any(m => m.Id == a.Id || m.Id == b.Id || m.Id == c.Id), "the old minions are gone");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(opp);
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 0 && opp.BoardZone.Count == 0, "no minions: no Treants");
            for (int i = 0; i < 7; i++) { Summon(p, "CS2_182"); Summon(opp, "CS2_168"); }
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 7 && opp.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id == "FP1_019t") && opp.BoardZone.All(m => m.Card.Id == "FP1_019t"), "full boards: seven Treants each");
            StandardSetsProof.ParkBoard(opp);
            Summon(opp, "CS2_182");
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 7 && opp.BoardZone.Count == 1 && p.BoardZone.All(m => m.Card.Id == "FP1_019t"), "an uneven pair: counts follow each side");
            StandardSetsProof.ParkBoard(opp);
            StandardSetsProof.ParkBoard(p); Summon(p, "CS2_182"); Summon(opp, "CS2_168");
            End(g);
            Play(g, opp, id);
            Core2026Proof.Check(p.BoardZone.Count == 1 && opp.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == "FP1_019t" && opp.BoardZone[0].Card.Id == "FP1_019t" && p.BoardZone[0].Controller == p, "the opponent's cast works the same way and keeps sides");
            End(g);
        });
        WildSetsProof.Case("FP1_020", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 1 && card.Type == CardType.SPELL && card.IsSecret && card.Class == CardClass.PALADIN, "Avenge is a 1-mana Paladin Secret");
            var yeti = Summon(p, "CS2_182"); var raider = Summon(p, "CS2_168");
            var avenge = Add(p, id);
            Twin(g, p, avenge, null, "Avenge");
            Core2026Proof.Check(p.SecretZone.Count == 1, "the Secret is in play");
            Slay(g, p, raider);
            Core2026Proof.Check(p.SecretZone.Count == 1 && yeti.AttackDamage == 4, "a Secret only works on the opponent's turn: my own turn's death does nothing");
            End(g);
            var enemy = Summon(opp, "CS2_182"); Slay(g, opp, enemy);
            Core2026Proof.Check(p.SecretZone.Count == 1 && yeti.AttackDamage == 4, "an enemy minion dying does not trigger it");
            Slay(g, opp, yeti);
            Core2026Proof.Check(p.SecretZone.Count == 1 && p.BoardZone.Count == 0, "the last minion dies: nobody left to buff, the Secret does not trigger");
            End(g);
            var a = Summon(p, "CS2_168"); var b = Summon(p, "CS2_182");
            Resume(g, next => {
                End(next); var q = next.Player1; var dying = (Minion)next.IdEntityDic[a.Id];
                Slay(next, next.Player2, dying);
                Core2026Proof.Check(q.SecretZone.Count == 0 && q.BoardZone.Single().AttackDamage == 7 && q.BoardZone.Single().Health == 7, "+3/+2 on a clone and a snapshot restore: " + Describe(q));
            });
            End(g);
            Slay(g, opp, a);
            Core2026Proof.Check(b.AttackDamage == 7 && b.Health == 7 && p.SecretZone.Count == 0, "the only other minion gets +3/+2 and the Secret is used: " + Describe(p));
            Core2026Proof.Check(p.BoardZone.Count == 1, "the buff goes to the survivor, not the dead minion");
            b.Silence();
            Core2026Proof.Check(b.AttackDamage == 4 && b.Health == 5, "Silence removes Vengeance");
            // Several survivors: one at random, never the dead one.
            var seen = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                End(g);
                EmptyHand(p); StandardSetsProof.ParkBoard(p);
                var m = new[] { Summon(p, "CS2_168"), Summon(p, "CS2_168"), Summon(p, "CS2_168"), Summon(p, "CS2_168") };
                Play(g, p, id);
                End(g);
                Slay(g, opp, m[0]);
                var buffed = p.BoardZone.Where(x => x.AttackDamage == 5).ToArray();
                Core2026Proof.Check(buffed.Length == 1 && p.BoardZone.Count == 3 && p.BoardZone.Count(x => x.AttackDamage == 2) == 2 && buffed[0].Health == 3 && p.SecretZone.Count == 0,
                    "exactly one of the three survivors gets +3/+2: " + Describe(p));
                seen.Add(p.BoardZone.ToList().IndexOf(buffed[0]));
            }
            Core2026Proof.Check(seen.Distinct().Count() == 3, "all three survivors can be chosen: " + string.Join(",", seen));
            // The opponent's Avenge buffs the opponent's minions only (the loop ended on the opponent's turn).
            StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p);
            var mine = Summon(p, "CS2_182");
            var theirs = Summon(opp, "CS2_168"); var theirs2 = Summon(opp, "CS2_182");
            var opSecret = Add(opp, id); PlayEntity(g, opp, opSecret);
            Core2026Proof.Check(opp.SecretZone.Count == 1, "the opponent's Secret is in play");
            End(g);
            Slay(g, p, theirs);
            Core2026Proof.Check(theirs2.AttackDamage == 7 && mine.AttackDamage == 4 && opp.SecretZone.Count == 0, "the opponent's Avenge buffs the opponent's minion only: " + Describe(opp) + " | " + Describe(p));
            End(g);
        });
        WildSetsProof.Case("FP1_021", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 4 && card.ATK == 4 && card.Tags.GetValueOrDefault(GameTag.DURABILITY) == 2 && card.Type == CardType.WEAPON && card.Class == CardClass.WARRIOR, "Death's Bite is a 4-mana 4/2 Warrior weapon");
            var mine = Summon(p, "CS2_168"); var big = Summon(p, "CS2_182"); var theirs = Summon(opp, "CS2_168"); var theirBig = Summon(opp, "CS2_182");
            Play(g, p, id);
            Core2026Proof.Check(p.Hero.Weapon != null && p.Hero.Weapon.Card.Id == id && p.Hero.Weapon.Durability == 2 && p.BoardZone.Count == 2 && opp.BoardZone.Count == 2, "the weapon is equipped, nothing else");
            Resume(g, next => {
                var q = next.Player1; q.Hero.Weapon.Destroy(); next.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(q.BoardZone.Count == 1 && next.Player2.BoardZone.Count == 1 && q.BoardZone[0].Damage == 1 && next.Player2.BoardZone[0].Damage == 1, "the Deathrattle runs on a clone and a snapshot restore");
            });
            Core2026Proof.Check(p.Hero.Weapon.Card.Id == id, "fixture");
            HeroHit(g, p, opp.Hero);
            Core2026Proof.Check(mine.Damage == 0 && p.Hero.Weapon != null, "no Deathrattle while the weapon lives");
            End(g); End(g);
            HeroHit(g, p, opp.Hero);
            Core2026Proof.Check(p.Hero.Weapon == null && mine.Zone.Type == Zone.GRAVEYARD && theirs.Zone.Type == Zone.GRAVEYARD, "the last durability is used: 1 damage kills the 1-health minions on both sides");
            Core2026Proof.Check(big.Damage == 1 && theirBig.Damage == 1 && p.Hero.Damage == 0 && opp.Hero.Damage == 8, "1 damage to the others, never the heroes (opponent took 4+4 from the attacks): " + opp.Hero.Damage);
            // Replacing the weapon destroys it too.
            Play(g, p, id); Play(g, p, "CS2_106");
            Core2026Proof.Check(big.Damage == 2 && theirBig.Damage == 2 && p.Hero.Weapon.Card.Id == "CS2_106", "replacing the weapon triggers the Deathrattle");
            // Baron Rivendare does not double a weapon's Deathrattle.
            var baron = Summon(p, "FP1_031"); Play(g, p, id); Play(g, p, "CS2_106");
            Core2026Proof.Check(big.Damage == 3 && theirBig.Damage == 3 && baron.Damage == 1, "Baron Rivendare does not double a weapon's Deathrattle");
            Kill(g, baron);
            End(g);
            // The opponent's weapon hits everything alike.
            Play(g, opp, id); Play(g, opp, "CS2_106");
            Core2026Proof.Check(big.Damage == 4 && theirBig.Damage == 4, "the opponent's replaced weapon deals the same 1 damage to all minions");
        });
        WildSetsProof.Case("FP1_022", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 4 && card.ATK == 3 && card.Health == 4 && card.IsRace(Race.DEMON), "Voidcaller is a 4-mana 3/4 Demon");
            var left = Summon(p, "CS2_182"); var caller = Summon(p, id); var right = Summon(p, "CS2_182");
            var plain = Add(p, "CS2_182"); var imp = Add(p, "EX1_301");
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[caller.Id]);
                Core2026Proof.Check(next.Player1.BoardZone.Count == 3 && next.Player1.BoardZone.Count(m => m.Card.Id == "EX1_301") == 1 && next.Player1.HandZone.Count == 1, "the Deathrattle runs on a clone and a snapshot restore: " + string.Join(",", next.Player1.BoardZone.Select(m => m.Card.Id + "#" + m.Id)) + "/" + string.Join(",", next.Player1.HandZone.Select(c => c.Card.Id + "#" + c.Id)) + " imp#" + imp.Id);
            });
            int mana = p.BaseMana; int hp = p.Hero.Damage;
            Kill(g, caller);
            var got = p.BoardZone.Single(m => m.Card.Id == "EX1_301");
            Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.Contains(left) && p.BoardZone.Contains(right) && got.Controller == p && p.HandZone.Count == 1 && p.HandZone.Single().Id == plain.Id,
                "the only Demon in hand goes onto my battlefield and leaves the hand; the other card stays");
            Core2026Proof.Check(p.BaseMana == mana && p.Hero.Damage == hp, "put onto the battlefield: Felguard's Battlecry does not run");
            // No Demon in the hand: nothing.
            StandardSetsProof.ParkBoard(p); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 0 && p.HandZone.Count == 1, "no Demon in hand: nothing");
            // Random among three Demons, distinct picks over repeated runs; Dread Infernal's Battlecry does not run either.
            var picks = new HashSet<string>();
            for (int i = 0; i < 24; i++)
            {
                EmptyHand(p); StandardSetsProof.ParkBoard(p);
                Add(p, "CS2_065"); Add(p, "EX1_301"); Add(p, "CS2_064"); Add(p, "CS2_182");
                var enemy = Summon(opp, "CS2_182"); var host = Summon(p, id);
                Kill(g, host);
                Core2026Proof.Check(p.BoardZone.Count == 1 && p.HandZone.Count == 3 && p.HandZone.Count(c => c.Card.Id == "CS2_182") == 1, "one Demon leaves the hand: " + Describe(p));
                Core2026Proof.Check(enemy.Damage == 0 && p.Hero.Damage == hp, "no Battlecry damage");
                picks.Add(p.BoardZone[0].Card.Id);
                StandardSetsProof.ParkBoard(opp);
            }
            Core2026Proof.Check(picks.Count == 3, "every Demon in hand can be chosen: " + string.Join(",", picks));
            // Another Voidcaller in hand is a Demon too; full board keeps the hand.
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            var spare = Add(p, id); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == id && p.HandZone.Count == 0, "a Voidcaller in hand is a Demon and can be put in");
            StandardSetsProof.ParkBoard(p); EmptyHand(p);
            Add(p, "CS2_065");
            for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
            Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.HandZone.Count == 0, "the Voidcaller's own slot is free: the Demon fits");
            StandardSetsProof.ParkBoard(p); EmptyHand(p); Add(p, "CS2_065");
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.BoardZone.Count == 0 && p.HandZone.Count == 1, "Silence removes the Deathrattle");
            Summon(p, "FP1_031"); Add(p, "EX1_301"); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.IsRace(Race.DEMON)) == 2 && p.HandZone.Count == 0, "Baron Rivendare puts in two Demons");
            End(g);
            // The opponent's Voidcaller reads the opponent's hand.
            EmptyHand(opp); Add(opp, "CS2_065"); Add(p, "CS2_064"); StandardSetsProof.ParkBoard(opp);
            Kill(g, Summon(opp, id));
            Core2026Proof.Check(opp.BoardZone.Count == 1 && opp.BoardZone[0].Card.Id == "CS2_065" && p.HandZone.Count(c => c.Card.Id == "CS2_064") == 1, "the opponent's Voidcaller uses the opponent's hand only");
            End(g);
        });
        WildSetsProof.Held("FP1_023", "held: FX-MISSING live (deathrattle)", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 3 && card.ATK == 3 && card.Health == 4 && card.Class == CardClass.PRIEST, "Dark Cultist is a 3-mana 3/4 Priest minion");
            var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_168"); var c = Summon(p, "CS2_179"); var cultist = Summon(p, id);
            Play(g, p, "CS2_092", a);
            var enemy = Summon(opp, "CS2_182");
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[cultist.Id]);
                Core2026Proof.Check(next.Player1.BoardZone.Count == 3 && next.Player1.BoardZone.Sum(m => m.Health) == 18, "the Deathrattle runs on a clone and a snapshot restore: " + Describe(next.Player1));
            });
            Kill(g, cultist);
            Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.Sum(m => m.Health) == 18 && enemy.Health == 5 && p.BoardZone.All(m => m.AttackDamage == m.Card.ATK + (m.Id == a.Id ? 4 : 0)),
                "one friendly minion gets +3 Health (and no Attack), the enemy none: " + Describe(p));
            // Random over three, never the Cultist, only Health.
            var seen = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                StandardSetsProof.ParkBoard(p);
                var s = new[] { Summon(p, "CS2_168"), Summon(p, "CS2_168"), Summon(p, "CS2_168") };
                var cult = Summon(p, id); Kill(g, cult);
                var grown = p.BoardZone.Where(m => m.Health == 4).ToArray();
                Core2026Proof.Check(grown.Length == 1 && grown[0].AttackDamage == 2 && p.BoardZone.Count == 3 && p.BoardZone.Count(m => m.Health == 1) == 2, "exactly one of the others gains +3 Health: " + Describe(p));
                seen.Add(p.BoardZone.ToList().IndexOf(grown[0]));
            }
            Core2026Proof.Check(seen.Distinct().Count() == 3, "every other minion can be chosen: " + string.Join(",", seen));
            StandardSetsProof.ParkBoard(p);
            Kill(g, Summon(p, id)); Core2026Proof.Check(p.BoardZone.Count == 0, "alone: nothing");
            var t = Summon(p, "CS2_182"); var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(t.Health == 5, "Silence removes the Deathrattle");
            Summon(p, "FP1_031"); Kill(g, Summon(p, id));
            var yeti = p.BoardZone.Single(m => m.Card.Id == "CS2_182"); var bar = p.BoardZone.Single(m => m.Card.Id == "FP1_031");
            Core2026Proof.Check((yeti.Health - 5) + (bar.Health - 7) == 6, "Baron Rivendare: the Deathrattle runs twice, two +3 Health buffs: " + Describe(p));
            yeti.Silence(); Core2026Proof.Check(yeti.Health == 5, "Silence removes the buff");
            End(g);
            StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p); Summon(p, "CS2_182");
            var ally = Summon(opp, "CS2_182"); Kill(g, Summon(opp, id));
            Core2026Proof.Check(ally.Health == 8 && p.BoardZone.All(m => m.Health == m.Card.Health), "the opponent's Cultist buffs the opponent's minions");
            End(g);
        });
        WildSetsProof.Held("FP1_024", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 2 && card.ATK == 1 && card.Health == 3 && card.IsRace(Race.UNDEAD), "Unstable Ghoul is a 2-mana 1/3 Undead");
            var ghoul = Summon(p, id); Core2026Proof.Check(ghoul.HasTaunt, "Unstable Ghoul has Taunt");
            var mine = Summon(p, "CS2_182"); var weak = Summon(p, "CS2_168"); var theirs = Summon(opp, "CS2_182"); var theirWeak = Summon(opp, "CS2_168");
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[ghoul.Id]);
                Core2026Proof.Check(next.Player1.BoardZone.Count == 1 && next.Player1.BoardZone[0].Damage == 1 && next.Player2.BoardZone.Count == 1, "the Deathrattle runs on a clone and a snapshot restore");
            });
            Kill(g, ghoul);
            Core2026Proof.Check(mine.Damage == 1 && theirs.Damage == 1 && weak.Zone.Type == Zone.GRAVEYARD && theirWeak.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 0 && opp.Hero.Damage == 0,
                "1 damage to all minions on both sides, 1-health ones die, heroes untouched");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(opp);
            var g1 = Summon(p, id); var g2 = Summon(opp, id);
            Kill(g, g1);
            Core2026Proof.Check(g2.Damage == 1 && g2.Zone.Type == Zone.PLAY, "the other Ghoul only takes 1");
            Kill(g, g2);
            Core2026Proof.Check(p.BoardZone.Count == 0 && opp.BoardZone.Count == 0, "and its Deathrattle hurts nothing");
            var quiet = Summon(p, id); var friend = Summon(p, "CS2_182"); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(friend.Damage == 0, "Silence removes the Deathrattle (and the Taunt)");
            var shield = Summon(opp, "EX1_008"); Kill(g, Summon(p, id));
            Core2026Proof.Check(shield.Zone.Type == Zone.PLAY && !shield.HasDivineShield && friend.Damage == 1, "Divine Shield absorbs the 1 damage");
            var baron = Summon(p, "FP1_031"); Kill(g, Summon(p, id));
            Core2026Proof.Check(friend.Damage == 3 && shield.Zone.Type == Zone.GRAVEYARD, "Baron Rivendare doubles it (the unshielded Squire dies): " + friend.Damage);
            Kill(g, baron);
            End(g);
            var theirGhoul = Summon(opp, id); var dmg = friend.Damage; Kill(g, theirGhoul);
            Core2026Proof.Check(friend.Damage == dmg + 1, "the opponent's Ghoul hurts my minions as well");
            End(g);
        });
        WildSetsProof.Case("FP1_025", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 1 && card.Type == CardType.SPELL && card.Class == CardClass.SHAMAN, "Reincarnate is a 1-mana Shaman Nature spell");
            var shield = Summon(p, "EX1_008"); var buddy = Summon(p, "CS2_182");
            Play(g, p, "CS2_092", shield); shield.Damage = 2; shield.HasDivineShield = false;
            var spell = Add(p, id);
            var targets = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source.Id == spell.Id).Select(t => t.Target?.Id).ToList();
            Core2026Proof.Check(targets.Count == 2 && targets.Contains(shield.Id) && targets.Contains(buddy.Id) && !targets.Contains(p.Hero.Id) && !targets.Contains(opp.Hero.Id), "only minions are targets");
            Twin(g, p, spell, shield, "Reincarnate");
            var back = p.BoardZone.Single(m => m.Card.Id == "EX1_008");
            Core2026Proof.Check(back.Id != shield.Id && shield.Zone.Type == Zone.GRAVEYARD && back.AttackDamage == 1 && back.Health == 1 && back.HasDivineShield && back.Damage == 0 && p.BoardZone.Count == 2,
                "a fresh Argent Squire: base stats, full Health, Divine Shield again, no buffs: " + Describe(p));
            // Deathrattle runs once, then the minion returns (with its Deathrattle).
            var swords = Summon(p, "FP1_029"); int h = opp.HandZone.Count;
            Play(g, p, id, swords);
            Core2026Proof.Check(opp.HandZone.Count == h + 1 && p.BoardZone.Count(m => m.Card.Id == "FP1_029") == 1, "the Deathrattle triggers once, the minion returns");
            // Full board: the destroyed minion's slot is free again.
            StandardSetsProof.ParkBoard(p); for (int i = 0; i < 7; i++) Summon(p, "CS2_182");
            var target = p.BoardZone[3]; Play(g, p, id, target);
            Core2026Proof.Check(p.BoardZone.Count == 7 && !p.BoardZone.Any(m => m.Id == target.Id), "a full board: the minion comes back");
            // Enemy minion.
            var enemy = Summon(opp, "CS2_182"); Play(g, p, "CS2_092", enemy);
            Play(g, p, id, enemy);
            Core2026Proof.Check(opp.BoardZone.Count == 1 && opp.BoardZone[0].Id != enemy.Id && opp.BoardZone[0].AttackDamage == 4 && p.BoardZone.Count == 7, "an enemy minion returns on the enemy side, fresh: " + Describe(opp) + " | " + Describe(p));
            // A minion of mine that came from the opponent returns to me.
            StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p);
            var mc = Summon(opp, "CS2_182"); Play(g, p, "CS1_113", mc); Core2026Proof.Check(mc.Controller == p, "fixture: mine now");
            Play(g, p, id, mc);
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_182") == 1 && opp.BoardZone.Count == 0, "a stolen minion returns under its current controller (me)");
            // Silence is gone on the new minion.
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(opp);
            var quiet = Summon(p, "CS2_179"); quiet.Silence(); Play(g, p, id, quiet);
            Core2026Proof.Check(p.BoardZone.Single().HasTaunt, "Silence is gone: Taunt is back on the new minion");
            End(g);
            var t = Summon(opp, "CS2_182"); Play(g, opp, id, t);
            Core2026Proof.Check(opp.BoardZone.Count == 1 && opp.BoardZone[0].Id != t.Id, "the opponent's cast works the same way");
            End(g);
        });
        WildSetsProof.Held("FP1_026", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 4 && card.ATK == 5 && card.Health == 5 && card.IsRace(Race.UNDEAD) && card.Class == CardClass.ROGUE, "Anub'ar Ambusher is a 4-mana 5/5 Undead Rogue");
            var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_168"); var c = Summon(p, "CS2_179"); var amb = Summon(p, id);
            Play(g, p, "CS2_092", a); var enemy = Summon(opp, "CS2_182");
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[amb.Id]);
                Core2026Proof.Check(next.Player1.HandZone.Count == 1 && next.Player1.BoardZone.Count == 2 && next.Player2.BoardZone.Count == 1, "the Deathrattle runs on a clone and a snapshot restore");
            });
            Kill(g, amb);
            var ret = p.HandZone.Single();
            Core2026Proof.Check(p.BoardZone.Count == 2 && new[] { a.Id, b.Id, c.Id }.Contains(ret.Id) && opp.BoardZone.Count == 1 && opp.HandZone.Count == 0, "one random friendly minion goes back to my hand, the enemy's stays");
            if (ret.Id == a.Id) Core2026Proof.Check(((Minion)ret).AttackDamage == 4 && ret.Cost == 4, "a returned minion loses its buffs");
            var seen = new HashSet<int>();
            for (int i = 0; i < 24; i++)
            {
                EmptyHand(p); StandardSetsProof.ParkBoard(p);
                var m = new[] { Summon(p, "CS2_182"), Summon(p, "CS2_168"), Summon(p, "CS2_179") };
                Kill(g, Summon(p, id));
                Core2026Proof.Check(p.HandZone.Count == 1 && p.BoardZone.Count == 2, "one of three goes back: " + Describe(p));
                seen.Add(Array.FindIndex(m, x => x.Id == p.HandZone.Single().Id));
            }
            Core2026Proof.Check(seen.Count == 3 && !seen.Contains(-1), "every other minion can be chosen: " + string.Join(",", seen));
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            Kill(g, Summon(p, id)); Core2026Proof.Check(p.HandZone.Count == 0, "alone: nothing");
            var lone = Summon(p, "CS2_182"); var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(p.HandZone.Count == 0 && p.BoardZone.Count == 1, "Silence removes the Deathrattle");
            Summon(p, "CS2_168"); Summon(p, "FP1_031"); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.HandZone.Count == 2, "Baron Rivendare: two minions go back (two different ones)");
            EmptyHand(p); StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 10; i++) Add(p, "CS2_182");
            Summon(p, "CS2_168"); Kill(g, Summon(p, id));
            Core2026Proof.Check(p.HandZone.Count == 10 && p.BoardZone.Count == 0, "a full hand: the minion is destroyed, not returned");
            End(g);
            EmptyHand(opp); StandardSetsProof.ParkBoard(opp); var mate = Summon(opp, "CS2_182"); Kill(g, Summon(opp, id));
            Core2026Proof.Check(opp.HandZone.Count == 1 && opp.HandZone.Single().Id == mate.Id && p.HandZone.Count == 10, "the opponent's Ambusher returns the opponent's minion");
            End(g);
        });
        WildSetsProof.Held("FP1_027", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 3 && card.ATK == 1 && card.Health == 4 && card.IsRace(Race.UNDEAD), "Stoneskin Gargoyle is a 3-mana 1/4 Undead");
            var garg = Summon(p, id); garg.Damage = 3;
            Play(g, p, "CS2_092", garg); garg.Damage = 5;
            Core2026Proof.Check(garg.Health == 3, "fixture: damaged and buffed");
            Resume(g, next => {
                End(next); Core2026Proof.Check(((Minion)next.IdEntityDic[garg.Id]).Health == 3, "no heal at the opponent's turn start");
                End(next); Core2026Proof.Check(((Minion)next.IdEntityDic[garg.Id]).Health == 8, "full heal at my turn start, on a clone and a snapshot restore");
            });
            End(g);
            Core2026Proof.Check(garg.Health == 3, "the opponent's turn start does not heal it");
            garg.Damage = 6;
            End(g);
            Core2026Proof.Check(garg.Damage == 0 && garg.Health == 8, "my turn start restores it to full Health, buffs included");
            var plain = Summon(p, "CS2_182"); plain.Damage = 3; End(g); End(g);
            Core2026Proof.Check(plain.Damage == 3, "other minions are not healed");
            var quiet = Summon(p, id); quiet.Damage = 2; quiet.Silence(); End(g); End(g);
            Core2026Proof.Check(quiet.Damage == 2, "Silence removes it");
            var theirs = Summon(opp, id); theirs.Damage = 3;
            End(g);
            Core2026Proof.Check(theirs.Damage == 0, "the opponent's Gargoyle heals at the opponent's turn start");
            theirs.Damage = 2; garg.Damage = 3;
            End(g);
            Core2026Proof.Check(theirs.Damage == 2 && garg.Damage == 0, "and not at mine; mine heals at mine");
        });
        WildSetsProof.Held("FP1_028", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 1 && card.ATK == 1 && card.Health == 2 && card.IsRace(Race.UNDEAD), "Undertaker is a 1-mana 1/2 Undead");
            var under = Summon(p, id);
            // A Deathrattle minion is summoned and removed again, so the board never fills up.
            void Dr(Controller c) { Kill(g, Summon(c, "FP1_029")); }
            Resume(g, next => {
                var u = (Minion)next.IdEntityDic[under.Id]; Kill(next, Summon(next.Player1, "FP1_029"));
                Core2026Proof.Check(u.AttackDamage == 2 && u.Health == 3, "+1/+1 on a clone and a snapshot restore: " + u.AttackDamage + "/" + u.Health);
            });
            Dr(p);
            Core2026Proof.Check(under.AttackDamage == 2 && under.Health == 3, "a summoned Deathrattle minion gives +1/+1: " + under.AttackDamage + "/" + under.Health);
            Kill(g, Summon(p, "CS2_182"));
            Core2026Proof.Check(under.AttackDamage == 2 && under.Health == 3, "a minion without Deathrattle does not");
            Kill(g, (Minion)Play(g, p, "FP1_029"));
            Core2026Proof.Check(under.AttackDamage == 3 && under.Health == 4, "a Deathrattle minion played from the hand counts too");
            Dr(opp);
            Core2026Proof.Check(under.AttackDamage == 3 && under.Health == 4, "an enemy Deathrattle minion does not");
            Play(g, p, "CS2_092", under);
            Dr(p);
            Core2026Proof.Check(under.AttackDamage == 8 && under.Health == 9, "buffs stack with +1/+1: " + under.AttackDamage + "/" + under.Health);
            var second = Summon(p, id);
            Dr(p);
            Core2026Proof.Check(under.AttackDamage == 9 && second.AttackDamage == 2 && second.Health == 3, "every Undertaker gains");
            Kill(g, second);
            under.Silence();
            Core2026Proof.Check(under.AttackDamage == 1 && under.Health == 2, "Silence removes the stats");
            Dr(p);
            Core2026Proof.Check(under.AttackDamage == 1 && under.Health == 2, "and the effect");
            End(g);
            var ut = Summon(opp, id); Dr(p);
            Core2026Proof.Check(ut.AttackDamage == 1, "the opponent's Undertaker ignores my summons");
            Dr(opp);
            Core2026Proof.Check(ut.AttackDamage == 2 && ut.Health == 3, "but gains for its own side");
            End(g);
        });
        WildSetsProof.Held("FP1_029", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 3 && card.ATK == 4 && card.Health == 4, "Dancing Swords is a 3-mana 4/4");
            var swords = Summon(p, id);
            Resume(g, next => {
                int before = next.Player2.HandZone.Count;
                Kill(next, (Minion)next.IdEntityDic[swords.Id]);
                Core2026Proof.Check(next.Player2.HandZone.Count == before + 1, "the Deathrattle runs on a clone and a snapshot restore");
            });
            int oh = opp.HandZone.Count, od = opp.DeckZone.Count, mh = p.HandZone.Count, md = p.DeckZone.Count;
            Kill(g, swords);
            Core2026Proof.Check(opp.HandZone.Count == oh + 1 && opp.DeckZone.Count == od - 1 && p.HandZone.Count == mh && p.DeckZone.Count == md, "my opponent draws a card, I do not");
            HiddenFromOpponent(g, opp, new[] { opp.HandZone.Last().Id }, "the drawn card");
            var quiet = Summon(p, id); quiet.Silence(); Kill(g, quiet);
            Core2026Proof.Check(opp.HandZone.Count == oh + 1, "Silence removes the Deathrattle");
            Summon(p, "FP1_031"); Kill(g, Summon(p, id));
            Core2026Proof.Check(opp.HandZone.Count == oh + 3, "Baron Rivendare: two draws");
            EmptyHand(opp); for (int i = 0; i < 10; i++) Add(opp, "CS2_182");
            int deck = opp.DeckZone.Count; Kill(g, Summon(p, id));
            Core2026Proof.Check(opp.HandZone.Count == 10 && opp.DeckZone.Count == deck - 2, "a full hand burns the cards");
            foreach (var d in opp.DeckZone.ToArray()) opp.SetasideZone.Add(opp.DeckZone.Remove(d));
            EmptyHand(opp); StandardSetsProof.ParkBoard(p);
            int hp = opp.Hero.Damage; Kill(g, Summon(p, id));
            Core2026Proof.Check(opp.Hero.Damage == hp + 1 && opp.HandZone.Count == 0, "an empty deck: the opponent takes fatigue damage");
            End(g);
            int mine = p.HandZone.Count; Kill(g, Summon(opp, id));
            Core2026Proof.Check(p.HandZone.Count == mine + 1, "the opponent's Swords make me draw");
            End(g);
        });
        WildSetsProof.Held("FP1_030", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 5 && card.ATK == 5 && card.Health == 5 && card.IsRace(Race.UNDEAD) && card.Rarity == Rarity.LEGENDARY, "Loatheb is a 5-mana 5/5 Undead Legendary");
            var oppSpell = Add(opp, "CS2_029"); var oppMinion = Add(opp, "CS2_182"); var mySpell = Add(p, "CS2_029");
            var held = Add(p, id);
            Twin(g, p, held, null, "Loatheb");
            Core2026Proof.Check(oppSpell.Cost == 9 && oppMinion.Cost == 4 && mySpell.Cost == 4, "the opponent's spells cost 5 more, minions and my spells do not: " + oppSpell.Cost + "," + oppMinion.Cost + "," + mySpell.Cost);
            Resume(g, next => {
                foreach (var d in next.Player2.DeckZone.ToArray()) next.Player2.SetasideZone.Add(next.Player2.DeckZone.Remove(d));
                next.Player2.DeckZone.Add(Entity.FromCard(next.Player2, Cards.FromId("CS2_029")));
                End(next);
                var spell = next.Player2.HandZone.First(c => c.Card.Id == "CS2_029");
                Core2026Proof.Check(spell.Cost == 9, "still 5 more during the opponent's turn, on a clone and a snapshot restore");
                var drawn = next.Player2.HandZone.Last();
                Core2026Proof.Check(drawn.Card.Id == "CS2_029" && drawn.Cost == 9, "a spell drawn at the start of their turn costs 5 more too: " + string.Join(",", next.Player2.HandZone.Select(c => c.Card.Id + ":" + c.Cost)));
                End(next);
                Core2026Proof.Check(spell.Cost == 4 && drawn.Cost == 4, "back to normal when their turn ends");
            });
            End(g);
            Core2026Proof.Check(oppSpell.Cost == 9, "the opponent's turn: 5 more");
            opp.BaseMana = 10; opp.UsedMana = 0;
            Core2026Proof.Check(opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source.Id == oppSpell.Id), "a 9-cost spell is playable with 10 Mana");
            End(g);
            Core2026Proof.Check(oppSpell.Cost == 4 && mySpell.Cost == 4, "gone after the opponent's turn");
            End(g); Core2026Proof.Check(oppSpell.Cost == 4, "and it does not come back");
            End(g);
            // Two Loathebs stack and can make a spell unplayable.
            var l2 = Add(p, id); var l3 = Add(p, id);
            PlayEntity(g, p, l2); PlayEntity(g, p, l3);
            Core2026Proof.Check(oppSpell.Cost == 14, "two Loathebs: +10");
            End(g); opp.BaseMana = 10; opp.UsedMana = 0;
            Core2026Proof.Check(!opp.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source.Id == oppSpell.Id), "a 14-cost spell is not playable");
            End(g);
            Core2026Proof.Check(oppSpell.Cost == 4, "both are gone");
            // Silence or death of Loatheb does not end the Battlecry's effect early; summoning has no Battlecry.
            Summon(p, id); Core2026Proof.Check(oppSpell.Cost == 4, "summoning has no Battlecry");
            var l4 = (Minion)Play(g, p, id); l4.Silence();
            Core2026Proof.Check(oppSpell.Cost == 9, "Silence on Loatheb does not undo the Battlecry");
            Kill(g, l4); Core2026Proof.Check(oppSpell.Cost == 9, "nor does his death");
            End(g); End(g);
            // The opponent's Loatheb hits my spells.
            End(g);
            var theirs = Add(opp, id); PlayEntity(g, opp, theirs);
            Core2026Proof.Check(mySpell.Cost == 9 && oppSpell.Cost == 4, "the opponent's Loatheb raises my spell costs only");
            End(g);
            Core2026Proof.Check(mySpell.Cost == 9, "still raised on my turn");
            End(g); Core2026Proof.Check(mySpell.Cost == 4, "and gone after");
        });
        WildSetsProof.Held("FP1_031", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 4 && card.ATK == 1 && card.Health == 7 && card.IsRace(Race.UNDEAD) && card.Rarity == Rarity.LEGENDARY, "Baron Rivendare is a 4-mana 1/7 Undead Legendary");
            var baron = Summon(p, id); var swords = Summon(p, "FP1_029"); var egg = Summon(p, "FP1_007");
            int oh = opp.HandZone.Count;
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[swords.Id]);
                Core2026Proof.Check(next.Player2.HandZone.Count == oh + 2, "a Deathrattle triggers twice on a clone and a snapshot restore");
            });
            Kill(g, swords);
            Core2026Proof.Check(opp.HandZone.Count == oh + 2, "my Deathrattle triggers twice");
            Kill(g, egg);
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "FP1_007t") == 2, "a summoning Deathrattle summons twice");
            var enemy = Summon(opp, "FP1_029"); int mh = p.HandZone.Count; Kill(g, enemy);
            Core2026Proof.Check(p.HandZone.Count == mh + 1, "the opponent's Deathrattle is not doubled");
            var second = Summon(p, id);
            Kill(g, Summon(p, "FP1_029"));
            Core2026Proof.Check(opp.HandZone.Count == oh + 4, "two Barons do not triple it");
            Kill(g, second); Kill(g, Summon(p, "FP1_029"));
            Core2026Proof.Check(opp.HandZone.Count == oh + 6, "one Baron left: still twice");
            baron.Silence(); Kill(g, Summon(p, "FP1_029"));
            Core2026Proof.Check(opp.HandZone.Count == oh + 7, "a silenced Baron has no effect");
            Core2026Proof.Check(baron.AttackDamage == 1 && baron.Health == 7, "fixture");
            StandardSetsProof.ParkBoard(p);
            var b2 = Summon(p, id); var s2 = Summon(p, "FP1_029");
            b2.ToBeDestroyed = true; s2.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(opp.HandZone.Count == oh + 8, "Baron dying in the same death phase as the minion: the Deathrattle triggers once (retail 34104_subspell.power.log; deathrattle-doubler-simultaneous-death record)");
            var b3 = Summon(p, id); var w = Summon(p, "FP1_024"); var vic = Summon(p, "CS2_182"); Kill(g, w);
            Core2026Proof.Check(vic.Damage == 2, "Unstable Ghoul deals 2 with Baron");
            Kill(g, b3);
            End(g);
            StandardSetsProof.ParkBoard(opp); StandardSetsProof.ParkBoard(p);
            Summon(opp, id); int mine = p.HandZone.Count; Kill(g, Summon(opp, "FP1_029"));
            Core2026Proof.Check(p.HandZone.Count == mine + 2, "the opponent's Baron doubles the opponent's Deathrattles");
            End(g);
        });
        BlockedDuplicate();
    }

    /// <summary>FP1_018 Duplicate: BLOCKED-STANDARD, see the class comment.</summary>
    internal static void BlockedDuplicate()
    {
        BlockedFailures.Clear();
        Blocked("FP1_018", (g, p, id) => {
            var opp = p.Opponent; var card = Cards.FromId(id);
            Core2026Proof.Check(card.Cost == 3 && card.IsSecret && card.Class == CardClass.MAGE, "Duplicate is a 3-mana Mage Secret");
            var a = Summon(p, "CS2_182"); Play(g, p, "CS2_092", a);
            var dup = Add(p, id);
            Twin(g, p, dup, null, "Duplicate");
            Core2026Proof.Check(p.SecretZone.Count == 1, "the Secret is in play");
            Slay(g, p, a);
            Core2026Proof.Check(p.SecretZone.Count == 1, "a Secret only works on the opponent's turn");
            End(g);
            Slay(g, opp, Summon(opp, "CS2_182"));
            Core2026Proof.Check(p.SecretZone.Count == 1, "an enemy minion dying does not trigger it");
            End(g);
            EmptyHand(p);
            var b = Summon(p, "CS2_182"); Play(g, p, "CS2_092", b);
            End(g);
            Slay(g, opp, b);
            var copies = p.HandZone.Where(c => c.Card.Id == "CS2_182").ToArray();
            Core2026Proof.Check(copies.Length == 2 && p.SecretZone.Count == 0 && copies.All(c => c.Cost == 4), "two plain copies enter my hand and the Secret is used");
            HiddenFromOpponent(g, p, copies.Select(c => c.Id), "the copies");
            End(g);
            var theirs = Add(opp, id); End(g); PlayEntity(g, opp, theirs);
            End(g);
            Slay(g, p, Summon(opp, "CS2_179"));
            Core2026Proof.Check(opp.HandZone.Count(c => c.Card.Id == "CS2_179") == 2 && p.HandZone.Count(c => c.Card.Id == "CS2_179") == 0, "the opponent's Duplicate fills the opponent's hand");
            End(g);
        });
        if (BlockedFailures.Count > 0) throw new Exception(string.Join("\n", BlockedFailures));
    }
}
