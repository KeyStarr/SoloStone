using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14K1EmeraldDreamProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("EDR_860", Unpinned_ResplendentDreamweaver);
        StandardSetsProof.Case("FIR_921", PetalPicker);
    }

    // data/rules-evidence/keywords/lifesteal-armor-damage.yaml: STILL-UNPINNED, owner-approved assumption (tier C lean).
    private static void Unpinned_ResplendentDreamweaver(Game g, Controller p, string id)
    {
        p.Hero.Damage = 20;
        var enemy = Summon(p.Opponent, "CS2_200"); enemy.BaseHealth = 30;
        var friendly = Summon(p, "CS2_182");
        foreach (int count in new[] { 0, 1, 2, 3 })
        {
            p[GameTag.IMBUE] = count; p.Opponent[GameTag.IMBUE] = 9;
            var hand = Add(p, id); p.UsedMana = 0;
            var options = p.Options().Where(t => t.Source?.Id == hand.Id).ToArray();
            Core2026Proof.Check(PoweredUp.IsMet(hand) == (count >= 2), "glow uses own lifetime count, threshold two");
            Core2026Proof.Check(options.Length > 0 && (count >= 2
                ? options.All(t => t.Target is Minion) && options.Any(t => t.Target?.Id == friendly.Id)
                    && options.Any(t => t.Target?.Id == enemy.Id)
                : options.All(t => t.Target == null)), "conditional minion-only target, either side; no hero");
            int damage = enemy.Damage, wounded = p.Hero.Damage;
            PoolPilotProof.Resume(g, next => {
                PlayEntity(next, next.Player1, next.IdEntityDic[hand.Id], count >= 2 ? (Minion)next.IdEntityDic[enemy.Id] : null);
                Core2026Proof.Check(((Minion)next.IdEntityDic[enemy.Id]).Damage == damage + (count >= 2 ? 4 : 0)
                    && next.Player1.Hero.Damage == wounded - (count >= 2 ? 4 : 0), "Battlecry deals four and Lifesteals only when condition is met");
            });
            Kill(g, (Minion)hand);
        }
        p[GameTag.IMBUE] = 2;
        var shield = Summon(p.Opponent, "CS2_231"); shield.HasDivineShield = true;
        int health = p.Hero.Damage; var body = (Minion)Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && p.Hero.Damage == health, "prevented damage heals zero");
        Kill(g, body);
        var fragile = Summon(p.Opponent, "CS2_231"); health = p.Hero.Damage;
        body = (Minion)Play(g, p, id, fragile);
        Core2026Proof.Check(fragile.Zone == p.Opponent.GraveyardZone && p.Hero.Damage == Math.Max(0, health - 4), "Unpinned_ResplendentDreamweaver: overkill heals full resolved four");
        body.Silence(); Kill(g, body);
        int prior = enemy.Damage; Summon(p, id);
        Core2026Proof.Check(enemy.Damage == prior, "summoning does not run Battlecry");
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        health = p.Hero.Damage;
        body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.Health == 4 && p.Hero.Damage == health, "no pre-existing minion permits untargeted play and no damage");
    }

    private static void PetalPicker(Game g, Controller p, string id)
    {
        foreach (int count in new[] { 0, 1, 2, 3 })
        {
            p[GameTag.IMBUE] = count; p.Opponent[GameTag.IMBUE] = 9;
            var source = Add(p, id); int deck = p.DeckZone.Count, hand = p.HandZone.Count;
            Core2026Proof.Check(PoweredUp.IsMet(source) == (count >= 2), "Petal glow threshold two, own side only");
            int cursor = g.PowerHistory.Full.Count;
            PoolPilotProof.Resume(g, next => {
                PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]);
                Core2026Proof.Check(next.Player1.DeckZone.Count == deck - (count >= 2 ? 2 : 0)
                    && next.Player1.HandZone.Count == hand - 1 + (count >= 2 ? 2 : 0), "draw two only at threshold, including clone/snapshot");
            });
            if (count >= 2)
            {
                var events = g.PowerHistory.Full.Skip(cursor).ToArray();
                int target = Array.FindIndex(events, e => e is PowerHistoryMetaData m
                    && m.Type == MetaDataType.TARGET && m.Info.SequenceEqual(new[] { source.Id }));
                int draw = Array.FindIndex(events, e => e is PowerHistoryShowEntity shown && shown.Entity.Id != source.Id);
                Core2026Proof.Check(target >= 0 && draw > target, "draw impact has typed source TARGET before draw SHOW");
            }
            Kill(g, (Minion)source);
        }
        p[GameTag.IMBUE] = 2;
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        var fullSource = Add(p, id);
        while (!p.HandZone.IsFull) Add(p, "CS2_231");
        int deckBefore = p.DeckZone.Count, graveBefore = p.GraveyardZone.Count;
        PlayEntity(g, p, fullSource);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deckBefore - 2
            && p.GraveyardZone.Count == graveBefore + 1, "one free hand slot draws first and burns second");
        StandardSetsProof.ParkBoard(p);
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        while (p.DeckZone.Count > 0) p.SetasideZone.Add(p.DeckZone.Remove(p.DeckZone[0]));
        p.Hero.Damage = 0; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.HandZone.Count == 0, "two empty-deck draws fatigue twice");
    }
}
