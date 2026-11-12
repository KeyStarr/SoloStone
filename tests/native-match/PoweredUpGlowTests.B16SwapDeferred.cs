using System;
using System.IO;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
internal static partial class PoweredUpGlowTests
{
    private static readonly string[] B16SwapIds = { "CATA_550", "JAIL_421", "JAIL_502", "JAIL_852" };
    [GroupProof("powered-up-b16-swap-deferred", ProofKind.PoweredUp)]
    private static void B16SwapGlow()
    {
        var f = new Fixture();
        foreach (string id in B16SwapIds)
        {
            Check(EffectModules.ByDbfId.ContainsKey(Cards.FromId(id)?.AssetId ?? 0), "b16 registered unconditional card " + id);
            var card = f.Draw(id); f.Flush(); f.Expected(card, 0, "unconditional card has no pre-play glow");
        }
    }
    [GroupProof("native-b16-swap-deferred", ProofKind.Native)]
    private static void B16SwapNative()
    {
        foreach (bool opponent in new[] { false, true })
        {
            B16HandPrivacy(opponent);
            B16AlarmProjection(opponent);
            B16DeferredProjection(opponent);
            B16MagmawProjection(opponent);
        }
        Console.WriteLine("PASS native b16 swap/deferred: both seats, hidden transfers, pending deck/hand, public summons and private tags");
    }

    private static Controller B16Owner(Fixture f, bool opponent)
    {
        if (opponent) Check(f.Game.Process(EndTurnTask.Any(f.Player)), "b16 opponent turn");
        foreach (var p in new[] { f.Game.Player1, f.Game.Player2 })
        {
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            foreach (var card in p.BoardZone.ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(card));
        }
        var owner = f.Game.CurrentPlayer; owner.BaseMana = 10; owner.UsedMana = 0;
        return owner;
    }

    private static bool B16Structural(int tag) => tag == (int)GameTag.ENTITY_ID || tag == (int)GameTag.CONTROLLER
        || tag == (int)GameTag.ZONE || tag == (int)GameTag.ZONE_POSITION;

    private static void B16Audit(Fixture f, int publiclyCommitted = 0)
    {
        var rows = f.Entries.ToList();
        // NativeMatch names a card handed to the human before its CONTROLLER move (the actor binds at the move); that
        // copy sits at the client's current location and discloses only what the following transfer SHOW does.
        bool NamedBeforeTransfer(int index, int id) => rows.Skip(index + 1).FirstOrDefault(r => r.HasTagChange && r.TagChange.Entity == id
            || r.HasShowEntity && r.ShowEntity.Entity == id || r.HasHideEntity && r.HideEntity.Entity == id) is { HasTagChange: true } next
            && next.TagChange.Tag == (int)GameTag.CONTROLLER && next.TagChange.Value == 1;
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            Check(!row.HasTagChange || !SwapRules.IsInternalTag((GameTag)row.TagChange.Tag), "b16 internal tag change filtered");
            foreach (var entity in new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }.Where(e => e != null))
            {
                Check(entity.Tags.All(t => !SwapRules.IsInternalTag((GameTag)t.Name)), "b16 internal snapshot tags filtered");
                int controller = entity.Tags.FirstOrDefault(t => t.Name == (int)GameTag.CONTROLLER)?.Value ?? 0;
                int zone = entity.Tags.FirstOrDefault(t => t.Name == (int)GameTag.ZONE)?.Value ?? 0;
                if (entity.Entity != publiclyCommitted && (zone == (int)SabberStoneCore.Enums.Zone.DECK || controller == 2 && zone == (int)SabberStoneCore.Enums.Zone.HAND)
                    && !(row.HasShowEntity && controller == 2 && NamedBeforeTransfer(index, entity.Entity)))
                    Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t => B16Structural(t.Name)),
                        "b16 hidden card never receives identity/definition tags: id=" + entity.Entity + " name=" + entity.Name + " controller=" + controller + " zone=" + zone + " tags=" + string.Join(",", entity.Tags.Select(t => t.Name + "=" + t.Value)));
            }
        }
        var packet = new PegasusGame.PowerHistory { List = f.Entries.ToList() };
        using var stream = new MemoryStream(); packet.Serialize(stream);
        Check(stream.Length == packet.GetSerializedSize(), "b16 native packets serialize");
    }

    private static void B16HandPrivacy(bool opponent)
    {
        var f = new Fixture(); var p = B16Owner(f, opponent); var g = f.Game;
        for (int i = 0; i < 3; i++) Generic.DrawCard(p, Cards.FromId(i == 0 ? "EX1_160" : "CS2_200"));
        for (int i = 0; i < 7; i++) Generic.DrawCard(p.Opponent, Cards.FromId(i == 0 ? "EX1_160" : "CS2_182"));
        var source = Generic.DrawCard(p, Cards.FromId("JAIL_852")); f.Flush(); B16Audit(f);
        Check(g.Process(PlayCardTask.Any(p, source)), "b16 native Togwaggle play"); f.Flush();
        foreach (var card in g.Player1.HandZone.Concat(g.Player2.HandZone).OfType<Minion>())
            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_550e1"), source, card);
        int cursor = f.Entries.Count(); f.Flush();
        var hidden = g.Player2.HandZone.Select(c => c.Id).ToArray();
        Check(f.Entries.Skip(cursor).Where(r => r.HasTagChange && hidden.Contains(r.TagChange.Entity))
            .All(r => B16Structural(r.TagChange.Tag)), "former owner cannot receive new opponent-hand buff state");
        var shown = f.Entries.Where(r => r.HasShowEntity).Select(r => r.ShowEntity).ToArray();
        foreach (var card in g.Player1.HandZone)
            Check(shown.Any(e => e.Entity == card.Id && e.Name == card.Card.Id), "new human controller sees transferred identity");
        Check(g.Player1.HandZone.Count == (opponent ? 7 : 3) && g.Player2.HandZone.Count == (opponent ? 3 : 7),
            "native hand counts match post-play counts");
        B16Audit(f, source.Id);
    }

    private static void B16AlarmProjection(bool opponent)
    {
        var f = new Fixture(); var p = B16Owner(f, opponent); var g = f.Game;
        var bot = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("JAIL_502")); Generic.SummonBlock(g, bot, -1, null);
        var incoming = Generic.DrawCard(p.Opponent, Cards.FromId("EX1_015"));
        while (!p.Opponent.HandZone.IsFull) Generic.DrawCard(p.Opponent, Cards.FromId("CS2_029"));
        f.Flush(); B16Audit(f);
        bot.Power.Trigger.SingleTask.Process(g, p, bot, null, new TaskStack()); g.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(bot.Zone == p.Opponent.HandZone && incoming.Zone == p.BoardZone && p.Opponent.HandZone.Count == 10,
            "native Alarm preserves reciprocal full-hand capacity");
        Check(f.Entries.Any(r => r.HasShowEntity && r.ShowEntity.Entity == incoming.Id && r.ShowEntity.Name == incoming.Card.Id
            && r.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.CONTROLLER && t.Value == p.PlayerId)
            && r.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.ZONE && t.Value == (int)SabberStoneCore.Enums.Zone.PLAY)),
            "incoming board actor uses exported identity, new controller and PLAY");
        B16Audit(f, incoming.Id);
    }

    private static void B16DeferredProjection(bool opponent)
    {
        var f = new Fixture(); var p = B16Owner(f, opponent); var g = f.Game;
        while (!p.BoardZone.IsFull) SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("CS2_200"), zone: p.BoardZone);
        var hand = Generic.DrawCard(p, Cards.FromId("JAIL_421"));
        var deck = SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("JAIL_421"), zone: p.DeckZone);
        f.Flush();
        p.Hero.TakeDamage(p.Opponent.Hero, 1);
        foreach (var victim in p.BoardZone.Take(3).ToArray()) victim.TakeDamage(p.Opponent.Hero, 1);
        g.DeathProcessingAndAuraUpdate(); f.Flush(); B16Audit(f);
        Check(hand[SwapRules.PendingSummon] > 0 && deck[SwapRules.PendingSummon] > 0, "both hidden pending requests latched");
        var victimId = p.BoardZone.First().Id; g.IdEntityDic[victimId].ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(hand.Zone == p.BoardZone && deck.Zone == p.DeckZone && g.Player1.Choice == null && g.Player2.Choice == null && !g.Resolution.HasSuspendedEffect,
            "pending hand summons publicly; deck copy waits without a stalled decision: side=" + opponent + " hand=" + hand.Zone?.Type + " deck=" + deck.Zone?.Type + " decisions=" + g.GetPendingDecisions().Count + " board=" + p.BoardZone.Count);
        Check(f.Entries.Any(r => r.HasShowEntity && r.ShowEntity.Entity == hand.Id && r.ShowEntity.Name == "JAIL_421"
            && r.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.CHARGE && t.Value == 1)), "public Warptooth actor retains Charge");
        B16Audit(f, hand.Id);
    }

    private static void B16MagmawProjection(bool opponent)
    {
        var f = new Fixture(); var p = B16Owner(f, opponent); var g = f.Game;
        var body = Generic.DrawCard(p, Cards.FromId("CATA_550")); f.Flush(); B16Audit(f);
        Check(g.Process(PlayCardTask.Any(p, body)), "native Magmaw play"); f.Flush();
        Check(p.BoardZone.Count == 7 && f.Match.SnapshotVisibleTransportTags(body)[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] == 93,
            "native Magmaw shows real remaining count after six appendages");
        var oldIds = p.BoardZone.Select(c => c.Id).ToArray();
        p.BoardZone.First(c => c.Id != body.Id).ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate(); f.Flush();
        var refill = p.BoardZone.Single(c => !oldIds.Contains(c.Id));
        Check(refill.Card.Id == "CATA_550t" && p.BoardZone.Count == 7,
            "native checkpoint refills one exported Body");
        Check(f.Entries.Any(r => r.HasShowEntity && r.ShowEntity.Entity == refill.Id && r.ShowEntity.Name == "CATA_550t"
            && r.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.CONTROLLER && t.Value == p.PlayerId)
            && r.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.DISPLAYED_CREATOR && t.Value == body.Id)),
            "refill wire actor has exact identity, controller and Colossal creator");
        Check(f.Entries.Any(r => r.HasTagChange && r.TagChange.Entity == body.Id
            && r.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1 && r.TagChange.Value == 92),
            "native remaining counter decrements through ordinary exported script tag");
        Check(f.Entries.Any(r => r.HasShowEntity && r.ShowEntity.Name == "CATA_550e1" || r.HasFullEntity && r.FullEntity.Name == "CATA_550e1"), "Body Deathrattle emits exported Stampede actor");
        B16Audit(f, body.Id);
    }
}
