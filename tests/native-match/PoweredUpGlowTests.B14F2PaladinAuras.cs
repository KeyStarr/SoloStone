using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-f2-paladin-auras", ProofKind.PoweredUp)]
    private static void RunB14F2PaladinAuras()
    {
        Scenario("TIME_019", (f, card, met) => {
            if (met)
            {
                var aura = f.Draw("TIME_700");
                f.Process(PlayCardTask.Spell(f.Player, aura));
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, card),
            (f, card) => f.Game.Player2.Hero.Damage == 3);
        Scenario("EDR_256", (f, card, met) => {
            if (met) SabberStoneCore.Model.Entities.Entity.FromCard(f.Player, Cards.FromId("CS2_231"), zone: f.Player.DeckZone);
        }, (f, card) => PlayCardTask.Minion(f.Player, card),
            (f, card) => ((Minion)card).AttackDamage == 5 && ((Minion)card).Health == 6);
    }

    [GroupProof("native-b14-f2-paladin-auras", ProofKind.Native)]
    private static void NativeB14F2PaladinAuras()
    {
        var f = new Fixture();
        var aura = f.Draw("TIME_700");
        f.Process(PlayCardTask.Spell(f.Player, aura));
        var tags = f.Match.SnapshotVisibleTransportTags(aura);
        Check(tags[(int)GameTag.ZONE] == (int)SabberStoneCore.Enums.Zone.SECRET && tags[2311] == 1 && tags[3429] == 1
            && tags[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "public Aura actor and initial counter");
        Check(tags[535] - (tags.TryGetValue(534, out int progress) ? progress : 0) == 3,
            "native objective banner reads total minus progress");
        f.Game.Process(EndTurnTask.Any(f.Game.CurrentPlayer)); f.Flush();
        Check(f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == aura.Id
            && e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1 && e.TagChange.Value == 2), "counter tick reaches native history");
        var enemyHand = Generic.DrawCard(f.Game.Player2, Cards.FromId("CS2_029"));
        var hidden = PayloadRules.StoreCard(aura, 913, Cards.FromId("CS2_024"));
        f.Flush();
        foreach (var held in f.Game.Player2.HandZone.ToArray())
            f.Game.Player2.SetasideZone.Add(f.Game.Player2.HandZone.Remove(held));
        var captured = Generic.DrawCard(f.Game.Player2, Cards.FromId("CS2_029"));
        var ursol = Generic.DrawCard(f.Game.Player2, Cards.FromId("EDR_259"));
        f.Game.Player2.BaseMana = 10; f.Game.Player2.UsedMana = 0;
        Check(f.Game.Process(PlayCardTask.Minion(f.Game.Player2, ursol)), "opponent Ursol captures its hidden hand spell");
        f.Flush();
        var wrapper = f.Game.Player2.SecretZone.Auras.Single();
        // The Aura is public but its captured spell came from a hidden hand: tag 2 is the client's entity reference (builder 19),
        // never named to the opponent (it used to send the spell's dbfId, which also named nothing). Retail unpinned: default private.
        Check(f.Match.SnapshotVisibleTransportTags(wrapper)[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] == 0
            && f.Match.SnapshotVisibleTransportTags(wrapper)[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] != captured.Card.AssetId,
            "opponent Aura is public but does not reveal its captured hidden spell");
        var privateIds = f.Game.IdEntityDic.Values.Where(PayloadRules.IsStored).Select(e => e.Id).Append(enemyHand.Id).ToHashSet();
        foreach (var entry in f.Entries)
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : null;
            if (entity != null && privateIds.Contains(entity.Entity))
                Check(string.IsNullOrEmpty(entity.Name), "opponent hand/stored payload identities stay private");
            if (entity != null) Check(entity.Tags.All(t => t.Name < 32500 || t.Name > 32502), "private Aura tags filtered from entities");
            if (entry.HasTagChange) Check(entry.TagChange.Tag < 32500 || entry.TagChange.Tag > 32502, "private Aura tag changes filtered");
        }
        Check(f.Match.SnapshotVisibleTransportTags(aura).Keys.All(tag => tag < 32500 || tag > 32502), "private Aura tags filtered from reset snapshots");
        for (int i = 0; i < 4; i++) Check(f.Game.Process(EndTurnTask.Any(f.Game.CurrentPlayer)), "native expiry turn accepted");
        f.Flush();
        Check(aura.Zone == f.Player.GraveyardZone && f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == aura.Id
            && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == (int)SabberStoneCore.Enums.Zone.GRAVEYARD),
            "expired objective actor moves to native graveyard");

        var oath = new Fixture();
        oath.Process(PlayCardTask.Spell(oath.Player, oath.Draw("BE_146")));
        oath.Process(PlayCardTask.SpellTarget(oath.Player, oath.Draw("CS2_089"), oath.Player.Hero));
        Check(oath.Player.BoardZone.Single().Card.Id == "BE_142t", "ordered after-cast Soldier reaches native projection");
        var witnesses = oath.Game.IdEntityDic.Values.Where(PayloadRules.IsStored).ToArray();
        Check(witnesses.Length != 0 && witnesses.All(e => e[PayloadRules.Owner] == 0),
            "after-cast consumes private witnesses once");
        foreach (var entry in oath.Entries)
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : null;
            if (entity != null && witnesses.Any(e => e.Id == entity.Entity))
                Check(string.IsNullOrEmpty(entity.Name), "retired cast witness identities remain hidden from owner");
        }
    }
}
