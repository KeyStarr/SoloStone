using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HeraldProof
{
    internal static Game New(CardClass cls, int seed = 1, CardClass opponent = CardClass.MAGE)
    {
        var g = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, Logging = false, History = true, Player1HeroClass = cls,
            Player2HeroClass = opponent,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 })
        { foreach (var x in p.HandZone.ToArray()) { p.HandZone.Remove(x); p.SetasideZone.Add(x); } p.BaseMana = 10; }
        return g;
    }
    internal static Minion Herald(Game g, Controller p, IEntity source = null)
    {
        int id = new EffectContext(g, p.Id, (source ?? p.Hero).Id).Herald();
        g.DeathProcessingAndAuraUpdate();
        return id == 0 ? null : (Minion)g.IdEntityDic[id];
    }
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, "Herald: " + message);
    internal static void Run()
    {
        IndirectCasts();
        foreach (var cls in new[] { (CardClass)14, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR })
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = New(cls, seed); var p = g.Player1;
            Check(HeraldRules.Count(p) == 0 && !p.NativeTags.ContainsKey(HeraldRules.Progress), "no default progression tag");
            var source = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
            Minion first = null;
            for (int count = 0; count <= 5; count++)
            {
                int expectedFactor = new[] { 1, 1, 2, 2, 4, 4 }[count];
                var m = Herald(g, p, source);
                Check(m != null && m.Card.Id == HeraldRules.Soldier(cls), "class-specific identity");
                Check(m.ZonePosition == source.ZonePosition + (first == null ? 1 : 2) && right.ZonePosition > m.ZonePosition, "right of source, after earlier Soldier");
                Check(m.AttackDamage - (m.AuraEffects?.ATK ?? 0) == m.Card.ATK * expectedFactor
                    && m.BaseHealth == m.Card.Health * expectedFactor && m.Cost == 1, "realized stats, fixed cost");
                Check(m[GameTag.TAG_SCRIPT_DATA_NUM_1] == m.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] * expectedFactor,
                    "effect magnitude scales with body");
                Check(HeraldRules.Count(p) == count + 1 && HeraldRules.Count(p.Opponent) == 0, "owner-only count");
                if (first == null) first = m;
                else { Check(first.BaseHealth == first.Card.Health, "existing Soldier keeps creation tier"); Kill(g, m); }
            }
            var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
            Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored), "clone/snapshot state");
            int before = HeraldRules.Count(p);
            Herald(clone, clone.Player1); Herald(restored, restored.Player1);
            Check(HeraldRules.Count(p) == before && Canonical.State(clone) == Canonical.State(restored), "clone isolation and replay");
            var copied = (Minion)Generic.Copy(p, source, first, Zone.PLAY);
            g.DeathProcessingAndAuraUpdate();
            Check(copied.BaseHealth == first.BaseHealth && copied[HeraldRules.RealizedFactor] == 1, "copy does not reapply current tier");
            copied.Silence(); g.DeathProcessingAndAuraUpdate();
            Check(copied.AttackDamage - (copied.AuraEffects?.ATK ?? 0) == copied.Card.ATK && copied.BaseHealth == copied.Card.Health
                && copied[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && HeraldRules.Count(p) == before, "Silence removes scaling without clearing progression");
            var transformed = (Minion)Generic.ChangeEntityBlock(p, first, Cards.FromId("CS2_182"), true);
            Check(transformed[HeraldRules.RealizedFactor] == 0 && transformed[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0
                && HeraldRules.Count(p) == before, "transform clears Soldier state and never Heralds");
            while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            Check(Herald(g, p) == null && HeraldRules.Count(p) == before + 1, "full board advances without wrong returned ID");
        }
        Azshara(); AlAkir(); ChoGall(); Ragnaros(); Boundaries(); CopyAndReturn();
        Console.WriteLine("PASS Herald primitive: four profiles, tiers 0..5, summon/copy/Silence/transform/full board, expiry, cross-class fallback (a/b/c/d), indirect casts, clone/snapshot/RNG");
    }
    private static void Azshara()
    {
        var g = New((CardClass)14); var p = g.Player1;
        p[HeraldRules.Progress] = 4;
        var m = Herald(g, p);
        Check(p.Hero.AttackDamage == 4, "self-summon grants scaled hero attack");
        var copy = (Minion)Generic.Copy(p, m, m, Zone.PLAY);
        Check(p.Hero.AttackDamage == 8 && copy.AttackDamage == 8, "copied Soldier repeats summon ability once");
        End(g); Check(p.Hero.AttackDamage == 0, "hero attack expires");
        var raised = Summon(p, "CATA_525t");
        Check(raised.AttackDamage == 8 && p.Hero.AttackDamage == 4, "fresh resummon uses current tier, including opponent turn");
        End(g); Check(p.Hero.AttackDamage == 0, "opponent-turn buff expires that turn");
    }
    private static void AlAkir()
    {
        var g = New(CardClass.SHAMAN); var p = g.Player1; p[HeraldRules.Progress] = 4;
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var m = Herald(g, p, left);
        Check(left.AttackDamage == 8 && right.AttackDamage == 8, "adjacent +4 on both sides");
        var clone = g.CloneExact(history: true);
        ((Minion)clone.IdEntityDic[m.Id]).Silence(); clone.DeathProcessingAndAuraUpdate();
        Check(((Minion)clone.IdEntityDic[left.Id]).AttackDamage == 4 && left.AttackDamage == 8, "aura clone isolation");
        Kill(g, left); var next = Summon(p, "CS2_182");
        Check(right.AttackDamage == 8 && next.AttackDamage == 4, "adjacency recomputed");
        m.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(right.AttackDamage == 4, "Silence removes adjacent aura");
        g = New(CardClass.SHAMAN); p = g.Player1; p[HeraldRules.Progress] = 4;
        left = Summon(p, "CS2_182"); m = Herald(g, p);
        var enemy = Summon(p.Opponent, "CS2_182");
        new ControlTask(EntityType.TARGET, true).Process(g, p, p.Hero, m, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        Check(left.AttackDamage == 4 && enemy.AttackDamage == 8 && m[HeraldRules.RealizedFactor] == 4,
            "control change moves aura and preserves creation tier");
        Check(HeraldRules.Count(p) == 5 && HeraldRules.Count(p.Opponent) == 0, "control change does not transfer progression");
        Kill(g, m); Check(enemy.AttackDamage == 4, "source leaving play removes aura");
    }
    private static void ChoGall()
    {
        var g = New(CardClass.WARLOCK); var p = g.Player1; p[HeraldRules.Progress] = 2;
        var m = Herald(g, p); var victim = Summon(p, "CS2_182");
        End(g);
        Check(victim.Zone.Type == Zone.GRAVEYARD && m.AttackDamage == 6 && m.BaseHealth == 6, "right neighbor destroyed for +4/+4");
        victim = Summon(p, "CS2_182"); End(g);
        Check(victim.Zone.Type == Zone.PLAY && m.AttackDamage == 6, "opponent end turn does not trigger");
        m.Silence(); End(g); Check(victim.Zone.Type == Zone.PLAY, "Silence stops consumption");
        var isolated = New(CardClass.WARLOCK); var only = Herald(isolated, isolated.Player1);
        End(isolated); Check(only.AttackDamage == 1, "no neighbor means no buff");
    }
    private static void Ragnaros()
    {
        var g = New(CardClass.WARRIOR); var p = g.Player1; p[HeraldRules.Progress] = 4;
        var m = Herald(g, p); var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        Kill(g, m); Kill(clone, (Minion)clone.IdEntityDic[m.Id]); Kill(restored, (Minion)restored.IdEntityDic[m.Id]);
        Check(p.Opponent.Hero.Damage == 8, "deathrattle scaled damage");
        Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored), "random deathrattle replay");
        m = Herald(g, p); m.Silence(); Kill(g, m);
        Check(p.Opponent.Hero.Damage == 8, "Silence removes deathrattle");
        g = New(CardClass.WARRIOR, 2); p = g.Player1; p[HeraldRules.Progress] = 2;
        Summon(p.Opponent, "CS2_182"); Summon(p.Opponent, "CS2_182"); m = Herald(g, p);
        clone = g.CloneExact(history: true); restored = Game.LoadSnapshot(g.SaveSnapshot());
        Kill(g, m); Kill(clone, (Minion)clone.IdEntityDic[m.Id]); Kill(restored, (Minion)restored.IdEntityDic[m.Id]);
        Check(p.Opponent.Hero.Damage + p.Opponent.BoardZone.Sum(x => x.Damage) == 4,
            "exactly one random enemy takes scaled damage");
        Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored),
            "multi-target random selection uses replayable game RNG");
    }
    private static void CopyAndReturn()
    {
        var g = New((CardClass)14); var p = g.Player1;
        var m = Herald(g, p); m.Silence(); g.DeathProcessingAndAuraUpdate();
        int beforeAttack = p.Hero.AttackDamage;
        var copy = (Minion)Generic.Copy(p, m, m, Zone.PLAY); g.DeathProcessingAndAuraUpdate();
        Check(copy.IsSilenced && p.Hero.AttackDamage == beforeAttack, "silenced copy neither scales nor triggers");
        p[HeraldRules.Progress] = 4;
        new SabberStoneCore.Tasks.SimpleTasks.ReturnHandTask(EntityType.TARGET)
            .Process(g, p, p.Hero, m, new TaskStack());
        Check(m.Zone.Type == Zone.HAND && m[HeraldRules.RealizedFactor] == 0, "bounce clears realized state");
        PlayEntity(g, p, m);
        Check(m.AttackDamage == 8 && m.BaseHealth == 4 && p.Hero.AttackDamage == beforeAttack + 4,
            "playing returned Soldier scales before summon ability");
    }

    private static void Boundaries()
    {
        // Cross-class: Herald cards play for every class; see CrossClass() for the Soldier-class order.
        CrossClass();
        var warrior = New(CardClass.WARRIOR); var owner = warrior.Player1;
        Summon(owner, "LOE_077"); Play(warrior, owner, "CATA_525");
        Check(HeraldRules.Count(owner) == 2 && owner.BoardZone.Count(x => x.Card.Id == "CATA_580t") == 2,
            "Brann repeats both summons and increments using controller profile");
        var soldiers = owner.BoardZone.Where(x => x.Card.Id == "CATA_580t").ToArray();
        Check(soldiers[0].Id < soldiers[1].Id, "Brann Soldiers retain entity creation order on board");
    }

    // Wiki (tier C, ClayByte DM): own class, else the Herald card's class, else the opponent's class; no lock-in; one shared count.
    // Branch (d), neither side has a Soldier class, is UNPINNED: the card plays, no Soldier, no progress.
    private static void CrossClass()
    {
        // Priest plays Rite of Twilight (Rogue): Soldier of Sinestra, plain play and Combo.
        var g = New(CardClass.PRIEST); var p = g.Player1;
        Play(g, p, "CATA_785");
        Check(HeraldRules.Count(p) == 1 && p.BoardZone.Single().Card.Id == "CATA_158t" && p.Opponent.Hero.Damage == 0
            && p[HeraldRules.Profile] == 0 && p.HeroClass == CardClass.PRIEST, "Priest plays Rite: Sinestra, no damage, no lock-in");
        Play(g, p, "CATA_785", p.Opponent.Hero);
        Check(HeraldRules.Count(p) == 2 && p.Opponent.Hero.Damage == 3 && p.BoardZone.Count(m => m.Card.Id == "CATA_158t") == 2,
            "Priest Rite Combo: Soldier plus 3 damage");
        // Mage: Rite then Fel Infusion: Sinestra, then Azshara (wiki example); one shared count, tier from it.
        g = New(CardClass.MAGE); p = g.Player1;
        Play(g, p, "CATA_785"); Play(g, p, "CATA_530");
        var soldiers = p.BoardZone.Where(m => HeraldRules.IsSoldier(m.Card)).ToArray();
        Check(soldiers.Select(m => m.Card.Id).SequenceEqual(new[] { "CATA_158t", "CATA_525t" }) && p[HeraldRules.Profile] == 0
            && HeraldRules.Count(p) == 2 && p.Hero.HasLifeSteal, "Mage Rite then Fel Infusion: Sinestra then Azshara (no lock-in), lifesteal resolves");
        Play(g, p, "CATA_561");
        var third = p.BoardZone.Where(m => HeraldRules.IsSoldier(m.Card)).Last();
        Check(third.Card.Id == "CATA_565t" && third[HeraldRules.RealizedFactor] == 2 && HeraldRules.Count(p) == 3
            && p.HandZone.Count(c => c.Card.Id == "CATA_561t") == 2, "count shared across classes: third Herald is tier 1; Ritual tail resolves");
        // Neutral Envoy: opponent's class decides; Mage opponent: no Soldier, no progress, the card still plays.
        g = New(CardClass.PRIEST, 1, CardClass.ROGUE); p = g.Player1;
        Play(g, p, "CATA_722");
        Check(p.BoardZone.Any(m => m.Card.Id == "CATA_158t") && HeraldRules.Count(p) == 1 && p[HeraldRules.Profile] == 0,
            "Priest Envoy vs Rogue: opponent's Sinestra");
        g = New(CardClass.PRIEST, 1, CardClass.MAGE); p = g.Player1;
        Play(g, p, "CATA_722");
        Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "CATA_722" && HeraldRules.Count(p) == 0,
            "Priest Envoy vs Mage (unpinned d): plays, no Soldier, no progress");
        NeutralHeraldCards();
        // Own class beats the card's class and the opponent's; a Warrior keeps its Soldier for a Rogue card.
        g = New(CardClass.WARRIOR, 1, CardClass.ROGUE); p = g.Player1;
        Play(g, p, "CATA_785");
        Check(p.BoardZone.Single().Card.Id == "CATA_580t" && p[HeraldRules.Profile] == (int)CardClass.WARRIOR, "own class wins");
        // Source-aware: a card from one class resolves its own class even if the opponent has a Soldier class.
        g = New(CardClass.PRIEST, 1, CardClass.WARLOCK); p = g.Player1;
        Play(g, p, "CATA_785");
        Check(p.BoardZone.Single().Card.Id == "CATA_158t", "card class beats opponent class");
        // Full board still advances.
        g = New(CardClass.PRIEST); p = g.Player1; while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, "CATA_530");
        Check(HeraldRules.Count(p) == 1 && p.Hero.HasLifeSteal && p.BoardZone.Count == 7, "full board: progress and Lifesteal still resolve");
        // Every Herald spell/minion is castable on every class (hand play and automatic casts).
        foreach (var cls in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST })
        {
            g = New(cls); p = g.Player1;
            foreach (var id in new[] { "CATA_530", "CATA_561", "CATA_156", "CATA_785" })
            {
                var spell = (Spell)Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
                Check(HeraldRules.CanAutoCast(spell) && HeraldRules.CanCast(p, spell, null), $"{cls} may auto-cast {id}");
            }
            g = New(cls); p = g.Player1; p.UsedMana = 10;
            var viaPlayTask = (Spell)Entity.FromCard(p, Cards.FromId("CATA_530"), zone: p.DeckZone);
            new PlayTask(PlayType.SPELL, true).Process(g, p, p.Hero, null, new TaskStack { Playables = new List<IPlayable> { viaPlayTask } });
            Check(viaPlayTask.Zone == p.GraveyardZone && HeraldRules.Count(p) == 1 && p.Hero.HasLifeSteal && p.RemainingMana == 0,
                $"{cls} PlayTask auto-casts a Herald spell");
        }
        // Wire contract: entity tag = source-aware class, controller tag = source-independent class.
        g = New(CardClass.PRIEST, 1, CardClass.ROGUE); p = g.Player1;
        var rite = Add(p, "CATA_785"); var dh = Add(p, "CATA_525"); var envoy = Add(p, "CATA_722");
        Check(HeraldRules.Class(p, rite.Card) == CardClass.ROGUE && HeraldRules.Class(p, dh.Card) == (CardClass)14
            && HeraldRules.Class(p, envoy.Card) == CardClass.ROGUE && HeraldRules.WireClass(p) == CardClass.ROGUE,
            "source-aware class per card; controller class from the opponent");
        g = New(CardClass.PRIEST); p = g.Player1;
        Check(HeraldRules.Class(p) == CardClass.INVALID && HeraldRules.WireClass(p) == CardClass.INVALID, "no Soldier class anywhere: INVALID (unpinned d)");
        // Hero replacement keeps the controller's own class profile (selected profile), cross-class fallbacks never change it.
        g = New(CardClass.ROGUE); p = g.Player1; Play(g, p, "CATA_530");
        Check(p[HeraldRules.Profile] == (int)CardClass.ROGUE && p.BoardZone.Single().Card.Id == "CATA_158t",
            "Rogue's own Soldier for a DH card; profile stored for own class only");
    }

    // Neutral multi-class Herald cards (Envoy of the End CATA_722, Ultraxion CATA_497) held by a class with no Soldier of its own
    // (owner hit the block live: Priest + Envoy). Branch (c) opponent class; branch (d, UNPINNED) none. Also what the held card's
    // text names: NativeHeraldText.Shown(card tag, controller tag), the Soldier name the client prints.
    private static void NeutralHeraldCards()
    {
        foreach (var id in new[] { "CATA_722", "CATA_497" })
        {
            // (c) Priest vs Rogue: the Rogue Soldier, progress advances, nothing stored, the body stays.
            var g = New(CardClass.PRIEST, 1, CardClass.ROGUE); var p = g.Player1;
            var held = Add(p, id);
            string shown = NativeHeraldText.Shown((int)HeraldRules.Class(p, held.Card), (int)HeraldRules.WireClass(p));
            Check(shown == "GAMEPLAY_HERALD_ROGUE", $"{id} held by a Priest vs Rogue names the Rogue Soldier: {shown}");
            Check(held.IsPlayableByPlayer, $"{id} lit for a Priest");
            PlayEntity(g, p, held);
            Check(p.BoardZone.Any(m => m.Card.Id == id) && p.BoardZone.Count(m => m.Card.Id == "CATA_158t") == 1
                && HeraldRules.Count(p) == 1 && p[HeraldRules.Profile] == 0, $"{id} Priest vs Rogue: body stays, Sinestra, progress 1, no lock-in");
            // (d) Priest vs Mage: plays, body stays, no Soldier, no progress; generic held text.
            g = New(CardClass.PRIEST, 1, CardClass.MAGE); p = g.Player1;
            held = Add(p, id);
            shown = NativeHeraldText.Shown((int)HeraldRules.Class(p, held.Card), (int)HeraldRules.WireClass(p));
            Check(shown == NativeHeraldText.DefaultKey, $"{id} held by a Priest vs Mage names no Soldier (generic text): {shown}");
            Check(held.IsPlayableByPlayer, $"{id} lit for a Priest vs Mage");
            PlayEntity(g, p, held);
            Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == id && !p.BoardZone.Any(m => HeraldRules.IsSoldier(m.Card))
                && HeraldRules.Count(p) == 0 && p[HeraldRules.Profile] == 0, $"{id} Priest vs Mage: plays, body stays, no Soldier, no progress");
        }
        // Text for every tag: own Soldier of the card wins over the controller's, unknown falls through, 0/0 is the generic string.
        Check(NativeHeraldText.Shown(14, 7) == "GAMEPLAY_HERALD_DEMONHUNTER" && NativeHeraldText.Shown(0, 7) == "GAMEPLAY_HERALD_ROGUE"
            && NativeHeraldText.Shown(0, 0) == NativeHeraldText.DefaultKey && NativeHeraldText.Shown(3, 0) == NativeHeraldText.DefaultKey
            && NativeHeraldText.Key(1) == "GAMEPLAY_HERALD_DEATHKNIGHT" && NativeHeraldText.Key(8) == "GAMEPLAY_HERALD_SHAMAN"
            && NativeHeraldText.Key(9) == "GAMEPLAY_HERALD_WARLOCK" && NativeHeraldText.Key(10) == "GAMEPLAY_HERALD_WARRIOR",
            "held Herald text keys follow the pinned builder's switch");
        // Keyword tooltip (second reader of 4534): Priest vs Shaman holding Rite (Rogue) and Fel Infusion (DH) names the CARD's Soldier,
        // not the controller's Shaman; no card class falls back to the controller; a Mage opponent leaves the generic text.
        var vs = New(CardClass.PRIEST, 1, CardClass.SHAMAN).Player1;
        int controllerTag = (int)HeraldRules.WireClass(vs);
        Check(controllerTag == (int)CardClass.SHAMAN
            && NativeHeraldText.TooltipKey(NativeHeraldText.TooltipClass((int)HeraldRules.Class(vs, Cards.FromId("CATA_785")), controllerTag)) == "GLOBAL_KEYWORD_HERALD_ROGUE_TEXT"
            && NativeHeraldText.TooltipKey(NativeHeraldText.TooltipClass((int)HeraldRules.Class(vs, Cards.FromId("CATA_525")), controllerTag)) == "GLOBAL_KEYWORD_HERALD_DEMON_HUNTER_TEXT"
            && NativeHeraldText.TooltipKey(NativeHeraldText.TooltipClass(0, controllerTag)) == "GLOBAL_KEYWORD_HERALD_SHAMAN_TEXT"
            && NativeHeraldText.TooltipKey(NativeHeraldText.TooltipClass(0, 0)) == NativeHeraldText.DefaultTooltipKey
            && NativeHeraldText.TooltipKey(1) == "GLOBAL_KEYWORD_HERALD_DEATH_KNIGHT_TEXT" && NativeHeraldText.TooltipKey(9) == "GLOBAL_KEYWORD_HERALD_WARLOCK_TEXT"
            && NativeHeraldText.TooltipKey(10) == "GLOBAL_KEYWORD_HERALD_WARRIOR_TEXT", "Herald keyword tooltip names the card's own Soldier, falls back to the controller");
        // Own class tag per branch (a) own, (b) card class, (c) opponent class, (d) none: the entity tag the prefix reads.
        var w = New(CardClass.WARRIOR, 1, CardClass.ROGUE).Player1;
        var rite = Add(w, "CATA_785");
        Check(HeraldRules.Class(w, rite.Card) == CardClass.WARRIOR, "(a) own class on the card tag");
        var pr = New(CardClass.PRIEST, 1, CardClass.ROGUE).Player1;
        Check(HeraldRules.Class(pr, Cards.FromId("CATA_525")) == (CardClass)14 && HeraldRules.Class(pr, Cards.FromId("CATA_722")) == CardClass.ROGUE,
            "(b) card class and (c) opponent class on the card tag");
        Check(HeraldRules.Class(New(CardClass.PRIEST).Player1, Cards.FromId("CATA_722")) == CardClass.INVALID, "(d) none: card tag 0");
    }

    private static void IndirectCasts()
    {
        var g = New(CardClass.MAGE); var p = g.Player1;
        Summon(p, "LOOT_414");
        var spell = Entity.FromCard(p.Opponent, Cards.FromId("CATA_530"), zone: p.Opponent.DeckZone);
        Play(g, p, "LOOT_541");
        Check(spell.Controller == p && spell.Zone == p.DeckZone, "Togwaggle transfers Herald spell to a Mage deck");
        var clone = g.CloneExact(history: true); var restored = Game.LoadSnapshot(g.SaveSnapshot());
        End(g);
        Check(spell.Zone == p.GraveyardZone && HeraldRules.Count(p) == 1
            && p.BoardZone.Any(m => m.Card.Id == "CATA_525t") && p[HeraldRules.Profile] == 0,
            $"Grand Archivist casts the stolen Herald spell: Azshara from the card's class, no lock-in; zone={spell.Zone?.Type}");
        End(clone); End(restored);
        Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(restored),
            "indirect cross-class cast replays after clone and snapshot");

        foreach (var cls in new[] { CardClass.WARRIOR })
        foreach (string path in new[] { "direct", "explicit" })
        {
            g = New(cls); p = g.Player1;
            var held = (Spell)Add(p, "CATA_530");
            var before = Canonical.State(g); var history = Canonical.History(g);
            clone = g.CloneExact(history: true);
            // An invalid supplied target is still rejected before effects or removal (class never matters).
            ICharacter target = p.Opponent.Hero;
            if (path == "direct") Generic.CastSpell(p, g, held, target, 0);
            else
            {
                var task = new PlayTask(PlayType.SPELL, EntityType.OP_HERO);
                task.Process(g, p, p.Hero, null, new TaskStack { Playables = new List<IPlayable> { held } });
            }
            Check(before == Canonical.State(g) && history == Canonical.History(g)
                && g.Random.Next() == clone.Random.Next(), $"{cls} invalid indirect cast preserves state/history/RNG, path={path}");
        }

        foreach (bool fullBoard in new[] { false, true })
        {
            g = New(CardClass.WARRIOR); p = g.Player1;
            if (fullBoard) while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            p.UsedMana = 10;
            var valid = (Spell)Entity.FromCard(p, Cards.FromId("CATA_530"), zone: p.DeckZone);
            new PlayTask(PlayType.SPELL, true).Process(g, p, p.Hero, null,
                new TaskStack { Playables = new List<IPlayable> { valid } });
            Check(valid.Zone == p.GraveyardZone && HeraldRules.Count(p) == 1 && p.Hero.HasLifeSteal
                && p.RemainingMana == 0 && p.BoardZone.Count == (fullBoard ? 7 : 1),
                "supported automatic cast ignores mana and retains full-board progression/Lifesteal");
        }
    }
}
