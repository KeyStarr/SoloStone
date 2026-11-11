using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class LotusTroublemakerProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_470", (g, p, id) => {
        var fresh = g.CloneExact(history: true);
        var source = Add(p, id);
        g.AuraUpdate();
        Core2026Proof.Check(source.Card.ConsumesHistory && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
            "history consumer and base one-shot display");
        int health = p.Opponent.Hero.Health;
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.Opponent.Hero.Health == health - 1 && ((Minion)source).AttackDamage == 2
            && ((Minion)source).Health == 2 && source.Card.Tags[GameTag.BATTLECRY] == 1,
            "printed 3-mana 2/2 Battlecry shoots once without progress");
        Residence(fresh.CloneExact(history: true), id);
        Payments(fresh.CloneExact(history: true), id);
        Missiles(fresh.CloneExact(history: true), id);
        Privacy(fresh, id);
    });

    private static void Verify(bool value, string message) => Core2026Proof.Check(value, "Lotus: " + message);
    private static int Display(IPlayable card) => card[GameTag.TAG_SCRIPT_DATA_NUM_1];
    private static DamageResolvedHistoryFact[] Hits(Game g, IPlayable card) =>
        g.HistoryFacts.Query.Damage().Where(f => f.SourceId == card.Id).ToArray();
    private static void Two(Game g, Controller p) => Play(g, p, "CS2_025");

    private static void Residence(Game g, string id)
    {
        var p = g.Player1;
        Two(g, p);
        var source = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone, zonePos: 0);
        Two(g, p);
        Verify(Display(source) == 1, "deck retains pinned base display without refresh");
        End(g); Two(g, p.Opponent); End(g);
        Verify(Generic.Draw(p, source) == source && source.Zone == p.HandZone, "draw exact physical instance");
        g.AuraUpdate();
        Verify(Display(source) == 2, "previous-turn own deck play retained; pre-creation/opponent plays excluded");
        var late = Add(p, id); g.AuraUpdate();
        Verify(Display(late) == 1, "new instance does not inherit prior plays");
        p.SetasideZone.Add(p.HandZone.Remove(source));
        Two(g, p);
        p.HandZone.Add(p.SetasideZone.Remove(source)); g.AuraUpdate();
        Verify(Display(source) == 2 && Display(late) == 2, "only plays inside each instance's hand/deck residence count");
        Two(g, p);
        Verify(Display(source) == 3 && Display(late) == 3, "current-turn hand play adds one shot");
        foreach (var copy in new[] { g.Clone(), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Two(copy, copy.Player1);
            Verify(Display(copy.IdEntityDic[source.Id]) == 4 && Display(source) == 3,
                "clone/load progress diverges without mutating original");
            var copiedSource = copy.IdEntityDic[source.Id];
            copiedSource[GameTag.TAG_SCRIPT_DATA_NUM_1] = 99;
            PlayEntity(copy, copy.Player1, copiedSource);
            Verify(Hits(copy, copiedSource).Length == 4, "resolution reads branch-local store, not stale display tag");
        }
        source.Cost = 2;
        PlayEntity(g, p, source);
        Verify(Hits(g, source).Length == 3 && Hits(g, source).All(f => f.Amount == 1),
            "own discounted two-Mana play excluded from its Battlecry");
        Verify(Display(late) == 4, "another held instance counts played Lotus");
    }

    private static void Payments(Game g, string id)
    {
        var p = g.Player1; var source = Add(p, id); g.AuraUpdate();
        var discounted = Add(p, "CS2_182"); discounted.Cost = 2;
        PlayEntity(g, p, discounted);
        var increased = Add(p, "CS2_231"); increased.Cost = 2;
        PlayEntity(g, p, increased);
        Verify(Display(source) == 3, "actual two-Mana plays count regardless of printed cost");
        foreach (int paid in new[] { 0, 1, 3 })
        {
            var spell = Add(p, "CS2_025"); spell.Cost = paid;
            spell[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] = 2;
            PlayEntity(g, p, spell);
        }
        Verify(Display(source) == 3, "zero/one/three Mana excluded, including stale last-known cost");
        var health = Add(p, "CS2_025");
        p.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 1; p.UsedMana = 0;
        Verify(g.Process(PlayCardTask.Any(p, health)), "Health-paid spell accepted");
        p.ControllerAuraEffects[GameTag.SPELLS_COST_HEALTH] = 0;
        Verify(g.HistoryFacts.Query.Payments().Last().Mana == 0 && g.HistoryFacts.Query.Payments().Last().Health == 2
            && Display(source) == 3, "two Health is not two Mana");
        p.UsedMana = 0;
        Verify(g.Process(HeroPowerTask.Any(p)), "two-Mana Rogue power accepted");
        var trade = Add(p, "CORE_EX1_005"); p.UsedMana = 0;
        Verify(g.Process(TradeCardTask.Any(p, trade)), "Trade accepted");
        var prepare = Add(p, "CS2_029"); prepare.Cost = 3; PrepareRules.Grant(prepare); p.UsedMana = 0;
        Verify(g.Process(PrepareCardTask.Any(p, prepare)) && g.HistoryFacts.Query.Payments().Last().Mana == 2,
            "two-Mana Prepare accepted");
        var other = Entity.FromCard(p, Cards.FromId("CS2_025")); p.UsedMana = 0;
        Verify(Generic.PayPhase(p, other), "direct nonplay payment accepted");
        g.AuraUpdate();
        Verify(Display(source) == 3, "Hero Power/Trade/Prepare/Other spending excluded");
        var temporary = Add(p, "CS2_025"); p.UsedMana = 10; p.TemporaryMana = 2;
        Verify(g.Process(PlayCardTask.Any(p, temporary)) && p.TemporaryMana == 0 && p.UsedMana == 10,
            "temporary two-Mana payment accepted");
        Verify(Display(source) == 4, "temporary Mana counts");
        PlayEntity(g, p, source);
        Verify(Hits(g, source).Length == 4, "payment boundaries affect actual Battlecry shots");
    }

    private static void Missiles(Game g, string id)
    {
        var p = g.Player1; var source = Add(p, id);
        for (int i = 0; i < 7; i++) Two(g, p);
        var enemies = Enumerable.Range(0, 7).Select(_ => Summon(p.Opponent, "CS2_231")).ToArray();
        Summon(p, "CS2_052");
        var exact = g.CloneExact(history: true);
        PlayEntity(g, p, source);
        PlayEntity(exact, exact.Player1, exact.IdEntityDic[source.Id]);
        var hits = Hits(g, source);
        Verify(hits.Length == 8 && hits.All(f => f.Amount == 1 && f.VictimControllerId == p.Opponent.Id)
            && enemies.Any(m => m.Zone.Type == Zone.GRAVEYARD)
            && hits.Where(f => f.VictimType == CardType.MINION).Select(f => f.VictimId).Distinct().Count()
                == hits.Count(f => f.VictimType == CardType.MINION),
            "eight single-damage enemy-only shots; dead targets not selected again; Spell Damage ignored");
        Verify(Canonical.State(g) == Canonical.State(exact), "game RNG yields identical exact-clone target sequence");
        foreach (var hit in hits.Take(hits.Length - 1).Where(f => f.VictimType == CardType.MINION))
            Verify(g.HistoryFacts.Query.Deaths().Single(f => f.EntityId == hit.VictimId).Sequence
                < hits.First(f => f.Sequence > hit.Sequence).Sequence, "lethal shot settles death before next shot");
        var noCry = Summon(p, id);
        noCry.Silence(); Kill(g, noCry);
        Verify(Hits(g, noCry).Length == 0, "summoning/silence/death do not fire Battlecry");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
        var blocked = Add(p, id);
        Verify(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == blocked.Id),
            "full board prohibits minion play and shots");
    }

#if CLIENT_TRANSPORT
    private static List<byte[]> Native(Game g)
    {
        var packets = new List<byte[]>();
        var match = new NativeMatch((_, message) => {
            using var stream = new MemoryStream(); message.Serialize(stream); packets.Add(stream.ToArray());
        });
        typeof(NativeMatch).GetProperty("Game").SetValue(match, g);
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(match, new object[] { g.PowerHistory.Full });
        return packets;
    }
#endif

    private static void Privacy(Game fresh, string id)
    {
        foreach (int viewer in new[] { 1, 2 })
        foreach (bool deck in new[] { false, true })
        {
            Game Fixture(bool lotus)
            {
                var g = fresh.CloneExact(history: true);
                if (viewer == 1) End(g);
                var owner = viewer == 1 ? g.Player2 : g.Player1;
                var hidden = Entity.FromCard(owner, Cards.FromId(lotus ? id : "CS2_182"),
                    zone: deck ? owner.DeckZone : owner.HandZone);
                Two(g, owner);
                Verify(!g.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>()
                    .Any(b => b.Source == hidden.Id), "hidden card emits no POWER blocks");
                return g;
            }
            var a = Fixture(true); var b = Fixture(false);
            var fa = FairBotState.Create(a, viewer, 42, DeckStyle.Control);
            var fb = FairBotState.Create(b, viewer, 42, DeckStyle.Control);
            var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
            var ga = (Game)field.GetValue(fa); var gb = (Game)field.GetValue(fb);
            ga.AuraUpdate(); gb.AuraUpdate();
            Verify(Canonical.State(ga) == Canonical.State(gb) && fa.Evaluate() == fb.Evaluate()
                && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())),
                "both viewers: hidden hand/deck presence leaves projected state/evaluation/actions identical");
            if (viewer == 1)
            {
#if CLIENT_TRANSPORT
                var packetsA = Native(a); var packetsB = Native(b);
                Verify(packetsA.Count == packetsB.Count && packetsA.Zip(packetsB).All(pair => pair.First.SequenceEqual(pair.Second)),
                    "hidden hand/deck native packets byte-identical with vanilla replacement");
#endif
            }
        }
    }
}
