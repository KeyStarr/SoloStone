using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
using EngineZone = SabberStoneCore.Enums.Zone;

internal static partial class Program
{
    private static void OverdrawHistory()
    {
        HiddenCardShapeHistory();
        var failures = new List<string>();
        int cases = 0;
        foreach (bool bot in new[] { false, true })
        foreach (bool retained in new[] { false, true })
        foreach (string id in new[] { "CS2_182", "CS2_029", "CS2_106", "JAIL_398" })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_182"));
            if (retained) SabberStoneCore.Model.Entities.Enchantment.GetInstance(owner, owner.Hero, owner, Cards.FromId("JAIL_509e"));
            var card = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.DeckZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count, deck = owner.DeckZone.Count;
            match.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, owner.Id, "", -1, 0));
            Generic.Draw(owner, card);
            match.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd());
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            string name = $"overdraw-{bot}-{retained}-{id}";
            DumpDrawHistory(name, capture, cursor);
            var show = data.FirstOrDefault(r => r.HasShowEntity && r.ShowEntity.Name == id
                && (!retained || r.ShowEntity.Entity != card.Id));
            if (show == null || SnapshotTag(show, GameTag.ZONE) != (int)EngineZone.GRAVEYARD)
                failures.Add(name + ": retail burn SHOW must name destination GRAVEYARD");
            if (show != null)
            {
                int at = data.IndexOf(show), display = show.ShowEntity.Entity;
                if (SnapshotTag(show, GameTag.REVEALED) != 0)
                    failures.Add(name + ": burn must not start a deck-reveal animation");
                if (data.Skip(at + 1).Any(r => IsZone(r, display, EngineZone.GRAVEYARD)))
                    failures.Add(name + ": duplicate burn transition after destination SHOW");
                if (retained && data.Any(r => r.HasHideEntity && r.HideEntity.Entity == display))
                    failures.Add(name + ": disposable burned actor must not move again");
                Check(data.Take(at).Count(r => r.HasPowerStart) > data.Take(at).Count(r => r.HasPowerEnd), name + " burn inside draw trigger");
                if (retained) Check(!data.Any(r => r.HasShowEntity && r.ShowEntity.Entity == card.Id
                    && !string.IsNullOrEmpty(r.ShowEntity.Name)), name + " retained identity stays private");
            }
            Check(owner.HandZone.Count == 10 && owner.DeckZone.Count == deck - 1
                && (retained ? card.Zone == owner.SetasideZone : card.Zone == owner.GraveyardZone), name + " authoritative destination/counts");
            capture.SerializeAll(); cases++;
        }
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            var source = EngineEntity.FromCard(owner, Cards.FromId("JAIL_379"), zone: owner.BoardZone);
            var card = EngineEntity.FromCard(owner, Cards.FromId("CORE_BT_072"), zone: owner.DeckZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            Generic.RevealCardBlock(source, card);
            FlushFieldHistory(match);
            var reveal = History(capture, cursor);
            int hide = reveal.FindIndex(r => r.HasHideEntity && r.HideEntity.Entity == card.Id);
            if (hide < 0 || !reveal.Skip(hide + 1).Any(r => r.HasTagChange && r.TagChange.Entity == card.Id
                && r.TagChange.Tag == (int)GameTag.REVEALED && r.TagChange.Value == 0))
                failures.Add("reveal-" + bot + ": HIDE must clear native REVEALED before later draw");
            Check(card[GameTag.REVEALED] == 0, "engine reveal flag already resets correctly");
            cursor = capture.Packets.Count;
            Generic.Draw(owner, card); FlushFieldHistory(match);
            var draw = History(capture, cursor).Single(r => r.HasShowEntity && r.ShowEntity.Entity == card.Id);
            Check(bot ? string.IsNullOrEmpty(draw.ShowEntity.Name) : draw.ShowEntity.Name == card.Card.Id,
                "post-reveal draw identity follows viewing side");
            cases++;
        }
        Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine($"PASS overdraw/reveal history: {cases} cases; both players, ordinary/retained burns, minion/spell/weapon/offboard deathrattle, later private draw");
    }
    private static void HiddenCardShapeHistory()
    {
        foreach (bool bot in new[] { false, true })
        foreach (string shape in new[] { "discard", "mill", "hand-full", "reveal-shuffle-draw", "joust", "deck-full" })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            var card = EngineEntity.FromCard(owner, Cards.FromId("CS2_182"),
                zone: shape == "discard" ? (SabberStoneCore.Model.Zones.IZone)owner.HandZone : owner.DeckZone);
            if (shape == "hand-full") while (!owner.HandZone.IsFull) Generic.DrawCard(owner, Cards.FromId("CS2_231"));
            if (shape == "deck-full") while (!owner.DeckZone.IsFull) EngineEntity.FromCard(owner, Cards.FromId("CS2_231"), zone: owner.DeckZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            match.Game.PowerHistory.Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, owner.Id, "", -1, 0));
            switch (shape)
            {
                case "discard": Generic.DiscardBlock(owner, card); break;
                case "mill": owner.GraveyardZone.Add(owner.DeckZone.Remove(card)); break;
                case "hand-full": Generic.AddHandPhase(owner, owner.DeckZone.Remove(card)); break;
                case "reveal-shuffle-draw":
                    Generic.RevealCardBlock(owner.Hero, card);
                    Generic.ShuffleIntoDeck(owner, owner.Hero, owner.DeckZone.Remove(card));
                    Generic.Draw(owner, card); break;
                case "joust": Generic.JoustBlock(owner, CardType.MINION); break;
                case "deck-full":
                    var offered = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"), zone: owner.SetasideZone);
                    Check(!Generic.ShuffleIntoDeck(owner, owner.Hero, offered), "full deck refuses shuffle without fictitious burn"); break;
            }
            match.Game.PowerHistory.Add(PowerHistoryBuilder.BlockEnd()); FlushFieldHistory(match);
            var data = History(capture, cursor);
            DumpDrawHistory("shape-" + bot + "-" + shape, capture, cursor);
            if (shape is "discard" or "mill" or "hand-full")
            {
                var show = data.Single(r => r.HasShowEntity && r.ShowEntity.Entity == card.Id);
                Check(show.ShowEntity.Name == card.Card.Id && SnapshotTag(show, GameTag.ZONE) == (int)EngineZone.GRAVEYARD,
                    shape + " public destination carries identity for both viewers");
            }
            if (shape is "joust" or "reveal-shuffle-draw")
                foreach (var hide in data.Where(r => r.HasHideEntity).Take(shape == "joust" ? 2 : 1))
                    Check(data.Skip(data.IndexOf(hide) + 1).Any(r => r.HasTagChange && r.TagChange.Entity == hide.HideEntity.Entity
                        && r.TagChange.Tag == (int)GameTag.REVEALED && r.TagChange.Value == 0), shape + " conceal clears reveal flag");
            if (shape == "reveal-shuffle-draw")
            {
                var draw = data.Last(r => r.HasShowEntity && r.ShowEntity.Entity == card.Id);
                Check(SnapshotTag(draw, GameTag.REVEALED) == 0 && (bot ? string.IsNullOrEmpty(draw.ShowEntity.Name) : draw.ShowEntity.Name == card.Card.Id),
                    "post-reveal shuffled draw restores viewing-side privacy");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS hidden card shapes: 12 cases; discard, mill, hand-full, reveal/shuffle/draw, joust, deck-full, both viewers");
    }

}
