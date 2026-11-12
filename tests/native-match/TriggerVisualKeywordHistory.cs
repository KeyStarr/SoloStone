using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Retail: a printed TRIGGER_VISUAL card's own activation carries TriggerKeyword=32, which makes the pinned client play the
    // actor trigger bauble (TriggerSpellController.GetActorTriggerSpellType). EDR_810t has no CardDef trigger effect, so
    // keyword 0 showed nothing. docs/reports/2026-11-01-trigger-visual-keyword.md.
    private static void TriggerVisualKeywordHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot);
            var game = match.Game;
            var owner = game.CurrentPlayer;
            foreach (var player in new[] { game.Player1, game.Player2 })
                foreach (var card in player.HandZone.ToArray()) player.SetasideZone.Add(player.HandZone.Remove(card));
            var leech = (Minion)EngineEntity.FromCard(owner, Cards.FromId("EDR_810t"), zone: owner.BoardZone);
            var totem = (Minion)EngineEntity.FromCard(owner, Cards.FromId("NEW1_009"), zone: owner.BoardZone);
            var victim = (Minion)EngineEntity.FromCard(owner.Opponent, Cards.FromId("CS2_172"), zone: owner.Opponent.BoardZone);
            // A TRIGGER_VISUAL card in the opponent's hidden hand: its identity is never on this wire.
            var hidden = EngineEntity.FromCard(owner.Opponent, Cards.FromId("EDR_810t"), zone: owner.Opponent.HandZone);
            totem.Damage = 1; // something for the Healing Totem to restore, so its authored trigger is not empty
            FlushFieldHistory(match);
            int heroHealth = owner.Hero.BaseHealth, cursor = capture.Packets.Count;
            string label = "trigger visual owner=" + (bot ? 2 : 1);
            InvokeProcess(match, EndTurnTask.Any(owner));
            FlushFieldHistory(match);
            var rows = capture.HistorySnapshots(cursor).SelectMany(p => p.List).ToArray();

            Check(victim.BaseHealth == 1 && victim.Health == 1 && owner.Hero.BaseHealth == heroHealth + 1,
                label + " Leech still steals 1 max Health (gameplay untouched)");
            var leechBlocks = rows.Where(r => r.HasPowerStart && r.PowerStart.Source == leech.Id).ToArray();
            Check(leechBlocks.Length == 1 && leechBlocks[0].PowerStart.Type == HistoryBlock.Type.TRIGGER
                && leechBlocks[0].PowerStart.HasEffectIndex && leechBlocks[0].PowerStart.EffectIndex == 0
                && leechBlocks[0].PowerStart.HasTriggerKeyword && leechBlocks[0].PowerStart.TriggerKeyword == (int)GameTag.TRIGGER_VISUAL,
                label + " Leech end-of-turn TRIGGER carries TriggerKeyword=TRIGGER_VISUAL at EffectIndex 0");
            int at = Array.IndexOf(rows, leechBlocks[0]);
            var body = rows.Skip(at + 1).TakeWhile(r => !r.HasPowerEnd).ToArray();
            Check(body.Any(r => SnapshotName(r) == "EDR_810e") && body.Any(r => SnapshotName(r) == "EDR_810e2"),
                label + " steal enchantments stay inside the keyworded trigger");
            Check(rows.Any(r => r.HasPowerStart && r.PowerStart.Source == totem.Id && r.PowerStart.Type == HistoryBlock.Type.TRIGGER
                && r.PowerStart.TriggerKeyword == (int)GameTag.TRIGGER_VISUAL && r.PowerStart.EffectIndex == 0),
                label + " authored-slot trigger card (NEW1_009) gets the same retail keyword");
            var lifecycle = new HashSet<int> { game.Id, game.Player1.Id, game.Player2.Id };
            Check(rows.Where(r => r.HasPowerStart && lifecycle.Contains(r.PowerStart.Source)).All(r => !r.PowerStart.HasTriggerKeyword
                || r.PowerStart.TriggerKeyword != (int)GameTag.TRIGGER_VISUAL), label + " game/player lifecycle triggers stay unkeyworded");
            // Engine history is untouched: the keyword is a wire presentation field only.
            Check(game.PowerHistory.Full.OfType<SabberStoneCore.Kettle.PowerHistoryBlockStart>()
                .Where(b => b.Source == leech.Id).All(b => b.TriggerKeyword == null), label + " engine history keeps no keyword");

            // Fair view and empty boundaries, through the real send path.
            cursor = capture.Packets.Count;
            SendHistoryEntries(match, new List<IPowerHistoryEntry> {
                PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, hidden.Id, "", -1, 0),
                PowerHistoryBuilder.TagChange(owner.Opponent.Id, GameTag.NUM_TURNS_IN_PLAY, 1), PowerHistoryBuilder.BlockEnd(),
                PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, leech.Id, "", -1, 0), PowerHistoryBuilder.BlockEnd(),
                PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, leech.Id, "", -1, 0, effectIndex: -1),
                PowerHistoryBuilder.TagChange(leech.Id, GameTag.NUM_TURNS_IN_PLAY, 3), PowerHistoryBuilder.BlockEnd() });
            var synthetic = History(capture, cursor).Where(r => r.HasPowerStart).Select(r => r.PowerStart).ToArray();
            Check(synthetic.Where(s => s.Source == hidden.Id).All(s => !s.HasTriggerKeyword),
                label + " hidden-hand TRIGGER_VISUAL source never gains the keyword");
            Check(synthetic.Where(s => s.Source == leech.Id).All(s => !s.HasTriggerKeyword && s.EffectIndex == -1),
                label + " empty and EffectIndex -1 boundaries keep no bauble");

            Check(NativeAdmission.ValidatePackets(capture.HistorySnapshots()
                .Select(p => (IReadOnlyList<PowerHistoryData>)p.List)).Count == 0, label + " native admission");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS TRIGGER_VISUAL keyword: Leech/Healing Totem end-of-turn bauble, gameplay unchanged, fair view, empty boundaries");
    }
}
