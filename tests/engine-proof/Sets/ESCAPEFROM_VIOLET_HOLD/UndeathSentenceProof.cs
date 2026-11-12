using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class UndeathSentenceProof
{
    // data/rules-evidence/deathrattle-copy/dead-rattle-{eligibility,payload-reset,source-position}.yaml
    // C; owner assumptions: printed Deathrattles only, fresh offboard source, append summons.
    internal static void Run() => StandardSetsProof.Case("JAIL_940", Unpinned_UndeathSentence);
    private static void Unpinned_UndeathSentence(Game g, Controller p, string id)
    {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.IsEmpty, "empty death history fizzles");
        Kill(g, Summon(p, "CS2_182")); Kill(g, Summon(p.Opponent, "EX1_556"));
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.IsEmpty, "vanilla and opposing deaths excluded");
        var source = Summon(p, "EX1_556"); source.Silence(); Kill(g, source);
        var loc = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        int deaths = g.HistoryFacts.Query.Deaths().Count();
        PoolPilotProof.Resume(g, next => Play(next, next.Player1, id));
        var output = p.BoardZone.Last();
        Core2026Proof.Check(output.Card.Id == "skele21" && output.AttackDamage == 2 && output.Health == 1
            && output.ZonePosition == 2 && g.HistoryFacts.Query.Deaths().Count() == deaths, "silenced printed Deathrattle runs clean offboard, at far right, without new death");
        StandardSetsProof.ParkBoard(p); DeathKnightC1Proof.FullBoard(p); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7, "mixed full board does not displace entities");
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "declared history consumer");
        var clean = DeathKnightC1Proof.NewGame(g.Seed); var q = clean.Player1;
        var meal = Summon(q, "CS2_182");
        var cube = (Minion)Play(clean, q, "LOOT_161", meal);
        Core2026Proof.Check(meal.Zone.Type == Zone.GRAVEYARD && cube.AppliedEnchantments.Single().Card.Id == "LOOT_161e"
            && cube.AppliedEnchantments.Single()[GameTag.TAG_SCRIPT_DATA_NUM_1] == meal.Id,
            "Cube Battlecry captured and destroyed the actual meal");
        Kill(clean, cube);
        Core2026Proof.Check(q.BoardZone.Count == 2 && q.BoardZone.All(m => m.Card.Id == "CS2_182"
            && m.AttackDamage == 4 && m.Health == 5), "populated Cube's normal Deathrattle produces two exact meal copies");
        StandardSetsProof.ParkBoard(q);
        PoolPilotProof.Resume(clean, next => Play(next, next.Player1, id));
        var replaySource = clean.IdEntityDic.Values.OfType<Minion>().Single(m => m.Card.Id == "LOOT_161" && m.Id != cube.Id);
        Core2026Proof.Check(q.BoardZone.IsEmpty && replaySource.Zone == q.SetasideZone
            && (replaySource.AppliedEnchantments?.Count ?? 0) == 0 && replaySource[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0,
            "Sentence runs a clean offboard Cube without the populated death-time payload");

        var granted = DeathKnightC1Proof.NewGame(g.Seed); var owner = granted.Player1;
        var vanilla = Summon(owner, "CS2_182");
        Generic.AddEnchantmentBlock(granted, Cards.FromId("EX1_158e"), owner.Hero, vanilla);
        Core2026Proof.Check(vanilla.HasDeathrattle && vanilla.Card[GameTag.DEATHRATTLE] == 0,
            "vanilla minion has a granted-only Deathrattle");
        Kill(granted, vanilla);
        Core2026Proof.Check(owner.BoardZone.Single().Card.Id == "EX1_158t" && owner.BoardZone.Single().AttackDamage == 2
            && owner.BoardZone.Single().Health == 2, "granted Deathrattle really summons its exact 2/2 Treant on death");
        StandardSetsProof.ParkBoard(owner);
        var sentence = Add(owner, id); int entities = granted.IdEntityDic.Count; var random = granted.Random.CaptureState();
        PoolPilotProof.Resume(granted, next => PlayEntity(next, next.Player1, next.IdEntityDic[sentence.Id]));
        Core2026Proof.Check(owner.BoardZone.IsEmpty && granted.IdEntityDic.Count == entities && granted.Random.CaptureState().Equals(random),
            "granted-only death is ineligible: no selection RNG or offboard source created");
    }
}
