using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class ImbueProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, "Imbue: " + message);

    internal static Game Make(CardClass cls, int seed = 1)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
            Player1HeroClass = cls, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
            p.BaseMana = 10;
        }
        return game;
    }

    internal static EffectContext Context(Controller p) => new(p.Game, p.Id, p.Hero.Id);

    private static void Replay(Game game, Action<Game> act)
    {
        var clone = game.CloneExact(history: true);
        var loaded = Game.LoadSnapshot(game.SaveSnapshot());
        string before = Canonical.State(game);
        act(clone);
        Core2026Proof.Check(Canonical.State(game) == before, "Imbue clone never mutates parent");
        act(loaded); act(game);
        Core2026Proof.Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(loaded),
            "Imbue clone/snapshot future state and RNG identical");
    }

    private static void Progression(CardClass cls)
    {
        var g = Make(cls); var p = g.Player1; var old = p.Hero.HeroPower;
        Core2026Proof.Check(!p.NativeTags.ContainsKey(GameTag.IMBUE), "no universal default progression tag");
        old.IsExhausted = true;
        Context(p).Imbue();
        var power = p.Hero.HeroPower;
        Core2026Proof.Check(power.Id != old.Id && power.Card.Id == ImbueRules.Blessing(cls)
            && old.Zone == p.SetasideZone && power[GameTag.ZONE] == (int)Zone.PLAY
            && !power.IsExhausted && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "first blessing replaces and refreshes");
        power.IsExhausted = true;
        Replay(g, x => Context(x.Player1).Imbue());
        Core2026Proof.Check(p.Hero.HeroPower.Id == power.Id && power.IsExhausted && Context(p).ImbueCount == 2,
            "same blessing upgrades in place, preserves exhaustion");
        int cursor = g.PowerHistory.Full.Count;
        Context(p).Imbue(3);
        Core2026Proof.Check(Context(p).ImbueCount == 5 && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 5, "five Imbues scale to five");
        var increments = g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryTagChange>()
            .Where(t => t.EntityId == p.Id && t.Tag == GameTag.IMBUE).Select(t => t.Value);
        Core2026Proof.Check(increments.SequenceEqual(new[] { 3, 4, 5 }), "multiple Imbues resolve sequentially");
        if (power.Card.Tags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2))
            Core2026Proof.Check(power[GameTag.TAG_SCRIPT_DATA_NUM_2] == 5, "both printed magnitudes update");
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(g, p, p.Hero, null);
        Context(p).Imbue();
        Core2026Proof.Check(Context(p).ImbueCount == 6 && p.Hero.HeroPower.Card.Id == ImbueRules.Blessing(cls)
            && p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 6, "history survives replacement and reacquisition");
    }

    private static void ActivePowers(int n, int seed)
    {
        var g = Make(CardClass.MAGE, seed); var p = g.Player1;
        Context(p).Imbue(n);
        int mana = p.RemainingMana, uses = p.NumTimesHeroPowerUsedThisGame;
        p.Hero.HeroPower.IsExhausted = true;
        Replay(g, x => {
            Core2026Proof.Check(Context(x.Player1).TriggerCurrentHeroPower() == TaskState.COMPLETE, "direct power resolves exhausted");
            x.DeathProcessingAndAuraUpdate();
        });
        Core2026Proof.Check(p.BoardZone.Count == n && p.BoardZone.All(m => m.Card.Id == "EDR_851t" && m.Card.IsRace(Race.UNDEAD)
            && m.AttackDamage == 1 && m.Health == 1), "Mage summons exact Undead Wisps");
        Core2026Proof.Check(p.Opponent.Hero.Damage == n && p.RemainingMana == mana && p.NumTimesHeroPowerUsedThisGame == uses
            && p.Hero.HeroPower.IsExhausted, "direct effect damages without mana/use/exhaustion changes");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Context(p).TriggerCurrentHeroPower(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.Opponent.Hero.Damage == 2 * n, "Mage full board still deals all damage");

        g = Make(CardClass.HUNTER, seed); p = g.Player1; Context(p).Imbue(n);
        var beast = Add(p, "EDR_850pe"); var other = Add(p, "CS2_182");
        Replay(g, x => Context(x.Player1).TriggerCurrentHeroPower());
        Core2026Proof.Check(((Minion)beast).AttackDamage == 3 + n && beast.Cost == Math.Max(0, 2 - n)
            && ((Minion)other).AttackDamage == other.Card.ATK, "Hunter buffs only Beast and discounts to zero floor");
        PlayEntity(g, p, beast);
        Core2026Proof.Check(((Minion)beast).AttackDamage == 3 + n, "Hunter attack buff survives play");
        new SilenceTask(EntityType.TARGET).Process(g, p, p.Hero, beast);
        Core2026Proof.Check(((Minion)beast).AttackDamage == 3, "Hunter attack enchant removed by Silence");
        int otherCost = other.Cost;
        Context(p).TriggerCurrentHeroPower();
        Core2026Proof.Check(other.Cost == otherCost, "empty Beast pool is no-op");

        g = Make(CardClass.DRUID, seed); p = g.Player1; Context(p).Imbue(n);
        Replay(g, x => { Context(x.Player1).TriggerCurrentHeroPower(); x.DeathProcessingAndAuraUpdate(); });
        var golem = p.BoardZone.Single();
        Core2026Proof.Check(golem.Card.Id == "EDR_847pt2" && golem.AttackDamage == n && golem.Health == n, $"Druid summons n/n n={n}, actual={golem.Card.Id} {golem.AttackDamage}/{golem.Health}");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int fullMana = p.RemainingMana, fullUses = p.NumTimesHeroPowerUsedThisGame;
        bool activated = g.Process(HeroPowerTask.Any(p));
        Core2026Proof.Check(!activated && p.BoardZone.Count == 7 && p.RemainingMana == fullMana
            && p.NumTimesHeroPowerUsedThisGame == fullUses && !p.Hero.HeroPower.IsExhausted
            && p.Options().All(option => option.PlayerTaskType != PlayerTaskType.HERO_POWER),
            "Druid full board rejects normal activation without spending mana, use, or exhaustion");
        Context(p).TriggerCurrentHeroPower();
        Core2026Proof.Check(p.BoardZone.Count == 7, "Druid direct trigger respects board cap");
        Kill(g, golem);
        Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)) && p.BoardZone.Count == 7
            && p.RemainingMana == fullMana - 2 && p.NumTimesHeroPowerUsedThisGame == fullUses + 1
            && p.Hero.HeroPower.IsExhausted, "Druid normal activation succeeds when one slot opens");

    }

    private static void MageBonuses()
    {
        var g = Make(CardClass.MAGE); var p = g.Player1;
        Context(p).Imbue(2);
        p.Hero.HeroPowerDamage = 3;
        p.ControllerAuraEffects[GameTag.HERO_POWER_DOUBLE] = 1;
        Summon(p, "EX1_012"); // Spell Damage must not leak into the power.
        int used = p.NumTimesHeroPowerUsedThisGame;
        Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)), "normal Mage power activation accepted");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10 && p.BoardZone.Count(m => m.Card.Id == "EDR_851t") == 2
            && p.UsedMana == 2 && p.Hero.HeroPower.IsExhausted && p.NumTimesHeroPowerUsedThisGame == used + 1,
            "Mage applies hero-power bonuses once to total damage; normal use pays/exhausts/counts; spell bonus isolated");
    }

    private static Card Consumer(bool spell = false)
    {
        var card = Cards.FromId(spell ? "CS2_029" : "CS2_182").Clone();
        card.RefTags[GameTag.IMBUE] = 1;
        card.Power = new SabberStoneCore.Enchants.Power { PowerTask = new CustomTask((g, c, source, target, stack) => {
            new EffectContext(g, c.Id, source.Id).Imbue(); c.Hero.Armor += 1;
        }) };
        return card;
    }

    private static void ProfilesAndGates()
    {
        // I1: no blessing means no Imbue mutation; other clauses remain playable.
        foreach (var cls in new[] { (CardClass)14, CardClass.WARLOCK, CardClass.WARRIOR })
        {
            var g = Make(cls); var p = g.Player1; string before = Canonical.State(g);
            Context(p).Imbue(2);
            Check(Canonical.State(g) == before, "I1 no-op class never changes count or power: " + cls);
        }
        // Priest/Rogue gate lift reaches normal/stale hand play and all existing indirect-cast entry points.
        foreach (var cls in new[] { CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE })
        for (int path = 0; path < 5; path++)
        {
            var g = Make(cls); var p = g.Player1;
            var spell = (Spell)Generic.DrawCard(p, Consumer(true));
            Check(spell.IsPlayableByPlayer && p.Options().Any(t => t.Source?.Id == spell.Id), "Priest/Rogue consumer options admitted");
            switch (path)
            {
                case 0: Check(Generic.PlayCard(p, spell, p.Hero), "Priest/Rogue normal spell play"); break;
                case 1: Check(Generic.PlayCard(p, spell, p.Hero, skipPrePhase: true), "Priest/Rogue stale/forced spell play"); break;
                case 2: Generic.CastSpell(p, g, spell, p.Hero, 0); break;
                case 3: Context(p).CastSpell(spell.Id, HistoryCastKind.Effect, p.Hero.Id); break;
                case 4: new PlayTask(PlayType.SPELL, true).Process(g, p, p.Hero, p.Hero,
                    new TaskStack { Playables = new List<IPlayable> { spell } }); break;
            }
            Check(p.Hero.Armor == 1 && Context(p).ImbueCount == 1 && p.Hero.HeroPower.Card.Id == ImbueRules.Blessing(cls),
                "Priest/Rogue consumer clauses run once through every gate path " + path);
        }
        // A proof-local recipe exercises gates without registering a collectible consumer.
        foreach (var cls in new[] { (CardClass)14, CardClass.WARLOCK, CardClass.WARRIOR, CardClass.SHAMAN, CardClass.DEATHKNIGHT, CardClass.ROGUE, CardClass.PRIEST })
        {
            var g = Make(cls); var p = g.Player1;
            PlayEntity(g, p, Generic.DrawCard(p, Consumer()));
            Check(p.Hero.Armor == 1, "independent recipe clause resolves for supported/no-op class");
            Check(Context(p).ImbueCount == (cls == CardClass.SHAMAN || cls == CardClass.DEATHKNIGHT || cls == CardClass.ROGUE || cls == CardClass.PRIEST ? 1 : 0), "recipe Imbue profile");
        }
        var replaced = Make(CardClass.DRUID); var r = replaced.Player1;
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(replaced, r, r.Hero, null);
        Context(r).Imbue();
        Check(r.Hero.HeroPower.Card.Id == "EDR_847p" && Context(r).ImbueCount == 1, "I3 first Imbue replaces already replaced power");
        CrossClassConsumers();
    }

    // Wiki: a class without an Imbue power (DH, Warlock, Warrior) can play Imbue cards; only the Imbue clause has no effect.
    // A Neutral hero card inherits the player's class, so Imbue keeps working after Reno / Kerrigan / Deathwing.
    private static void CrossClassConsumers()
    {
        var consumers = Cards.All.Where(c => ImbueRules.IsConsumer(c) && c.Implemented).OrderBy(c => c.Id).ToArray();
        Check(consumers.Length > 10, "Imbue consumer universe found: " + consumers.Length);
        foreach (var cls in new[] { CardClass.WARLOCK, CardClass.WARRIOR, (CardClass)14 })
        {
            var g = Make(cls); var p = g.Player1;
            foreach (var card in consumers)
            {
                var held = Entity.FromCard(p, card, zone: p.HandZone);
                var reference = Make(CardClass.PRIEST); var other = Entity.FromCard(reference.Player1, card, zone: reference.Player1.HandZone);
                Check(held.IsPlayableByPlayer == other.IsPlayableByPlayer, $"{cls} playability of {card.Id} matches Priest");
                p.HandZone.Remove(held); p.SetasideZone.Add(held);
            }
            var test = (Spell)Generic.DrawCard(p, Consumer(true));
            Check(test.IsPlayableByPlayer && Generic.PlayCard(p, test, p.Hero) && p.Hero.Armor == 1 && Context(p).ImbueCount == 0,
                $"{cls} plays an Imbue card: other clauses resolve, Imbue has no effect");
        }
        // Deathwing, Worldbreaker is the one implemented Neutral hero card (Reno/Kerrigan/Artanis/Raynor are not registered).
        foreach (var cls in new[] { CardClass.PRIEST, CardClass.ROGUE, CardClass.DEATHKNIGHT, CardClass.WARLOCK })
        {
            var g = Make(cls); var p = g.Player1;
            var neutral = (Hero)Entity.FromCard(p, Cards.FromId("CATA_190h"), zone: p.HandZone);
            Check(neutral.Card.Class == CardClass.NEUTRAL || neutral.Card.Class == CardClass.INVALID, "CATA_190h has no single printed class: " + neutral.Card.Class);
            p.BaseMana = 10; p.UsedMana = 0;
            PlayEntity(g, p, neutral);
            B14H3LifecycleProof.Answer(g, "CATA_190t10");
            g.DeathProcessingAndAuraUpdate();
            Check(p.Hero.Id == neutral.Id && p.HeroClass == cls && p.Hero.Card.Class != cls,
                $"Deathwing replaces the hero, class stays {cls}: {p.HeroClass}");
            p.BaseMana = 10; p.UsedMana = 0;
            var test = (Spell)Generic.DrawCard(p, Consumer(true));
            Check(test.IsPlayableByPlayer && p.Options().Any(t => t.Source?.Id == test.Id), $"{cls} after Deathwing: Imbue card playable");
            Check(Generic.PlayCard(p, test, p.Hero) && p.Hero.Armor >= 1, $"{cls} after Deathwing: Imbue card plays");
            bool has = ImbueRules.Blessing(cls) != null;
            Check(Context(p).ImbueCount == (has ? 1 : 0) && (!has || p.Hero.HeroPower.Card.Id == ImbueRules.Blessing(cls)),
                $"{cls} after Deathwing: Imbue " + (has ? "installs its class power" : "has no effect"));
        }
        ReplaceHeroKeepsClass();
    }

    // ReplaceHeroTask (Majordomo Executus -> Ragnaros, Neutral hero) bypasses PlayHeroCore: it must inherit the class too.
    private static void ReplaceHeroKeepsClass()
    {
        foreach (var cls in new[] { CardClass.ROGUE, CardClass.PRIEST, CardClass.WARLOCK })
        {
            var g = Make(cls); var p = g.Player1;
            PlayEntity(g, p, Generic.DrawCard(p, Cards.FromId("BRM_027"))); // the legacy definition resolves Ragnaros on play
            Check(p.Hero.Card.Id == "BRM_027h" && p.HeroClass == cls, $"{cls}: Majordomo's Ragnaros keeps the class: {p.Hero.Card.Id}/{p.HeroClass}");
            p.BaseMana = 10; p.UsedMana = 0;
            var test = (Spell)Generic.DrawCard(p, Consumer(true));
            Check(test.IsPlayableByPlayer && Generic.PlayCard(p, test, p.Hero), $"{cls} after Ragnaros: Imbue card plays");
            bool has = ImbueRules.Blessing(cls) != null;
            Check(Context(p).ImbueCount == (has ? 1 : 0), $"{cls} after Ragnaros: Imbue " + (has ? "installs" : "has no effect"));
        }
    }

    private static void Lean_Wind(int seed, int n, int expectedCost)
    {
        // imbue-10: printed Cost + N, exact offline pool, empty pool leaves target untouched.
        var g = Make(CardClass.SHAMAN, seed); var p = g.Player1; Context(p).Imbue(n);
        var left = Summon(p, "CS2_182"); var target = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var admission = OfflinePoolAdmission.Evaluate(g.OfflinePools).Cards.Single(c => c.CardId == "EDR_448p");
        Check(admission.PoolRequirements.Count == 2 && admission.PoolRequirements.All(r => r.Class == "SHAMAN" && r.Count >= 3)
            && admission.PoolRequirements.Single(r => r.Binding == "ordinary").Count >= 3,
            "Wind printed Shaman domain meets ordinary admission");
        var location = LocationProofs.Location(p, position: 1);
        var power = p.Hero.HeroPower; var enemy = Summon(p.Opponent, "CS2_182");
        Check(power.Card.Id == "EDR_448p" && power.Card.AssetId == 114059 && power.Card.Type == CardType.HERO_POWER
            && power.Card.Name == "Blessing of the Wind"
            && power.Card.Text == "[x]Transform a friendly\nminion into a random\none that costs (<b>@</b>) more." && power.Cost == 2 && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == n
            && power[GameTag.TAG_SCRIPT_DATA_NUM_2] == n, "Wind exact identity/magnitudes");
        Check(!power.IsValidPlayTarget(null) && !power.IsValidPlayTarget(enemy) && !power.IsValidPlayTarget(p.Hero), "Wind friendly minion target required");
        target[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = 1;
        Check(!power.IsValidPlayTarget(target), "Wind rejects Elusive"); target[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = 0;
        target.HasStealth = true; target.IsImmune = true;
        Check(power.IsValidPlayTarget(target), "Wind allows friendly Stealth/Immune");
        target.Damage = 1; target[GameTag.TAUNT] = 1; target.Cost = 0;
        int id = target.Id; int historyCursor = g.PowerHistory.Full.Count;
        Check(Context(p).TriggerCurrentHeroPower(id) == TaskState.STOP && target.Card.Id == "CS2_182", "Lean_Wisprider never triggers targeted Wind even with explicit target");
        Replay(g, x => {
            Check(x.Process(HeroPowerTask.Any(x.Player1, (ICharacter)x.IdEntityDic[id])), "Wind normal target activation");
            x.DeathProcessingAndAuraUpdate();
        });
        var result = (Minion)g.IdEntityDic[id];
        Check(g.PowerHistory.Full.Skip(historyCursor).OfType<PowerHistoryTagChange>()
            .Any(t => t.EntityId == id && t.Tag == GameTag.IMMUNE && t.Value == 0),
            "Wind explicitly clears previous Immune flag on wire");
        Check(result.Card.Type == CardType.MINION && result.Card.Cost == expectedCost && result.Card.Id != "CS2_182"
            && result.AttackDamage == result.Card.ATK && result.BaseHealth == result.Card.Health
            && result.IsRush == result.Card.Rush && result.HasCharge == result.Card.Charge
            && result.Damage == 0 && result.HasStealth == result.Card.Stealth && !result.IsImmune && result.Cost == expectedCost, "Wind uses printed Cost + N and clears old state");
        Check(left.ZonePosition == 0 && location.ZonePosition == 1 && result.ZonePosition == 2 && right.ZonePosition == 3, "Wind preserves physical slot");
        var candidates = OfflineCardPools.Candidates(g.OfflinePools, CardClass.SHAMAN, OfflinePoolFilter.Random(type: CardType.MINION, minCost: expectedCost, maxCost: expectedCost), power.Card.AssetId);
        Check(candidates.Any(c => c.Id == result.Card.Id), "Wind result admitted and filter conformant");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        power.IsExhausted = false; p.UsedMana = 0;
        Check(g.Process(HeroPowerTask.Any(p, left)) && p.BoardZone.SlotCount == 7, "Wind transforms on full board");
        Context(p).Imbue(99); power.IsExhausted = false; p.UsedMana = 0;
        string original = result.Card.Id; var noCandidate = g.CloneExact(history: true);
        Check(g.Process(HeroPowerTask.Any(p, result)) && result.Card.Id == original && p.BoardZone.SlotCount == 7, "Wind empty exact-cost pool no-op");
        Check(g.Random.Next() == noCandidate.Random.Next(), "Wind empty exact-cost pool consumes no RNG");
        Check(Context(p).TriggerCurrentHeroPower(int.MaxValue) == TaskState.STOP, "stale target safely refused");
    }

    private static void Lean_WindRebornAndReadiness()
    {
        // F1: exercise real Reborn twice, with Wind's transform between deaths.
        {
            var g = Make(CardClass.SHAMAN, 104); var p = g.Player1; Context(p).Imbue(2);
            Check(p.Hero.HeroPower.Card.Id == "EDR_448p", "Shaman installs restored Wind");
            var murmy = Summon(p, "ULD_723"); Kill(g, murmy);
            var returned = p.BoardZone.Single();
            Check(returned.Card.Id == "ULD_723" && returned.Health == 1
                && returned[GameTag.HAS_BEEN_REBORN] == 1, "Murmy really died and returned before Wind");
            returned.Silence(); returned.IsFrozen = true;
            returned[GameTag.NUM_TURNS_IN_PLAY] = 4; returned[GameTag.TAG_SCRIPT_DATA_NUM_1] = 9;
            returned[GameTag.CONTROLLER_CHANGED_THIS_TURN] = 1;
            LostCityPoolProof.WithPool(g, "EDR_448p", new[] { "TLC_443" }, () =>
                Check(g.Process(HeroPowerTask.Any(p, returned)), "Wind can target returned Murmy"));
            Check(returned.Card.Id == "TLC_443" && returned.Card.Cost == 3 && returned.AttackDamage == 1 && returned.Health == 1
                && returned[GameTag.REBORN] == 1 && returned[GameTag.HAS_BEEN_REBORN] == 0
                && !returned.IsSilenced && !returned.IsFrozen && returned[GameTag.NUM_TURNS_IN_PLAY] == 0
                && returned[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && returned[GameTag.CONTROLLER_CHANGED_THIS_TURN] == 0,
                "Wind new Reborn body has unused Reborn");
            Check(returned.HasDeathrattle, "old Silence never suppresses new body's Deathrattle flag");
            Replay(g, x => {
                Kill(x, (Minion)x.IdEntityDic[returned.Id]);
                Check(x.Player1.BoardZone.Any(m => m.Card.Id == "TLC_443" && m.Id != returned.Id
                    && m.Health == 1 && m[GameTag.HAS_BEEN_REBORN] == 1), "Wind transformed body really reborns after death");
                Check(x.Player1.BoardZone.Any(m => m.Card.Id == "TLC_443t" && m.AttackDamage == 2 && m.Health == 2
                    && m.HasTaunt && m.Card.IsRace(Race.UNDEAD) && m.Card.IsRace(Race.BEAST)),
                    "old Silence never suppresses new body's Deathrattle");
            });
        }

        // Round 2: an exhausted Rush body must lose its old restriction when replaced.
        bool ordinary = false, rush = false, charge = false;
        foreach (var (seed, n, member) in new[] {
            (1, 1, "CORE_EX1_014"), (22, 1, "CORE_WC_701"), (204, 3, "CORE_EX1_310") })
        {
            var g = Make(CardClass.SHAMAN, seed); var p = g.Player1; Context(p).Imbue(n);
            var target = Summon(p, "EDR_850pe");
            var enemy = Summon(p.Opponent, "CS2_182");
            var held = Add(p, "CS2_182");
            Check(target.IsRush && target.AttackableByRush && g.RushMinions.Contains(target.Id), "original Rush restriction installed");
            target.NumAttacksThisTurn = 1; target.IsExhausted = true;
            LostCityPoolProof.WithPool(g, "EDR_448p", new[] { member }, () =>
                Check(g.Process(HeroPowerTask.Any(p, target)), "Wind transforms exhausted Rush body"));
            Check(target.Card.Id == member, "Wind readiness fixture identity");
            Check(target.NumAttacksThisTurn == 0 && p.HandZone.Contains(held),
                "new body has no previous attacks and Charge body's discard Battlecry never fires");
            if (target.HasCharge && !charge)
            {
                charge = true;
                Check(!target.IsExhausted && !target.AttackableByRush && !g.RushMinions.Contains(target.Id)
                    && target.ValidAttackTargets.Contains(p.Opponent.Hero)
                    && g.Process(MinionAttackTask.Any(p, target, p.Opponent.Hero)), "new Charge body attacks hero normally");
            }
            else if (target.IsRush && !target.HasCharge && !rush)
            {
                rush = true;
                Check(!target.IsExhausted && target.AttackableByRush && g.RushMinions.Contains(target.Id)
                    && !target.ValidAttackTargets.Contains(p.Opponent.Hero)
                    && g.Process(MinionAttackTask.Any(p, target, enemy)), "new Rush body attacks minion only");
            }
            else if (!target.IsRush && !target.HasCharge && !ordinary)
            {
                ordinary = true;
                Check(target.IsExhausted && !target.AttackableByRush && !g.RushMinions.Contains(target.Id)
                    && !g.Process(MinionAttackTask.Any(p, target, p.Opponent.Hero)), "new ordinary body cannot attack immediately");
                End(g); End(g);
                Check(!target.IsExhausted && target.ValidAttackTargets.Contains(p.Opponent.Hero)
                    && g.Process(MinionAttackTask.Any(p, target, p.Opponent.Hero)), "ordinary body may attack hero next turn");
            }
        }
        Check(ordinary && rush && charge, "readiness corpus reaches ordinary, Rush and Charge");
    }

    private static void Lean_InfiniteTrackerRequiresCurrentPower()
    {
        // F2: removing the current-power guard must fail without relying on frozen replay hashes.
        foreach (var cls in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN,
            CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR,
            CardClass.DEATHKNIGHT, (CardClass)14 })
        {
            var g = Make(cls); var p = g.Player1;
            PlayEntity(g, p, Add(p, "EDR_851t"));
            Check(!p.NativeTags.ContainsKey(ImbueRules.UndeadPlayedTurn)
                && !g.PowerHistory.Full.OfType<PowerHistoryTagChange>().Any(t => t.Tag == ImbueRules.UndeadPlayedTurn),
                "Lean_Infinite no tracker mutation without current passive: " + cls);
        }
        var game = Make(CardClass.DEATHKNIGHT); var player = game.Player1;
        Context(player).Imbue(); PlayEntity(game, player, Add(player, "EDR_851t"));
        int tracked = player[ImbueRules.UndeadPlayedTurn];
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(game, player, player.Hero, null);
        End(game); End(game);
        PlayEntity(game, player, Add(player, "EDR_851t"));
        Check(player[ImbueRules.UndeadPlayedTurn] == tracked,
            "Lean_Infinite tracker stops updating after passive replaced");
    }

    private static void Lean_InfiniteStaleOption()
    {
        // imbue-19 / D3: passive activation must be rejected even through a pre-Imbue cached option.
        var g = Make(CardClass.DEATHKNIGHT); var p = g.Player1;
        Summon(p, "AT_100"); // Verified Silver Hand Regent: Inspire summons CS2_101t.
        var cached = p.Options().Single(o => o.PlayerTaskType == PlayerTaskType.HERO_POWER);
        Check(cached.SkipPrePhase, "cached option bypasses ordinary playability preflight");
        var active = g.CloneExact(history: true);
        Check(active.Process(HeroPowerTask.Any(active.Player1)) && active.Player1.BoardZone.Any(m => m.Card.Id == "CS2_101t"),
            "Inspire observer reacts to legitimate active power");
        Context(p).Imbue();
        string before = Canonical.State(g); int history = g.PowerHistory.Full.Count;
        int mana = p.UsedMana, uses = p.NumTimesHeroPowerUsedThisGame, turnUses = p.HeroPowerActivationsThisTurn;
        Check(!g.Process(cached), "cached pre-Imbue option cannot activate Infinite");
        Check(!Generic.HeroPower(p, skipPrePhase: true), "forced activation cannot activate Infinite");
        Check(p.UsedMana == mana && p.NumTimesHeroPowerUsedThisGame == uses && p.HeroPowerActivationsThisTurn == turnUses
            && !p.Hero.HeroPower.IsExhausted && p.BoardZone.Count == 1 && p.BoardZone.All(m => m.Card.Id != "CS2_101t")
            && g.PowerHistory.Full.Count == history && Canonical.State(g) == before,
            "rejected passive activation changes no payment, usage, exhaustion, Inspire, history or game state");
    }

    private static void Lean_Infinite()
    {
        // Lean_ imbue-19 / F2: pre-installation hand-plays do not count.
        var g = Make(CardClass.DEATHKNIGHT); var p = g.Player1;
        var earlier = (Minion)Add(p, "EDR_851t"); PlayEntity(g, p, earlier);
        Context(p).Imbue(2);
        var power = p.Hero.HeroPower;
        Check(power.Card.AssetId == 121272 && power.Card.Id == "END_003p" && power.Card.Type == CardType.HERO_POWER
            && power.Card.Text == "<b>Passive</b>\nThe first Undead you\nplay each turn gains\n+<b>@</b> Attack." && power.Cost == 0 && power.Card[GameTag.HIDE_COST] == 1 && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
            "Infinite verified passive identity and magnitude");
        Check(!p.Options().Any(o => o.PlayerTaskType == PlayerTaskType.HERO_POWER)
            && !g.Process(HeroPowerTask.Any(p)) && Context(p).TriggerCurrentHeroPower() == TaskState.STOP, "passive never activates");
        var second = (Minion)Add(p, "EDR_851t"); PlayEntity(g, p, second);
        Check(second.AttackDamage == 3, "Lean_Infinite install ignores earlier pre-blessing Undead");
        End(g); End(g);
        var summoned = Summon(p, "EDR_851t");
        Check(summoned.AttackDamage == 1, "summons do not consume or receive first-play buff");
        var nonUndead = (Minion)Add(p, "CS2_182"); PlayEntity(g, p, nonUndead);
        Check(nonUndead.AttackDamage == 4, "non-Undead never buffed or counted");
        var first = (Minion)Add(p, "EDR_850pe"); // Multi-type Undead/Beast.
        int firstId = first.Id;
        Replay(g, x => PlayEntity(x, x.Player1, x.IdEntityDic[firstId]));
        Check(first.AttackDamage == 5 && first.AppliedEnchantments.Any(e => e.Card.Id == "END_003pe" && e.Card.AssetId == 121273
            && e.Card.Type == CardType.ENCHANTMENT && e.Card.Name == "The End is Nigh" && e.Card.Text == "Increased Attack."
            && e[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2), "first multi-type hand-play gets exact snapshot enchantment");
        Context(p).Imbue(); Check(first.AttackDamage == 5, "existing buff never grows retroactively");
        first.Silence(); g.DeathProcessingAndAuraUpdate(); Check(first.AttackDamage == 3, "Silence removes Infinite Attack");
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(g, p, p.Hero, null); Context(p).Imbue();
        second = (Minion)Add(p, "EDR_851t"); PlayEntity(g, p, second);
        Check(second.AttackDamage == 1, "replacement/reinstallation never resets turn tracker");
        var opponent = Summon(p.Opponent, "EDR_851t"); Check(opponent.AttackDamage == 1, "opponent summon never buffed");
        End(g);
        opponent = (Minion)Add(p.Opponent, "EDR_851t"); PlayEntity(g, p.Opponent, opponent);
        Check(opponent.AttackDamage == 1, "opponent hand-play not buffed by controller passive");
        End(g);
        // ALL and Battlecry timing are proof-local; no collectible module admitted.
        var card = Cards.FromId("EDR_850pe"); var old = card.Power;
        try
        {
            card.Power = new SabberStoneCore.Enchants.Power { PowerTask = new CustomTask((game, c, source, target, stack) => {
                c.Hero.Armor += ((Minion)source).AttackDamage;
            }) };
            var battlecry = (Minion)Add(p, "EDR_850pe"); PlayEntity(g, p, battlecry);
            Check(p.Hero.Armor == 7 && battlecry.AttackDamage == 7, "Lean_Infinite buff visible before Battlecry");
        }
        finally { card.Power = old; }
        Check(Generic.IsDecisionInternalTag((GameTag)31020), "Infinite tracker filtered from wire");
        var fresh = Make(CardClass.DEATHKNIGHT); var q = fresh.Player1; Context(q).Imbue(2);
        while (!q.BoardZone.IsFull) Summon(q, "CS2_182");
        var blocked = Add(q, "EDR_851t");
        Check(!Generic.PlayCard(q, blocked), "full board hand-play fails");
        Kill(fresh, q.BoardZone.First());
        PlayEntity(fresh, q, blocked);
        Check(((Minion)blocked).AttackDamage == 3, "failed full-board play never consumes first");
        End(fresh); End(fresh);
        Kill(fresh, q.BoardZone.First());
        var all = (Minion)Add(q, "GIL_681"); PlayEntity(fresh, q, all);
        Check(all.AttackDamage == 5, "ALL minion qualifies as Undead");
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(fresh, q, q.Hero, null);
        Check(all.AttackDamage == 5, "source leaving keeps existing snapshot enchantment");
        Kill(fresh, all);
        End(fresh); End(fresh);
        var removed = (Minion)Add(q, "EDR_851t"); PlayEntity(fresh, q, removed);
        Check(removed.AttackDamage == 1, "source power leaving play stops future buffs");
        Replay(fresh, x => {
            Context(x.Player1).Imbue();
            var later = (Minion)Add(x.Player1, "EDR_851t");
            Kill(x, x.Player1.BoardZone.First()); PlayEntity(x, x.Player1, later);
            Check(later.AttackDamage == 4, "Lean_Infinite clone ignores hand-play while passive absent");
        });
    }

    private static void Lean_WispriderChoices()
    {
        var g = Make(CardClass.DRUID); var p = g.Player1; Context(p).Imbue();
        var choice = p.Hero.HeroPower.Card.Clone();
        new ReplaceHeroPower(choice).Process(g, p, p.Hero, null);
        typeof(Card).GetProperty(nameof(Card.ChooseOne)).SetValue(choice, true);
        string before = Canonical.State(g);
        Check(Context(p).TriggerCurrentHeroPower() == TaskState.STOP && before == Canonical.State(g), "Lean_Wisprider choice power refuses free activation");
        typeof(Card).GetProperty(nameof(Card.ChooseOne)).SetValue(choice, false);
        choice.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
        Check(Context(p).TriggerCurrentHeroPower() == TaskState.STOP && before == Canonical.State(g), "Lean_Wisprider optional targeted power also refuses free activation");
        new ReplaceHeroPower(Cards.FromId("EDR_847p")).Process(g, p, p.Hero, null);
        Check(Context(p).TriggerCurrentHeroPower(int.MaxValue) == TaskState.STOP && Context(p).TriggerCurrentHeroPower(-1) == TaskState.STOP, "untargeted power stale/negative explicit entity safely refused");
    }

#if CLIENT_TRANSPORT
    private static void NativeProjection()
    {
        foreach (var cls in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.SHAMAN, CardClass.DEATHKNIGHT, CardClass.ROGUE, CardClass.PRIEST })
        {
            var g = Make(cls); var p = g.Player1;
            Context(p).Imbue(2);
            var power = p.Hero.HeroPower;
            var full = g.PowerHistory.Full.OfType<PowerHistoryFullEntity>().Single(f => f.Entity.Id == power.Id);
            Check(full.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "blessing magnitude initialized before FULL_ENTITY");
            var match = new NativeMatch((_, _) => { });
            typeof(NativeMatch).GetProperty("Game").SetValue(match, g);
            var convert = typeof(NativeMatch).GetMethod("ToNativeHistory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            PegasusGame.PowerHistoryData Convert(IPowerHistoryEntry entry) => (PegasusGame.PowerHistoryData)convert.Invoke(match, new object[] { entry });
            var native = Convert(full).FullEntity;
            Check(native.Tags.Any(t => t.Name == 2 && t.Value == 1), "native initial blessing magnitude");
            p[(GameTag)31020] = g.Turn;
            var create = Convert(PowerHistoryBuilder.CreateGame(g, new[] { p, p.Opponent })).CreateGame;
            Check(create.Players.All(player => player.Entity.Tags.All(t => t.Name != 31020)), "private tracker absent from CREATE_GAME/reconnect");
            Check(Convert(new PowerHistoryTagChange { EntityId = p.Id, Tag = (GameTag)31020, Value = g.Turn }) == null, "private tracker tag changes suppressed");
            var reconnect = PowerHistoryBuilder.FullEntity(power);
            reconnect.Entity.Tags[(GameTag)31020] = g.Turn;
            Check(Convert(reconnect).FullEntity.Tags.All(t => t.Name != 31020), "private tracker FULL_ENTITY suppressed");
            var show = PowerHistoryBuilder.ShowEntity(power); show.Entity.Tags[(GameTag)31020] = g.Turn;
            Check(Convert(show).ShowEntity.Tags.All(t => t.Name != 31020), "private tracker SHOW_ENTITY suppressed");
            var visible = (IEnumerable<KeyValuePair<GameTag, int>>)typeof(NativeMatch)
                .GetMethod("VisibleStats", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { power });
            Check(visible.Any(t => (int)t.Key == 2 && t.Value == 2), "native current blessing magnitudes projected after upgrades");
        }
    }

#endif
    internal static void Run()
    {
        Lean_WindRebornAndReadiness();
        Lean_InfiniteTrackerRequiresCurrentPower();
        ProfilesAndGates();
        foreach (var cls in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.SHAMAN, CardClass.DEATHKNIGHT, CardClass.ROGUE, CardClass.PRIEST }) Progression(cls);
        foreach (int seed in new[] { 1, 2, 3 })
        {
            foreach (int n in new[] { 1, 2, 5 }) ActivePowers(n, seed);
            Lean_Wind(seed, 1, 5); Lean_Wind(seed, 2, 6); Lean_Wind(seed, 5, 9);
        }
        Lean_InfiniteStaleOption(); Lean_Infinite(); MageBonuses(); Lean_WispriderChoices();
#if CLIENT_TRANSPORT
        NativeProjection();
#endif
        var g = Make(CardClass.DRUID); var p = g.Player1; string before = Canonical.State(g);
        Context(p).Imbue(0);
        bool threw = false; try { Context(p).Imbue(-1); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw && before == Canonical.State(g), "zero no-op and negative count rejected");
        Console.WriteLine("PASS Imbue primitive: seven blessings; no-op/gated profiles; Wind/Infinite leans; replacement; seeds=3; clone/snapshot; Silence; pre-Battlecry");
    }
}
