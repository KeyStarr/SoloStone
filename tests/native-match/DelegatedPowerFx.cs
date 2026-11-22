using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Delegated-power sweep (#109): a power that runs inside another entity's block keeps its own recipe. Every damage spell with
    // a recipe is played once on its own and once with a repeat mark (its power runs again from the mark's block); the repeat
    // must add a missile for each hit, so the marked play shows more of the spell's missiles than the plain one.
    private static void DelegatedPowerFx()
    {
        var recipes = (System.Collections.IDictionary)typeof(SabberStoneCore.Model.Game).Assembly
            .GetType("SabberStoneCore.Presentation.SubSpellRecipes").GetField("Recipes", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var spells = new List<(string Card, string Prefab)>();
        // A spell with an area recipe plays it for every run of its damage (one target, none, or prevented hits too): count it as well.
        var areaPrefab = new Dictionary<string, string>();
        foreach (System.Collections.DictionaryEntry entry in recipes)
        {
            var key = (System.Runtime.CompilerServices.ITuple)entry.Key;
            if ((BlockType)key[1] == BlockType.POWER && (int)key[2] == 0 && (int)key[3] == -1 && (int)key[4] == 0 && key[5].ToString() == "DamageGroup")
                areaPrefab[(string)key[0]] = (string)entry.Value.GetType().GetField("Prefab", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry.Value);
        }
        foreach (System.Collections.DictionaryEntry entry in recipes)
        {
            var key = (System.Runtime.CompilerServices.ITuple)entry.Key;
            if ((BlockType)key[1] != BlockType.POWER || (int)key[2] != 0 || (int)key[3] != -1 || (int)key[4] != 0
                || key[5].ToString() != "Damage") continue;
            if (Cards.FromId((string)key[0])?.Type != CardType.SPELL) continue;
            var recipe = entry.Value; const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            spells.Add(((string)key[0], (string)recipe.GetType().GetField("Prefab", any).GetValue(recipe)));
        }
        int compared = 0, repeated = 0;
        foreach (var (card, prefab) in spells.OrderBy(s => s.Card))
        {
            areaPrefab.TryGetValue(card, out string area);
            var plain = PlaySpell(card, prefab, false, area); var marked = PlaySpell(card, prefab, true, area);
            if (plain.Shots < 0 || marked.Shots < 0) continue; // not playable on this board
            compared++;
            if (marked.Damage > plain.Damage)
            {
                repeated++;
                Check(marked.Shots > plain.Shots, $"{card}: the repeat from the mark's block shows its missiles too (plain {plain.Shots}, marked {marked.Shots})");
            }
        }
        // The repeat must really happen: if a mark stopped repeating, every comparison above would be skipped and the sweep would pass.
        Check(repeated >= 50, "delegated-power sweep: only " + repeated + " spells repeated their damage under the mark");
        Check(Crashed.Count == 0, "delegated-power sweep: spells crashed: " + string.Join("; ", Crashed));
        Check(compared >= 50, "delegated-power sweep compared " + compared + " damage spells");
        Console.WriteLine("PASS delegated-power sweep: " + compared + " recipe spells, plain and repeated");
    }

    private static readonly List<string> Crashed = new List<string>();

    private static (int Shots, int Damage) PlaySpell(string card, string prefab, bool mark, string area = null)
    {
        try
        {
            var (match, capture) = AnimationMatch(); var game = match.Game; var owner = game.CurrentPlayer;
            var filler = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_200"), zone: owner.Opponent.BoardZone); filler.BaseHealth = 50;
            var spell = EngineEntity.FromCard(owner, Cards.FromId(card), zone: owner.HandZone); spell.Cost = 0;
            if (mark) RepeatRules.Mark(spell, owner.Hero, "JAIL_123e");
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            var task = owner.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id);
            if (task == null) return (-1, 0);
            InvokeProcess(match, task); FlushFieldHistory(match);
            var rows = History(capture, cursor);
            return (rows.Count(e => e.HasSubSpellStart && (e.SubSpellStart.SpellPrefabGuid == prefab || e.SubSpellStart.SpellPrefabGuid == area)),
                rows.Count(e => e.HasMetaData && (int)e.MetaData.Type == 1));
        }
        catch (Exception error) { Crashed.Add(card + (mark ? " (marked)" : "") + ": " + error.GetType().Name + " " + error.Message); return (-2, 0); }
    }
}
