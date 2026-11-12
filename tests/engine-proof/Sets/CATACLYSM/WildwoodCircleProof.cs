using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class WildwoodCircleProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_134", (g, p, id) => {
        ShatterSpellProof.Family(g, p, id, "CATA_134t", "CATA_134t2", "CATA_134t3", "CATA_134e");
        var location = LocationProofs.Location(p);
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var sleeper = Summon(p, "TIME_046");
        var source = ShatterRules.GiveCombined(p, id, p.Hero); PlayEntity(g, p, source);
        var treants = p.BoardZone.Where(m => m.Card.Id == "CATA_134t3").ToArray();
        Core2026Proof.Check(treants.Length == 2 && treants.All(m => m.AttackDamage == 2 && m.Health == 2 && m.Card.Cost == 1
            && m.Card.AssetId == 126886 && m.HasDeathrattle && m.AppliedEnchantments.Single().Card.Id == "CATA_134e")
            && treants[0].Id < treants[1].Id && p.BoardZone.GetSlot(3) == treants[0] && p.BoardZone.GetSlot(4) == treants[1],
            "two exact Treants append in creation order, then gain Deathrattle");
        Core2026Proof.Check(friendly.HasDeathrattle && !enemy.HasDeathrattle && !sleeper.HasDeathrattle
            && location.AppliedEnchantments?.Any() != true, "Unpinned_DormantExclusions: grant reaches current active friendly minions only");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var c = next.Player1; var target = (Minion)next.IdEntityDic[friendly.Id];
            Kill(next, target);
            Core2026Proof.Check(c.BoardZone.GetSlot(1).Card.Id == "CATA_134t3" && !((Minion)c.BoardZone.GetSlot(1)).HasDeathrattle
                && c.BoardZone.GetSlot(0).Id == location.Id && c.BoardZone.GetSlot(2).Id == sleeper.Id,
                "clone/load Deathrattle replaces dying physical slot, no recursive grant");
            var silenced = (Minion)next.IdEntityDic[treants[0].Id]; silenced.Silence();
            int slots = c.BoardZone.SlotCount; Kill(next, silenced);
            Core2026Proof.Check(c.BoardZone.SlotCount == slots - 1, "Silence removes granted Deathrattle independently");
        }
        Kill(g, friendly);
        Core2026Proof.Check(p.BoardZone.GetSlot(1).Card.Id == "CATA_134t3" && !((Minion)p.BoardZone.GetSlot(1)).HasDeathrattle,
            "spell already left; granted Deathrattle still summons in dying slot");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, "CATA_134t");
        Core2026Proof.Check(p.BoardZone.SlotCount == 2 && p.BoardZone.All(m => !m.HasDeathrattle), "first half only summons");
        Play(g, p, "CATA_134t2"); Play(g, p, "CATA_134t2");
        var future = Summon(p, "CS2_182");
        Core2026Proof.Check(!future.HasDeathrattle && p.BoardZone.SlotCount == 3, "second half grants, never summons or buffs future entries");
        var stacked = p.BoardZone[0]; Kill(g, stacked);
        Core2026Proof.Check(p.BoardZone.SlotCount == 4 && p.BoardZone.GetSlot(0).Card.Id == "CATA_134t3"
            && p.BoardZone.GetSlot(1).Card.Id == "CATA_134t3", "separate grants stack and both real Deathrattles resolve");
        StandardSetsProof.ParkBoard(p);
        LocationProofs.Location(p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        PlayEntity(g, p, ShatterRules.GiveCombined(p, id, p.Hero));
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CATA_134t3") == 1
            && p.BoardZone.All(m => m.HasDeathrattle), "one physical slot summons one, then grants to all active minions");
        Play(g, p, "CATA_134t"); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board blocks both summons");
        StandardSetsProof.ParkBoard(p); Play(g, p, "CATA_134t2");
        Core2026Proof.Check(p.BoardZone.SlotCount == 0 && !enemy.HasDeathrattle, "empty friendly board does not grant across sides");
    });
}

// Shared per-family checks complement the primitive's exhaustive lifecycle proof.
internal static class ShatterSpellProof
{
    internal static void Family(Game g, Controller p, string parent, string first, string second, params string[] helpers)
    {
        foreach (string id in new[] { parent, first, second }.Concat(helpers))
        {
            var card = Cards.FromId(id);
            Core2026Proof.Check(card.Implemented && EffectModules.ByDbfId.GetValueOrDefault(card.AssetId) == id, "registered shatter module: " + id);
            if (ClientExport.TryCards() is not { } export) { ClientExport.Skip("Shatter family identity and tags against the client export"); continue; }
            var data = export[id];
            Core2026Proof.Check(card.AssetId == data.GetProperty("dbfId").GetInt32() && card.Text == data.GetProperty("text").GetString()
                && card.Cost == data.GetProperty("cost").GetInt32() && card.ATK == data.GetProperty("atk").GetInt32()
                && card.Health == data.GetProperty("health").GetInt32() && card.Type.ToString() == data.GetProperty("cardType").GetString(),
                "exact registered export closure: " + id);
            foreach (var tag in data.GetProperty("tags").EnumerateObject())
                Core2026Proof.Check(card[(GameTag)int.Parse(tag.Name)] == tag.Value.GetInt32(), "exact helper keyword/tag " + id + ":" + tag.Name);
        }
        var divider = Add(p, "CS2_182"); var retired = Add(p, parent);
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(new[] { first, "CS2_182", second })
            && p.HandZone[0].Cost == 4 && p.HandZone[2].Cost == 4 && retired.Zone != p.HandZone,
            ShatterRulesProof.Leans + ": family really splits into exact full-cost halves");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var c = next.Player1; c.SetasideZone.Add(c.HandZone.Remove(next.IdEntityDic[divider.Id]));
            var joined = c.HandZone.Single();
            Core2026Proof.Check(joined.Card.Id == parent && joined.Cost == 4 && joined[ShatterRules.AlreadyCombined] == 1
                && joined[(GameTag)4239] == 0, ShatterRulesProof.Leans + ": clone/load adjacent join uses parent");
        }
        p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.Single()));
    }
    internal static void Resume(Game g, Action<Game> action)
    {
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var next in new[] { g, clone, loaded }) action(next);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded), "effect clone/load future parity");
    }
}
