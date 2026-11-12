using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CosmicManifestationsProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_892", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).AssetId == 127159 && Cards.FromId(id)[GameTag.SPELL_SCHOOL] == 6, "Cosmic identity and Shadow school");
        var copied = PlayerDecisionProof.Fixture(); var caster = copied.Player1;
        var voraxx = Summon(caster, "UNG_843");
        voraxx.BaseHealth = 5;
        var originalSpell = Add(caster, id);
        int beforeCopy = caster.DeckZone.Count;
        var copyHits = new List<int>();
        void RecordHit(IEntity entity) => copyHits.Add(entity.Id);
        copied.TriggerManager.DamageTrigger += RecordHit;
        try { PlayEntity(copied, caster, originalSpell, voraxx); }
        finally { copied.TriggerManager.DamageTrigger -= RecordHit; }
        Core2026Proof.Check(caster.DeckZone.Count == beforeCopy + 3,
            $"R3 Voraxx reuses original spell entity: primary shuffles two, copied cast one; got {caster.DeckZone.Count - beforeCopy}");
        var plant = caster.GraveyardZone.Single(c => c.Card.Id == "UNG_999t2t1");
        Core2026Proof.Check(voraxx.Damage == 4 && voraxx.Zone == caster.BoardZone
            && copyHits.SequenceEqual(new[] { voraxx.Id, voraxx.Id, plant.Id }),
            "Voraxx primary hits twice; copy hits its summoned Plant once without Outcast");
        foreach (int position in new[] { 0, 1, 2, 3 })
        {
            OutcastProof.EmptyHand(p);
            IPlayable source = null;
            int size = position == 3 ? 1 : 3;
            for (int i = 0; i < size; i++) {
                var card = Add(p, i == (position == 3 ? 0 : position) ? id : "CS2_182");
                if (card.Card.Id == id) source = card;
            }
            var target = Summon(p.Opponent, "CS2_200");
            int repeats = position == 1 ? 1 : 2, deck = p.DeckZone.Count;
            var options = p.Options().Where(t => t.Source?.Id == source.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
            Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is ICharacter)
                && options.Any(t => t.Target == p.Hero) && options.Any(t => t.Target == p.Opponent.Hero)
                && options.Any(t => t.Target == target) && !options.Any(t => t.Target == null), "required any-character target");
            var before = p.DeckZone.Select(c => c.Id).ToHashSet();
            int historyCount = g.PowerHistory.Full.Count;
            var clone = g.CloneExact(history: true);
            foreach (var next in new[] { g, clone })
                PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], (ICharacter)next.IdEntityDic[target.Id]);
            Core2026Proof.Check(target.Damage == 2 * repeats && p.DeckZone.Count == deck + repeats, "edge/middle/single repeats damage and shuffle");
            var generated = p.DeckZone.Where(c => !before.Contains(c.Id)).ToArray();
            var filter = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DEMONHUNTER, type: CardType.SPELL);
            var candidates = OfflineCardPools.Candidates(g.OfflinePools, CardClass.DEMONHUNTER, filter, 127159);
            Core2026Proof.Check(candidates.Count >= 3 && generated.Length == repeats && generated.All(c =>
                candidates.Any(candidate => candidate.AssetId == c.Card.AssetId) && c.Card.Type == CardType.SPELL
                && c.Card.AssetId != 127159 && c[GameTag.DISPLAYED_CREATOR] == source.Id),
                $"admitted DH spells; exact filter/source exclusion and creator: count={candidates.Count}, generated={string.Join(",", generated.Select(c => c.Card.Id + ":creator=" + c[GameTag.DISPLAYED_CREATOR]))}, source={source.Id}");
            Core2026Proof.Check(p.DeckZone.Select(c => (c.Id, c.Card.Id)).SequenceEqual(clone.Player1.DeckZone.Select(c => (c.Id, c.Card.Id)))
                && Canonical.Value(g.PowerHistory.Full.Skip(historyCount)) == Canonical.Value(clone.PowerHistory.Full)
                && g.Random.CaptureState().Equals(clone.Random.CaptureState()), "seed 1-3 deterministic picks, shuffle positions, history and RNG");
            Kill(g, target);
        }
        OutcastProof.EmptyHand(p);
        Summon(p, "EX1_012");
        int damage = p.Opponent.Hero.Damage;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 6, "Spell Damage applies separately to each hit");
        OutcastProof.EmptyHand(p);
        var victim = Summon(p.Opponent, "EX1_096"); int hits = 0;
        void Hit(IEntity _) { hits++; Core2026Proof.Check(victim.Zone == p.Opponent.BoardZone, "no death phase between hits"); }
        victim.TakeDamageTrigger += Hit;
        int hand = p.Opponent.HandZone.Count;
        try { Play(g, p, id, victim); }
        finally { victim.TakeDamageTrigger -= Hit; }
        Core2026Proof.Check(hits == 2 && victim.Zone.Type == Zone.GRAVEYARD && p.Opponent.HandZone.Count == hand + 1,
            "same lethal target hit twice; Deathrattle settles once after spell");
        foreach (int position in new[] { 0, 1, 2 })
        {
            OutcastProof.EmptyHand(p);
            IPlayable source = null;
            for (int i = 0; i < 3; i++)
            {
                var card = Add(p, i == position ? id : "CS2_182");
                if (i == position) source = card;
            }
            var target = Summon(p.Opponent, "CS2_200");
            PoolPilotProof.Pool(g, id, 0, () => {
                var random = g.Random.CaptureState();
                var deck = p.DeckZone.Select(c => (c.Id, c.Card.Id)).ToArray();
                var entities = g.IdEntityDic.Select(pair => pair.Key).Order().ToArray();
                var damaged = new List<int>();
                void Hit(IEntity entity) => damaged.Add(entity.Id);
                target.TakeDamageTrigger += Hit;
                try { PlayEntity(g, p, source, target); }
                finally { target.TakeDamageTrigger -= Hit; }
                int repeats = position == 1 ? 1 : 2;
                Core2026Proof.Check(target.Damage == 3 * repeats && damaged.SequenceEqual(Enumerable.Repeat(target.Id, repeats)),
                    "empty pool: Spell Damage and every hit resolve on same target");
                Core2026Proof.Check(deck.SequenceEqual(p.DeckZone.Select(c => (c.Id, c.Card.Id)))
                    && entities.SequenceEqual(g.IdEntityDic.Select(pair => pair.Key).Order()) && g.Random.CaptureState().Equals(random),
                    "empty pool: no card created/shuffled and no pool or shuffle RNG consumed");
            });
            Kill(g, target);
        }
    });
}
