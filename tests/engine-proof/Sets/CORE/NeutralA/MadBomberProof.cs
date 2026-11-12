using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class MadBomberProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_082", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var spellpower = Summon(p, "EX1_012"); spellpower.BaseHealth = 10;
        var held = Add(p, id);
        // Hand-checked RNG fixtures cover every eligible character, both sides, repeated hits, and no self hit.
        foreach (var fixture in new[] { (Seed: 1, Hits: new[] { spellpower.Id, p.Opponent.Hero.Id, friend.Id }),
            (Seed: 2, Hits: new[] { spellpower.Id, p.Hero.Id, p.Hero.Id }),
            (Seed: 7, Hits: new[] { enemy.Id, spellpower.Id, p.Hero.Id }) })
        {
            var trial = g.CloneExact(history: true); trial.SetRandomSeed(fixture.Seed);
            PoolPilotProof.Resume(trial, next => {
                var source = (Minion)next.IdEntityDic[held.Id]; int cursor = next.PowerHistory.Full.Count;
                PlayEntity(next, next.Player1, source);
                Hits(next, cursor, source, fixture.Hits);
                Core2026Proof.Check(source.Damage == 0, "Bomber excludes itself");
                foreach (int target in new[] { friend.Id, spellpower.Id, enemy.Id, p.Hero.Id, p.Opponent.Hero.Id })
                    Core2026Proof.Check(((ICharacter)next.IdEntityDic[target]).Damage == fixture.Hits.Count(hit => hit == target),
                        "seeded individual pings, no Spell Damage scaling");
            });
        }
        ParkBoard(p); ParkBoard(p.Opponent);
        friend = Summon(p, "EX1_506a"); enemy = Summon(p.Opponent, "EX1_506a");
        foreach (var fixture in new[] { (Seed: 1, Hits: new[] { enemy.Id, p.Opponent.Hero.Id, friend.Id }),
            (Seed: 5, Hits: new[] { friend.Id, enemy.Id, p.Hero.Id }) })
        {
            var trial = g.CloneExact(history: true); trial.SetRandomSeed(fixture.Seed);
            int cursor = trial.PowerHistory.Full.Count; var source = Play(trial, trial.Player1, id);
            Hits(trial, cursor, source, fixture.Hits);
            Core2026Proof.Check(trial.IdEntityDic[friend.Id].Zone == trial.Player1.GraveyardZone
                && trial.IdEntityDic[enemy.Id].Zone == trial.Player2.GraveyardZone,
                "lethal targets cannot be selected again and die after the Battlecry");
        }
        ParkBoard(p); ParkBoard(p.Opponent);
        var engineer = Summon(p.Opponent, "EX1_015");
        var cultMaster = Summon(p.Opponent, "EX1_595"); cultMaster.BaseHealth = 1;
        var deaths = g.CloneExact(history: true); deaths.SetRandomSeed(5);
        // Tier C: https://hearthstone.wiki.gg/wiki/Advanced_rulebook, death creation after the Battlecry Phase;
        // Cult Master + Novice Engineer example: both mortally wounded minions die together, so no card is drawn.
        PoolPilotProof.Resume(deaths, next => {
            int hand = next.Player2.HandZone.Count, deck = next.Player2.DeckZone.Count;
            int cursor = next.PowerHistory.Full.Count; var source = Play(next, next.Player1, id);
            Hits(next, cursor, source, new[] { engineer.Id, cultMaster.Id, next.Player1.Hero.Id });
            Core2026Proof.Check(next.IdEntityDic[engineer.Id].Zone == next.Player2.GraveyardZone
                && next.IdEntityDic[cultMaster.Id].Zone == next.Player2.GraveyardZone,
                "Engineer and Cult Master both die after their lethal pings");
            Core2026Proof.Check(next.Player2.HandZone.Count == hand && next.Player2.DeckZone.Count == deck,
                "Cult Master draws nothing when both minions die after the Battlecry");
        });
        ParkBoard(p); ParkBoard(p.Opponent);
        var acolyte = Summon(p, "EX1_007");
        var reactions = g.CloneExact(history: true); reactions.SetRandomSeed(24);
        PoolPilotProof.Resume(reactions, next => {
            int cursor = next.PowerHistory.Full.Count, hand = next.Player1.HandZone.Count;
            var source = Play(next, next.Player1, id);
            Hits(next, cursor, source, new[] { acolyte.Id, acolyte.Id, next.Player1.Hero.Id });
            var entries = next.PowerHistory.Full.Skip(cursor).ToArray();
            var hits = entries.Select((entry, index) => (entry, index))
                .Where(x => x.entry is PowerHistoryMetaData { Type: MetaDataType.DAMAGE }).Select(x => x.index).ToArray();
            var triggers = entries.Select((entry, index) => (entry, index))
                .Where(x => x.entry is PowerHistoryBlockStart { BlockType: BlockType.TRIGGER } b && b.Source == acolyte.Id)
                .Select(x => x.index).ToArray();
            Core2026Proof.Check(next.Player1.HandZone.Count == hand + 2 && triggers.Length == 2
                && hits[0] < triggers[0] && triggers[0] < hits[1] && hits[1] < triggers[1] && triggers[1] < hits[2],
                "Acolyte draws resolve between individual hits in original/clone/snapshot");
        });
        ParkBoard(p); ParkBoard(p.Opponent);
        var bolvar = Summon(p, "ICC_858");
        var shields = g.CloneExact(history: true); shields.SetRandomSeed(24);
        PoolPilotProof.Resume(shields, next => {
            int cursor = next.PowerHistory.Full.Count;
            Play(next, next.Player1, id);
            var entries = next.PowerHistory.Full.Skip(cursor).ToArray();
            var hits = entries.Select((entry, index) => (entry, index))
                .Where(x => x.entry is PowerHistoryMetaData { Type: MetaDataType.DAMAGE }).ToArray();
            int reaction = Array.FindIndex(entries, e => e is PowerHistoryBlockStart { BlockType: BlockType.TRIGGER } b
                && b.Source == bolvar.Id);
            var survivor = (Minion)next.IdEntityDic[bolvar.Id];
            Core2026Proof.Check(hits.Length == 3 && ((PowerHistoryMetaData)hits[0].entry).Data == 0
                && hits[0].index < reaction && reaction < hits[1].index
                && !survivor.HasDivineShield && survivor.Damage == 1 && survivor.AttackDamage == 3,
                "shield-loss reaction resolves before the next hit in original/clone/snapshot");
        });
        ParkBoard(p); ParkBoard(p.Opponent); int before = p.Hero.Damage + p.Opponent.Hero.Damage;
        var solo = (Minion)Play(g, p, id);
        Core2026Proof.Check(solo.Damage == 0 && p.Hero.Damage + p.Opponent.Hero.Damage == before + 3, "with no other minions all three pings hit heroes");
    }, legacyPoolMember: true);

    private static void Hits(Game game, int cursor, IPlayable source, int[] expected)
    {
        var entries = game.PowerHistory.Full.Skip(cursor).ToArray();
        int start = Array.FindIndex(entries, e => e is PowerHistoryBlockStart { BlockType: BlockType.POWER } b && b.Source == source.Id);
        Core2026Proof.Check(start >= 0, "Bomber opens its own POWER block");
        int depth = 0, powers = 0; var targets = new List<int>(); var damage = new List<int>();
        for (int i = start; i < entries.Length; i++)
        {
            if (entries[i] is PowerHistoryBlockStart block)
            {
                if (block.BlockType == BlockType.POWER && block.Source == source.Id) powers++;
                depth++;
            }
            else if (entries[i] is PowerHistoryBlockEnd) { if (--depth == 0) break; }
            else if (depth == 1 && entries[i] is PowerHistoryMetaData meta)
            {
                if (meta.Type == MetaDataType.TARGET && meta.Data == source.Id) targets.Add(meta.Info.Single());
                if (meta.Type == MetaDataType.DAMAGE)
                {
                    Core2026Proof.Check(meta.Data == 1, "each damage packet is exactly one");
                    damage.Add(meta.Info.Single());
                }
            }
        }
        Core2026Proof.Check(powers == 1 && targets.SequenceEqual(expected) && damage.SequenceEqual(expected),
            "three seeded TARGET/DAMAGE pairs inline in one POWER block: " + string.Join(",", targets));
    }
}
