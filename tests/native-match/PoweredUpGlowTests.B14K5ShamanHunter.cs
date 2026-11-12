using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-k5-shaman-hunter", ProofKind.PoweredUp)]
    private static void RunB14K5ShamanHunter()
    {
        Scenario("TLC_223", (f, card, met) => {
            if (met)
            {
                f.Process(PlayCardTask.Any(f.Player, f.Draw("GIL_681")));
                f.Process(EndTurnTask.Any(f.Player)); f.Process(EndTurnTask.Any(f.Game.Player2));
            }
            foreach (var c in f.Player.DeckZone.ToArray()) f.Player.SetasideZone.Add(f.Player.DeckZone.Remove(c));
            SabberStoneCore.Model.Entities.Entity.FromCard(f.Player, Cards.FromId("CORE_CS2_029"), zone: f.Player.DeckZone);
        }, (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => {
            var drawn = f.Player.HandZone.Single(c => c.Card.Id == "CORE_CS2_029");
            Check(drawn[GameTag.SPELLPOWER] is 0 or 2 && f.Player.CurrentSpellPower == 0,
                "Kindred grants only the drawn spell's local bonus");
            return drawn[GameTag.SPELLPOWER] == 2;
        });
    }
}

internal static partial class Program
{
    [GroupProof("b14-k5-native", ProofKind.Native)]
    private static void B14K5Native()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(); var g = match.Game;
            if (bot) Check(g.Process(EndTurnTask.Any(g.Player1)), "K5 bot turn");
            var owner = bot ? g.Player2 : g.Player1;
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            void Play(SabberStoneCore.Model.Entities.IPlayable card, ICharacter target = null)
            {
                owner.BaseMana = 10; owner.UsedMana = 0;
                Check(g.Process(PlayCardTask.Any(owner, card, target)), "K5 native play " + card.Card.Id);
            }
            var held = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CATA_553"));
            var dragon = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("EX1_043"));
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            Play(dragon); FlushFieldHistory(match);
            var changes = History(capture, cursor).Where(e => SnapshotEntity(e) == held.Id).ToArray();
            Check(held.Card.Id == "CATA_553t" && changes.Any()
                && changes.All(e => bot ? string.IsNullOrEmpty(SnapshotName(e)) : SnapshotName(e) == "CATA_553t"),
                "held Ebyssian transform identity is private to owner");
            Play(held); FlushFieldHistory(match);
            Check(((Minion)held).IsRush && ((Minion)dragon).IsRush
                && match.SnapshotVisibleTransportTags(dragon).GetValueOrDefault((int)GameTag.RUSH) == 1,
                "Ebyssian player aura projects Rush to existing friendly Dragons");
            ((Minion)held).Silence(); ((Minion)held).Destroy(); g.DeathProcessingAndAuraUpdate();
            var futureDragon = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("EX1_043"));
            SabberStoneCore.Actions.Generic.SummonBlock(g, futureDragon, -1, owner.Hero);
            g.DeathProcessingAndAuraUpdate(); FlushFieldHistory(match);
            Check(futureDragon.IsRush && match.SnapshotVisibleTransportTags(futureDragon).GetValueOrDefault((int)GameTag.RUSH) == 1,
                "future Dragon keeps visible Rush after Ebyssian Silence and death");

            Play(SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("GIL_681")));
            Check(g.Process(EndTurnTask.Any(owner)) && g.Process(EndTurnTask.Any(owner.Opponent)), "K5 Kindred prior turn");
            foreach (var card in owner.DeckZone.ToArray()) owner.SetasideZone.Add(owner.DeckZone.Remove(card));
            var drawn = SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId("CORE_CS2_029"), zone: owner.DeckZone);
            var thrasher = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("TLC_223"));
            FlushFieldHistory(match); cursor = capture.Packets.Count;
            Play(thrasher);
            Check(g.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryMetaData>().Any(e =>
                e.Type == MetaDataType.TARGET && e.Info.SequenceEqual(new[] { thrasher.Id })), "draw emits typed source TARGET");
            FlushFieldHistory(match);
            var rows = History(capture, cursor).ToArray();
            Check(drawn.Zone == owner.HandZone && drawn[GameTag.SPELLPOWER] == 2, "native Kindred draw retains +2");
            var actor = drawn.AppliedEnchantments.Single(e => e.Card.Id == "TLC_223e");
            if (bot)
            {
                Check(rows.Where(e => SnapshotEntity(e) == drawn.Id || SnapshotEntity(e) == actor.Id)
                    .All(e => string.IsNullOrEmpty(SnapshotName(e))), "opponent draw and bonus actor identities stay private");
                Check(!rows.Any(e => e.HasTagChange && e.TagChange.Entity == drawn.Id
                    && e.TagChange.Tag == (int)GameTag.CURRENT_SPELLPOWER), "opponent spell bonus is hidden");
            }
            else
            {
                Check(rows.Any(e => SnapshotEntity(e) == drawn.Id && SnapshotName(e) == "CORE_CS2_029")
                    && rows.Any(e => SnapshotEntity(e) == actor.Id && SnapshotName(e) == "TLC_223e"), "owner sees drawn card and named buff");
                Check(match.SnapshotVisibleTransportTags(drawn).GetValueOrDefault((int)GameTag.CURRENT_SPELLPOWER) == 2,
                    "visible local Spell Damage uses retail tag 291");
            }

            var cloud = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CATA_563"));
            var coin = SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("GAME_005"));
            FlushFieldHistory(match); cursor = capture.Packets.Count;
            Play(cloud);
            var decision = g.GetPendingDecisions().Single();
            Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId,
                new[] { coin.Id }), out var error), "K5 native absorb answer: " + error);
            FlushFieldHistory(match);
            rows = History(capture, cursor).ToArray();
            Check(coin.Zone == owner.SetasideZone && owner.DiscardedEntities.Count == 0, "absorb is not discard");
            Check(rows.Where(e => SnapshotEntity(e) == coin.Id).All(e => string.IsNullOrEmpty(SnapshotName(e))),
                "retired absorbed actor remains private rather than leaking SETASIDE identity");
            Check(rows.Where(e => SnapshotEntity(e) == drawn.Id).All(e => !bot || string.IsNullOrEmpty(SnapshotName(e))),
                "unselected hidden spell stays private");
            Check(rows.All(e => !e.HasTagChange || !SabberStoneCore.Actions.PayloadRules.IsInternalTag((GameTag)e.TagChange.Tag)),
                "absorbed-actor bookkeeping never reaches client");

            foreach (var minion in owner.BoardZone.ToArray()) owner.SetasideZone.Add(owner.BoardZone.Remove(minion));
            var rook = (Minion)SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("TIME_217"));
            Play(rook); FlushFieldHistory(match); cursor = capture.Packets.Count;
            Play(SabberStoneCore.Actions.Generic.DrawCard(owner, Cards.FromId("CORE_EX1_238")), rook);
            FlushFieldHistory(match);
            var spawned = owner.BoardZone.Single(m => m.Id != rook.Id);
            Check(rook.Damage == 0 && spawned.Card.Cost == 5 && spawned[GameTag.DISPLAYED_CREATOR] == rook.Id
                && spawned.ZonePosition == rook.ZonePosition + 1, "native Stormrook replaces damage with adjacent five-Cost summon");
            Check(History(capture, cursor).Any(e => SnapshotEntity(e) == spawned.Id && SnapshotName(e) == spawned.Card.Id),
                "summoned Stormrook minion is public for either controller");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS K5 native: held transform, Kindred draw TARGET/local bonus, absorb privacy");
    }
}
