using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ImbueConsumersProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_226", Houndmaster);
        StandardSetsProof.Case("EDR_227", Umbraclaw);
        StandardSetsProof.Case("EDR_231", Embrace);
        StandardSetsProof.Case("EDR_518", Garden);
        StandardSetsProof.Case("EDR_519", Wisprider);
        StandardSetsProof.Case("EDR_800", Guardian);
        StandardSetsProof.Case("EDR_852", Knight);
        StandardSetsProof.Case("EDR_871", Gatherer);
    }

    // Hero class is a fixture, not a claim about hero-card replacement rules.
    private static void Class(Controller p, string hero = "HERO_08") => p.Hero.Card = Cards.FromId(hero);
    private static void Imbued(Controller p, int n) => Core2026Proof.Check(ImbueRules.Count(p) == n
        && p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == n, "Imbue progression and power magnitude");
    private static void Replay(Game g, Action<Game> action)
    {
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        string before = Canonical.State(g);
        action(clone);
        Core2026Proof.Check(Canonical.State(g) == before, "clone leaves parent untouched");
        action(loaded); action(g);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone)
            && Canonical.State(g) == Canonical.State(loaded), "future behavior/RNG survives clone and snapshot");
    }
    private static void Gates(Game g, Controller p, string id)
    {
        Core2026Proof.Check(ImbueRules.IsConsumer(Cards.FromId(id)), "export IMBUE tag reaches play gate");
        var supported = g.CloneExact(history: true); var owner = supported.Player1; Class(owner, "HERO_04");
        var source = Add(owner, id);
        Core2026Proof.Check(source.IsPlayableByPlayer, "Paladin consumer gate lifted with Portal family");
    }
    private static int Change(Game g, int cursor, int entity, GameTag tag, int value) => Array.FindIndex(
        g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryTagChange>().ToArray(),
        change => change.EntityId == entity && change.Tag == tag && change.Value == value);
    private static void HandBeforeImbue(Game g, Controller p, int cursor, int entity, int count)
    {
        int draw = Change(g, cursor, entity, GameTag.ZONE, (int)Zone.HAND);
        int imbue = Change(g, cursor, p.Id, GameTag.IMBUE, count);
        Core2026Proof.Check(draw >= 0 && imbue > draw, "hand entry resolves before later Imbue clause");
    }
    private static void FillHand(Controller p) { while (!p.HandZone.IsFull) Add(p, "CS2_182"); }
    private static void Resolve(Game g, Controller p, IPlayable source, IPlayable target = null)
    {
        source.Card.Power.PowerTask.Process(g, p, source, target, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
    }
    // Lean_ConsumerClauseOrder: imbue-16; missing/burned draws never suppress the next clause.
    private static void Houndmaster(Game g, Controller p, string id)
    {
        Gates(g, p, id); Class(p, "HERO_05");
        var beast = Entity.FromCard(p, Cards.FromId("EDR_227"), zone: p.DeckZone);
        var other = Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone);
        Replay(g, x => {
            var owner = x.Player1; int cursor = x.PowerHistory.Full.Count; Play(x, owner, id);
            Core2026Proof.Check(owner.HandZone.Count == 1 && owner.HandZone.Single().Card.IsRace(Race.BEAST)
                && new[] { beast.Id, other.Id }.Contains(owner.HandZone.Single().Id), "draws actual deck Beast only");
            Imbued(owner, 1); HandBeforeImbue(x, owner, cursor, owner.HandZone.Single().Id, 1);
        });
        var source = p.BoardZone.Single(m => m.Card.Id == id);
        Resolve(g, p, source); Imbued(p, 2);
        int fatigue = p.Hero.Damage, hand = p.HandZone.Count;
        Resolve(g, p, source); Imbued(p, 3);
        Core2026Proof.Check(p.HandZone.Count == hand && p.Hero.Damage == fatigue, "no Beast neither draws non-Beast nor fatigues");
        var burn = Entity.FromCard(p, Cards.FromId("EDR_227"), zone: p.DeckZone);
        FillHand(p); Resolve(g, p, source); Imbued(p, 4);
        Core2026Proof.Check(burn.Zone == p.GraveyardZone && p.HandZone.Count == 10, "matching draw burns at full hand, still Imbues");
    }
    private static void Umbraclaw(Game g, Controller p, string id)
    {
        Gates(g, p, id); Class(p, "HERO_05");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRush && source.Card.IsRace(Race.BEAST) && ImbueRules.Count(p) == 0, "Rush Beast; no Battlecry Imbue");
        var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == enemy.Id)
            && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target is Hero), "Rush attacks minions only on play turn");
        Replay(g, x => Kill(x, (Minion)x.IdEntityDic[source.Id])); Imbued(p, 1);
        g.DeathProcessingAndAuraUpdate(); Imbued(p, 1);
        var silence = Summon(p, id); silence.Silence(); Kill(g, silence); Imbued(p, 1);
        var enemyCopy = Summon(p.Opponent, id); Kill(g, enemyCopy);
        Imbued(p, 1); Imbued(p.Opponent, 1);
        End(g); End(g); Imbued(p, 1);
    }
    private static void Embrace(Game g, Controller p, string id)
    {
        Gates(g, p, id); Class(p, "HERO_02");
        p.Hero.Damage = 10; p.Hero[GameTag.SPELLPOWER] = 5;
        int deck = p.DeckZone.Count, cursor = g.PowerHistory.Full.Count;
        var first = Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 6 && p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1,
            "heals four (Spell Damage excluded), then draws"); Imbued(p, 1);
        HandBeforeImbue(g, p, cursor, p.HandZone.Single().Id, 1);
        Core2026Proof.Check(Change(g, cursor, p.Hero.Id, GameTag.DAMAGE, 6) >= 0
            && Change(g, cursor, p.Hero.Id, GameTag.DAMAGE, 6) < Change(g, cursor, p.HandZone.Single().Id, GameTag.ZONE, (int)Zone.HAND),
            "healing resolves before draw clause");
        var friendly = Summon(p, "CS2_182"); friendly.Damage = 2;
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 4;
        var spell = Add(p, id); p.UsedMana = 0;
        var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(new[] { p.Hero.Id, p.Opponent.Hero.Id, friendly.Id, enemy.Id }.All(target => options.Any(t => t.Target?.Id == target))
            && options.All(t => t.Target is ICharacter), "ordinary required character target on either side");
        PlayEntity(g, p, spell, friendly);
        Core2026Proof.Check(friendly.Damage == 0, "heal caps at maximum health");
        p.ControllerAuraEffects[GameTag.SPELL_HEALING_DOUBLE] = 1;
        p.Opponent.Hero.Damage = 12; Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "normal spell healing multiplier applies");
        p.ControllerAuraEffects[GameTag.SPELL_HEALING_DOUBLE] = 0;
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 0, "enemy minion can be healed");
        FillHand(p); int beforeBurn = p.DeckZone.Count;
        Resolve(g, p, first, p.Hero); Imbued(p, 5);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == beforeBurn - 1, "full-hand draw burns, later Imbue still resolves");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        p.Hero.Damage = 10; Replay(g, x => Play(x, x.Player1, id, x.Player1.Hero));
        Core2026Proof.Check(p.Hero.Damage == 7, "empty deck still heals, then fatigue one"); Imbued(p, 6);
    }
    private static void Garden(Game g, Controller p, string id)
    {
        Gates(g, p, id); Class(p, "HERO_02");
        Play(g, p, id); Imbued(p, 1);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "empty hand continues after Imbue");
        var chosen = Add(p, "CS2_182"); var unchosen = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var enemy = Add(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id); Imbued(p, 2);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.SequenceEqual(new[] { chosen.Id, unchosen.Id }),
            "Imbue precedes prompt; only friendly hand minions offered");
        Replay(g, x => PlayerDecisionProof.Answer(x, chosen.Id));
        Core2026Proof.Check(chosen.Cost == 3 && chosen[GameTag.COST] == 3 && unchosen.Cost == 4 && spell.Cost == 4 && enemy.Cost == 4,
            "only chosen entity reduced by one; COST visible");
        source.Silence();
        Core2026Proof.Check(chosen.Cost == 3 && chosen[GameTag.COST] == 3, "discount survives actual source Silence");
        Kill(g, source);
        Core2026Proof.Check(chosen.Cost == 3 && chosen[GameTag.COST] == 3, "discount survives actual source death");
        End(g); End(g);
        Core2026Proof.Check(chosen.Cost == 3 && chosen[GameTag.COST] == 3, "discount survives turn end");
        Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.Contains(chosen.Id) && p.Choice.Choices.Contains(unchosen.Id)
            && !p.Choice.Choices.Contains(spell.Id) && !p.Choice.Choices.Contains(enemy.Id), "later Battlecry includes turn-drawn minion and prompts without stale selection");
        PlayerDecisionProof.Answer(g, unchosen.Id);
        Core2026Proof.Check(chosen.Cost == 3 && unchosen.Cost == 3, "new pick reduced independently");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        var zero = Add(p, "EDR_851t"); Play(g, p, id);
        Core2026Proof.Check(p.Choice?.Choices.Single() == zero.Id, "singleton zero-cost minion still prompts");
        PlayerDecisionProof.Answer(g, zero.Id); Core2026Proof.Check(zero.Cost == 0, "discount floors at zero");
        p.SetasideZone.Add(p.HandZone.Remove(zero)); Add(p, "CS2_029");
        ClearBoard(g, p); Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "spell-only hand continues without prompt");
        var repeated = ImbueProof.Make(CardClass.SHAMAN); var owner = repeated.Player1;
        Summon(owner, "LOE_077"); var firstPick = Add(owner, "CS2_182"); var secondPick = Add(owner, "CS2_182");
        Play(repeated, owner, id); Imbued(owner, 1);
        PlayerDecisionProof.Answer(repeated, firstPick.Id); Imbued(owner, 2);
        Core2026Proof.Check(firstPick.Cost == 3 && secondPick.Cost == 4 && owner.Choice != null,
            "Brann second Imbue precedes fresh second prompt");
        Replay(repeated, x => PlayerDecisionProof.Answer(x, secondPick.Id));
        Core2026Proof.Check(firstPick.Cost == 3 && secondPick.Cost == 3 && owner.Choice == null,
            "repeated Battlecry uses new selection and finishes");
    }
    // Lean_Wisprider: imbue-13 D2 (current amendment), no target/choice policy invented.
    private static void Wisprider(Game g, Controller p, string id)
    {
        Gates(g, p, id); Class(p);
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        int uses = p.NumTimesHeroPowerUsedThisGame;
        var source = PlayAt(g, p, id, 2);
        var wisp = p.BoardZone.Single(m => m.Card.Id == "EDR_851t");
        Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id, right.Id, wisp.Id })
            && p.Opponent.Hero.Damage == 1, "Imbue then trigger; HP Wisp at physical far right");
        Core2026Proof.Check(p.UsedMana == 5 && p.NumTimesHeroPowerUsedThisGame == uses && !p.Hero.HeroPower.IsExhausted,
            "trigger consumes no hero power mana/use/exhaustion");
        Imbued(p, 1);
        p.Hero.HeroPower.IsExhausted = true;
        Replay(g, x => Resolve(x, x.Player1, x.IdEntityDic[source.Id]));
        Imbued(p, 2);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3 && p.Hero.HeroPower.IsExhausted && p.NumTimesHeroPowerUsedThisGame == uses,
            "upgraded exhausted power triggers at new magnitude without using it");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int damage = p.Opponent.Hero.Damage; Resolve(g, p, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.Opponent.Hero.Damage == damage + 3, "full board still gets Mage damage");
        foreach (var cls in new[] { CardClass.DRUID, CardClass.HUNTER, CardClass.SHAMAN, CardClass.DEATHKNIGHT })
        {
            var test = ImbueProof.Make(cls); var owner = test.Player1;
            Play(test, owner, id); Imbued(owner, 1);
            if (cls == CardClass.DRUID) Core2026Proof.Check(owner.BoardZone.Last().Card.Id == "EDR_847pt2" && owner.BoardZone.Last().AttackDamage == 1, "Druid trigger summons magnitude-one Golem");
            else Core2026Proof.Check(owner.BoardZone.SlotCount == 1 && owner.Choice == null && owner.Opponent.Hero.Damage == 0,
                "Hunter empty Beast hand / Wind targeted / Infinite passive trigger no-op");
        }
        var hunter = ImbueProof.Make(CardClass.HUNTER); var beast = (Minion)Add(hunter.Player1, "EDR_227");
        var nonBeast = Add(hunter.Player1, "CS2_182");
        Play(hunter, hunter.Player1, id);
        Core2026Proof.Check(beast.AttackDamage == 6 && beast.Cost == 3 && nonBeast.Cost == 4,
            "Wolf trigger buffs and discounts eligible Beast only");
        var druid = ImbueProof.Make(CardClass.DRUID); var druidSource = Play(druid, druid.Player1, id);
        while (!druid.Player1.BoardZone.IsFull) Summon(druid.Player1, "CS2_182");
        Resolve(druid, druid.Player1, druidSource); Imbued(druid.Player1, 2);
        Core2026Proof.Check(druid.Player1.BoardZone.SlotCount == 7 && druid.Player1.BoardZone.Count(m => m.Card.Id == "EDR_847pt2") == 1,
            "Golem trigger respects physical board limit while Imbue still advances");
    }
    private static void Guardian(Game g, Controller p, string id)
    {
        Gates(g, p, id); var source = (Minion)Play(g, p, id); Imbued(p, 1);
        Core2026Proof.Check(source.HasTaunt && source.HasDivineShield, "printed Taunt and Divine Shield");
        source.Silence(); Kill(g, source); End(g); End(g); Imbued(p, 1);
        var hand = Add(p, id); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == hand.Id), "full board blocks minion before Battlecry");
    }
    private static void Knight(Game g, Controller p, string id)
    {
        Gates(g, p, id);
        Replay(g, x => Play(x, x.Player1, id)); Imbued(p, 1);
        var test = ImbueProof.Make(CardClass.WARRIOR); var owner = test.Player1; var power = owner.Hero.HeroPower;
        Play(test, owner, id);
        Core2026Proof.Check(owner.BoardZone.Single().Card.Id == id && owner.UsedMana == 2
            && ImbueRules.Count(owner) == 0 && owner.Hero.HeroPower.Id == power.Id, "Warrior plays Knight; Imbue no-op");
        var source = p.BoardZone.Single(); source.Silence(); Kill(g, source); Imbued(p, 1);
    }
    private static void Gatherer(Game g, Controller p, string id)
    {
        Gates(g, p, id); int cursor = g.PowerHistory.Full.Count; var source = Play(g, p, id); Imbued(p, 1);
        var token = p.HandZone.Single(); HandBeforeImbue(g, p, cursor, token.Id, 1);
        Core2026Proof.Check(token.Card.Id == "EDR_851t" && token.Card.AssetId == 116450 && token.Card.Type == CardType.MINION
            && token.Card.Class == CardClass.MAGE && token.Card.IsRace(Race.UNDEAD) && token.Cost == 0
            && ((Minion)token).AttackDamage == 1 && ((Minion)token).Health == 1 && token.Card.Text == "",
            "exact exported Mage Undead Wisp, no neutral substitute");
        FillHand(p); Replay(g, x => Resolve(x, x.Player1, x.IdEntityDic[source.Id])); Imbued(p, 2);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Id == "EDR_851t") == 1,
            "full-hand generation adds no Wisp, still Imbues");
        ((Minion)source).Silence(); Kill(g, (Minion)source); Imbued(p, 2);
        var test = ImbueProof.Make(CardClass.WARRIOR); Play(test, test.Player1, id);
        Core2026Proof.Check(test.Player1.HandZone.Single().Card.Id == "EDR_851t" && ImbueRules.Count(test.Player1) == 0,
            "no-op class still resolves other Battlecry clause");
    }
}
