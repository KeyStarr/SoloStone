using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static WildY0aL1ALegacyProof;
using static WildY0aL1BLegacyProof;

// Wild wave Y0a, lane l1b: Expert1 neutral minions. Run from the group in Sets/LEGACY/WildY0aL1BProof.cs.
internal static class WildY0aL1BExpertProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case (used by Standard until #87).
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    internal static void Run()
    {
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("CS2_181", InjuredBlademaster);
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("CS2_227", VentureCoMercenary);
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_004", YoungPriestess);
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_006", AlarmOBot);
        // held: used by Standard (plugin/LocalCardCatalog.cs, LocalDecks.cs or data/) until #87
        HeldForStandard("EX1_009", AngryChicken);
    }

    // Proven against their 37.0 identity row (all pass, 2026-11-15) but not registered: see WildY0aL1BLegacyProof.Unregistered.
    // CS2_221 (Undead row) and EX1_001 (Draenei row): the projection suite pins their old race 0 as an observed difference
    // (tests/projection/Program.cs, directDifferences), so the new race fails it.
    // CS2_231 (Wisp): its Undead identity row breaks the Standard proof TLC_229 (B15QuestClosureCardsProof.Unpinned_SpiritMatching),
    // which plays Wisp as its untyped minion.
    // EX1_002 and EX1_005 also have changed English text (Tradeable) that needs the locale files (BLOCKED-HSDATA).
    internal static readonly (string Id, Action<Game, Controller, string> Scenario)[] Unregistered =
    {
        ("CS2_221", SpitefulSmith),
        ("CS2_231", (g, p, id) => Plain(g, p, id, Race.UNDEAD)),
        ("EX1_001", Lightwarden),
        ("EX1_002", TheBlackKnight),
        ("EX1_005", BigGameHunter),
        ("EX1_008", ArgentSquire),
    };

    private static void InjuredBlademaster(Game g, Controller p, string id)
    {
        var ogre = Summon(p, "CS2_197");
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var held = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).All(t => t.Target == null),
            id + " takes no target");
        PlayEntity(g, p, held);
        var m = (Minion)held;
        Only(m, id, GameTag.BATTLECRY);
        Core2026Proof.Check(m.Damage == 4 && m.Health == 3 && m.AttackDamage == 4 && m.Zone.Type == Zone.PLAY,
            id + " deals exactly 4 to itself and survives at 3 Health (Spell Damage does not scale a Battlecry)");
        Core2026Proof.Check(friend.Damage == 0 && enemy.Damage == 0 && ogre.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0,
            id + " hurts nothing else");
        Kill(g, ogre);
        Silence(g, m);
        Core2026Proof.Check(m.Damage == 4 && m.Health == 3 && m.AttackDamage == 4, id + " Silence does not heal it");
        Resume(g, next => {
            var c = (Minion)next.IdEntityDic[m.Id];
            Core2026Proof.Check(c.Damage == 4 && c.Health == 3, id + " damage survives clone and snapshot");
            Play(next, next.Player1, "CS2_008", c); Play(next, next.Player1, "CS2_008", c);
            Core2026Proof.Check(c.Zone.Type == Zone.PLAY && c.Health == 1, id + " two more points leave it at 1 Health");
            Play(next, next.Player1, "CS2_008", c);
            Core2026Proof.Check(c.Zone.Type == Zone.GRAVEYARD, id + " dies at exact Health after the Battlecry");
        });
        var summoned = Summon(p, id);
        Core2026Proof.Check(summoned.Damage == 0 && summoned.Health == 7, id + " summoned without a Battlecry stays at full 4/7");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(theirs.Damage == 4 && m.Damage == 4 && summoned.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0,
            id + " opponent copy hurts only itself");
        End(g);
    }

    private static void SpitefulSmith(Game g, Controller p, string id)
    {
        var smith = (Minion)Play(g, p, id);
        Core2026Proof.Check(smith.AttackDamage == 4 && smith.Health == 6, id + " is a 4/6");
        Play(g, p, "CS2_082");
        var weapon = p.Hero.Weapon;
        Core2026Proof.Check(weapon.AttackDamage == 1 && p.Hero.AttackDamage == 1, id + " undamaged adds nothing to my weapon");
        Play(g, p, "CS2_008", smith);
        Core2026Proof.Check(smith.Damage == 1 && weapon.AttackDamage == 3 && p.Hero.AttackDamage == 3 && weapon.Durability == 2 && smith.AttackDamage == 4,
            id + " damaged: my weapon has +2 Attack, durability and the minion itself unchanged");
        Play(g, p, "CS2_008", smith);
        Core2026Proof.Check(weapon.AttackDamage == 3, id + " further damage does not stack the bonus");
        Resume(g, next => {
            var w = next.Player1.Hero.Weapon; var s = (Minion)next.IdEntityDic[smith.Id];
            Core2026Proof.Check(w.AttackDamage == 3, id + " bonus survives clone and snapshot");
            Play(next, next.Player1, "CS2_007", s);
            Core2026Proof.Check(s.Damage == 0 && w.AttackDamage == 1, id + " healing in the restored game removes the bonus");
        });
        Play(g, p, "CS2_007", smith);
        Core2026Proof.Check(smith.Damage == 0 && weapon.AttackDamage == 1, id + " healed to full: the bonus ends");
        Play(g, p, "CS2_008", smith);
        Core2026Proof.Check(weapon.AttackDamage == 3, id + " and returns when damaged again");
        Play(g, p, "CS2_082");
        var fresh = p.Hero.Weapon;
        Core2026Proof.Check(fresh != weapon && fresh.AttackDamage == 3, id + " a weapon equipped while damaged gets the bonus");
        p.Hero.RemoveWeapon(); g.DeathProcessingAndAuraUpdate();
        Play(g, p, "CS2_007", smith);
        Play(g, p, "CS2_082");
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 1, id + " a weapon equipped while undamaged has no bonus");
        Play(g, p, "CS2_008", smith);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 3, id + " damage after equipping applies the bonus");
        var second = (Minion)Play(g, p, id);
        Play(g, p, "CS2_008", second);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 5, id + " two damaged Smiths stack +4");
        Silence(g, smith);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 3 && smith.IsSilenced, id + " Silence removes that Smith's bonus only");
        Play(g, p, "CS2_008", smith);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 3, id + " a Silenced Smith stays quiet");
        Kill(g, second);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 1, id + " the bonus ends when its Smith dies");
        var third = (Minion)Play(g, p, id); Play(g, p, "CS2_008", third);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 3, id + " a new damaged Smith applies it again");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Play(g, p.Opponent, "CS2_082");
        Core2026Proof.Check(p.Opponent.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.AttackDamage == 3, id + " the opponent's weapon is untouched by my Smith");
        Play(g, p.Opponent, "CS2_008", theirs);
        Core2026Proof.Check(p.Opponent.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.AttackDamage == 3 && p.Opponent.Hero.AttackDamage == 3,
            id + " opponent Smith buffs the opponent's weapon only");
        End(g);
        Core2026Proof.Check(p.Hero.AttackDamage == 3, id + " the bonus carries into my own next turn");
    }

    private static void VentureCoMercenary(Game g, Controller p, string id)
    {
        var yeti = Add(p, "CS2_182"); var spell = Add(p, "CS2_008"); var wisp = Add(p, "CS2_231");
        var theirYeti = Add(p.Opponent, "CS2_182");
        var deckTop = p.DeckZone.TopCard;
        var merc = (Minion)Play(g, p, id);
        Core2026Proof.Check(merc.AttackDamage == 7 && merc.Health == 6 && merc.Card.Cost == 5, id + " is a 5-mana 7/6");
        Core2026Proof.Check(yeti.Cost == 7 && wisp.Cost == 3 && spell.Cost == 0, id + " my hand minions cost (3) more, spells do not");
        Core2026Proof.Check(theirYeti.Cost == 4 && deckTop.Cost == 4, id + " opponent hand and my deck are unchanged");
        var later = Add(p, "CS2_182"); var tall = Add(p, "CS2_186");
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(later.Cost == 7 && tall.Cost == 10, id + " cards that arrive later cost more too " + later.Cost + "/" + tall.Cost);
        var second = (Minion)Play(g, p, id);
        Core2026Proof.Check(yeti.Cost == 10 && wisp.Cost == 6 && spell.Cost == 0 && merc.Card.Cost == 5, id + " two Mercenaries stack (+6)");
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == tall.Id),
            id + " War Golem at 13 Mana cannot be played");
        Resume(g, next => Core2026Proof.Check(((IPlayable)next.IdEntityDic[yeti.Id]).Cost == 10, id + " costs survive clone and snapshot"));
        Kill(g, second);
        Core2026Proof.Check(yeti.Cost == 7 && tall.Cost == 10, id + " the surcharge leaves with its source");
        Silence(g, merc);
        Core2026Proof.Check(yeti.Cost == 4 && wisp.Cost == 0 && tall.Cost == 7, id + " Silence removes it");
        var third = (Minion)Play(g, p, id);
        Core2026Proof.Check(yeti.Cost == 7, id + " a fresh Mercenary charges again");
        PlayEntity(g, p, yeti);
        Core2026Proof.Check(p.UsedMana == 7, id + " the raised price is what is actually paid");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(theirYeti.Cost == 7 && later.Cost == 7, id + " opponent copy raises only the opponent's hand; mine stays raised by mine");
        Play(g, p.Opponent, "CS1_113", third);
        Core2026Proof.Check(third.Controller == p.Opponent && theirYeti.Cost == 10 && later.Cost == 4, id + " control change moves the surcharge to the new owner");
        Kill(g, theirs);
        End(g);
    }

    private static void Lightwarden(Game g, Controller p, string id)
    {
        var lw = (Minion)Play(g, p, id);
        Core2026Proof.Check(lw.AttackDamage == 1 && lw.Health == 2 && lw.Card.Races.Contains(Race.DRAENEI), id + " is a 1/2 Draenei");
        var enemy = Summon(p.Opponent, "CS2_182");
        p.Hero.Damage = 5; enemy.Damage = 3;
        Play(g, p, "CS2_007", p.Hero);
        Core2026Proof.Check(lw.AttackDamage == 3 && p.Hero.Damage == 0, id + " a healed friendly hero gives +2 Attack");
        Play(g, p, "CS2_007", enemy);
        Core2026Proof.Check(lw.AttackDamage == 5 && enemy.Damage == 0, id + " a healed enemy minion counts too");
        Play(g, p, "CS2_007", enemy);
        Core2026Proof.Check(lw.AttackDamage == 5, id + " healing an undamaged character is not a heal");
        lw.Damage = 1;
        Play(g, p, "CS2_007", lw);
        Core2026Proof.Check(lw.AttackDamage == 7 && lw.Damage == 0, id + " it also triggers on its own healing");
        p.Opponent.Hero.Damage = 2;
        Play(g, p, "CS2_007", p.Opponent.Hero);
        Core2026Proof.Check(lw.AttackDamage == 9, id + " the enemy hero counts; a 3-point heal of 2 damage is one heal");
        Resume(g, next => {
            next.Player1.Hero.Damage = 4;
            Play(next, next.Player1, "CS2_007", next.Player1.Hero);
            Core2026Proof.Check(((Minion)next.IdEntityDic[lw.Id]).AttackDamage == 11, id + " trigger survives clone and snapshot");
        });
        var twin = (Minion)Play(g, p, id);
        p.Hero.Damage = 3;
        Play(g, p, "CS2_007", p.Hero);
        Core2026Proof.Check(lw.AttackDamage == 11 && twin.AttackDamage == 3, id + " two Lightwardens both trigger on one heal");
        Silence(g, lw);
        Core2026Proof.Check(lw.AttackDamage == 1, id + " Silence removes the accumulated Attack");
        p.Hero.Damage = 3; Play(g, p, "CS2_007", p.Hero);
        Core2026Proof.Check(lw.AttackDamage == 1 && twin.AttackDamage == 5, id + " a Silenced Lightwarden no longer triggers");
        Kill(g, twin);
        p.Hero.Damage = 3; Play(g, p, "CS2_007", p.Hero);
        Core2026Proof.Check(twin.Zone.Type == Zone.GRAVEYARD && twin.AttackDamage == 5, id + " a dead Lightwarden gains nothing");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        enemy.Damage = 2; Play(g, p.Opponent, "CS2_007", p.Opponent.Hero);
        p.Hero.Damage = 3; Play(g, p.Opponent, "CS2_007", p.Hero);
        Core2026Proof.Check(theirs.AttackDamage == 3, id + " opponent copy triggers on any heal, including my hero's");
        End(g);
    }

    private static void TheBlackKnight(Game g, Controller p, string id)
    {
        var tauntE = Summon(p.Opponent, "CS2_179"); var plainE = Summon(p.Opponent, "CS2_182");
        var tauntF = Summon(p, "CS2_187");
        var held = Add(p, id);
        var tasks = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
        Core2026Proof.Check(tasks.Select(x => x.Target?.Id).Distinct().SequenceEqual(new int?[] { tauntE.Id }),
            id + " may only target the enemy Taunt minion: not a plain enemy, not my Taunt, no heroes " + string.Join(";", tasks.Select(x => x.Target?.Card.Id + "#" + x.Target?.Id)));
        Core2026Proof.Check(held.Card[GameTag.TRADEABLE] == 1 && held.Cost == 4, id + " is a 4-mana Tradeable");
        PlayEntity(g, p, held, tauntE);
        var knight = (Minion)held;
        Core2026Proof.Check(tauntE.Zone.Type == Zone.GRAVEYARD && plainE.Zone.Type == Zone.PLAY && tauntF.Zone.Type == Zone.PLAY
            && knight.AttackDamage == 4 && knight.Health == 4 && knight.Card.Races.Contains(Race.UNDEAD), id + " destroys the chosen Taunt only; a 4/4 Undead");
        var sil = Summon(p.Opponent, "CS2_179"); sil.Silence(); g.DeathProcessingAndAuraUpdate();
        var hidden = Summon(p.Opponent, "CS2_161"); hidden.HasTaunt = true; g.DeathProcessingAndAuraUpdate();
        var probe = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == probe.Id && t.Target != null),
            id + " cannot target a Silenced Taunt or a Stealthed one");
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == probe.Id && t.Target == null),
            id + " is still playable with no legal target");
        var cloneCheck = g.CloneExact(history: true);
        PlayEntity(g, p, probe);
        Core2026Proof.Check(sil.Zone.Type == Zone.PLAY && hidden.Zone.Type == Zone.PLAY && plainE.Zone.Type == Zone.PLAY, id + " without a target destroys nothing");
        Core2026Proof.Check(cloneCheck.Player1.BoardZone.Count == p.BoardZone.Count - 1, id + " exact clone is independent of the play");
        // Tradeable
        var trade = Add(p, id);
        p.BaseMana = 1; p.UsedMana = 0; p.TemporaryMana = 0;
        Core2026Proof.Check(p.Options().Any(t => t is TradeCardTask && t.Source == trade) && !p.Options().Any(t => t is PlayCardTask && t.Source == trade),
            id + " Tradeable offered at one Mana, play is not");
        int replacement = p.DeckZone.TopCard.Id; int hand = p.HandZone.Count, theirHand = p.Opponent.HandZone.Count;
        var board = p.BoardZone.Concat(p.Opponent.BoardZone).Select(m => m.Id).ToArray();
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, trade)), id + " trade accepted");
        Core2026Proof.Check(trade.Zone == p.DeckZone && p.HandZone.Count == hand && p.HandZone.Any(c => c.Id == replacement) && p.UsedMana == 1
            && board.SequenceEqual(p.BoardZone.Concat(p.Opponent.BoardZone).Select(m => m.Id)) && p.Opponent.HandZone.Count == theirHand,
            id + " trade shuffles the same card, draws the top card, no Battlecry, nothing of the opponent's moves");
        End(g);
        var theirs = Add(p.Opponent, id);
        var theirTargets = p.Opponent.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == theirs.Id).ToArray();
        Core2026Proof.Check(theirTargets.Select(x => x.Target?.Id).Distinct().SequenceEqual(new int?[] { tauntF.Id }), id + " opponent copy may only target my Taunt");
        PlayEntity(g, p.Opponent, theirs, tauntF);
        Core2026Proof.Check(tauntF.Zone.Type == Zone.GRAVEYARD, id + " opponent copy destroys it");
        End(g);
    }

    private static void YoungPriestess(Game g, Controller p, string id)
    {
        var priestess = (Minion)Play(g, p, id);
        Core2026Proof.Check(priestess.AttackDamage == 2 && priestess.Health == 1, id + " is a 2/1");
        End(g); End(g);
        Core2026Proof.Check(priestess.Health == 1, id + " alone never buffs itself");
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_182"); var c = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(a.Health == 5 && b.Health == 5 && c.Health == 5, id + " does not buff on summon");
        var picks = new HashSet<int>();
        End(g);
        Core2026Proof.Check(a.Health + b.Health + c.Health == 16 && priestess.Health == 1 && enemy.Health == 5,
            id + " at the end of my turn exactly one other friendly minion gains +1 Health");
        End(g);
        Core2026Proof.Check(a.Health + b.Health + c.Health == 16, id + " the opponent's turn end does nothing");
        for (int i = 0; i < 8; i++) { int before = a.Health * 100 + b.Health * 10 + c.Health; End(g); End(g);
            picks.Add(a.Health != before / 100 ? 0 : b.Health != before / 10 % 10 ? 1 : 2); }
        Core2026Proof.Check(a.Health + b.Health + c.Health == 24 && priestess.Health == 1 && enemy.Health == 5 && picks.Count >= 2,
            id + " one random pick each turn, over the three candidates, never itself or the enemy");
        Resume(g, next => {
            End(next);
            var all = new[] { a.Id, b.Id, c.Id }.Select(i => (Minion)next.IdEntityDic[i]).Sum(m => m.Health);
            Core2026Proof.Check(all == 25, id + " a restored game still buffs exactly one candidate");
        });
        Silence(g, priestess);
        End(g);
        Core2026Proof.Check(a.Health + b.Health + c.Health == 24, id + " Silence removes the trigger");
        End(g);
        ClearHand(p);
        var stealth = Summon(p, "CS2_161");
        var second = (Minion)Play(g, p, id);
        Kill(g, a); Kill(g, b); Kill(g, c); Kill(g, priestess);
        int s = stealth.Health;
        End(g);
        Core2026Proof.Check(stealth.Health == s + 1 && second.Health == 1, id + " a Stealth friendly minion is a legal pick; a lone survivor takes it");
        End(g);
        End(g);
        ClearHand(p.Opponent);
        var theirs = (Minion)Play(g, p.Opponent, id);
        int mine = stealth.Health, theirE = enemy.Health;
        End(g);
        Core2026Proof.Check(enemy.Health == theirE + 1 && stealth.Health == mine && theirs.Health == 1, id + " opponent copy buffs only the opponent's minions");
        End(g);
    }

    private static void BigGameHunter(Game g, Controller p, string id)
    {
        var friendly = Summon(p, "CS2_186"); var enemy = Summon(p.Opponent, "CS2_186"); var small = Summon(p.Opponent, "CS2_200");
        var hidden = Summon(p.Opponent, "CS2_161");
        var source = Add(p, id);
        var plays = p.Options().Where(t => t is PlayCardTask && t.Source == source).ToArray();
        Core2026Proof.Check(plays.Any(t => t.Target == friendly) && plays.Any(t => t.Target == enemy)
            && plays.All(t => t.Target == friendly || t.Target == enemy), id + " targets 7+ Attack on either side; excludes 6 Attack and enemy Stealth");
        Core2026Proof.Check(source.Cost == 4 && source.Card[GameTag.TRADEABLE] == 1, id + " is a 4-mana Tradeable");
        PlayEntity(g, p, source, enemy); Play(g, p, id, friendly);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friendly.Zone.Type == Zone.GRAVEYARD && small.Zone == p.Opponent.BoardZone,
            id + " destroys the chosen targets only");
        var mine = Summon(p, "CS2_161");
        Play(g, p, id, mine);
        Core2026Proof.Check(mine.Zone.Type == Zone.GRAVEYARD, id + " may destroy my own 7-Attack Stealth minion");
        var rager = Summon(p.Opponent, "CS2_118");
        var probe = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source == probe && t.Target == rager), id + " five Attack is not enough");
        Play(g, p, "CS2_188", rager);
        Core2026Proof.Check(rager.AttackDamage == 7 && p.Options().Any(t => t is PlayCardTask && t.Source == probe && t.Target == rager),
            id + " buffed to exactly 7 Attack counts");
        End(g); End(g);
        Core2026Proof.Check(rager.AttackDamage == 5 && !p.Options().Any(t => t is PlayCardTask && t.Source == probe && t.Target == rager),
            id + " the temporary Attack is gone again");
        var none = Add(p, id);
        Core2026Proof.Check(p.Options().Any(t => t is PlayCardTask && t.Source == none && t.Target == null),
            id + " is playable with no 7-Attack target");
        Play(g, p, id);
        Core2026Proof.Check(small.Zone == p.Opponent.BoardZone && rager.Zone.Type == Zone.PLAY, id + " with no target destroys nothing");
        var trade = Add(p, id); p.BaseMana = 1; p.UsedMana = 0; p.TemporaryMana = 0;
        Core2026Proof.Check(p.Options().Any(t => t is TradeCardTask && t.Source == trade) && !p.Options().Any(t => t is PlayCardTask && t.Source == trade),
            id + " Tradeable available below play cost");
        int replacement = p.DeckZone.TopCard.Id; int hand = p.HandZone.Count; var slots = p.BoardZone.Slots.Select(m => m.Id).ToArray();
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, trade)) && clone.Process(TradeCardTask.Any(clone.Player1, (IPlayable)clone.IdEntityDic[trade.Id])),
            id + " trade accepted in original and clone");
        Core2026Proof.Check(trade.Zone == p.DeckZone && p.UsedMana == 1 && p.HandZone.Count == hand && p.HandZone.Any(c => c.Id == replacement)
            && p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(slots) && Canonical.State(g) == Canonical.State(clone),
            id + " same entity shuffled, top drawn, no Battlecry; deterministic clone");
        End(g);
        var big = Summon(p, "CS2_186");
        var theirs = Add(p.Opponent, id);
        PlayEntity(g, p.Opponent, theirs, big);
        Core2026Proof.Check(big.Zone.Type == Zone.GRAVEYARD, id + " opponent copy destroys my big minion");
        End(g);
    }

    // The deck becomes spells only, so a turn's draw can never be a minion candidate and never exhausts the deck.
    private static void SpellDeck(Controller owner)
    {
        foreach (var card in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(card));
        for (int i = 0; i < 30; i++) owner.DeckZone.Add((IPlayable)Entity.FromCard(owner, Cards.FromId("CS2_008")));
    }

    private static void ClearHand(Controller owner, IPlayable keep = null)
    {
        foreach (var card in owner.HandZone.ToArray()) if (card != keep) owner.SetasideZone.Add(owner.HandZone.Remove(card));
    }

    private static int[] Layout(Controller owner) => owner.BoardZone.Select(m => m.Id).ToArray();

    private static void AlarmOBot(Game g, Controller p, string id)
    {
        SpellDeck(p); SpellDeck(p.Opponent);
        var left = Summon(p, "CS2_182");
        var bot = (Minion)Play(g, p, id);
        var right = Summon(p, "CS2_182");
        int slot = bot.ZonePosition;
        Core2026Proof.Check(bot.AttackDamage == 0 && bot.Health == 3 && bot.Card.Cost == 3 && bot.Card.Races.Contains(Race.MECHANICAL),
            id + " is a 3-mana 0/3 Mech");
        ClearHand(p);
        var spell = Add(p, "CS2_008");
        NextOwnTurn(g);
        Core2026Proof.Check(bot.Zone.Type == Zone.PLAY && bot.ZonePosition == slot, id + " with no minion in hand stays put");
        Core2026Proof.Check(bot.Zone.Type == Zone.PLAY && p.HandZone.All(c => !(c is Minion)), id + " a hand of spells has nothing to swap");
        ClearHand(p);
        var spells = Enumerable.Range(0, 9).Select(_ => Add(p, "CS2_008")).ToArray();
        var wisp = Add(p, "CS2_231");
        Core2026Proof.Check(p.HandZone.Count == 10, "hand is full");
        bot.Damage = 2;
        var layout = Layout(p);
        End(g);
        Core2026Proof.Check(bot.Zone.Type == Zone.PLAY, id + " the opponent's turn start does nothing");
        Resume(g, next => {
            End(next);
            var restoredBot = (IPlayable)next.IdEntityDic[bot.Id];
            Core2026Proof.Check(restoredBot.Zone.Type == Zone.HAND && next.Player1.BoardZone[slot].Card.Id == "CS2_231",
                id + " the swap also happens in a restored game");
        });
        End(g);
        Core2026Proof.Check(bot.Zone == p.HandZone && wisp.Zone == p.BoardZone && wisp.ZonePosition == slot && p.BoardZone.Count == 3,
            id + " swaps with the only hand minion even in a full hand, in place");
        Core2026Proof.Check(p.BoardZone.Where(m => m != wisp).Select(m => m.Id).SequenceEqual(layout.Where(i => i != bot.Id)), id + " neighbours keep their order");
        Core2026Proof.Check(spells.Count(s => s.Zone == p.HandZone) == 9 && p.HandZone.Count == 10, id + " moves no spell");
        Core2026Proof.Check(bot.Damage == 0 && bot.Health == 3, id + " returns to hand undamaged");
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0 && left.Damage == 0 && right.Damage == 0, id + " no Battlecry or damage from the swap");
        var picks = new HashSet<string>(); var candidates = new[] { "CS2_231", "CS2_186", "CS2_189", "CS2_200" };
        for (int i = 0; i < 6; i++)
        {
            ClearHand(p, bot);
            foreach (var minion in p.BoardZone.Where(m => m != left && m != right && m != bot).ToArray()) minion.ToBeDestroyed = true;
            g.DeathProcessingAndAuraUpdate();
            foreach (var cand in candidates) Add(p, cand);
            if (bot.Zone != p.BoardZone) PlayEntity(g, p, bot);
            Core2026Proof.Check(bot.Zone == p.BoardZone, "bot back in play");
            NextOwnTurn(g);
            var chosen = p.BoardZone.Single(m => m != left && m != right && m != bot);
            Core2026Proof.Check(bot.Zone == p.HandZone && candidates.Contains(chosen.Card.Id) && p.HandZone.Count(c => c is Minion) == candidates.Length,
                id + " swaps with exactly one random hand minion");
            picks.Add(chosen.Card.Id);
        }
        Core2026Proof.Check(picks.Count >= 2, id + " the choice varies over the candidates: " + string.Join(",", picks));
        ClearHand(p, bot);
        Add(p, "CS2_231");
        if (bot.Zone != p.BoardZone) PlayEntity(g, p, bot);
        Silence(g, bot);
        NextOwnTurn(g);
        Core2026Proof.Check(bot.Zone == p.BoardZone && bot.IsSilenced, id + " Silence removes the swap");
        Kill(g, bot);
        var theirBot = Summon(p.Opponent, id);
        var theirWisp = Add(p.Opponent, "CS2_231");
        int mineHand = p.HandZone.Count; var mineBoard = Layout(p);
        End(g);
        Core2026Proof.Check(theirBot.Zone == p.Opponent.HandZone && theirWisp.Zone == p.Opponent.BoardZone && p.HandZone.Count == mineHand
            && mineBoard.SequenceEqual(Layout(p)), id + " opponent copy swaps on its own start of turn and touches nothing of mine");
        FairViewHidesCards(g, id + " after hand cards moved on both seats");
        End(g);
    }

    private static void ArgentSquire(Game g, Controller p, string id)
    {
        var m = (Minion)Play(g, p, id);
        Only(m, id, GameTag.DIVINE_SHIELD);
        Play(g, p, "CS2_008", m);
        Core2026Proof.Check(m.Damage == 0 && !m.HasDivineShield && m.Zone.Type == Zone.PLAY, id + " the shield absorbs the first damage entirely");
        Play(g, p, "CS2_008", m);
        Core2026Proof.Check(m.Zone.Type == Zone.GRAVEYARD, id + " dies to the next point");
        var fresh = (Minion)Play(g, p, id);
        var enemy = Summon(p.Opponent, "CS2_182");
        NextOwnTurn(g);
        Resume(g, next => Core2026Proof.Check(((Minion)next.IdEntityDic[fresh.Id]).HasDivineShield, id + " the shield survives clone and snapshot"));
        var attacker = Summon(p, "CS2_182");
        Attack(g, attacker, Summon(p.Opponent, id));
        Core2026Proof.Check(p.Opponent.BoardZone.Single(x => x.Card.Id == id).Damage == 0 && attacker.Damage == 1,
            id + " an enemy copy takes no damage from the first attacker; the attacker takes 1 back");
        Silence(g, fresh);
        Core2026Proof.Check(!fresh.HasDivineShield, id + " Silence removes the shield");
        Play(g, p, "CS2_008", fresh);
        Core2026Proof.Check(fresh.Zone.Type == Zone.GRAVEYARD, id + " a Silenced Squire dies to one point");
    }

    private static void AngryChicken(Game g, Controller p, string id)
    {
        var champ = Summon(p, "CS2_222");
        var chicken = (Minion)Play(g, p, id);
        Only(chicken, id);
        Core2026Proof.Check(chicken.Card.Races.Contains(Race.BEAST) && chicken.AttackDamage == 2 && chicken.Health == 2, id + " a 1/1 Beast, here 2/2 under a Stormwind Champion");
        Play(g, p, "CS2_008", chicken);
        Core2026Proof.Check(chicken.Damage == 1 && chicken.AttackDamage == 7 && chicken.Health == 1, id + " damaged: +5 Attack");
        Play(g, p, "CS2_008", chicken);
        Core2026Proof.Check(chicken.Zone.Type == Zone.GRAVEYARD, id + " dies at exact Health");
        var c2 = (Minion)Play(g, p, id);
        Play(g, p, "CS2_008", c2);
        Resume(g, next => {
            var r = (Minion)next.IdEntityDic[c2.Id];
            Core2026Proof.Check(r.AttackDamage == 7, id + " enrage survives clone and snapshot");
            Play(next, next.Player1, "CS2_007", r);
            Core2026Proof.Check(r.AttackDamage == 2 && r.Damage == 0, id + " healed: the bonus ends in the restored game");
        });
        Core2026Proof.Check(c2.AttackDamage == 7, id + " a damaged copy is enraged");
        Play(g, p, "CS2_007", c2);
        Core2026Proof.Check(c2.AttackDamage == 2, id + " healing ends the bonus");
        Play(g, p, "CS2_008", c2);
        End(g); End(g);
        Core2026Proof.Check(c2.AttackDamage == 7, id + " the bonus persists across turns");
        Silence(g, c2);
        Core2026Proof.Check(c2.AttackDamage == 2 && c2.Health == 1, id + " Silence removes the bonus (the Champion's aura stays)");
        Core2026Proof.Check(c2.Damage == 1, id + " Silence does not heal it");
        End(g);
        var theirs = (Minion)Play(g, p.Opponent, id);
        Core2026Proof.Check(theirs.AttackDamage == 1, id + " opponent copy is a plain 1/1 until damaged");
        End(g);
    }
}
