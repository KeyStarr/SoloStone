using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;

internal static partial class Program
{
    // #155: Escape Artist attacks, survives, draws and leaves the game. The client has no zone for it and hides a card sent
    // there at once, so the wire carries retail's removed-from-game rows: ZONE_POSITION=0, HIDE_ENTITY, ZONE=REMOVEDFROMGAME
    // (not a bare SETASIDE). The META_DATA TARGET the transport adds at a neighbour whose position shifted is its R1 anchor
    // for the hidden drawn card (NativeAdmissionTransportControls pins it); it carries no effect.
    private static void EscapeArtistLeavesHistory()
    {
        foreach (bool bot in new[] { false, true })
        {
            string seat = bot ? "opponent" : "owner";
            {
                string name = "escape-artist-" + seat;
                var (match, capture, owner) = LeaveBoardStart(bot);
                var artist = LeaveBoardSummon(match, owner, "JAIL_030");
                var other = LeaveBoardSummon(match, owner, "CS2_182");
                var enemy = LeaveBoardSummon(match, owner.Opponent, "CS2_171"); enemy.BaseHealth = 40;
                artist.IsExhausted = false; FlushFieldHistory(match);
                int cursor = capture.Packets.Count;
                InvokeProcess(match, MinionAttackTask.Any(owner, artist, enemy));
                FlushFieldHistory(match);
                Check(artist.Zone == owner.SetasideZone, name + ": the Escape Artist escaped, now " + artist.Zone?.Type);
                DumpDrawHistory(name, capture, cursor);
                var history = History(capture, cursor).ToList();
                var effects = history.Where(e => e.HasSubSpellStart
                    && e.SubSpellStart.SpellPrefabGuid == "ReuseFX_KeywordImpact_Stealth_Super_ImpactOnly:f391dcf35565e4e4e80d15ebe8a2de8f").ToArray();
                Check(effects.Length == 1, name + ": one escape effect");
                var effect = effects[0].SubSpellStart;
                Check(effect.SourceEntityId == artist.Id && effect.TargetEntityIds.SequenceEqual(new[] { artist.Id }),
                    name + ": escape effect on the artist only");
                int effectAt = history.IndexOf(effects[0]);
                int hideAt = history.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == artist.Id);
                Check(effectAt + 1 < hideAt && history[effectAt + 1].HasSubSpellEnd,
                    name + ": escape effect finishes its scope before the artist is hidden");
                var rows = history.Select(e => LeaveRow(e, artist.Id)).Where(r => r != null).ToList();
                string zone5 = "ZONE=" + (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME, hide5 = "HIDE=" + (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME;
                string shown = string.Join(",", rows);
                Check(rows.Contains(zone5) && rows.Contains(hide5), name + ": REMOVEDFROMGAME and HIDE: " + shown);
                Check(rows.IndexOf("POS=0") >= 0 && rows.IndexOf("POS=0") < rows.IndexOf(hide5) && rows.IndexOf(hide5) < rows.IndexOf(zone5)
                    && !rows.Contains("ZONE=" + (int)SabberStoneCore.Enums.Zone.SETASIDE), name + ": POS=0, HIDE, ZONE, no SETASIDE: " + shown);
            }
        }
        Console.WriteLine("PASS Escape Artist leaves the game with REMOVEDFROMGAME and a HIDE, both seats (#155)");
    }
}
