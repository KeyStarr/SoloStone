using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineSpell = SabberStoneCore.Model.Entities.Spell;

internal static partial class Program
{
    // Live 2026-11-01: a human Mirror Entity on a full board played its reveal on each of three NPC minion plays while
    // the engine (correctly) kept it. Retail (wiki Secret/Mirror Entity/Noble Sacrifice notes): the Secret stays in
    // place, untriggered and unrevealed. Both seats: the bot's Secret must not be publicly revealed either.
    [GroupProof("secret-full-board", ProofKind.Native)]
    private static void SecretFullBoardHistory()
    {
        foreach (bool botSecret in new[] { false, true })
        {
            string seat = botSecret ? "bot" : "human";
            var capture = new Capture(); var match = StartFixture(capture, "EX1_294", null); var g = match.Game;
            Controller owner = botSecret ? g.Player2 : g.Player1, opponent = owner.Opponent;
            void Flush() { FlushHistory(match); g.PowerHistory.Last.Clear(); }
            Flush();
            if (g.CurrentPlayer != owner) { Check(g.Process(EndTurnTask.Any(g.CurrentPlayer)), "secret owner turn: " + seat); Flush(); }
            var secret = (EngineSpell)EngineEntity.FromCard(owner, Cards.FromId("EX1_294"));
            Generic.CastSpell(owner, g, secret, null, 0);
            while (!owner.BoardZone.IsFull)
                Generic.SummonBlock(g, (Minion)EngineEntity.FromCard(owner, Cards.FromId("CS2_120")), -1, null);
            g.DeathProcessingAndAuraUpdate(); Flush();
            Check(g.Process(EndTurnTask.Any(owner)), "opponent turn: " + seat); Flush();
            Check(g.CurrentPlayer == opponent && secret.Zone == owner.SecretZone && owner.BoardZone.IsFull,
                "full-board Mirror Entity fixture: " + seat);

            Minion Play(out List<PowerHistoryData> rows, out List<SabberStoneCore.Kettle.IPowerHistoryEntry> engine)
            {
                opponent.BaseMana = 10; opponent.UsedMana = 0;
                var minion = (Minion)EngineEntity.FromCard(opponent, Cards.FromId("CS2_120"), zone: opponent.HandZone);
                Flush();
                int cursor = capture.Packets.Count, full = g.PowerHistory.Full.Count;
                InvokeProcess(match, PlayCardTask.Minion(opponent, minion));
                FlushHistory(match);
                rows = History(capture, cursor);
                engine = g.PowerHistory.Full.Skip(full).ToList();
                Check(minion.Zone == opponent.BoardZone, "opponent minion played: " + seat);
                return minion;
            }

            for (int play = 1; play <= 3; play++)
            {
                Play(out var rows, out var engine);
                string label = seat + " play " + play;
                Check(secret.Zone == owner.SecretZone && owner.SecretZone.Count == 1 && secret[GameTag.REVEALED] == 0
                    && owner.BoardZone.Count == 7, "full-board Mirror Entity stays installed: " + label);
                Check(!engine.Any(e => e is SabberStoneCore.Kettle.PowerHistoryBlockStart b && b.Source == secret.Id),
                    "engine history has no block from unfired Secret: " + label);
                Check(!rows.Any(e => e.HasPowerStart && e.PowerStart.Source == secret.Id),
                    "wire has no TRIGGER from unfired Secret: " + label);
                Check(!rows.Any(e => e.HasTagChange && e.TagChange.Entity == secret.Id
                    && (e.TagChange.Tag == (int)GameTag.ZONE || e.TagChange.Tag == (int)GameTag.REVEALED)),
                    "wire keeps unfired Secret in its zone: " + label);
                if (botSecret) Check(!EmittedIds(rows).Contains("EX1_294") && !rows.Any(e => SnapshotEntity(e) == secret.Id),
                    "unfired bot Secret stays hidden: " + label);
            }

            // Control: with a free slot the same Secret triggers, reveals and resolves normally.
            owner.BoardZone[0].Destroy(); g.DeathProcessingAndAuraUpdate(); Flush();
            Check(owner.BoardZone.Count == 6, "free one slot: " + seat);
            var before = owner.BoardZone.Select(m => m.Id).ToHashSet();
            var played = Play(out var fired, out var firedEngine);
            Check(secret.Zone == owner.GraveyardZone && owner.SecretZone.Count == 0 && owner.BoardZone.Count == 7
                && owner.BoardZone.Any(m => !before.Contains(m.Id) && m.Card.Id == played.Card.Id),
                "Mirror Entity with space copies and is consumed: " + seat);
            Check(firedEngine.Any(e => e is SabberStoneCore.Kettle.PowerHistoryBlockStart b && b.Source == secret.Id
                && b.BlockType == BlockType.TRIGGER), "engine history keeps fired Secret TRIGGER: " + seat);
            Check(fired.Any(e => e.HasPowerStart && e.PowerStart.Source == secret.Id
                && e.PowerStart.Type == HistoryBlock.Type.TRIGGER), "wire keeps fired Secret TRIGGER: " + seat);
            if (botSecret) Check(EmittedIds(fired).Contains("EX1_294"), "fired bot Secret is revealed");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS full-board Mirror Entity: no TRIGGER/reveal over three plays on either seat; triggers with space");
    }
}
