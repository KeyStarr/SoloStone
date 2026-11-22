using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// #198: effects that lower, steal or set Health. Retail rule (HSReplay replay gyvzAwjnwLotyqj595ZtrT, build 229238, and the
// set-Health behaviour of Hunter's Mark and Equality): "-N Health" and "steal N Health" lower the HEALTH tag only and
// never touch DAMAGE, so current Health falls by the full N and a damaged minion can die; a steal is capped at the
// victim's current Health; "set Health to N" gives max N and no damage.
internal static class LowerHealthProof
{
    [GroupProof("lower-health")]
    internal static void Run()
    {
        var log = new List<string>();
        void Probe(string name, Action body)
        {
            try { body(); log.Add("ok   " + name); }
            catch (Exception e) { log.Add("FAIL " + name + ": " + e.Message); }
        }
        // A 4/5 Chillwind Yeti on the enemy side, with `damage` damage.
        Minion Victim(Game g, int damage = 0, int baseHealth = 5)
        {
            var m = Summon(g.Player2, "CS2_182"); m.BaseHealth = baseHealth; m.Damage = damage; return m;
        }
        void Expect(bool ok, string what) => Core2026Proof.Check(ok, what);
        string S(Minion m) => $"{m.Zone?.Type} base={m.BaseHealth} hp={m.Health} dmg={m.Damage}";

        foreach (var id in new[] { "CS2_084", "EX1_619" })
            Probe(id + " set Health clears damage", () => {
                var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
                var m = Victim(g, 3); var full = Victim(g, 0, 7);
                if (id == "CS2_084") Play(g, p, id, m); else Play(g, p, id);
                Expect(m.BaseHealth == 1 && m.Health == 1 && m.Damage == 0, "damaged: " + S(m));
                if (id == "EX1_619") Expect(full.BaseHealth == 1 && full.Health == 1 && full.Damage == 0, "undamaged: " + S(full));
            });
        Probe("LOE_017 Keeper sets 3/3", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var m = Victim(g, 4); Play(g, p, "LOE_017", m);
            Expect(m.BaseHealth == 3 && m.Health == 3 && m.Damage == 0, S(m));
        });
        Probe("EX1_059 Crazed Alchemist swaps", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var m = Victim(g, 4); Play(g, p, "EX1_059", m);
            Expect(m.AttackDamage == 1 && m.BaseHealth == 4 && m.Health == 4 && m.Damage == 0, S(m));
        });
        Probe("TLC_813 Purifying Vines enemy", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var damaged = Victim(g, 3); var healthy = Victim(g); var weak = Victim(g, 4);
            Play(g, p, "TLC_813", damaged);
            Expect(damaged.BaseHealth == 3 && damaged.Health == 0 && damaged.Zone.Type == Zone.GRAVEYARD, "damaged dies: " + S(damaged));
            Play(g, p, "TLC_813", healthy);
            Expect(healthy.BaseHealth == 3 && healthy.Health == 3 && healthy.Damage == 0, "undamaged: " + S(healthy));
            Play(g, p, "TLC_813", weak);
            Expect(weak.Zone.Type == Zone.GRAVEYARD, "1 Health dies: " + S(weak));
        });
        Probe("TLC_831 Pterrordax Egg steals 1 from each", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var damaged = Victim(g, 4); var healthy = Victim(g);
            var egg = Summon(p, "TLC_831"); Kill(g, egg);
            var dax = p.BoardZone.Single(m => m.Card.Id == "TLC_831t");
            Expect(damaged.Zone.Type == Zone.GRAVEYARD && healthy.BaseHealth == 4 && healthy.Health == 4, $"victims {S(damaged)} / {S(healthy)}");
            Expect(dax.Health == dax.Card.Health + 2, "gains 2: " + S(dax));
        });
        Probe("TLC_831 Pterrordax Egg: nothing to steal from a minion Abomination left at 0 Health", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var victim = Victim(g, 3);                     // 2 current Health
            var abomination = Summon(p, "EX1_097"); var egg = Summon(p, "TLC_831");
            abomination.ToBeDestroyed = true; egg.ToBeDestroyed = true;
            g.DeathProcessingAndAuraUpdate();              // Abomination's 2 damage leaves the Yeti at 0, then the Egg resolves
            var dax = p.BoardZone.Single(m => m.Card.Id == "TLC_831t");
            Expect(victim.AppliedEnchantments?.Any(e => e.Card.Id == "TLC_831e") != true && dax.Health == dax.Card.Health,
                $"no steal from 0 Health: Pterrordax {dax.Health}/{dax.Card.Health}, victim {S(victim)}");
        });
        Probe("BE_287 G'huun steals", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var damaged = Victim(g, 3); var healthy = Victim(g, 0, 20);
            var src = (Minion)Play(g, p, "BE_287");
            Expect(damaged.Zone.Type == Zone.GRAVEYARD, "lowest dies: " + S(damaged));
            Expect(src.Health == src.Card.Health + 6, $"gains the full budget ({src.Health})");
        });
        Probe("GIL_623 Witchwood Grizzly", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            for (int i = 0; i < 10; i++) Add(g.Player2, "CS2_182");
            var bear = (Minion)Play(g, p, "GIL_623");
            Expect(bear.BaseHealth == 2 && bear.Health == 2, S(bear));
        });
        Probe("CATA_699 Dread Leviathan: undamaged and damaged victims, capped steal, repeated picks", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var shortOne = Victim(g, 3);            // 5 max, 2 current: a steal of 3 is capped at 2
            var big = Victim(g, 0, 20);              // undamaged
            var src = (Minion)Play(g, p, "CATA_699");
            PlayerDecisionProof.Answer(g, shortOne.Id);
            Expect(shortOne.Zone.Type == Zone.GRAVEYARD && src.Health == 10, $"short victim dies, steals only 2: {S(shortOne)}, Leviathan {src.Health}");
            PlayerDecisionProof.Answer(g, big.Id); PlayerDecisionProof.Answer(g, big.Id);
            Expect(big.BaseHealth == 14 && big.Health == 14 && big.Damage == 0 && src.Health == 16, $"same target three-way: {S(big)}, Leviathan {src.Health}");
        });
        Probe("CATA_699 Dread Leviathan: damaged victim loses current Health by the full 3 and can die", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var hurt = Victim(g, 15, 20);            // 20 max, 5 current
            var filler = Victim(g);
            var src = (Minion)Play(g, p, "CATA_699");
            PlayerDecisionProof.Answer(g, hurt.Id);
            Expect(hurt.BaseHealth == 17 && hurt.Health == 2 && hurt.Damage == 15 && src.Health == 11, $"first steal: {S(hurt)}, Leviathan {src.Health}");
            PlayerDecisionProof.Answer(g, hurt.Id);
            Expect(hurt.Zone.Type == Zone.GRAVEYARD && src.Health == 13, $"second steal capped at 2 kills it: {S(hurt)}, Leviathan {src.Health}");
            PlayerDecisionProof.Answer(g, filler.Id);
            Expect(filler.BaseHealth == 2 && filler.Health == 2 && src.Health == 16, $"third pick goes to the next target: {S(filler)}, Leviathan {src.Health}");
        });
        Probe("CATA_699 Dread Leviathan: history lowers HEALTH and leaves DAMAGE alone, so the client shows the current drop", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var hurt = Victim(g, 15, 20);
            var src = (Minion)Play(g, p, "CATA_699");
            int mark = g.PowerHistory.Full.Count;
            PlayerDecisionProof.Answer(g, hurt.Id);
            var changes = g.PowerHistory.Full.Skip(mark).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().Where(e => e.EntityId == hurt.Id).ToArray();
            Expect(changes.Any(e => e.Tag == GameTag.HEALTH && e.Value == 17) && !changes.Any(e => e.Tag == GameTag.DAMAGE),
                "victim history: " + string.Join(", ", changes.Select(e => $"{e.Tag}={e.Value}")));
            var gained = g.PowerHistory.Full.Skip(mark).OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().Where(e => e.EntityId == src.Id && e.Tag == GameTag.HEALTH).ToArray();
            Expect(gained.Any(e => e.Value == src.BaseHealth), "Leviathan history raises HEALTH");
        });
        Probe("CATA_699 Dread Leviathan: a target that dies mid-battlecry", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            var first = Victim(g, 4); var second = Victim(g);
            var src = (Minion)Play(g, p, "CATA_699");
            PlayerDecisionProof.Answer(g, first.Id);                // 1 current: steals 1, dies
            Expect(first.Zone.Type == Zone.GRAVEYARD && src.Health == 9 && p.Choice.Choices.SequenceEqual(new[] { second.Id }), "dead target leaves the choice");
            PlayerDecisionProof.Answer(g, second.Id); PlayerDecisionProof.Answer(g, second.Id);
            Expect(second.Zone.Type == Zone.GRAVEYARD && src.Health == 14 && p.Choice == null, $"{S(second)}, Leviathan {src.Health}");
        });
        Probe("EDR_810 Bloated Leech: damaged hero victim, steal capped at current Health", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            Summon(p, "EDR_810t"); Summon(p, "EDR_810"); Summon(p, "EDR_810"); // drain 3
            p.Opponent.Hero.Damage = p.Opponent.Hero.BaseHealth - 2;           // current 2: lowest Health enemy
            int mine = p.Hero.BaseHealth, theirs = p.Opponent.Hero.BaseHealth;
            End(g);
            Expect(p.Hero.BaseHealth == mine + 2 && p.Opponent.Hero.BaseHealth == theirs - 2 && p.Opponent.Hero.Health == 0 && p.Opponent.Hero.Damage == theirs - 2
                && p.Opponent.PlayState == PlayState.LOST,
                $"hero gains 2, victim hero {p.Opponent.Hero.BaseHealth}/{p.Opponent.Hero.Health}");
        });
        Probe("EDR_810 Bloated Leech short victim", () => {
            var g = B15DrawReplacementProof.NewGame(); var p = g.Player1;
            Summon(p, "EDR_810t"); Summon(p, "EDR_810"); Summon(p, "EDR_810"); // drain 3
            var m = Victim(g, 3); // 2 current Health left, base 5
            int hero = p.Hero.BaseHealth;
            End(g);
            Expect(m.Zone.Type == Zone.GRAVEYARD && p.Hero.BaseHealth == hero + 2 && p.Hero.Health == p.Hero.BaseHealth,
                $"steal capped at current Health 2: victim {S(m)}, hero base {p.Hero.BaseHealth} (was {hero})");
        });
        Console.WriteLine(string.Join("\n", log));
        if (log.Any(l => l.StartsWith("FAIL"))) throw new Exception("lower-health proof failed");
        Console.WriteLine("PASS lower-health");
    }
}
