using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;
using static PoolPilotProof;

internal static class FinalityProof
{
    internal static void Run() => StandardSetsProof.Case("END_003", (game, player, id) => {
        int seed = (int)game.Seed;
        var g = ImbueProof.Make(CardClass.DEATHKNIGHT, seed); var p = g.Player1;
        EmptyDeck(p);
        var other = InDeck(p, "CS2_172");
        var undead = InDeck(p, "EDR_851t");
        var multi = InDeck(p, "EDR_850pe");
        Core2026Proof.Check(undead.Card.IsRace(Race.UNDEAD) && multi.Card.IsRace(Race.UNDEAD)
            && multi.Card.IsRace(Race.BEAST), "single/multi-type Undead fixtures");
        var random = g.Random.Clone();
        int expected = new[] { undead.Id, multi.Id }[random.Next(2)];
        var old = p.Hero.HeroPower;
        var spell = Add(p, id);
        int cursor = g.PowerHistory.Full.Count;
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id]));
        Core2026Proof.Check(p.HandZone.Single().Id == expected && other.Zone == p.DeckZone
            && p.DeckZone.Count == 2 && g.Random.CaptureState().Equals(random.CaptureState()),
            "draws actual random Undead, excludes Beast-only, consumes one game RNG draw");
        var power = p.Hero.HeroPower;
        var entries = g.PowerHistory.Full.Skip(cursor).ToArray();
        var installed = entries.OfType<PowerHistoryFullEntity>().Single(e => e.Entity.Id == power.Id);
        Core2026Proof.Check(old.Zone == p.SetasideZone && power.Card.Id == "END_003p"
            && installed.Entity.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1
            && power[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && ImbueRules.Count(p) == 2,
            "first Imbue installs Infinite at one, second upgrades same power to two");
        Core2026Proof.Check(entries.OfType<PowerHistoryTagChange>().Where(t => t.EntityId == p.Id && t.Tag == GameTag.IMBUE)
            .Select(t => t.Value).SequenceEqual(new[] { 1, 2 }), "two sequential Imbues, never one collapsed increment");
        int drawnAt = Array.FindIndex(entries, e => e is PowerHistoryTagChange t
            && t.EntityId == expected && t.Tag == GameTag.ZONE && t.Value == (int)Zone.HAND);
        int imbuedAt = Array.FindIndex(entries, e => e is PowerHistoryTagChange t
            && t.EntityId == p.Id && t.Tag == GameTag.IMBUE);
        Core2026Proof.Check(drawnAt >= 0 && imbuedAt > drawnAt, "Undead draw precedes Imbues");
        EmptyHand(p);
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Single().Id == (expected == undead.Id ? multi.Id : undead.Id)
            && other.Zone == p.DeckZone && p.DeckZone.Count == 1 && ImbueRules.Count(p) == 4,
            "remaining Undead is drawn, including multi-type, repeated Finality upgrades twice");

        // Lean_ imbue-16: missing/burned draw never suppresses independent Imbue clauses.
        EmptyHand(p);
        var before = g.Random.CaptureState();
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsEmpty && other.Zone == p.DeckZone && ImbueRules.Count(p) == 6
            && before.Equals(g.Random.CaptureState()) && p.Hero.Fatigue == 0, "no Undead: no draw/RNG/fatigue, two Imbues");
        EmptyDeck(p); before = g.Random.CaptureState();
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsEmpty && ImbueRules.Count(p) == 8 && p.Hero.Fatigue == 0
            && before.Equals(g.Random.CaptureState()), "empty deck: no fatigue/RNG, two Imbues");

        var burned = InDeck(p, "EDR_850pe");
        var automatic = Add(p, id); p.SetasideZone.Add(p.HandZone.Remove(automatic));
        FillHand(p);
        var held = p.HandZone.Select(c => c.Id).ToArray();
        random = g.Random.Clone(); random.Next(1);
        Resume(g, next => {
            new SabberStoneCore.EffectSystem.EffectContext(next, next.Player1.Id, next.Player1.Hero.Id)
                .CastSpell(automatic.Id, HistoryCastKind.Effect);
            next.ProcessTasks(); next.DeathProcessingAndAuraUpdate();
        });
        Core2026Proof.Check(burned.Zone == p.GraveyardZone && p.DeckZone.IsEmpty
            && p.HandZone.Select(c => c.Id).SequenceEqual(held) && ImbueRules.Count(p) == 10
            && p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 10
            && g.Random.CaptureState().Equals(random.CaptureState()),
            "full-hand effect cast burns actual Undead, preserves held entities, Imbues twice");

        g = ImbueProof.Make(CardClass.PRIEST, seed); p = g.Player1;
        EmptyDeck(p); undead = InDeck(p, "EDR_851t");
        Play(g, p, id);
        Core2026Proof.Check(undead.Zone == p.HandZone && ImbueRules.Count(p) == 2
            && p.Hero.HeroPower.Card.Id == "EDR_449p" && p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
            "cross-class Priest Finality draws then installs/upgrades Moon");

        g = ImbueProof.Make(CardClass.PALADIN, seed); p = g.Player1;
        EmptyDeck(p); undead = InDeck(p, "EDR_851t"); spell = Add(p, id);
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id]));
        Core2026Proof.Check(undead.Zone == p.HandZone && ImbueRules.Count(p) == 2 && p.Hero.HeroPower.Card.Id == "EDR_445p",
            "Paladin draws then installs/upgrades Dragon twice");
    });
}
