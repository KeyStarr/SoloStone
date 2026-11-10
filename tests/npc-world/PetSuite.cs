using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// NPC pets (PROPOSED, docs/design/npc-pets.md): the pure resolver. Synthetic ids and a synthetic ten-family catalog, so
// the suite needs no shop, profile or world.
internal static class PetSuite
{
    // Same shape as the shop catalog; family 8 lists its forms out of card-id order like PET_8_1, _3, _2, _4.
    private static readonly IReadOnlyList<NpcPetFamily> Catalog = new[] { 3, 4, 6, 7, 8, 9, 10, 12, 15, 16 }
        .Select(id => new NpcPetFamily("PET_" + id + "_1", id, id == 8
            ? new[] { "PET_8_1", "PET_8_3", "PET_8_2", "PET_8_4" }
            : Enumerable.Range(1, 4).Select(n => "PET_" + id + "_" + n).ToArray())).ToArray();

    // Star index in the middle of each band (Bronze..Diamond) and Legend.
    private static readonly int[] BandIndex = { 15, 45, 75, 105, 135, LadderRules.LegendIndex };
    private static readonly double[] Share = { 0.05, 0.12, 0.20, 0.30, 0.40, 0.50 };

    private static NpcPet Pet(string id, string deck, int home, int shown) =>
        NpcPets.Resolve(new NpcPetSeat(id, deck, home, shown), Catalog);

    // The first `count` synthetic ids that own a pet (Legend home): the form/family tests need owners.
    private static List<string> Owners(string prefix, int count)
    {
        var owners = new List<string>();
        for (int i = 0; owners.Count < count; i++)
            if (Pet(prefix + i, "d", LadderRules.LegendIndex, LadderRules.LegendIndex) != null) owners.Add(prefix + i);
        return owners;
    }

    internal static void Run()
    {
        Deterministic();
        NoIdentityNoPet();
        Presence();
        Families();
        Forms();
        Fingerprint();
        Console.WriteLine("PASS npc pets: deterministic, home-band presence, per-deck family, monotone form, pinned fingerprint");
    }

    private static void Deterministic()
    {
        for (int i = 0; i < 200; i++)
        {
            string id = "npc.g.det" + i;
            var a = Pet(id, "deck-a", 75, 80); var b = Pet(id, "deck-a", 75, 80);
            Program.Require((a == null) == (b == null) && (a == null || (a.CardId == b.CardId && a.Form == b.Form)), "same inputs, same pet " + id);
        }
        // Catalog order never matters: the pick works on the family list sorted by native id.
        var reversed = Catalog.Reverse().ToArray();
        var orderOwners = Owners("npc.n.ord", 300);
        for (int i = 0; i < 300; i++)
        {
            var seat = new NpcPetSeat(orderOwners[i], "d", LadderRules.LegendIndex, 105);
            var a = NpcPets.Resolve(seat, Catalog); var b = NpcPets.Resolve(seat, reversed);
            Program.Require(a.CardId == b.CardId, "catalog order independent " + i);
        }
        Program.Require(NpcPets.Resolve(new NpcPetSeat("npc.n.x", "d", 0, 0), new NpcPetFamily[0]) == null, "empty catalog, no pet");
    }

    private static void NoIdentityNoPet()
    {
        foreach (string id in new string[] { null, "" })
            Program.Require(Pet(id, "d", 150, 150) == null, "legacy opponent without NpcId shows no pet");
        Program.Require(NpcPets.Resolve(null, Catalog) == null && NpcPets.Resolve(new NpcPetSeat("a", "d", 0, 0), null) == null, "null seat or catalog");
    }

    private static void Presence()
    {
        // Share per home band, within sampling error. Notables follow the same table (owner ruling 2026-10-28).
        for (int band = 0; band < 6; band++)
        {
            int have = 0; const int n = 20000;
            for (int i = 0; i < n; i++) if (Pet("npc.g.share" + i, "d", BandIndex[band], BandIndex[band]) != null) have++;
            Program.Require(Math.Abs((double)have / n - Share[band]) < 0.015, "band " + band + " share " + (double)have / n);
            Program.Require(NpcPets.PresenceShare((LadderBand)band) == Share[band], "share table " + band);
        }
        // Ownership is identity: the displayed rating (drift, promotion, demotion) never adds or removes the pet.
        for (int i = 0; i < 1500; i++)
        {
            string id = "npc.g.drift" + i;
            bool has = Pet(id, "d", 75, 75) != null;
            for (int shown = 0; shown <= LadderRules.LegendIndex; shown += 10)
                Program.Require((Pet(id, "d", 75, shown) != null) == has, "presence stable across displayed rank " + id + " " + shown);
        }
        // A higher HOME band only ever adds owners (same roll, rising share).
        for (int i = 0; i < 3000; i++)
        {
            string id = "npc.g.home" + i; bool had = false;
            for (int band = 0; band < 6; band++)
            {
                bool has = Pet(id, "d", BandIndex[band], BandIndex[band]) != null;
                Program.Require(!(had && !has), "home band never removes a pet " + id);
                had = has;
            }
        }
    }

    private static void Families()
    {
        int one = 0, two = 0, three = 0;
        foreach (string id in Owners("npc.n.fam", 4000))
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int deck = 0; deck < 60; deck++)
            {
                var pet = Pet(id, "deck-" + deck, 150, 150);
                Program.Require(pet.CardId == Pet(id, "deck-" + deck, 150, 150).CardId, "same NPC and deck, same pet");
                seen.Add(pet.Family);
            }
            Program.Require(seen.Count >= 1 && seen.Count <= 3, "owns one to three families: " + seen.Count);
            if (seen.Count == 1) one++; else if (seen.Count == 2) two++; else three++;
            // The family never depends on displayed rank.
            Program.Require(seen.Contains(Pet(id, "deck-0", 150, 0).Family), "family independent of rank");
        }
        Program.Require(one > 0 && two > 0 && three > 0 && one > two && two > three, "family counts skew to one: " + one + "/" + two + "/" + three);
        // Every catalog family is reachable.
        var all = new HashSet<string>(Owners("npc.n.reach", 2000).Select(id => Pet(id, "d", 150, 150).Family));
        Program.Require(all.Count == Catalog.Count, "every family reachable");
    }

    private static void Forms()
    {
        // Monotone: for every NPC a higher displayed rank never lowers the form.
        foreach (string id in Owners("npc.n.form", 2000))
        {
            int previous = 0;
            for (int shown = 0; shown <= LadderRules.LegendIndex; shown++)
            {
                var pet = Pet(id, "d", 150, shown);
                Program.Require(pet.Form >= previous && pet.Form >= 1 && pet.Form <= 4, "form monotone in rank " + id + " " + shown);
                previous = pet.Form;
            }
        }
        // Piecewise-linear table at the tier midpoints, clamped outside.
        double[][] table = { new[] { .10, .25, .45, .65, .85, 1.0 }, new[] { 0, .05, .15, .35, .60, .90 }, new[] { 0, 0, .03, .12, .30, .75 } };
        for (int k = 2; k <= 4; k++)
        {
            for (int tier = 0; tier < 6; tier++)
                Program.Require(Math.Abs(NpcPets.FormAtLeastChance(k, BandIndex[tier]) - table[k - 2][tier]) < 1e-12, "table " + k + "/" + tier);
            Program.Require(NpcPets.FormAtLeastChance(k, 0) == table[k - 2][0] && NpcPets.FormAtLeastChance(k, 999) == table[k - 2][5], "clamped " + k);
            for (int index = 1; index <= LadderRules.LegendIndex; index++)
                Program.Require(NpcPets.FormAtLeastChance(k, index) >= NpcPets.FormAtLeastChance(k, index - 1) - 1e-12, "chance rises " + k + "/" + index);
        }
        for (int index = 0; index <= LadderRules.LegendIndex; index++)
            Program.Require(NpcPets.FormAtLeastChance(2, index) >= NpcPets.FormAtLeastChance(3, index) && NpcPets.FormAtLeastChance(3, index) >= NpcPets.FormAtLeastChance(4, index), "thresholds nest " + index);
        // Distribution at the anchors matches P(form >= k) within sampling error.
        var distOwners = Owners("npc.n.dist", 20000);
        for (int tier = 0; tier < 6; tier++)
        {
            var count = new int[5]; int n = distOwners.Count;
            foreach (string id in distOwners) count[Pet(id, "d", 150, BandIndex[tier]).Form]++;
            for (int k = 2; k <= 4; k++)
            {
                double atLeast = (double)count.Skip(k).Sum() / n;
                Program.Require(Math.Abs(atLeast - table[k - 2][tier]) < 0.015, "form >= " + k + " at tier " + tier + ": " + atLeast);
            }
        }
        Program.Require(Owners("npc.n.legend", 500).All(id => Pet(id, "d", 150, LadderRules.LegendIndex).Form >= 2), "Legend never shows form 1");
        // Forms come from the catalog's growth order, never a card-id suffix: family 8 lists _1, _3, _2, _4.
        var eight = distOwners.Select(id => Pet(id, "d", 150, LadderRules.LegendIndex))
            .Where(pet => pet.Family == "PET_8_1").ToList();
        Program.Require(eight.Count > 0 && eight.All(pet => pet.NativeId == 8), "family 8 reached");
        string[] growth = { "PET_8_1", "PET_8_3", "PET_8_2", "PET_8_4" };
        Program.Require(eight.All(pet => pet.CardId == growth[pet.Form - 1]), "form indexes the growth-ordered forms");
        Program.Require(eight.Any(pet => pet.Form == 3 && pet.CardId == "PET_8_2"), "form 3 of family 8 is PET_8_2");
        // A short family caps the form at its last stage.
        var short2 = new[] { new NpcPetFamily("PET_1_1", 1, new[] { "PET_1_1", "PET_1_2" }) };
        Program.Require(Owners("npc.n.s", 500).All(id => NpcPets.Resolve(new NpcPetSeat(id, "d", 150, 150), short2).Form <= 2), "short family cap");
    }

    // Pins the whole mapping (presence, family, form) over synthetic ids and ranks; a change is a deliberate re-tune.
    private static void Fingerprint()
    {
        var text = new StringBuilder();
        foreach (int home in new[] { 15, 75, 135, 150 })
            for (int i = 0; i < 40; i++)
                foreach (int shown in new[] { 0, 60, 120, 150 })
                {
                    var pet = Pet("npc.g.pin" + i, "deck-" + (i % 7), home, shown);
                    text.Append(pet == null ? "-" : pet.CardId + "@" + pet.Form).Append(';');
                }
        string hash = BitConverter.ToString(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(text.ToString()))).Replace("-", "").Substring(0, 16);
        Program.Require(hash == PinnedFingerprint, "pet fingerprint " + hash + " (pinned " + PinnedFingerprint + ")");
    }

    private const string PinnedFingerprint = "C16F1226B73F571E";
}
