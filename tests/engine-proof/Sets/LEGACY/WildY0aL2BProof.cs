using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static WildY0aL2BProof;

// Wave Y0a, lane l2b: the Legacy-set cards. The group proof lives in EXPERT1/WildY0aL2BProof.cs and calls Run() below.
internal static class WildY0aL2BLegacyProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case (used by Standard until #87).
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    internal static void Run()
    {
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_186", (g, p, id) => {
            var o = p.Opponent;
            Play(g, p, IceBlock);
            var empty = (Minion)Play(g, p, id);
            Check(empty.AttackDamage == 5 && empty.Health == 4, "SI:7 Infiltrator is a 5/4");
            Check(p.SecretZone.Count == 1 && o.SecretZone.Count == 0, "no enemy Secret: nothing happens, my own Secret stays");
            End(g);
            Play(g, o, "UNG_028");
            End(g);
            Check(o.SecretZone.Quest != null && o.SecretZone.Count == 0, "fixture: the enemy has a Quest and no Secret");
            Play(g, p, id);
            Check(o.SecretZone.Quest != null && o.SecretZone.Count == 0, "a Quest is not a Secret and survives");
            End(g);
            Play(g, o, IceBarrier); Play(g, o, Counterspell); Play(g, o, IceBlock);
            End(g);
            Check(o.SecretZone.Count == 3 && o.SecretZone.Quest != null, "fixture: Quest plus three enemy Secrets");
            Replay(g, b => {
                var enemy = b.Player2;
                int graves = enemy.GraveyardZone.Count;
                Play(b, b.Player1, id);
                var left = enemy.SecretZone.Select(s => s.Card.Id).ToArray();
                Check(enemy.SecretZone.Count == 2 && enemy.SecretZone.Quest != null, "exactly one enemy Secret destroyed, the Quest kept: " + string.Join(",", left));
                Check(enemy.GraveyardZone.Count == graves + 1, "the destroyed Secret goes to their graveyard: " + graves + ">" + enemy.GraveyardZone.Count
                    + " " + string.Join(",", enemy.GraveyardZone.Select(s => s.Card.Id + "@" + s.Zone.Type)) + " left " + string.Join(",", left));
                Check(b.Player1.SecretZone.Count == 1, "my own Secret is never a target");
            });
            Play(g, p, id); Play(g, p, id);
            Check(o.SecretZone.Count == 0 && o.SecretZone.Quest != null, "three Infiltrators clear all three Secrets, one each");
            Play(g, p, id);
            Check(o.SecretZone.Quest != null && p.SecretZone.Count == 1, "with only a Quest left the Battlecry does nothing");
            End(g);
            Summon(g.Player2, id);
            Check(p.SecretZone.Count == 1, "summoning without a Battlecry destroys nothing");
            Play(g, g.Player2, id);
            Check(p.SecretZone.Count == 0, "the opponent's Infiltrator destroys my Secret");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_187", (g, p, id) => {
            var devourer = (Minion)Play(g, p, id);
            Check(devourer.AttackDamage == 4 && devourer.Health == 8 && devourer.Card.IsRace(Race.ELEMENTAL), "Arcane Devourer is a 4/8 Elemental");
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(devourer.AttackDamage == 6 && devourer.Health == 10, "a spell gives +2/+2");
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(devourer.AttackDamage == 8 && devourer.Health == 12, "each spell stacks");
            Play(g, p, Yeti);
            Check(devourer.AttackDamage == 8, "a minion is not a spell");
            Play(g, p, IceBarrier);
            Check(devourer.AttackDamage == 10 && devourer.Health == 14, "a Secret is a spell too");
            Play(g, p, "CS2_032");
            Check(devourer.AttackDamage == 12 && devourer.Health == 16, "a spell without a target counts");
            devourer.Damage = 5;
            Replay(g, b => {
                var d = (Minion)b.IdEntityDic[devourer.Id];
                Play(b, b.Player1, Moonfire, b.Player2.Hero);
                Check(d.AttackDamage == 14 && d.Health == 18 - 5 && d.Damage == 5, "the Health bonus keeps existing damage, on clones too");
            });
            End(g);
            Play(g, g.Player2, Moonfire, p.Hero);
            Check(devourer.AttackDamage == 14, "an enemy spell does nothing");
            End(g);
            devourer.Damage = 0;
            Mute(g, devourer);
            Check(devourer.AttackDamage == 4 && devourer.Health == 8 && devourer.Damage == 0, "Silence drops every bonus: " + devourer.AttackDamage + "/" + devourer.Health);
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(devourer.AttackDamage == 4, "a Silenced Devourer stops growing");
            var second = (Minion)Play(g, p, id); var third = Summon(p, id);
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(second.AttackDamage == 6 && third.AttackDamage == 6 && devourer.AttackDamage == 4, "every active Devourer grows");
            Kill(g, third);
            Play(g, p, Moonfire, p.Opponent.Hero);
            Check(second.AttackDamage == 8, "a dead Devourer does not matter");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_188", (g, p, id) => {
            var pool = new HashSet<string>();
            for (int i = 0; i < 8; i++)
            {
                StandardSetsProof.ParkBoard(p);
                var hand = (Minion)Add(p, id);
                PlayEntity(g, p, hand);
                Check(p.BoardZone.Count == 2 && p.BoardZone[0].Card.Id == id, "Stablehand plus exactly one summoned minion");
                var beast = p.BoardZone[1];
                Check(beast.Card.IsRace(Race.BEAST) && beast.Card.Collectible && beast.Card.Type == CardType.MINION
                    && beast.Controller == p && Cards.IsFormatLegal(beast.Card, FormatType.FT_WILD), "an implemented collectible Wild Beast: " + beast.Card.Id);
                pool.Add(beast.Card.Id);
                Check(hand.AttackDamage == 5 && hand.Health == 5, "Barrens Stablehand is a 5/5");
            }
            Check(pool.Count >= 2, "the Beast is random: " + string.Join(",", pool));
            StandardSetsProof.ParkBoard(p);
            var left = Summon(p, Yeti); var right = Summon(p, Boar);
            var middle = StandardSetsProof.PlayAt(g, p, id, 1);
            Check(p.BoardZone.Count == 4 && p.BoardZone[0] == left && p.BoardZone[1] == middle && p.BoardZone[3] == right
                && p.BoardZone[2].Card.IsRace(Race.BEAST), "the Beast arrives just right of the Stablehand: " + string.Join(",", p.BoardZone.Select(m => m.Card.Id)));
            StandardSetsProof.ParkBoard(p);
            Summon(p, id);
            Check(p.BoardZone.Count == 1, "a summoned Stablehand has no Battlecry");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 5; i++) Summon(p, Yeti);
            Play(g, p, id);
            Check(p.BoardZone.Count == 7, "one free slot: the Beast fills it");
            StandardSetsProof.ParkBoard(p);
            for (int i = 0; i < 6; i++) Summon(p, Yeti);
            Play(g, p, id);
            Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == Yeti) == 6, "a full board gets no Beast");
            Replay(g, b => {
                var o = b.Player2;
                End(b);
                int mine = b.Player1.BoardZone.Count;
                Play(b, o, id);
                Check(o.BoardZone.Count == 2 && o.BoardZone[1].Card.IsRace(Race.BEAST) && o.BoardZone[1].Controller == o
                    && b.Player1.BoardZone.Count == mine, "the opponent's Stablehand gives the opponent its Beast");
            });
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_189", (g, p, id) => {
            Trim(p, 0);
            var ids = new HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                StandardSetsProof.ParkBoard(p);
                var bw = (Minion)Add(p, id);
                Check(bw.AttackDamage == 3 && bw.Health == 2 && bw.Cost == 2 && bw.Card.IsRace(Race.DRAGON), "Brightwing is a 3/2 Dragon for 2");
                PlayEntity(g, p, bw);
                var got = p.HandZone.Single();
                Check(got.Card.Type == CardType.MINION && got.Card.Rarity == Rarity.LEGENDARY && got.Card.Collectible
                    && Cards.IsFormatLegal(got.Card, FormatType.FT_WILD),
                    "an implemented collectible Legendary minion: " + got.Card.Id);
                Check(!Hidden(g, p.PlayerId, got.Id) && Hidden(g, p.Opponent.PlayerId, got.Id),
                    "the new card is shown to its owner and hidden from the opponent");
                ids.Add(got.Card.Id);
                Trim(p, 0);
            }
            Check(ids.Count >= 2, "the Legendary is random: " + string.Join(",", ids));
            Summon(p, id);
            Check(p.HandZone.Count == 0, "a summoned Brightwing has no Battlecry");
            Replay(g, b => {
                Play(b, b.Player1, id);
                Check(b.Player1.HandZone.Count == 1 && b.Player1.HandZone[0].Card.Rarity == Rarity.LEGENDARY, "same result on a clone");
            });
            Trim(p, 0);
            End(g);
            int mine = p.HandZone.Count;
            var foe = g.Player2;
            Play(g, foe, id);
            var theirs = foe.HandZone.Last();
            Check(p.HandZone.Count == mine && theirs.Card.Rarity == Rarity.LEGENDARY && theirs.Card.Type == CardType.MINION,
                "the opponent's Brightwing fills their hand, not mine");
            Check(Hidden(g, p.PlayerId, theirs.Id), "and I cannot see it");
            End(g);
            Trim(p, 0);
            var brann = Play(g, p, "LOE_077");
            for (int i = 0; i < 9; i++) Add(p, Yeti);
            var last = Add(p, id);
            Check(p.HandZone.Count == 10, "fixture: full hand");
            PlayEntity(g, p, last);
            Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Rarity == Rarity.LEGENDARY) == 1,
                "with Brann the second Legendary burns on a full hand");
        });

        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_190", (g, p, id) => {
            var buffed = Summon(p, Yeti); var second = Summon(p, "CS2_179"); var third = Summon(p, "CS2_200");
            var enemy = Summon(p.Opponent, Yeti);
            Play(g, p, "CS2_004", buffed);
            Check(buffed.Health == 7, "fixture: Power Word: Shield");
            Kill(g, buffed); Kill(g, enemy); Kill(g, second); Kill(g, third);
            Check(p.NumFriendlyMinionsThatDiedThisTurn == 3, "fixture: three of mine died");
            Summon(p, "CS2_171"); // an anchor on the right: "right of Whitemane" and "far right" differ
            Replay(g, b => {
                var q = b.Player1;
                var w = (Minion)StandardSetsProof.PlayAt(b, q, id, 0);
                Check(w.AttackDamage == 5 && w.Health == 7, "High Inquisitor Whitemane is a 5/7");
                Check(q.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { id, Yeti, "CS2_179", "CS2_200", "CS2_171" }),
                    "my dead minions return in the order they died, right of Whitemane: " + string.Join(",", q.BoardZone.Select(m => m.Card.Id)));
                var back = q.BoardZone.Single(m => m.Card.Id == Yeti);
                Check(back.Health == 5 && back.Damage == 0 && back.AttackDamage == 4, "they come back fresh, without the old buff");
                Check(b.Player2.BoardZone.Count == 0, "the enemy minion that died is not mine to revive");
            });
            End(g); End(g);
            StandardSetsProof.ParkBoard(p);
            var left = Summon(p, "CS2_171"); var right = Summon(p, "CS2_171");
            var p1 = Summon(p, Yeti); var p2 = Summon(p, "CS2_179"); Kill(g, p1); Kill(g, p2);
            var middle = (Minion)StandardSetsProof.PlayAt(g, p, id, 1);
            Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { "CS2_171", id, Yeti, "CS2_179", "CS2_171" })
                && p.BoardZone[0] == left && p.BoardZone[4] == right,
                "the revived minions arrive right of Whitemane, in death order: " + string.Join(",", p.BoardZone.Select(m => m.Card.Id)));
            End(g); End(g);
            StandardSetsProof.ParkBoard(p);
            var fish = Summon(p, Murloc); Kill(g, fish); var caller = Summon(p, "EX1_509");
            Play(g, p, id);
            Check(caller.AttackDamage == 2 && p.BoardZone.Count(m => m.Card.Id == Murloc) == 1, "a revived Murloc is a real summon: Tidecaller grows: " + caller.AttackDamage + " " + string.Join(",", p.BoardZone.Select(m => m.Card.Id)));
            End(g); End(g);
            StandardSetsProof.ParkBoard(p);
            var old = Summon(p, Yeti); Kill(g, old);
            End(g); End(g);
            var a = Summon(p, Yeti); var b2 = Summon(p, "CS2_179"); var c = Summon(p, "CS2_200"); Kill(g, a); Kill(g, b2); Kill(g, c);
            for (int i = 0; i < 4; i++) Summon(p, "CS2_171");
            var whitemane = (Minion)Play(g, p, id);
            Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == Yeti) == 1 && p.BoardZone.Count(m => m.Card.Id == "CS2_179") == 1
                && p.BoardZone.All(m => m.Card.Id != "CS2_200"), "two free slots: the first two to die return, the last stays dead; last turn's death is ignored");
            Kill(g, whitemane);
            End(g);
            var opp = g.Player2;
            var theirs = Summon(opp, Yeti); Kill(g, theirs);
            Play(g, opp, id);
            Check(opp.BoardZone.Count(m => m.Card.Id == Yeti) == 1 && opp.BoardZone.Count == 2, "the opponent's Whitemane revives its owner's dead minion");
            End(g);
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(opp);
            Play(g, p, id);
            Check(p.BoardZone.Count == 1, "nothing died this turn: just the body");
        });
    }

    private static void Check(bool ok, string why) => Core2026Proof.Check(ok, why);

    // BLOCKED-STANDARD: EX1_399, EX1_506 and EX1_508 are in LocalCardCatalog.NewBasicIds (the Standard starter collection),
    // so no Wild identity row is written for them yet (waits for #87). Written against the engine's current Core-era numbers
    // (not run: a Case needs the identity row). Register with WildSetsProof.Case("EX1_399", Blocked_EX1_399) once unblocked.
    // 37.0 identity differences: EX1_399 is 2/8 (engine 2/7); EX1_506 and EX1_508 keep their stats and move to set LEGACY.
    private static void Blocked_EX1_399(Game g, Controller p, string id)
    {
        var berserker = (Minion)Play(g, p, id);
        Check(berserker.AttackDamage == 2 && berserker.Health == 8, "Gurubashi Berserker is a 2/8");
        Play(g, p, Moonfire, berserker);
        Check(berserker.AttackDamage == 5, "one damage instance gives +3 Attack");
        Play(g, p, Moonfire, berserker);
        Check(berserker.AttackDamage == 8 && berserker.Damage == 2, "each instance stacks");
        Play(g, p, Heal, berserker);
        Check(berserker.AttackDamage == 8, "healing does not remove the bonus");
        Mute(g, berserker);
        Check(berserker.AttackDamage == 2, "Silence removes it");
        Play(g, p, Moonfire, berserker);
        Check(berserker.AttackDamage == 2, "a Silenced Berserker does not grow");
    }

    private static void Blocked_EX1_506(Game g, Controller p, string id)
    {
        var hunter = (Minion)Play(g, p, id);
        Check(p.BoardZone.Count == 2 && p.BoardZone[1].Card.Id == "EX1_506a" && p.BoardZone[1].AttackDamage == 1, "a 1/1 Murloc Scout joins on the right");
        Check(hunter.AttackDamage == 2 && hunter.Health == 1, "Murloc Tidehunter is a 2/1");
        for (int i = 0; i < 5; i++) Summon(p, Yeti);
        Play(g, p, id);
        Check(p.BoardZone.Count == 7, "a full board gets no Scout");
    }

    private static void Blocked_EX1_508(Game g, Controller p, string id)
    {
        var raider = Summon(p, Murloc); var yeti = Summon(p, Yeti); var foe = Summon(p.Opponent, Murloc);
        var oracle = (Minion)Play(g, p, id);
        Check(oracle.AttackDamage == 1 && raider.AttackDamage == 3 && yeti.AttackDamage == 4 && foe.AttackDamage == 2,
            "other friendly Murlocs only");
        Mute(g, oracle);
        Check(raider.AttackDamage == 2, "Silence removes the aura");
    }
}
