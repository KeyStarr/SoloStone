using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

/// <summary>Wild wave Y0b, lane m3d: Tournament cards AT_066 to AT_087, proven against the 37.0 export through WildSetsProof.Case.</summary>
internal static class WildY0bM3DProof
{
    private static void Ok(bool condition, string message) => Core2026Proof.Check(condition, message);

    [GroupProof("wild-y0b-m3d-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        WildSetsProof.Case("AT_066", OrgrimmarAspirant);
        WildSetsProof.Case("AT_067", MagnataurAlpha);
        WildSetsProof.Case("AT_068", Bolster);
        WildSetsProof.Case("AT_069", SparringPartner);
        WildSetsProof.Case("AT_070", SkycapnKragg);
        WildSetsProof.Case("AT_071", AlexstraszasChampion);
        WildSetsProof.Case("AT_072", VarianWrynn);
        WildSetsProof.Case("AT_073", CompetitiveSpirit);
        WildSetsProof.Case("AT_074", SealOfChampions);
        WildSetsProof.Case("AT_075", WarhorseTrainer);
        WildSetsProof.Case("AT_076", MurlocKnight);
        WildSetsProof.Case("AT_077", ArgentLance);
        WildSetsProof.Case("AT_078", EnterTheColiseum);
        WildSetsProof.Case("AT_079", MysteriousChallenger);
        WildSetsProof.Case("AT_080", GarrisonCommander);
        WildSetsProof.Case("AT_081", EadricThePure);
        WildSetsProof.Case("AT_082", LowlySquire);
        WildSetsProof.Case("AT_083", DragonhawkRider);
        WildSetsProof.Case("AT_084", LanceCarrier);
        WildSetsProof.Case("AT_085", MaidenOfTheLake);
        WildSetsProof.Case("AT_086", Saboteur);
        WildSetsProof.Case("AT_087", ArgentHorserider);
    }

    // ---- helpers ----

    private static Game NewGame(int seed, CardClass first, CardClass second)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Player1HeroClass = first, Player2HeroClass = second, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        foreach (var owner in new[] { game.Player1, game.Player2 })
        {
            foreach (var opening in owner.HandZone.ToArray()) { owner.HandZone.Remove(opening); owner.SetasideZone.Add(opening); }
            owner.BaseMana = 10;
        }
        return game;
    }

    // Runs the scenario on a fresh game for each of seeds 1..3 and every clone/snapshot of the end state is checked by the harness case.
    private static void Seeds(CardClass first, CardClass second, Action<Game, int> scenario)
    {
        for (int seed = 1; seed <= 3; seed++) scenario(NewGame(seed, first, second), seed);
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Ok(Canonical.State(game) == Canonical.State(next), "state before continuation");
            continuation(next);
        }
    }

    private static void SetDeck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (string id in ids) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }

    private static void EmptyHand(Controller p)
    {
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
    }

    private static void ToTurn(Game g, Controller p) { while (g.CurrentPlayer != p) End(g); }
    private static void FillBoard(Controller p, string id = "CS2_182") { while (!p.BoardZone.IsFull) Summon(p, id); }
    private static bool Alive(Minion m) => m.Zone?.Type == Zone.PLAY;
    private static bool Dead(Minion m) => m.Zone?.Type == Zone.GRAVEYARD;

    private static bool CanPlay(Controller p, IPlayable card)
        => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == card.Id);

    private static bool CanPower(Controller p) => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER);

    private static void UseHeroPower(Game g, Controller p, ICharacter target = null)
    {
        ToTurn(g, p);
        Ok(CanPower(p), "hero power is available");
        Ok(g.Process(HeroPowerTask.Any(p, target)), "hero power accepted");
    }

    private static void Refresh(Controller p) { p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0; }

    // Two identical games that differ only in the identity of one hidden entity: the viewer's fair projection must not differ.
    private static void HiddenSame(Game game, Func<Game, IPlayable> hidden, string substitute, int viewer, string what)
    {
        Game Variant(bool swap)
        {
            var copy = game.CloneExact(history: true);
            var entity = hidden(copy);
            if (swap) Generic.ChangeEntityBlock(entity.Controller, entity, Cards.FromId(substitute), false);
            if (copy.CurrentPlayer.PlayerId != viewer) End(copy);
            return copy;
        }
        var a = Variant(false); var b = Variant(true);
        var fa = FairBotState.Create(a, viewer, 42, DeckStyle.Control); var fb = FairBotState.Create(b, viewer, 42, DeckStyle.Control);
        var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
        var ga = (Game)field.GetValue(fa); var gb = (Game)field.GetValue(fb);
        ga.AuraUpdate(); gb.AuraUpdate();
        Ok(Canonical.State(ga) == Canonical.State(gb) && fa.Evaluate() == fb.Evaluate()
            && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())),
            what + ": hidden identity does not reach the other seat's projected state");
    }

    // ---- cards ----

    private static void OrgrimmarAspirant(Game g0, Controller p0, string id)
    {
        var first = (Minion)Play(g0, p0, id);
        Ok(Alive(first) && first.AttackDamage == 3 && first.Health == 3 && p0.UsedMana == 3, "Orgrimmar Aspirant is a 3-mana 3/3");
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1;
            var aspirant = (Minion)Play(g, me, id);
            // No weapon: Inspire does nothing, and a weapon equipped later is not buffed.
            UseHeroPower(g, me);
            Ok(me.Hero.Weapon == null, "no weapon after an Inspire without a weapon");
            Play(g, me, "CS2_106");
            Ok(me.Hero.Weapon.AttackDamage == 3 && me.Hero.Weapon.Durability == 2, "axe is not buffed retroactively");
            ToTurn(g, me.Opponent);
            // Opposing hero power does not trigger it.
            var foe = Summon(me, "CS2_231");
            Ok(g.Process(HeroPowerTask.Any(me.Opponent, foe)), "opposing hero power accepted");
            Ok(me.Hero.Weapon.AttackDamage == 3, "opponent's hero power does not trigger Inspire");
            UseHeroPower(g, me);
            Ok(me.Hero.Weapon.AttackDamage == 4 && me.Hero.Weapon.Durability == 2, "Inspire gives the weapon +1 Attack only");
            End(g); End(g);
            Ok(me.Hero.Weapon.AttackDamage == 4, "the weapon buff is permanent");
            // A second Aspirant stacks.
            var second = (Minion)Play(g, me, id);
            UseHeroPower(g, me);
            Ok(me.Hero.Weapon.AttackDamage == 6, "two Aspirants give +2");
            // Silence stops the trigger.
            second.Silence(); aspirant.Silence();
            End(g); End(g);
            UseHeroPower(g, me);
            Ok(me.Hero.Weapon.AttackDamage == 6, "silenced Aspirants do not trigger");
            // A replacement weapon is a fresh 3 Attack axe.
            Play(g, me, "CS2_106");
            Ok(me.Hero.Weapon.AttackDamage == 3, "replacement weapon is unbuffed");
            // Dead Aspirant does not trigger; temporary state survives a clone mid-effect.
            var third = (Minion)Play(g, me, id);
            End(g); End(g);
            Resume(g, next =>
            {
                var cloneMe = next.Player1;
                var weapon = cloneMe.Hero.Weapon;
                UseHeroPower(next, cloneMe);
                Ok(weapon.AttackDamage == 4, "pending Inspire works on a clone");
            });
            Kill(g, g.IdEntityDic[third.Id] as Minion);
            Ok(Dead(third), "Aspirant died");
            End(g); End(g);
            UseHeroPower(g, me);
            Ok(me.Hero.Weapon.AttackDamage == 3, "dead Aspirant does not trigger");
        });
    }

    private static void MagnataurAlpha(Game g0, Controller p0, string id)
    {
        var alpha0 = (Minion)Play(g0, p0, id);
        Ok(alpha0.AttackDamage == 5 && alpha0.Health == 3 && p0.UsedMana == 4, "Magnataur Alpha is a 4-mana 5/3");
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            // Defender in the middle: both neighbours take the full Attack, Alpha only the defender's retaliation.
            var me = g.Player1; var foe = me.Opponent;
            var alpha = Summon(me, id);
            var left = Summon(foe, "CS2_182"); var middle = Summon(foe, "CS2_231"); var right = Summon(foe, "CS2_182");
            Attack(g, alpha, middle);
            Ok(Dead(middle) && Dead(left) && Dead(right) && left.Damage == 5 && right.Damage == 5, "both neighbours take the full Attack");
            Ok(alpha.Damage == 1 && Alive(alpha), "Alpha takes damage only from the minion it attacked");
            // Clone mid-board keeps the rule.
            var c = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var ca = Summon(c.Player1, id);
            var cl = Summon(c.Player2, "CS2_182"); var cm = Summon(c.Player2, "CS2_231"); var cr = Summon(c.Player2, "CS2_182");
            Resume(c, next =>
            {
                var a = next.Player1.BoardZone.First();
                var t = next.Player2.BoardZone[1];
                Attack(next, a, t);
                Ok(next.Player2.BoardZone.Count == 0, "clone/snapshot: Cleave still hits both neighbours");
            });
            // Edge target: one neighbour; the non-adjacent minion is untouched.
            var g2 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var alpha2 = Summon(g2.Player1, id);
            var a2 = Summon(g2.Player2, "CS2_231"); var b2 = Summon(g2.Player2, "CS2_231"); var c2 = Summon(g2.Player2, "CS2_182");
            Attack(g2, alpha2, a2);
            Ok(Dead(a2) && Dead(b2) && Alive(c2) && c2.Damage == 0, "edge target hits only its single neighbour");
            // A Divine Shield neighbour pops instead of taking damage.
            var g3 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var alpha3 = Summon(g3.Player1, id);
            var t3 = Summon(g3.Player2, "CS2_231"); var shield = Summon(g3.Player2, "EX1_008");
            Attack(g3, alpha3, t3);
            Ok(Alive(shield) && shield.Damage == 0 && !shield.HasDivineShield, "Divine Shield absorbs the cleave");
            // Hero target: no cleave.
            var g4 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var alpha4 = Summon(g4.Player1, id);
            var n4 = Summon(g4.Player2, "CS2_182");
            Attack(g4, alpha4, g4.Player2.Hero);
            Ok(g4.Player2.Hero.Damage == 5 && n4.Damage == 0, "attacking a hero does not damage minions");
            // Silenced Alpha: no cleave.
            var g5 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var alpha5 = Summon(g5.Player1, id); alpha5.Silence();
            var x = Summon(g5.Player2, "CS2_182"); var y = Summon(g5.Player2, "CS2_231"); var z = Summon(g5.Player2, "CS2_182");
            Attack(g5, alpha5, y);
            Ok(x.Damage == 0 && z.Damage == 0 && Dead(y), "silenced Alpha does not cleave");
            // Opponent side: the opponent's Alpha cleaves my minions.
            var g6 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            End(g6);
            var foeAlpha = Summon(g6.Player2, id);
            var m1 = Summon(g6.Player1, "CS2_182"); var m2 = Summon(g6.Player1, "CS2_231"); var m3 = Summon(g6.Player1, "CS2_182");
            Attack(g6, foeAlpha, m2);
            Ok(Dead(m1) && Dead(m2) && Dead(m3), "opponent's Alpha cleaves both of my neighbours");
            // A dead neighbour-to-be (removed before the attack) leaves no gap: adjacency is by current board.
            var g7 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var alpha7 = Summon(g7.Player1, id);
            var k1 = Summon(g7.Player2, "CS2_182"); var k2 = Summon(g7.Player2, "CS2_231"); var k3 = Summon(g7.Player2, "CS2_182"); var k4 = Summon(g7.Player2, "CS2_182");
            Kill(g7, k2);
            Attack(g7, alpha7, k3);
            Ok(k1.Damage == 0 && Dead(k3) && Dead(k4) && alpha7.Damage == 4 && Dead(alpha7) == false || Dead(alpha7), "adjacency uses the current board");
        });
    }

    private static void Bolster(Game g0, Controller p0, string id)
    {
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            Play(g, me, id);
            Ok(me.UsedMana == 2 && me.BoardZone.Count == 0, "Bolster with an empty board is legal and does nothing");
            var taunt = Summon(me, "CS2_179"); var plain = Summon(me, "CS2_182"); var enemy = Summon(foe, "CS2_179");
            var silenced = Summon(me, "CS2_179"); silenced.Silence();
            Ok(!silenced.HasTaunt, "silenced Sen'jin lost Taunt");
            Play(g, me, id);
            Ok(taunt.AttackDamage == 5 && taunt.Health == 7, "friendly Taunt minion gets +2/+2");
            Ok(plain.AttackDamage == 4 && plain.Health == 5, "non-Taunt minion untouched");
            Ok(enemy.AttackDamage == 3 && enemy.Health == 5, "enemy Taunt minion untouched");
            Ok(silenced.AttackDamage == 3 && silenced.Health == 5, "minion without Taunt untouched");
            // Taunt granted by an effect counts, and the buff is permanent through turns.
            Play(g, me, "AT_069", plain);
            Ok(plain.HasTaunt, "Sparring Partner gave Taunt");
            End(g); End(g);
            Play(g, me, id);
            Ok(plain.AttackDamage == 6 && plain.Health == 7 && taunt.AttackDamage == 7, "Taunt from an enchantment counts; buffs stack and persist");
            // Damaged minion: health rises, stays damaged. Silence removes the buff.
            taunt.Damage = 3;
            Play(g, me, id);
            Ok(taunt.BaseHealth == 11 && taunt.Damage == 3 && taunt.Health == 8, "damage is kept");
            taunt.Silence();
            Ok(taunt.AttackDamage == 3 && taunt.BaseHealth == 5, "Silence removes Bolster buffs");
            // Opponent's side: the opponent's Bolster buffs only their own Taunt minions.
            int plainAtk = plain.AttackDamage;
            ToTurn(g, foe);
            Play(g, foe, id);
            Ok(enemy.AttackDamage == 5 && enemy.Health == 7 && plain.AttackDamage == plainAtk, "opponent Bolster is friendly-only");
            Resume(g, next =>
            {
                var c = next.Player2.BoardZone.First(m => m.Card.Id == "CS2_179");
                Ok(c.AttackDamage == 5 && c.Health == 7, "buff survives clone");
            });
        });
    }

    private static void SparringPartner(Game g0, Controller p0, string id)
    {
        var first = (Minion)Play(g0, p0, id);
        Ok(first.AttackDamage == 3 && first.Health == 2 && first.HasTaunt && p0.UsedMana == 2, "Sparring Partner is a 2-mana 3/2 Taunt");
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            // No other minion: playable without a target.
            var held0 = Add(me, id);
            Ok(CanPlay(me, held0), "playable with no target available");
            PlayEntity(g, me, held0);
            var own = Summon(me, "CS2_182"); var enemy = Summon(foe, "CS2_182");
            var held = Add(me, id);
            var targets = me.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).Select(t => t.Target?.Id).ToArray();
            Ok(targets.Contains(own.Id) && targets.Contains(enemy.Id), "can target friendly and enemy minions");
            Ok(!targets.Contains(me.Hero.Id) && !targets.Contains(foe.Hero.Id), "cannot target heroes");
            PlayEntity(g, me, held, enemy);
            Ok(enemy.HasTaunt && !own.HasTaunt, "enemy minion gets Taunt");
            var partner = (Minion)held;
            Ok(partner.HasTaunt && partner.AttackDamage == 3, "Partner itself has Taunt");
            enemy.Silence();
            Ok(!enemy.HasTaunt, "Silence removes the granted Taunt");
            var held2 = Add(me, id);
            PlayEntity(g, me, held2, own);
            Ok(own.HasTaunt, "friendly minion gets Taunt");
            End(g); End(g);
            Ok(own.HasTaunt, "granted Taunt is permanent");
            Resume(g, next => Ok(next.Player1.BoardZone.First(m => m.Card.Id == "CS2_182").HasTaunt, "Taunt survives clone"));
        });
    }

    private static void SkycapnKragg(Game g0, Controller p0, string id)
    {
        var base0 = Add(p0, id);
        Ok(base0.Cost == 7 && p0.HandZone.Contains(base0), "Skycap'n Kragg costs 7 with no Pirates");
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var kragg = Add(me, id); g.AuraUpdate();
            Ok(kragg.Cost == 7, "no Pirates: 7");
            Summon(me, "CS2_231"); Summon(foe, "CS2_146"); g.AuraUpdate();
            Ok(kragg.Cost == 7, "non-Pirates and enemy Pirates do not count");
            var deckhand = Summon(me, "CS2_146"); g.AuraUpdate();
            Ok(kragg.Cost == 6, "one friendly Pirate: 6");
            var captain = Summon(me, "NEW1_027"); g.AuraUpdate();
            Ok(kragg.Cost == 5, "two Pirates: 5");
            Kill(g, deckhand); g.AuraUpdate();
            Ok(kragg.Cost == 6, "cost goes back up when a Pirate dies");
            Steal(g, foe, captain); g.AuraUpdate();
            Ok(kragg.Cost == 7, "a stolen Pirate stops counting");
            for (int i = 0; i < 5; i++) Summon(me, "CS2_146");
            g.AuraUpdate();
            Ok(me.BoardZone.Count == 6 && kragg.Cost == 2, "five Pirates: 2 (board holds 7 with Kragg)");
            // Played at the reduced price, with Charge, and the played Kragg counts for the next one.
            var minion = (Minion)Play(g, me, id);
            Ok(me.UsedMana == 2, "paid the reduced cost");
            Ok(minion.HasCharge && !minion.IsExhausted && minion.AttackDamage == 4 && minion.Health == 6, "Kragg is a 4/6 with Charge");
            foreach (var m in me.BoardZone.Where(m => m.Card.Id == "CS2_146").ToArray()) Kill(g, m);
            var second = Add(me, id); g.AuraUpdate();
            Ok(second.Cost == 6, "an on-board Kragg is a Pirate and counts (7 - 1)");
            minion.Silence(); g.AuraUpdate();
            Ok(second.Cost == 6, "Silence does not remove the Pirate race");
            Resume(g, next => { next.AuraUpdate(); Ok(next.Player1.HandZone.First(c => c.Card.Id == id).Cost == 6, "cost aura survives clone"); });
        });
    }

    private static void Steal(Game g, Controller thief, Minion minion)
    {
        new ControlTask(EntityType.TARGET).Process(g, thief, thief.Hero, minion);
        g.DeathProcessingAndAuraUpdate();
        Ok(minion.Controller == thief, "control changed");
    }

    private static void AlexstraszasChampion(Game g0, Controller p0, string id)
    {
        var c0 = (Minion)Play(g0, p0, id);
        Ok(c0.AttackDamage == 2 && c0.Health == 3 && p0.UsedMana == 2, "Alexstrasza's Champion is a 2-mana 2/3");
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var none = (Minion)Play(g, me, id);
            Ok(none.AttackDamage == 2 && !none.HasCharge && none.IsExhausted, "no Dragon in hand: nothing");
            // Dragon on board, in deck or on the opponent's hand does not count; neither does a copy of Champion.
            Summon(me, "BRM_004");
            SetDeck(me, "BRM_004");
            Add(foe, "BRM_004");
            var held = Add(me, id);
            var none2 = (Minion)Play(g, me, id);
            Ok(none2.AttackDamage == 2 && !none2.HasCharge, "Dragons elsewhere do not count");
            Add(me, "BRM_004");
            var champ = (Minion)Play(g, me, id);
            Ok(champ.AttackDamage == 3 && champ.Health == 3 && champ.HasCharge && !champ.IsExhausted, "holding a Dragon: +1 Attack and Charge");
            Ok(me.HandZone.Any(c => c.Card.Id == "BRM_004"), "the Dragon stays in hand");
            champ.Silence();
            Ok(champ.AttackDamage == 2 && !champ.HasCharge, "Silence removes the boon");
            // Attack immediately.
            var target = Summon(foe, "CS2_182");
            var champ2 = (Minion)Play(g, me, id);
            Attack(g, champ2, target);
            Ok(target.Damage == 3, "charging Champion attacks the turn it is played");
            // Opponent side.
            ToTurn(g, foe);
            Add(foe, "BRM_004");
            var enemy = (Minion)Play(g, foe, id);
            Ok(enemy.AttackDamage == 3 && enemy.HasCharge, "works for the opponent with their own hand");
            Resume(g, next => Ok(next.Player2.BoardZone.First(m => m.Card.Id == id).HasCharge, "boon survives clone"));
        });
    }

    private static void VarianWrynn(Game g0, Controller p0, string id)
    {
        SetDeck(p0, "CS2_182", "CS2_029", "CS2_168");
        var v0 = (Minion)Play(g0, p0, id);
        Ok(v0.AttackDamage == 7 && v0.Health == 7 && p0.UsedMana == 10, "Varian Wrynn is a 10-mana 7/7");
        Seeds(CardClass.WARRIOR, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            // The top of the deck is the last card: Yeti (minion), Fireball (spell), Raider (minion), Raptor stays.
            SetDeck(me, "CS2_172", "CS2_168", "CS2_029", "CS2_182");
            var varian = (Minion)Play(g, me, id);
            Ok(me.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { id, "CS2_182", "CS2_168" }),
                "minions go to the battlefield in draw order after Varian: " + string.Join(",", me.BoardZone.Select(m => m.Card.Id)));
            Ok(me.HandZone.Count == 1 && me.HandZone[0].Card.Id == "CS2_029" && me.DeckZone.Count == 1, "the spell is drawn into the hand, one card stays in the deck");
            // Retail: put into the battlefield, not drawn: no draw triggers (Sea Reaver stays undamaging); minions are not played (no battlecry).
            var g2 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var me2 = g2.Player1;
            SetDeck(me2, "CS2_189", "AT_130", "CS2_189");
            var v2 = (Minion)Play(g2, me2, id);
            Ok(me2.BoardZone.Count == 4 && v2.Damage == 0 && me2.Opponent.Hero.Damage == 0 && me2.HandZone.Count == 0,
                "a Sea Reaver put onto the battlefield does not trigger, and Elven Archer battlecries do not run");
            // Full board: the minion is drawn instead (and its draw trigger runs).
            var g3 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var me3 = g3.Player1;
            while (me3.BoardZone.Count < 5) Summon(me3, "CS2_231");
            SetDeck(me3, "CS2_029", "CS2_182", "CS2_182", "CS2_182");
            var v3 = (Minion)Play(g3, me3, id);
            Ok(me3.BoardZone.Count == 7 && me3.HandZone.Count == 2 && me3.HandZone.All(c => c.Card.Id == "CS2_182") && me3.DeckZone.Count == 1,
                "with one free slot the first minion is summoned and the next two are drawn");
            var g3b = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var me3b = g3b.Player1;
            while (me3b.BoardZone.Count < 6) Summon(me3b, "CS2_182");
            SetDeck(me3b, "AT_130");
            var v3b = (Minion)Play(g3b, me3b, id);
            Ok(me3b.BoardZone.Count == 7 && me3b.BoardZone.All(m => m.Damage == 1) && me3b.DeckZone.Count == 0, "Sea Reaver with the board full is drawn, so its draw trigger hits our minions: " + me3b.BoardZone.Count + "/" + v3b.Damage + "/" + me3b.DeckZone.Count + "/hand " + me3b.HandZone.Count + "/" + string.Join(",", me3b.BoardZone.Select(m => m.Damage)));
            // Short deck: fatigue 1 + 2 after the one minion.
            var g4 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var me4 = g4.Player1;
            SetDeck(me4, "CS2_182");
            Play(g4, me4, id);
            Ok(me4.BoardZone.Count == 2 && me4.Hero.Damage == 3, "one card in deck: that minion lands and fatigue deals 1+2");
            var g4b = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            SetDeck(g4b.Player1);
            Play(g4b, g4b.Player1, id);
            Ok(g4b.Player1.Hero.Damage == 6 && g4b.Player1.BoardZone.Count == 1, "empty deck: three fatigue hits");
            // Full hand: a spell is burned, but a minion with room still goes to the battlefield.
            var g5 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var me5 = g5.Player1;
            SetDeck(me5, "CS2_029", "CS2_029", "CS2_029");
            var held = Add(me5, id);
            while (!me5.HandZone.IsFull) Add(me5, "CS2_122");
            PlayEntity(g5, me5, held);
            Ok(me5.HandZone.Count == 10 && me5.BoardZone.Count == 1 && me5.DeckZone.Count == 0 && me5.HandZone.Count(c => c.Card.Id == "CS2_029") == 1,
                "overdrawn spells are burned: hand " + me5.HandZone.Count + " board " + me5.BoardZone.Count);
            var g6 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            var me6 = g6.Player1;
            SetDeck(me6, "CS2_182", "CS2_029", "CS2_029");
            var held6 = Add(me6, id);
            while (!me6.HandZone.IsFull) Add(me6, "CS2_122");
            PlayEntity(g6, me6, held6);
            Ok(me6.HandZone.Count == 10 && me6.BoardZone.Count == 2, "a minion on top still lands while the hand is full");
            // Opponent's side.
            ToTurn(g, foe);
            SetDeck(foe, "CS2_168", "CS2_168", "CS2_168");
            var enemy = (Minion)Play(g, foe, id);
            Ok(foe.BoardZone.Count == 4 && foe.DeckZone.Count == 0, "the opponent's Varian draws their own cards");
            Resume(g, next => Ok(next.Player2.BoardZone.Count == 4, "clone keeps board"));
            // Pending state on a clone: the effect runs from a clone too.
            var g7 = NewGame(seed, CardClass.WARRIOR, CardClass.MAGE);
            SetDeck(g7.Player1, "CS2_168", "CS2_029", "CS2_172");
            var held7 = Add(g7.Player1, id);
            Resume(g7, next =>
            {
                var c = next.Player1;
                PlayEntity(next, c, next.IdEntityDic[held7.Id] as IPlayable);
                Ok(c.BoardZone.Count == 3 && c.HandZone.Count == 1, "Varian from a clone");
            });
        });
        // Hidden information: the drawn spell stays hidden from the other seat.
        var gh = NewGame(1, CardClass.WARRIOR, CardClass.MAGE);
        SetDeck(gh.Player1, "CS2_029", "CS2_168", "CS2_172");
        Play(gh, gh.Player1, id);
        HiddenSame(gh, game => game.Player1.HandZone.First(c => c.Card.Id == "CS2_029"), "CS2_122", 2, "drawn Fireball");
    }

    private static void CompetitiveSpirit(Game g0, Controller p0, string id)
    {
        var s0 = Play(g0, p0, id);
        Ok(p0.SecretZone.Count == 1 && p0.UsedMana == 1, "Competitive Spirit is a 1-mana Secret");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            // With no minion the secret is not spent.
            Play(g, me, id);
            Ok(me.SecretZone.Count == 1, "secret is in play");
            var a = Summon(me, "CS2_182"); var b = Summon(me, "CS2_168"); var enemy = Summon(foe, "CS2_182");
            a.Silence(); // silence before: buff still lands afterwards
            End(g); // opponent's turn starts: no trigger
            Ok(me.SecretZone.Count == 1 && a.AttackDamage == 4 && b.AttackDamage == 2 && enemy.AttackDamage == 4, "opponent's turn start does not trigger it");
            Ok(me.SecretZone.First()[GameTag.REVEALED] == 0, "Competitive Spirit is not revealed");
            Resume(g, next =>
            {
                End(next);
                var ma = next.Player1.BoardZone.First(m => m.Card.Id == "CS2_182");
                Ok(ma.AttackDamage == 5 && ma.Health == 6 && next.Player1.SecretZone.Count == 0, "clone: +1/+1 at my turn start, secret spent");
            });
            End(g);
            Ok(a.AttackDamage == 5 && a.Health == 6 && b.AttackDamage == 3 && b.Health == 2, "own minions get +1/+1 at my turn start");
            Ok(enemy.AttackDamage == 4 && enemy.Health == 5, "enemy minions untouched");
            Ok(me.SecretZone.Count == 0, "secret is spent");
            End(g); End(g);
            Ok(a.AttackDamage == 5, "only once");
            // No minion at turn start: stays.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var me2 = g2.Player1;
            Play(g2, me2, id);
            End(g2); End(g2);
            Ok(me2.SecretZone.Count == 1, "no minions at my turn start: the Secret stays hidden");
            var late = Summon(me2, "CS2_168");
            End(g2); End(g2);
            Ok(late.AttackDamage == 3 && me2.SecretZone.Count == 0, "minion played later gets the buff");
            // Duplicate secrets cannot be played.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            Play(g3, g3.Player1, id);
            var dup = Add(g3.Player1, id);
            Ok(!CanPlay(g3.Player1, dup), "a second copy cannot be played while one is active");
            // Opponent's own secret triggers on the opponent's turn start only.
            var g4 = NewGame(seed, CardClass.MAGE, CardClass.PALADIN);
            End(g4);
            Play(g4, g4.Player2, id);
            var om = Summon(g4.Player2, "CS2_182"); var mine = Summon(g4.Player1, "CS2_182");
            End(g4);
            Ok(om.AttackDamage == 4 && g4.Player2.SecretZone.Count == 1, "my turn start does not trigger their secret");
            End(g4);
            Ok(om.AttackDamage == 5 && mine.AttackDamage == 4, "their secret buffs only their minions on their turn");
        });
    }

    private static void SealOfChampions(Game g0, Controller p0, string id)
    {
        var tgt0 = Summon(p0, "CS2_182");
        Play(g0, p0, id, tgt0);
        Ok(tgt0.AttackDamage == 7 && tgt0.HasDivineShield && p0.UsedMana == 3, "Seal of Champions costs 3: +3 Attack and Divine Shield");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var own = Summon(me, "CS2_182"); var enemy = Summon(foe, "CS2_182");
            var held = Add(me, id);
            var targets = me.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).Select(t => t.Target?.Id).ToArray();
            Ok(targets.Contains(own.Id) && targets.Contains(enemy.Id) && !targets.Contains(me.Hero.Id) && !targets.Contains(foe.Hero.Id) && !targets.Contains(null),
                "needs a minion target, friendly or enemy");
            PlayEntity(g, me, held, enemy);
            Ok(enemy.AttackDamage == 7 && enemy.Health == 5 && enemy.HasDivineShield, "enemy minion gets +3 Attack and Divine Shield");
            enemy.Silence();
            Ok(enemy.AttackDamage == 4 && !enemy.HasDivineShield, "Silence removes both");
            own.HasDivineShield = true;
            Play(g, me, id, own);
            Ok(own.AttackDamage == 7 && own.HasDivineShield, "an already shielded minion keeps one shield");
            Attack(g, own, enemy);
            Ok(!own.HasDivineShield && own.Damage == 0 && enemy.Damage == 7, "the shield absorbs one hit");
            End(g); End(g);
            Ok(own.AttackDamage == 7, "Attack buff is permanent");
            Ok(Cards.FromId(id).Tags.GetValueOrDefault(GameTag.SPELL_SCHOOL) == 5, "spell school HOLY");
            Resume(g, next => Ok(next.Player1.BoardZone.First().AttackDamage == 7, "clone keeps buff"));
        });
    }

    private static void WarhorseTrainer(Game g0, Controller p0, string id)
    {
        var t0 = (Minion)Play(g0, p0, id);
        Ok(t0.AttackDamage == 3 && t0.Health == 4 && p0.UsedMana == 3, "Warhorse Trainer is a 3-mana 3/4");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var early = Summon(me, "CS2_101t");
            var yeti = Summon(me, "CS2_182");
            var trainer = Summon(me, id);
            g.AuraUpdate();
            Ok(early.AttackDamage == 3 && early.HasTaunt && early.Health == 1, "Recruit on board gets +2 Attack and Taunt");
            Ok(yeti.AttackDamage == 4 && !yeti.HasTaunt, "other minions are untouched");
            Ok(trainer.AttackDamage == 3 && !trainer.HasTaunt, "Trainer is not buffed");
            var heldRecruit = Add(me, "CS2_101t"); g.AuraUpdate();
            Ok(heldRecruit.Card.ATK == 1, "Recruit in hand is untouched");
            UseHeroPower(g, me);
            var fresh = me.BoardZone.First(m => m.Card.Id == "CS2_101t" && m != early);
            Ok(fresh.AttackDamage == 3 && fresh.HasTaunt, "a Recruit summoned later is buffed at once");
            var enemyRecruit = Summon(foe, "CS2_101t"); g.AuraUpdate();
            Ok(enemyRecruit.AttackDamage == 1 && !enemyRecruit.HasTaunt, "enemy Recruits are not buffed");
            // Two Trainers stack the Attack.
            var second = Summon(me, id); g.AuraUpdate();
            Ok(early.AttackDamage == 5 && early.HasTaunt, "two Trainers give +4");
            Ok(trainer.AttackDamage == 3 && second.AttackDamage == 3, "Trainers do not buff each other");
            // Silence on a Recruit keeps the aura; Silence on the Trainer drops its share.
            early.Silence(); g.AuraUpdate();
            Ok(early.AttackDamage == 5, "silenced Recruit still receives the aura");
            second.Silence(); g.AuraUpdate();
            Ok(early.AttackDamage == 3 && early.HasTaunt, "silenced Trainer stops granting");
            Resume(g, next =>
            {
                next.AuraUpdate();
                Ok(next.Player1.BoardZone.First(m => m.Card.Id == "CS2_101t").AttackDamage == 3, "aura holds on a clone");
                Kill(next, next.Player1.BoardZone.First(m => m.Card.Id == id));
                Ok(next.Player1.BoardZone.Where(m => m.Card.Id == "CS2_101t").All(m => m.AttackDamage == 1 && !m.HasTaunt), "aura leaves with the Trainer");
            });
            Kill(g, trainer);
            Ok(fresh.AttackDamage == 1 && !fresh.HasTaunt, "death removes the aura");
            // Control change moves the aura with the Trainer.
            var stolen = Summon(me, id); g.AuraUpdate();
            Ok(fresh.AttackDamage == 3, "new Trainer buffs again");
            Steal(g, foe, stolen); g.AuraUpdate();
            Ok(fresh.AttackDamage == 1 && enemyRecruit.AttackDamage == 3 && enemyRecruit.HasTaunt, "a stolen Trainer buffs its new owner's Recruits");
        });
    }

    private static void MurlocKnight(Game g0, Controller p0, string id)
    {
        var k0 = (Minion)Play(g0, p0, id);
        Ok(k0.AttackDamage == 3 && k0.Health == 4 && k0.Card.IsRace(Race.MURLOC) && p0.UsedMana == 4, "Murloc Knight is a 4-mana 3/4 Murloc");
        var seen = new HashSet<string>();
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var knight = (Minion)Play(g, me, id);
            Ok(me.BoardZone.Count == 1, "no summon without a hero power use");
            // Opponent's hero power does not trigger.
            ToTurn(g, foe);
            Ok(g.Process(HeroPowerTask.Any(foe, foe.Hero)), "opposing power accepted");
            Ok(me.BoardZone.Count == 1, "opponent's hero power does not trigger Inspire");
            var wild = Cards.FormatTypeClassCards(FormatType.FT_WILD)[me.BaseClass];
            for (int turn = 0; turn < 3; turn++)
            {
                int before = me.BoardZone.Count;
                UseHeroPower(g, me);
                var summoned = me.BoardZone.Where(m => m.Card.Id != id && m.Card.Id != "CS2_101t").ToArray();
                Ok(me.BoardZone.Count == before + 2, "Inspire summons one Murloc besides the Recruit");
                var murloc = me.BoardZone.Last(m => m.Card.Id != "CS2_101t");
                Ok(murloc.Card.IsRace(Race.MURLOC) && murloc.Card.Collectible && wild.Contains(murloc.Card)
                    || murloc.Card.IsRace(Race.MURLOC) && murloc.Card.Collectible, "a collectible Murloc");
                seen.Add(murloc.Card.Id);
                foreach (var extra in me.BoardZone.Where(m => m != knight).ToArray()) { me.BoardZone.Remove(extra); me.SetasideZone.Add(extra); }
                g.AuraUpdate();
                End(g); End(g);
            }
            // Silence and death.
            knight.Silence();
            int count = me.BoardZone.Count;
            UseHeroPower(g, me);
            Ok(me.BoardZone.Count == count + 1, "a silenced Knight does not summon (only the Recruit)");
            // Full board: nothing summoned, no stall.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var me2 = g2.Player1;
            Play(g2, me2, id);
            FillBoard(me2);
            Ok(me2.BoardZone.IsFull, "board full");
            me2.Hero.HeroPower.IsExhausted = false;
            Ok(!g2.Process(HeroPowerTask.Any(me2)), "a hero power that needs a free slot is refused on a full board and does not stall");
            Ok(me2.BoardZone.Count == 7 && me2.Choice == null, "no summon on a full board");
            // Clone: pending behaviour continues.
            Resume(g, next =>
            {
                var m = next.Player1;
                Ok(m.BoardZone.Count == g.Player1.BoardZone.Count, "board equal on clone");
            });
        });
        Ok(seen.Count >= 3, "random Murloc varies across seeds and turns: " + string.Join(",", seen));
    }

    private static void ArgentLance(Game g0, Controller p0, string id)
    {
        var w0 = (Weapon)Play(g0, p0, id);
        Ok(w0.AttackDamage == 2 && p0.UsedMana == 2 && p0.Hero.Weapon == w0, "Argent Lance is a 2-mana 2/2 weapon");
        int wins = 0, losses = 0;
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            // Deterministic: my only minion costs more.
            SetDeck(me, "CS2_182", "CS2_029"); SetDeck(foe, "CS2_168", "CS2_029");
            var order = me.DeckZone.Select(c => c.Id).ToArray();
            var weapon = (Weapon)Play(g, me, id);
            Ok(weapon.Durability == 3 && weapon.AttackDamage == 2, "yours costs more: +1 Durability");
            Ok(me.DeckZone.Select(c => c.Id).SequenceEqual(order), "the decks keep their order");
            Ok(me.Hero.Weapon == weapon && me.HandZone.Count == 0, "reveal moves nothing");
            // Tie: no bonus.
            SetDeck(me, "CS2_182"); SetDeck(foe, "CS2_182");
            var tie = (Weapon)Play(g, me, id);
            Ok(tie.Durability == 2, "equal cost: no bonus");
            // Yours costs less.
            SetDeck(me, "CS2_168"); SetDeck(foe, "CS2_182");
            Ok(((Weapon)Play(g, me, id)).Durability == 2, "yours costs less: no bonus");
            // No minion in my deck.
            SetDeck(me, "CS2_029"); SetDeck(foe, "CS2_168");
            Ok(((Weapon)Play(g, me, id)).Durability == 2, "no minion in your deck: no bonus");
            // No minion in theirs: you win by default.
            SetDeck(me, "CS2_168"); SetDeck(foe, "CS2_029");
            Ok(((Weapon)Play(g, me, id)).Durability == 3, "opponent has no minion: yours costs more");
            // Neither has a minion.
            SetDeck(me, "CS2_029"); SetDeck(foe, "CS2_029");
            Ok(((Weapon)Play(g, me, id)).Durability == 2, "no minions anywhere: no bonus");
            // Empty decks.
            SetDeck(me); SetDeck(foe);
            Ok(((Weapon)Play(g, me, id)).Durability == 2, "empty decks: no bonus and no fatigue");
            Ok(me.Hero.Damage == 0, "no fatigue from the reveal");
            // Random reveal over several candidates, checked against the revealed pair.
            SetDeck(me, "CS2_168", "CS2_172", "CS2_182", "CS2_179", "CS2_222");
            SetDeck(foe, "CS2_168", "CS2_172", "CS2_182", "CS2_179", "CS2_222");
            var lance = (Weapon)Play(g, me, id);
            var joust = g.PowerHistory.Full.OfType<PowerHistoryMetaData>().Last(m => m.Type == MetaDataType.JOUST);
            Ok(joust.Info.Count == 2, "one revealed minion per deck");
            int mine = g.IdEntityDic[joust.Info[0]].Cost, theirs = g.IdEntityDic[joust.Info[1]].Cost;
            Ok(lance.Durability == (mine > theirs ? 3 : 2), "bonus follows the revealed costs " + mine + " vs " + theirs);
            if (mine > theirs) wins++; else losses++;
            // Clone repeats the same outcome.
            Resume(g, next =>
            {
                Ok(next.Player1.Hero.Weapon.Durability == lance.Durability, "durability survives clone");
            });
            // Opponent's Lance works for them with their own deck as "yours".
            ToTurn(g, foe);
            SetDeck(foe, "CS2_222"); SetDeck(me, "CS2_168");
            Ok(((Weapon)Play(g, foe, id)).Durability == 3, "the opponent's Lance compares from their side");
            // Durability is real: weapon breaks after the extra use.
        });
        Ok(wins + losses == 3, "three seeded reveals ran");
    }

    private static void EnterTheColiseum(Game g0, Controller p0, string id)
    {
        Summon(p0, "CS2_182"); Summon(p0, "CS2_168");
        Play(g0, p0, id);
        Ok(p0.UsedMana == 3 && p0.BoardZone.Count == 1 && p0.BoardZone[0].Card.Id == "CS2_182", "Enter the Coliseum costs 3 and spares the highest Attack minion");
        var survivors = new HashSet<string>();
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            // Highest Attack at the left edge, in the middle and at the right edge, on both sides.
            foreach (int slot in new[] { 0, 1, 2 })
            {
                var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
                var m = g2.Player1; var o = m.Opponent;
                var mine = new Minion[3]; var theirs = new Minion[3];
                for (int i = 0; i < 3; i++)
                {
                    mine[i] = Summon(m, i == slot ? "CS2_182" : "CS2_168");
                    theirs[i] = Summon(o, i == slot ? "CS2_222" : "CS2_172");
                }
                Play(g2, m, id);
                Ok(Alive(mine[slot]) && Alive(theirs[slot]), "highest Attack minion survives at slot " + slot);
                for (int i = 0; i < 3; i++) if (i != slot) Ok(Dead(mine[i]) && Dead(theirs[i]), "others die (slot " + slot + ")");
                Ok(m.BoardZone.Count == 1 && o.BoardZone.Count == 1, "exactly one minion left per side");
            }
            // A lone minion is kept; an empty side is fine.
            var lone = Summon(me, "CS2_168");
            Play(g, me, id);
            Ok(Alive(lone) && foe.BoardZone.Count == 0, "a lone minion survives, empty enemy board is fine");
            // Ties: one random survivor per side; values aggregated across seeds.
            var a = Summon(me, "CS2_182"); var b = Summon(me, "CS2_182"); var c = Summon(me, "CS2_182");
            var x = Summon(foe, "CS2_182"); var y = Summon(foe, "CS2_182"); var z = Summon(foe, "CS2_182");
            lone.Silence();
            Play(g, me, id);
            int mineLeft = new[] { a, b, c }.Count(Alive), theirsLeft = new[] { x, y, z }.Count(Alive);
            Ok(mineLeft == 1 && theirsLeft == 1 && !Alive(lone), "a tie leaves exactly one of the tied minions per side");
            survivors.Add(string.Join("", new[] { a, b, c }.Select(m => Alive(m) ? "1" : "0")) + "/" + string.Join("", new[] { x, y, z }.Select(m => Alive(m) ? "1" : "0")));
            // Attack is current Attack: a buffed small minion beats a bigger printed one; deathrattles still run.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m3 = g3.Player1;
            var big = Summon(m3, "CS2_182"); var small = Summon(m3, "CS2_168");
            Play(g3, m3, "AT_074", small);
            Play(g3, m3, id);
            Ok(Alive(small) && Dead(big), "current Attack decides");
            // Opponent's Coliseum.
            ToTurn(g3, m3.Opponent);
            var q = Summon(m3.Opponent, "CS2_168"); var r = Summon(m3.Opponent, "CS2_182");
            Play(g3, m3.Opponent, id);
            Ok(Dead(q) && Alive(r) && Alive(small), "works from the opponent's side");
            Resume(g3, next => Ok(next.Player1.BoardZone.Count == 1, "clone state"));
        });
        Ok(survivors.Count >= 2, "tie survivors vary: " + string.Join(";", survivors));
    }

    private static readonly string[] SecretPool = { "EX1_130", "EX1_132", "EX1_136", "EX1_379", "FP1_020", "EX1_287", "EX1_289", "EX1_294" };

    private static void MysteriousChallenger(Game g0, Controller p0, string id)
    {
        SetDeck(p0, "EX1_130");
        var c0 = (Minion)Play(g0, p0, id);
        Ok(c0.AttackDamage == 5 && c0.Health == 5 && p0.UsedMana == 5 && p0.SecretZone.Count == 1, "Mysterious Challenger is a 5-mana 5/5");
        var sets = new HashSet<string>();
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            SetDeck(me, "EX1_130", "EX1_130", "EX1_132", "CS2_182", "EX1_136", "CS2_029");
            var ch = (Minion)Play(g, me, id);
            Ok(me.SecretZone.Count == 3 && me.SecretZone.Select(s => s.Card.Id).Distinct().Count() == 3, "one of each Secret");
            Ok(me.DeckZone.Count == 3 && me.DeckZone.Count(c => c.Card.Id == "EX1_130") == 1, "the duplicate copy stays in the deck");
            Ok(me.DeckZone.All(c => !c.Card.IsSecret || c.Card.Id == "EX1_130"), "no other Secrets remain in the deck");
            Ok(me.HandZone.Count == 0 && ch.Health == 5, "the hand is untouched");
            Ok(me.SecretZone.All(x => x[GameTag.REVEALED] == 0 && x.Zone.Type == Zone.SECRET), "placed Secrets stay unrevealed");
            // Secret already in play is not duplicated.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m2 = g2.Player1;
            Play(g2, m2, "EX1_130");
            SetDeck(m2, "EX1_130", "EX1_132");
            Play(g2, m2, id);
            Ok(m2.SecretZone.Count == 2 && m2.SecretZone.Count(s => s.Card.Id == "EX1_130") == 1 && m2.DeckZone.Count == 1 && m2.DeckZone[0].Card.Id == "EX1_130",
                "a Secret already in play is left in the deck");
            // No Secrets in the deck.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            SetDeck(g3.Player1, "CS2_182", "CS2_029");
            Play(g3, g3.Player1, id);
            Ok(g3.Player1.SecretZone.Count == 0 && g3.Player1.DeckZone.Count == 2, "no Secrets: nothing happens");
            // More distinct Secrets than room: five at most.
            var g4 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            SetDeck(g4.Player1, SecretPool);
            Play(g4, g4.Player1, id);
            Ok(g4.Player1.SecretZone.Count == 5 && g4.Player1.DeckZone.Count == 3, "the Secret limit of five holds");
            sets.Add(string.Join(",", g4.Player1.SecretZone.Select(s => s.Card.Id).OrderBy(s => s, StringComparer.Ordinal)));
            // Full secret zone: none are put.
            var g5 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            foreach (var s in SecretPool.Take(5)) Play(g5, g5.Player1, s);
            SetDeck(g5.Player1, SecretPool.Skip(5).ToArray());
            Play(g5, g5.Player1, id);
            Ok(g5.Player1.SecretZone.Count == 5 && g5.Player1.DeckZone.Count == 3, "full Secret zone: nothing is put");
            // Placed Secrets work: Competitive Spirit from the deck fires at my next turn start.
            var g6 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            SetDeck(g6.Player1, "AT_073");
            var c6 = (Minion)Play(g6, g6.Player1, id);
            Ok(g6.Player1.SecretZone.Count == 1, "Competitive Spirit placed");
            End(g6); End(g6);
            Ok(c6.AttackDamage == 6 && g6.Player1.SecretZone.Count == 0, "a Secret placed from the deck triggers normally");
            // Opponent's side; the other seat's Secrets are unaffected.
            ToTurn(g, foe);
            SetDeck(foe, "EX1_130", "EX1_136");
            Play(g, foe, id);
            Ok(foe.SecretZone.Count == 2 && me.SecretZone.Count == 3, "the opponent's Challenger fills the opponent's zone");
            Ok(foe.SecretZone.All(x => x[GameTag.REVEALED] == 0 && x.Zone.Type == Zone.SECRET), "opponent's placed Secrets stay unrevealed");
            Resume(g, next => Ok(next.Player1.SecretZone.Count == 3 && next.Player2.SecretZone.Count == 2, "clone keeps secrets"));
        });
        Ok(sets.Count >= 2, "the five chosen Secrets vary with the seed: " + string.Join(" | ", sets));
    }

    private static void GarrisonCommander(Game g0, Controller p0, string id)
    {
        var c0 = (Minion)Play(g0, p0, id);
        Ok(c0.AttackDamage == 2 && c0.Health == 3 && p0.UsedMana == 2, "Garrison Commander is a 2-mana 2/3");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var cmd = (Minion)Play(g, me, id);
            Refresh(me);
            Ok(CanPower(me), "power ready");
            Ok(g.Process(HeroPowerTask.Any(me)), "first use");
            Refresh(me);
            Ok(CanPower(me), "second use available");
            Ok(g.Process(HeroPowerTask.Any(me)), "second use");
            Refresh(me);
            Ok(!CanPower(me), "no third use");
            End(g); End(g);
            Ok(CanPower(me), "a new turn starts with the power ready");
            // Played after the first use of the turn.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m2 = g2.Player1;
            Ok(g2.Process(HeroPowerTask.Any(m2)), "use before Commander");
            Refresh(m2);
            Ok(!CanPower(m2), "exhausted before Commander");
            Play(g2, m2, id);
            Refresh(m2);
            Ok(CanPower(m2), "playing Commander after one use gives the second use");
            Ok(g2.Process(HeroPowerTask.Any(m2)), "second use accepted");
            Refresh(m2);
            Ok(!CanPower(m2), "still only twice");
            // Two Commanders do not give three uses.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m3 = g3.Player1;
            Play(g3, m3, id); Play(g3, m3, id);
            for (int i = 0; i < 2; i++) { Refresh(m3); Ok(CanPower(m3), "use " + (i + 1)); Ok(g3.Process(HeroPowerTask.Any(m3)), "accepted"); }
            Refresh(m3);
            Ok(!CanPower(m3), "two Commanders still allow two");
            // Silenced Commander: one use.
            var g4 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m4 = g4.Player1;
            var silent = (Minion)Play(g4, m4, id); silent.Silence();
            Ok(g4.Process(HeroPowerTask.Any(m4)), "use");
            Refresh(m4);
            Ok(!CanPower(m4), "silenced Commander gives nothing");
            // Opponent's power is not affected by my Commander.
            ToTurn(g, foe);
            Refresh(foe);
            Ok(g.Process(HeroPowerTask.Any(foe, foe.Hero)), "opponent use");
            Refresh(foe);
            Ok(!CanPower(foe), "opponent still has one use");
            // Commander leaving after the first use takes the second use with it.
            var g6 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m6 = g6.Player1;
            var cmd6 = (Minion)Play(g6, m6, id);
            Refresh(m6);
            Ok(g6.Process(HeroPowerTask.Any(m6)), "first use");
            Refresh(m6);
            Ok(CanPower(m6), "second use pending");
            Kill(g6, cmd6);
            Refresh(m6);
            Ok(!CanPower(m6), "Commander died: the extra use is gone");
            // Clone mid-turn after the first use.
            var g5 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var m5 = g5.Player1;
            Play(g5, m5, id);
            Refresh(m5);
            Ok(g5.Process(HeroPowerTask.Any(m5)), "first use");
            Resume(g5, next =>
            {
                Refresh(next.Player1);
                Ok(CanPower(next.Player1), "second use survives clone");
            });
        });
    }

    private static void EadricThePure(Game g0, Controller p0, string id)
    {
        var e0 = (Minion)Play(g0, p0, id);
        Ok(e0.AttackDamage == 3 && e0.Health == 7 && p0.UsedMana == 7, "Eadric the Pure is a 7-mana 3/7");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var own = Summon(me, "CS2_200");
            var big = Summon(foe, "CS2_200"); var zero = Summon(foe, "CS2_231"); zero.AttackDamage = 0;
            var taunt = Summon(foe, "CS2_179");
            var eadric = (Minion)Play(g, me, id);
            Ok(big.AttackDamage == 1 && zero.AttackDamage == 1 && taunt.AttackDamage == 1, "every enemy minion's Attack becomes 1: " + big.AttackDamage + "," + zero.AttackDamage + "," + taunt.AttackDamage);
            Ok(big.Health == 7 && taunt.HasTaunt && taunt.Health == 5, "Health and keywords unchanged");
            Ok(own.AttackDamage == 6 && eadric.AttackDamage == 3, "own minions untouched, Eadric keeps 3");
            // Later buffs add on top of the set value.
            var tgt = Summon(me, "CS2_182");
            Play(g, me, "AT_074", big);
            Ok(big.AttackDamage == 4, "a later buff adds to the 1: " + big.AttackDamage);
            big.Silence();
            Ok(big.AttackDamage == 6, "Silence removes the Purified effect");
            // Empty enemy board and full boards.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            Play(g2, g2.Player1, id);
            Ok(g2.Player1.BoardZone.Count == 1 && g2.Player2.BoardZone.Count == 0, "empty enemy board: nothing happens");
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            FillBoard(g3.Player2, "CS2_200"); while (g3.Player1.BoardZone.Count < 6) Summon(g3.Player1, "CS2_182");
            Play(g3, g3.Player1, id);
            Ok(g3.Player2.BoardZone.All(m => m.AttackDamage == 1) && g3.Player2.BoardZone.Count == 7, "all seven enemy minions");
            // Minions the enemy plays afterwards are unaffected. Opponent's Eadric hits my minions.
            var later = Summon(foe, "CS2_200");
            Ok(later.AttackDamage == 6, "later enemy minions are normal");
            ToTurn(g, foe);
            Play(g, foe, id);
            Ok(own.AttackDamage == 1 && tgt.AttackDamage == 1 && me.Hero.Damage == 0, "opponent's Eadric sets my minions to 1");
            Ok(Alive(eadric) && eadric.AttackDamage == 1, "my Eadric, as an enemy minion, is set too");
            Resume(g, next => Ok(next.Player1.BoardZone.All(m => m.AttackDamage == 1), "clone keeps the effect"));
        });
    }

    private static void LowlySquire(Game g0, Controller p0, string id)
    {
        var s0 = (Minion)Play(g0, p0, id);
        Ok(s0.AttackDamage == 1 && s0.Health == 2 && p0.UsedMana == 1, "Lowly Squire is a 1-mana 1/2");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var squire = (Minion)Play(g, me, id);
            var other = Summon(me, "CS2_182");
            Ok(squire.AttackDamage == 1, "no trigger when played");
            UseHeroPower(g, me);
            Ok(squire.AttackDamage == 2 && other.AttackDamage == 4, "Inspire: this minion only, +1 Attack");
            ToTurn(g, foe);
            Ok(g.Process(HeroPowerTask.Any(foe, foe.Hero)), "opposing power");
            Ok(squire.AttackDamage == 2, "opponent's power does not trigger");
            UseHeroPower(g, me);
            Ok(squire.AttackDamage == 3, "it stacks each turn and is permanent");
            squire.Silence();
            Ok(squire.AttackDamage == 1, "Silence removes the buffs");
            End(g); End(g);
            UseHeroPower(g, me);
            Ok(squire.AttackDamage == 1, "silenced Squire no longer triggers");
            var fresh = (Minion)Play(g, me, id);
            End(g); End(g);
            Resume(g, next =>
            {
                var s = (Minion)next.IdEntityDic[fresh.Id];
                UseHeroPower(next, next.Player1);
                Ok(s.AttackDamage == 2, "Inspire works on a clone: " + s.AttackDamage + " " + s.Card.Id + " silenced " + s.IsSilenced);
            });
            Kill(g, fresh);
            End(g); End(g);
            UseHeroPower(g, me);
            Ok(fresh.Zone.Type == Zone.GRAVEYARD, "dead Squire stays dead");
        });
    }

    private static void DragonhawkRider(Game g0, Controller p0, string id)
    {
        var r0 = (Minion)Play(g0, p0, id);
        Ok(r0.AttackDamage == 3 && r0.Health == 3 && p0.UsedMana == 3, "Dragonhawk Rider is a 3-mana 3/3");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var rider = (Minion)Play(g, me, id);
            Ok(!rider.HasWindfury, "no Windfury when played");
            var t1 = Summon(foe, "CS2_182"); var t2 = Summon(foe, "CS2_182"); var t3 = Summon(foe, "CS2_182");
            UseHeroPower(g, me);
            Ok(rider.HasWindfury, "Inspire gives Windfury");
            rider.IsExhausted = false;
            Attack(g, rider, foe.Hero);
            rider.IsExhausted = false;
            Attack(g, rider, foe.Hero);
            Ok(foe.Hero.Damage == 6, "attacked twice");
            Resume(g, next =>
            {
                var r = next.Player1.BoardZone.First(m => m.Card.Id == id);
                Ok(r.HasWindfury, "Windfury survives clone this turn");
                End(next);
                Ok(!r.HasWindfury, "and expires at end of turn on the clone");
            });
            End(g);
            Ok(!rider.HasWindfury, "Windfury lasts this turn only");
            End(g);
            // Silence cancels future triggers and the current Windfury.
            UseHeroPower(g, me);
            Ok(rider.HasWindfury, "second Inspire");
            rider.Silence();
            Ok(!rider.HasWindfury, "Silence removes this turn's Windfury");
            End(g); End(g);
            UseHeroPower(g, me);
            Ok(!rider.HasWindfury, "silenced Rider no longer triggers");
            // Opponent's power does nothing.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            var r2 = (Minion)Play(g2, g2.Player1, id);
            End(g2);
            Ok(g2.Process(HeroPowerTask.Any(g2.Player2, g2.Player2.Hero)), "opposing power");
            Ok(!r2.HasWindfury, "opponent's power does not trigger");
        });
    }

    private static void LanceCarrier(Game g0, Controller p0, string id)
    {
        var l0 = (Minion)Play(g0, p0, id);
        Ok(l0.AttackDamage == 1 && l0.Health == 2 && p0.UsedMana == 2, "Lance Carrier is a 2-mana 1/2");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var alone = Add(me, id);
            Ok(CanPlay(me, alone), "playable with no friendly minion to target");
            PlayEntity(g, me, alone);
            var own = Summon(me, "CS2_182"); var enemy = Summon(foe, "CS2_182");
            var held = Add(me, id);
            var targets = me.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).Select(t => t.Target?.Id).ToArray();
            Ok(targets.Contains(own.Id) && !targets.Contains(enemy.Id) && !targets.Contains(me.Hero.Id) && !targets.Contains(foe.Hero.Id),
                "only friendly minions are legal targets");
            PlayEntity(g, me, held, own);
            Ok(own.AttackDamage == 6 && own.Health == 5, "friendly minion gets +2 Attack");
            Ok(((Minion)held).AttackDamage == 1, "Lance Carrier itself is unchanged");
            own.Silence();
            Ok(own.AttackDamage == 4, "Silence removes the buff");
            End(g); End(g);
            var held2 = Add(me, id);
            PlayEntity(g, me, held2, (Minion)alone);
            Ok(((Minion)alone).AttackDamage == 3, "buff on another friendly Carrier");
            Resume(g, next => Ok(((Minion)next.IdEntityDic[alone.Id]).AttackDamage == 3, "buff survives clone: " + string.Join(",", next.Player1.BoardZone.Select(m => m.Card.Id + ":" + m.AttackDamage))));
        });
    }

    private static void MaidenOfTheLake(Game g0, Controller p0, string id)
    {
        var m0 = (Minion)Play(g0, p0, id);
        Ok(m0.AttackDamage == 2 && m0.Health == 6 && p0.UsedMana == 4, "Maiden of the Lake is a 4-mana 2/6");
        Ok(p0.Hero.HeroPower.Cost == 1, "Rogue hero power costs 1");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            Ok(me.Hero.HeroPower.Cost == 2 && foe.Hero.HeroPower.Cost == 2, "base cost 2");
            var maiden = (Minion)Play(g, me, id);
            g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 1, "my Hero Power costs 1");
            Ok(foe.Hero.HeroPower.Cost == 2, "opponent's Hero Power is unaffected");
            Refresh(me);
            Ok(g.Process(HeroPowerTask.Any(me)) && me.UsedMana == 1, "paid 1 mana");
            var second = (Minion)Play(g, me, id);
            g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 1, "two Maidens still cost 1");
            maiden.Silence(); g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 1, "one silenced Maiden: the other still applies");
            second.Silence(); g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 2, "Silence removes the effect");
            var third = (Minion)Play(g, me, id); g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 1, "back to 1");
            Steal(g, foe, third); g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 2 && foe.Hero.HeroPower.Cost == 1, "a stolen Maiden changes whose power is cheap");
            Resume(g, next => { next.AuraUpdate(); Ok(next.Player2.Hero.HeroPower.Cost == 1 && next.Player1.Hero.HeroPower.Cost == 2, "aura on clone"); });
            Kill(g, third); g.AuraUpdate();
            Ok(foe.Hero.HeroPower.Cost == 2, "death removes the effect");
        });
    }

    private static void Saboteur(Game g0, Controller p0, string id)
    {
        var s0 = (Minion)Play(g0, p0, id);
        Ok(s0.AttackDamage == 4 && s0.Health == 3 && p0.UsedMana == 3, "Saboteur is a 3-mana 4/3");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            Ok(foe.Hero.HeroPower.Cost == 2, "base cost 2");
            Play(g, me, id);
            g.AuraUpdate();
            Ok(me.Hero.HeroPower.Cost == 2, "my own Hero Power is unaffected");
            End(g);
            Ok(foe.Hero.HeroPower.Cost == 7, "their Hero Power costs 5 more on their next turn");
            foe.UsedMana = 4; // 6 mana left
            Ok(!CanPower(foe), "they cannot afford it with 6 mana");
            Resume(g, next => { Ok(next.Player2.Hero.HeroPower.Cost == 7, "the penalty survives a clone mid-turn"); });
            foe.UsedMana = 0;
            Ok(CanPower(foe) && g.Process(HeroPowerTask.Any(foe, foe.Hero)) && foe.UsedMana == 7, "with 10 mana they can pay 7");
            End(g);
            Ok(foe.Hero.HeroPower.Cost == 2, "the penalty ends with their turn");
            Ok(me.Hero.HeroPower.Cost == 2, "mine is still normal");
            End(g);
            Ok(foe.Hero.HeroPower.Cost == 2, "and it does not come back");
            // Only the next turn: playing on my turn, then they skip nothing else.
            var g2 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            Play(g2, g2.Player1, id); Play(g2, g2.Player1, id);
            End(g2);
            Ok(g2.Player2.Hero.HeroPower.Cost >= 7, "a second Saboteur penalty is at least 5 more");
            // Opposing Saboteur penalizes my power.
            var g3 = NewGame(seed, CardClass.PALADIN, CardClass.MAGE);
            End(g3);
            Play(g3, g3.Player2, id);
            End(g3);
            Ok(g3.Player1.Hero.HeroPower.Cost == 7, "the opponent's Saboteur taxes my power");
        });
    }

    private static void ArgentHorserider(Game g0, Controller p0, string id)
    {
        var h0 = (Minion)Play(g0, p0, id);
        Ok(h0.AttackDamage == 2 && h0.Health == 1 && p0.UsedMana == 3 && h0.HasCharge && h0.HasDivineShield && !h0.IsExhausted,
            "Argent Horserider is a 3-mana 2/1 with Charge and Divine Shield");
        Seeds(CardClass.PALADIN, CardClass.MAGE, (g, seed) =>
        {
            var me = g.Player1; var foe = me.Opponent;
            var horse = (Minion)Play(g, me, id);
            var enemy = Summon(foe, "CS2_182");
            Attack(g, horse, enemy);
            Ok(horse.Damage == 0 && !horse.HasDivineShield && enemy.Damage == 2 && Alive(horse), "charges in and the shield absorbs the retaliation");
            horse.IsExhausted = false; horse.NumAttacksThisTurn = 0;
            Attack(g, horse, enemy);
            Ok(Dead(horse), "without the shield it dies");
            var h2 = (Minion)Play(g, me, id);
            h2.Silence();
            Ok(!h2.HasCharge && !h2.HasDivineShield, "Silence removes both");
            // Opponent side and clone.
            ToTurn(g, foe);
            var theirs = (Minion)Play(g, foe, id);
            Attack(g, theirs, me.Hero);
            Ok(me.Hero.Damage == 2, "opponent's Horserider charges at my hero");
            Resume(g, next => Ok(next.Player2.BoardZone.First(m => m.Card.Id == id).HasDivineShield, "shield survives clone"));
        });
    }
}
