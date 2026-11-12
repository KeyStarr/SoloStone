using System;
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
    // Locations have no Choose One sub-effects, so their activation POWER must use SubOption = -1 (the primary play effect).
    // It was written as 0, which the client reads as a sub-effect: the play spell started by the human's targeting never got
    // POWER and stayed lit. Same shape for both seats, copied or not.
    private static void LocationActivationHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool copied in new[] { true, false })
        foreach (bool final in new[] { false, true })
        foreach (string id in new[] { "CORE_REV_990", "JAIL_877", "BE_251", "TIME_044" })
        {
            var (match, capture) = AnimationMatch(botTurn: bot, deck: "showcase-rogue-imbue");
            var game = match.Game; var owner = game.CurrentPlayer;
            var victim = (Minion)EngineEntity.FromCard(owner, Cards.FromId("JAIL_384"), zone: owner.BoardZone);
            victim.BaseHealth = 30;
            BoardObject source;
            if (copied)
            {
                var original = (BoardObject)EngineEntity.FromCard(owner.Opponent, Cards.FromId(id), zone: owner.Opponent.BoardZone);
                original[GameTag.DAMAGE] = 1;
                original.IsExhausted = true; original[GameTag.EXHAUSTED] = 1;
                original[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
                FlushFieldHistory(match);
                source = (BoardObject)Generic.Copy(owner, owner.Hero, original, SabberStoneCore.Enums.Zone.HAND);
                Check(source.Id != original.Id && source.Controller == owner, "stolen location copy must be a new owned entity");
            }
            else source = (BoardObject)Generic.DrawCard(owner, Cards.FromId(id));
            FlushFieldHistory(match);
            void Act(PlayerTask task)
            {
                if (bot) { InvokeProcess(match, task); FlushFieldHistory(match); }
                else
                {
                    typeof(NativeMatch).GetMethod("SendHumanOptions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, null);
                    var options = capture.Last<PegasusGame.AllOptions>(14);
                    int index = options.Options.FindIndex(o => o.MainOption?.Id == source.Id);
                    Check(index >= 0, "location native option missing");
                    match.SendOption(options.Id, index, task.ActionTarget?.Id ?? 0, -1,
                        task is PlayCardTask play ? play.ZonePosition + 1 : 0);
                }
            }
            Act(owner.Options().OfType<PlayCardTask>().First(t => t.Source == source));
            Check(source.Zone == owner.BoardZone && source.Durability == source.Card.Health && !source.IsExhausted
                && source[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "playing copied/normal location must reset charges and readiness");
            if (final) source[GameTag.DAMAGE] = source[GameTag.HEALTH] - 1;
            FlushFieldHistory(match);
            int cursor = capture.WirePackets.Count, damage = victim.Damage, attack = victim.AttackDamage;
            var activation = owner.Options().OfType<ActivateLocationTask>().First(t => t.Source == source
                && (t.ActionTarget == null || t.ActionTarget == victim));
            Act(activation);
            var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToList();
            string label = $"{id} bot={bot} copied={copied} final={final}";
            var playStart = rows.Single(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                && (int)e.PowerStart.Type == (int)BlockType.PLAY);
            var powerStart = rows.Single(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                && (int)e.PowerStart.Type == (int)BlockType.POWER);
            var power = powerStart.PowerStart;
            Check(power.SubOption == -1 && power.HasEffectIndex && power.EffectIndex == 0
                && string.IsNullOrEmpty(power.EffectCardId) && !power.HasTriggerKeyword,
                label + ": location activation must select primary play effect (SubOption=-1, EffectIndex=0), got " + power.SubOption);
            int target = activation.ActionTarget?.Id ?? 0;
            Check(rows.IndexOf(playStart) < rows.IndexOf(powerStart) && power.Target == target && playStart.PowerStart.Target == target,
                label + ": targeted POWER must remain inside targeted PLAY");
            if (target != 0) Check(rows.Any(e => e.HasMetaData && (int)e.MetaData.Type == (int)MetaDataType.TARGET
                && e.MetaData.Info.SequenceEqual(new[] { target })), label + ": activation target metadata missing");
            int balance = 0;
            foreach (var row in rows)
            {
                if (row.HasPowerStart) balance++;
                if (row.HasPowerEnd) Check(--balance >= 0, label + ": block underflow");
            }
            Check(balance == 0, label + ": unterminated history");
            if (final) Check(source.Zone == owner.GraveyardZone, label + ": final charge must retire location");
            else Check(source.Durability == source.Card.Health - 1 && source.IsExhausted && source[GameTag.LOCATION_ACTION_COOLDOWN] == 1,
                label + ": spent location must close with one charge consumed");
            if (id == "CORE_REV_990") Check(victim.Damage == damage + 1 && victim.AttackDamage == attack + 2,
                label + ": friendly target must take damage and attack buff once");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS location activation primary-effect lifecycle: both seats, copied/normal hand plays, friendly targets, final/non-final charges, summon/draw/stage effects");
    }
}
