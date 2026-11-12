using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class EvTimeProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, message);
    internal static Game Fixture(int seed = 1) => PlayerDecisionProof.Fixture(seed);
    internal static void Answer(Game g, int id) => PlayerDecisionProof.Answer(g, id);
    internal static void Require(string id) => Check(Cards.FromId(id)?.Implemented == true, "missing EVENT recipe: " + id);
    internal static void Unpinned_RuniClockState(Game g, Controller p, string id)
    {
        var a = (Minion)Add(p, "CS2_182"); var b = Add(p, "CS2_231");
        Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_004e"), p.Hero, a);
        int attack = a.AttackDamage, health = a.BaseHealth; a.Cost = 1;
        var spell = Add(p, "GAME_005");
        var source = (Minion)Play(g, p, id);
        Check(p.HandZone.Count == 1 && p.HandZone[0] == spell && a.Zone == p.SetasideZone, "Runi removes only minions without discarding");
        source.Silence(); Kill(g, source);
        var bytes = g.SaveSnapshot();
        Check(bytes.SequenceEqual(Game.LoadSnapshot(bytes).SaveSnapshot()), "Runi pending snapshot bytes");
        foreach (var future in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(bytes) })
        {
            End(future); End(future);
            Check(future.IdEntityDic[a.Id].Zone == future.Player1.SetasideZone, "Runi skips first own start and enemy boundary");
            End(future); End(future);
            var returned = (Minion)future.IdEntityDic[a.Id];
            Check(returned.Zone == future.Player1.HandZone && returned.AttackDamage == attack + 5 && returned.BaseHealth == health + 5 && returned.Cost == 1,
                "Runi preserves identity, buffs and cost then adds 5/5");
            Check(returned.ZonePosition < future.IdEntityDic[b.Id].ZonePosition, "Runi return order");
            PlayEntity(future, future.Player1, returned); returned.Silence();
            Check(returned.AttackDamage == returned.Card.ATK && returned.BaseHealth == returned.Card.Health, "Runi buff removable by Silence");
        }
    }
    internal static void Unpinned_PhoenixClockCopy(Game g, Controller p, string id)
    {
        var fuel = Add(p, "GAME_005"); var source = (Minion)Add(p, id);
        Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_004e"), p.Hero, source);
        PlayEntity(g, p, source);
        Check(source.Health > 5, "Phoenix source has non-base stats at ignition");
        Check(p.Choice?.Choices.SequenceEqual(new[] { fuel.Id }) == true, "Phoenix chooses actual hand card");
        Answer(g, fuel.Id);
        Check(fuel.AppliedEnchantments?.Single().Card.Id == "CATA_EVENT_001e", "Phoenix fire enchantment");
        source.Silence(); Kill(g, source);
        End(g); End(g);
        Check(fuel.Zone == p.HandZone && fuel.AppliedEnchantments.Single()[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2, "Phoenix ignition end counts; enemy end does not");
        var bytes = g.SaveSnapshot();
        foreach (var future in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(bytes) })
        {
            End(future); End(future); End(future);
            Check(future.IdEntityDic[fuel.Id].Zone.Type == Zone.GRAVEYARD, "Phoenix discards exact fuel at third own end");
            var phoenix = future.Player1.BoardZone.Single();
            Check(phoenix.Card.Id == id && phoenix.AttackDamage == 5 && phoenix.Health == 5 && future.Player1.Choice == null,
                "Phoenix fresh 5/5 summon without Battlecry after source death");
        }
    }
    internal static void Unpinned_SandsSwitch(Game g, Controller p, string id)
    {
        var source = Play(g, p, id);
        Check(p.Choice?.Choices.Count == 3, "Sands three class-spell offers");
        var offered = p.Choice.Choices.Select(x => g.IdEntityDic[x].Card).ToArray();
        Check(offered.Select(c => c.Id).Distinct().Count() == 3 && offered.All(c => c.Type == CardType.SPELL && c.Class != CardClass.NEUTRAL), "Sands distinct nonneutral spells");
        Answer(g, p.Choice.Choices[0]);
        Check(g.Rewind != null && p.HandZone.Count == 1, "Sands Discover settles before Rewind");
        var bytes = g.SaveSnapshot();
        foreach (var future in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(bytes) })
        {
            Answer(future, future.Rewind.RewindEntityId);
            var owner = future.Player1;
            Check(future.IdEntityDic[source.Id].Card.Id == "TIME_EVENT_999t" && owner.HandZone.Count == 0, "Sands rewinds result and changes identity");
            Check(owner.Choice.Choices.All(x => future.IdEntityDic[x].Card.Class == owner.HeroClass || future.IdEntityDic[x].Card.MultiClassGroup != 0), "Sands YOUR-class replay");
            Answer(future, owner.Choice.Choices.Last());
            Check(future.Rewind == null && owner.HandZone.Count == 1 && owner.UsedMana == 1 && owner.NumCardsPlayedThisTurn == 1, "Sands one final result and payment");
        }
    }
    [GroupProof("ev-time")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var (id, proof) in new (string, Action<Game, Controller, string>)[] {
            ("TIME_EVENT_998", Unpinned_RuniClockState), ("CATA_EVENT_001", Unpinned_PhoenixClockCopy), ("TIME_EVENT_999", Unpinned_SandsSwitch) })
        {
            try { Require(id); var g = Fixture(); proof(g, g.Player1, id); Console.WriteLine("PASS Unpinned_ev_time " + id); }
            catch (Exception e) { failures.Add(id + ": " + e); }
        }
        Check(failures.Count == 0, string.Join("\n", failures));
    }
}
