using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class FlashFloodProof
{
    // repeat-effects/flood-lone-minion.yaml and flood-outcast-retarget.yaml: STILL-UNPINNED,
    // owner assumptions (D export, E discussion): two entries per wave; deaths before Outcast reselection.
    internal static void Run() => StandardSetsProof.Case("CATA_533", Unpinned_FlashFlood);
    private static void Unpinned_FlashFlood(Game g, Controller p, string id)
    {
        foreach (int position in new[] { 0, 1, 2, -1 })
        {
            EmptyHand(p); ParkBoard(p.Opponent);
            var lone = Summon(p.Opponent, "CS2_182"); lone.Health = 31;
            IPlayable source = null;
            for (int i = 0; i < (position == -1 ? 1 : 3); i++)
            {
                var held = Add(p, i == (position == -1 ? 0 : position) ? id : "CS2_182");
                if (held.Card.Id == id) source = held;
            }
            Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
            Core2026Proof.Check(lone.Damage == (position == 1 ? 10 : 20), "Unpinned_FlashFlood: lone target hit twice per wave; only original hand edges repeat");
        }
        EmptyHand(p); ParkBoard(p.Opponent);
        var shield = Summon(p.Opponent, "CS2_182"); shield.Health = 11; shield.HasDivineShield = true;
        Add(p, "CS2_182"); var middle = Add(p, id); Add(p, "CS2_182"); PlayEntity(g, p, middle);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 5, "Unpinned_FlashFlood: first entry consumes Shield, second damages same minion");
        EmptyHand(p); ParkBoard(p.Opponent);
        var acolyte = Summon(p.Opponent, "EX1_007"); acolyte.Health = 3;
        int hits = 0, handBefore = p.Opponent.HandZone.Count;
        TriggerManager.TriggerHandler onHit = _ => hits++;
        acolyte.TakeDamageTrigger += onHit;
        Add(p, "CS2_182"); middle = Add(p, id); Add(p, "CS2_182"); PlayEntity(g, p, middle);
        acolyte.TakeDamageTrigger -= onHit;
        Core2026Proof.Check(hits == 2 && p.Opponent.HandZone.Count == handBefore + 2 && acolyte.Zone.Type == Zone.GRAVEYARD,
            $"Unpinned_FlashFlood: lethal first hit retains second snapshotted entry before death settlement; hits={hits}, draws={p.Opponent.HandZone.Count - handBefore}");
        EmptyHand(p); ParkBoard(p.Opponent);
        var location = LocationProofs.Location(p.Opponent);
        var sleeper = Summon(p.Opponent, "EDR_840t2");
        var left = Summon(p.Opponent, "CS2_182"); left.Health = 20;
        var center = Summon(p.Opponent, "CS2_182");
        var right = Summon(p.Opponent, "CS2_182"); right.Health = 20;
        Summon(p, "CS2_052");
        Add(p, "CS2_182"); middle = Add(p, id); Add(p, "CS2_182"); PlayEntity(g, p, middle);
        Core2026Proof.Check(left.Damage == 6 && right.Damage == 6 && center.Damage == 0 && sleeper.Damage == 0 && location[GameTag.DAMAGE] == 0
            && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "Spell Damage per edge; skip locations/Dormant; middle and heroes untouched");
        ParkBoard(p); ParkBoard(p.Opponent); EmptyHand(p);
        var first = Summon(p.Opponent, "EX1_556"); var second = Summon(p.Opponent, "EX1_556");
        center = Summon(p.Opponent, "CS2_182");
        // Reposition one golem so both edges die in the first wave.
        p.Opponent.BoardZone.Remove(second); p.Opponent.BoardZone.Add(second);
        Play(g, p, id);
        Core2026Proof.Check(first.Zone.Type == Zone.GRAVEYARD && second.Zone.Type == Zone.GRAVEYARD && center.Zone == p.Opponent.BoardZone
            && center.Damage == 0 && p.Opponent.BoardZone.Count == 1
            && p.Opponent.GraveyardZone.Count(m => m.Card.Id == "skele21") == 2,
            "Unpinned_FlashFlood: Outcast hits freshly summoned edge Deathrattles rather than stale golems");
        ParkBoard(p.Opponent); EmptyHand(p);
        var indirect = Summon(p.Opponent, "CS2_182"); indirect.Health = 31;
        var spell = Add(p, id);
        new PlayTask(PlayType.SPELL).Process(g, p, p.Hero, null, new TaskStack { Playables = new[] { spell } });
        Core2026Proof.Check(indirect.Damage == 10, "effect cast never gains Outcast");
        ParkBoard(p.Opponent); EmptyHand(p); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0, "empty board fizzles both waves");
    }
}
