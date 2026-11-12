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
    private static void WispHeroPowerHistory()
    {
        // Blessing of the Wisp (EDR_851p) has no play effect in its card definition, so the client draws no damage
        // visual by itself; the server sends one sub-spell per hit instead. This prefab is a stand-in (Arcane Missile
        // recipe) because the retail asset is unknown, so the checks pin the sub-spell shape per hit, not the asset
        // itself.
        const string arcane = "ReuseFX_Arcane_Missile_PurpleLightning_Super_Simultaneous:a40e6c32f4253314db432d7808a092de";
        foreach (bool bot in new[] { false, true })
        foreach (string mode in new[] { "missiles", "wisp", "full-board", "mixed", "bonuses", "zero" })
        {
            var (match, capture) = AnimationMatch(bot);
            var game = match.Game; var owner = game.CurrentPlayer;
            bool wisp = mode != "missiles";
            int magnitude = mode == "zero" ? 0 : 3;
            IPlayable source;
            if (wisp)
            {
                ImbueRules.Imbue(owner, owner.Hero, 3);
                source = owner.Hero.HeroPower;
                source[GameTag.TAG_SCRIPT_DATA_NUM_1] = magnitude;
            }
            else source = EngineEntity.FromCard(owner, Cards.FromId("EX1_277"), zone: owner.HandZone);
            if (mode == "full-board")
                for (int i = 0; i < 7; i++) EngineEntity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.BoardZone);
            if (mode == "bonuses")
            {
                owner.Hero.HeroPowerDamage = 1;
                owner.ControllerAuraEffects[GameTag.HERO_POWER_DOUBLE] = 1;
                Generic.SummonBlock(game, (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_222")), -1, null);
            }
            if (mode == "mixed")
            {
                var shield = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("EX1_008"), zone: owner.Opponent.BoardZone);
                shield.HasDivineShield = true;
                EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_231"), zone: owner.Opponent.BoardZone);
            }
            FlushFieldHistory(match);
            PlayerTask Action(Controller player) => wisp ? HeroPowerTask.Any(player)
                : PlayCardTask.Spell(player, player.HandZone.Single(p => p.Id == source.Id));
            var on = game.CloneExact(history: true); var off = game.CloneExact(history: false);
            foreach (var clone in new[] { on, off }) Check(clone.Process(Action(clone.CurrentPlayer)), "split-damage clone action accepted");
            Check(on.Hash() == off.Hash(), "Wisp presentation leaves state unchanged: " + mode);
            var random = typeof(SabberStoneCore.Model.Game).GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic);
            var left = random.GetValue(on); var right = random.GetValue(off); var next = left.GetType().GetMethod("Next", System.Type.EmptyTypes);
            for (int i = 0; i < 5; i++) Check(Equals(next.Invoke(left, null), next.Invoke(right, null)), "Wisp presentation leaves RNG unchanged");
            int cursor = capture.Packets.Count;
            InvokeProcess(match, Action(owner)); FlushFieldHistory(match);
            var rows = History(capture, cursor);
            if (Environment.GetEnvironmentVariable("HS_WISP_FX_TRACE") == "1")
            {
                Console.WriteLine($"WIRE {mode} seat={owner.PlayerId} source={source.Id} hero={owner.Hero.Id}");
                foreach (var row in rows)
                {
                    if (row.HasPowerStart) Console.WriteLine($"BLOCK {row.PowerStart.Type} source={row.PowerStart.Source} index={row.PowerStart.EffectIndex} sub={row.PowerStart.SubOption} target={row.PowerStart.Target}");
                    if (row.HasPowerEnd) Console.WriteLine("BLOCK_END");
                    if (row.HasMetaData) Console.WriteLine($"META {row.MetaData.Type} data={row.MetaData.Data} info={string.Join(',', row.MetaData.Info)}");
                    if (row.HasSubSpellStart) Console.WriteLine($"SUB_SPELL {row.SubSpellStart.SpellPrefabGuid} source={row.SubSpellStart.SourceEntityId} targets={string.Join(',', row.SubSpellStart.TargetEntityIds)}");
                    if (row.HasSubSpellEnd) Console.WriteLine("SUB_SPELL_END");
                }
            }
            int hits = mode == "bonuses" ? 8 : magnitude;
            var powers = rows.Where(e => e.HasPowerStart && e.PowerStart.Source == source.Id && (int)e.PowerStart.Type == 3).ToArray();
            Check(powers.Length == 1 && powers[0].PowerStart.EffectIndex == 0 && powers[0].PowerStart.SubOption == -1
                && powers[0].PowerStart.Target == 0 && string.IsNullOrEmpty(powers[0].PowerStart.EffectCardId),
                "Wisp and Arcane Missiles share one primary POWER activation");
            var metadata = rows.Where(e => e.HasMetaData && (int)e.MetaData.Type is 0 or 1).Select(e => e.MetaData).ToArray();
            Check(metadata.Length == hits * 2, "each missile retains TARGET/DAMAGE pair: " + mode);
            for (int i = 0; i < hits; i++)
                Check((int)metadata[2 * i].Type == 0 && metadata[2 * i].Data == source.Id
                    && (int)metadata[2 * i + 1].Type == 1 && metadata[2 * i + 1].Data is 0 or 1
                    && metadata[2 * i].Info.SequenceEqual(metadata[2 * i + 1].Info), "ordered per-hit target and damage");
            if (mode != "mixed") Check(owner.Opponent.Hero.Damage == hits, "split damage total unchanged: " + mode);
            Check(!wisp || owner.BoardZone.Count(p => p.Card.Id == "EDR_851t") == (mode == "full-board" ? 0 : magnitude), "Wisp summons unchanged");
            Check(rows.Count(e => e.HasSubSpellStart) == (wisp ? hits : 0), "Wisp missing damage sub-spells: " + mode);
            var blocks = new Stack<int>(); int scope = 0, target = 0, damage = 0;
            foreach (var row in rows)
            {
                if (row.HasPowerStart) blocks.Push(row.PowerStart.Source);
                if (row.HasPowerEnd) blocks.Pop();
                if (row.HasSubSpellStart)
                {
                    Check(scope++ == 0 && blocks.Peek() == source.Id && row.SubSpellStart.SpellPrefabGuid == arcane
                        && row.SubSpellStart.SourceEntityId == owner.Hero.Id, "public hero missile inside Wisp POWER");
                    target = row.SubSpellStart.TargetEntityIds.Single(); damage = 0;
                }
                if (row.HasMetaData && (int)row.MetaData.Type == 1)
                {
                    Check(blocks.Peek() == source.Id && (!wisp || scope == 1 && row.MetaData.Info.Single() == target), "damage inside matching missile scope");
                    damage++;
                }
                if (row.HasSubSpellEnd) Check(--scope == 0 && damage == 1, "one completed sub-spell per hit");
            }
            Check(scope == 0 && blocks.Count == 0, "balanced viewer blocks");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Wisp hero-power FX: Arcane Missiles analogue, both seats, repeated/mixed targets, full board, bonuses, zero, state/RNG equality");
    }
}
