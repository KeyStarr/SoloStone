using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

// "Skip Untouchable minions" as one engine rule: a card that counts, scales on, picks or affects board minions does not see
// an Untouchable one (a Dormant minion, or a permanent such as Nether Portal), while occupancy still does (hearthstone.wiki.gg
// "Permanent" and "Dormant" pages). Each part stages Nether Portal (UNG_829t2) and a Dormant minion next to ordinary minions and runs one card; for the cards
// that key on one specific card or on damage the sleeper is a sleeping copy of that card and the Portal is a bystander.
// UntouchableBoardSourceCheck keeps new raw board reads out of CardSets/Client.
internal static class UntouchableBoardProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, "UntouchableBoard: " + message);

    private static Minion Portal(Controller side)
    {
        var m = Summon(side, "UNG_829t2");
        Verify(m.Untouchable && !m.IsDormant, "Nether Portal is Untouchable without being Dormant");
        return m;
    }

    // A real Dormant card (Cyborg Patriarch sleeps for three turns).
    private static Minion Sleeper(Controller side)
    {
        var m = Summon(side, "TIME_046");
        Verify(m.IsDormant && m.Untouchable, "Cyborg Patriarch is Dormant");
        return m;
    }

    // A copy of a given card put to sleep, for cards that count or scale on one specific minion.
    private static Minion Asleep(Controller side, string id)
    {
        var m = Summon(side, id);
        m[GameTag.DORMANT] = 1;
        Verify(m.IsDormant && m.Untouchable, id + " staged Dormant");
        return m;
    }

    private static void Parts(List<string> failures, string name, Action part)
    {
        try { part(); } catch (Exception e) { failures.Add(name + ": " + e.Message); }
    }

    private static int CostOf(Controller p, string id) => ((Playable)Add(p, id)).Cost;

    internal static void Run()
    {
        var failures = new List<string>();

        // "Costs (1) less for each enemy minion": 3 ordinary minions, a permanent and a sleeper cost 3, not 5.
        foreach (var (id, baseCost) in new[] { ("BE_733", 8), ("TIME_715", 5) })
            Parts(failures, id + " per-minion cost", () => {
                var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
                Verify(CostOf(p, id) == baseCost, id + " base cost " + baseCost);
                for (int i = 0; i < 3; i++) Summon(p.Opponent, "CS2_182");
                Portal(p.Opponent); Sleeper(p.Opponent);
                g.DeathProcessingAndAuraUpdate();
                Verify(CostOf(p, id) == baseCost - 3, id + " cost with 3 ordinary + permanent + sleeper is " + CostOf(p, id) + ", want " + (baseCost - 3));
            });

        // "Immune while you control no other minions": a permanent and a sleeper do not count, an ordinary minion does.
        Parts(failures, "Survivalist", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var s = Summon(p, "CATA_613");
            Portal(p); Sleeper(p); Sleeper(p.Opponent);
            g.DeathProcessingAndAuraUpdate();
            Verify(s.IsImmune, "Survivalist stays Immune beside a permanent and sleepers");
            Summon(p, "CS2_182"); g.DeathProcessingAndAuraUpdate();
            Verify(!s.IsImmune, "an ordinary minion ends the Immunity");
        });

        // "Costs (2) if there are no minions on the battlefield".
        Parts(failures, "Solitary Prisoner", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            Portal(p.Opponent); Sleeper(p); g.DeathProcessingAndAuraUpdate();
            Verify(CostOf(p, "JAIL_204") == 2, "Solitary Prisoner costs 2 beside only a permanent and a sleeper (cost " + CostOf(p, "JAIL_204") + ")");
            Summon(p.Opponent, "CS2_182"); g.DeathProcessingAndAuraUpdate();
            Verify(CostOf(p, "JAIL_204") == 5, "an ordinary enemy minion restores the full cost");
        });

        // "Improved by Wisps you control": a sleeping Wisp does not count.
        Parts(failures, "Merry Moonkin", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var moon = Summon(p, "EDR_940");
            Summon(p, "CS2_231"); Asleep(p, "CS2_231"); Portal(p);
            g.DeathProcessingAndAuraUpdate();
            Verify(moon[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "Armor is 1 + one ordinary Wisp (shown " + moon[GameTag.TAG_SCRIPT_DATA_NUM_1] + ")");
        });

        // "+2/+2 for each damaged minion".
        Parts(failures, "Heir of Hereafter", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var a = Summon(p, "CS2_182"); a.Damage = 1;
            var b = Summon(p.Opponent, "CS2_182"); b.Damage = 1;
            var d = Sleeper(p.Opponent); d.Damage = 1; Portal(p);
            var heir = (Minion)Play(g, p, "TIME_871");
            Verify(heir.AttackDamage == 2 + 2 * 2, "Heir gains +2/+2 for the two ordinary damaged minions (attack " + heir.AttackDamage + ")");
        });

        // "Costs (1) if you control a Legendary card".
        Parts(failures, "Medivh's Triumph", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            Verify(CostOf(p, "CATA_308") == 5, "base cost 5");
            Asleep(p, "EX1_572"); Portal(p.Opponent); g.DeathProcessingAndAuraUpdate();
            Verify(CostOf(p, "CATA_308") == 5, "a sleeping Legendary does not discount it (cost " + CostOf(p, "CATA_308") + ")");
            Summon(p, "EX1_572"); g.DeathProcessingAndAuraUpdate();
            Verify(CostOf(p, "CATA_308") == 1, "an ordinary Legendary does");
        });

        // Atiesh "Costs (0) if you control Medivh".
        Parts(failures, "Atiesh and Medivh", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            int full = CostOf(p, "TIME_890t");
            Verify(full > 0, "Atiesh has a printed cost");
            Asleep(p, "TIME_890"); Portal(p); g.DeathProcessingAndAuraUpdate();
            Verify(CostOf(p, "TIME_890t") == full, "a sleeping Medivh does not make Atiesh free");
            Summon(p, "TIME_890"); g.DeathProcessingAndAuraUpdate();
            Verify(CostOf(p, "TIME_890t") == 0, "an ordinary Medivh does");
        });

        // "Give other Rafaams in your hand and battlefield +2/+2".
        Parts(failures, "Green Rafaam", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var a = Summon(p, "TIME_005t1"); var d = Asleep(p, "TIME_005t1"); var portal = Portal(p);
            int atk = a.AttackDamage; int portalAtk = portal.AttackDamage;
            Cast(g, p, "TIME_005t2");
            Verify(a.AttackDamage == atk + 2, "the ordinary Rafaam gets +2/+2");
            Verify(portal.AttackDamage == portalAtk, "the Portal is not buffed");
            Verify(d.AttackDamage == atk, "the sleeping Rafaam does not (attack " + d.AttackDamage + ")");
        });

        // "Give your Hydralodon Heads Rush".
        Parts(failures, "Hydralodon", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var a = Summon(p, "TSC_950t"); var d = Asleep(p, "TSC_950t"); var portal = Portal(p);
            Cast(g, p, "TSC_950");
            Verify(a.IsRush, "the ordinary Head gets Rush");
            Verify(!d.IsRush, "the sleeping Head does not");
            Verify(!portal.IsRush, "the Portal does not");
        });

        // "At the start of your turn, combine with any other friendly Beetles": a sleeping Beetle is left alone.
        Parts(failures, "Nythendra Beetles", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            Summon(p, "EDR_818t"); Summon(p, "EDR_818t"); var d = Asleep(p, "EDR_818t"); var portal = Portal(p);
            End(g); End(g);
            var body = p.BoardZone.GetAll().SingleOrDefault(m => m.Card.Id == "EDR_818");
            Verify(body != null && body.AttackDamage == 2, "two ordinary Beetles reform a 2/2 Nythendra (" + (body == null ? "none" : body.AttackDamage.ToString()) + ")");
            Verify(d.Zone.Type == Zone.PLAY && d.Card.Id == "EDR_818t", "the sleeping Beetle stays as it was");
            Verify(portal.Zone.Type == Zone.PLAY && portal.Card.Id == "UNG_829t2", "the Portal stays as it was");
        });

        // "If you control a Mech, Discover the perfect tech card": a sleeping Mech does not enable it.
        Parts(failures, "Mimiron", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            Asleep(p, "GVG_085"); Portal(p); g.DeathProcessingAndAuraUpdate();
            Cast(g, p, "BE_588");
            Verify(p.Choice == null, "a sleeping Mech does not give the Discover");
            var g2 = QuestLifecycleProof.Fixture(); var p2 = g2.Player1;
            Summon(p2, "GVG_085");
            Cast(g2, p2, "BE_588");
            Verify(p2.Choice != null, "an ordinary Mech does");
        });

        // Ravitz "Your left-most minion has Rush. Your right-most minion is Immune while attacking": left- and right-most skip
        // Untouchable minions (owner ruling 2026-11-18, no retail source found), as Flash Flood and Diabolus Rex do. A Portal at the
        // left edge and a sleeper at the right edge do not take the ends; with nothing Untouchable the plain ends get them.
        Parts(failures, "Ravitz edges", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var portal = Portal(p); var a = Summon(p, "CS2_182"); var ravitz = Summon(p, "BE_230"); var b = Summon(p, "CS2_182"); var sleeper = Sleeper(p);
            g.DeathProcessingAndAuraUpdate();
            Verify(a[GameTag.RUSH] == 1, "the left-most interactable minion has Rush beside an edge Portal (Rush " + a[GameTag.RUSH] + ")");
            Verify(b[GameTag.IMMUNE_WHILE_ATTACKING] > 0, "the right-most interactable minion is Immune while attacking beside an edge sleeper");
            Verify(ravitz[GameTag.RUSH] == 0 && ravitz[GameTag.IMMUNE_WHILE_ATTACKING] == 0, "Ravitz in the middle gets neither");
            Verify(portal[GameTag.RUSH] == 0 && sleeper[GameTag.IMMUNE_WHILE_ATTACKING] == 0, "the Untouchable edge minions get neither");
            var g2 = QuestLifecycleProof.Fixture(); var p2 = g2.Player1;
            var a2 = Summon(p2, "CS2_182"); Summon(p2, "BE_230"); var b2 = Summon(p2, "CS2_182");
            g2.DeathProcessingAndAuraUpdate();
            Verify(a2[GameTag.RUSH] == 1 && b2[GameTag.IMMUNE_WHILE_ATTACKING] > 0, "control: with nothing Untouchable the plain ends get Rush and Immune while attacking");
        });

        // The helper itself: board order is kept, Untouchable minions are left out, occupancy still counts them.
        Parts(failures, "BoardZone.Interactable", () => {
            var g = QuestLifecycleProof.Fixture(); var p = g.Player1;
            var a = Summon(p, "CS2_182"); var portal = Portal(p); var b = Summon(p, "CS2_231"); var sleeper = Sleeper(p); var c = Summon(p, "CS2_182");
            var seen = p.BoardZone.Interactable;
            Verify(seen.Length == 3 && seen[0] == a && seen[1] == b && seen[2] == c, "Interactable lists the three ordinary minions in board order");
            Verify(p.BoardZone.Count == 5 && p.BoardZone.SlotCount == 5 && p.BoardZone.FreeSpace == 2, "occupancy counts the permanent and the sleeper");
            Verify(!seen.Contains(portal) && !seen.Contains(sleeper), "neither Untouchable minion is listed");
        });

        if (failures.Count != 0) throw new Exception(string.Join("\n", failures));
        Console.WriteLine("PASS untouchable board: per-minion, count, threshold and pick effects skip permanents and sleepers");
    }

    private static void Cast(Game g, Controller p, string id, IPlayable target = null)
    {
        var source = Add(p, id);
        Cards.FromId(id).Power.PowerTask.Process(g, p, source, target, null);
    }
}
