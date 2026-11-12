using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-k1-neutral-a", ProofKind.PoweredUp)]
    private static void RunB14K1NeutralA()
    {
        Scenario("EDR_860", (f, card, met) => {
            f.Player[GameTag.IMBUE] = met ? 2 : 1;
            f.Game.Player2[GameTag.IMBUE] = 8;
            f.Player.Hero.Damage = 10;
            f.Summon("CS2_200", f.Game.Player2);
        }, (f, card) => PlayCardTask.Any(f.Player, card,
            f.Player[GameTag.IMBUE] >= 2 ? f.Game.Player2.BoardZone.Single() : null), (f, card) => {
            int damage = f.Game.Player2.BoardZone.Single().Damage;
            Check(damage == 0 || damage == 4, "Dreamweaver deals exactly four");
            Check(f.Player.Hero.Damage == 10 - damage, "Dreamweaver Lifesteal agrees with damage");
            return damage == 4;
        });
        Scenario("FIR_921", (f, card, met) => {
            f.Player[GameTag.IMBUE] = met ? 2 : 1;
            f.Game.Player2[GameTag.IMBUE] = 8;
        }, (f, card) => PlayCardTask.Any(f.Player, card), (f, card) => {
            Check(f.Player.HandZone.Count == 0 || f.Player.HandZone.Count == 2, "Picker draws exactly two");
            return f.Player.HandZone.Count == 2;
        });
        B14K1PetalDrawPrivacy();
        Console.WriteLine("PASS b14-k1-neutral-a: Imbue glow/outcomes; owner draw identities; opponent draw privacy");
    }

    private static void B14K1PetalDrawPrivacy()
    {
        var f = new Fixture();
        f.Process(EndTurnTask.Any(f.Player));
        var opponent = f.Game.Player2;
        opponent.BaseMana = 10; opponent.UsedMana = 0; opponent[GameTag.IMBUE] = 2;
        var source = Generic.DrawCard(opponent, SabberStoneCore.Model.Cards.FromId("FIR_921"));
        int hand = opponent.HandZone.Count;
        opponent.DeckZone.DrawWithRandom = false;
        int[] hidden = opponent.DeckZone.Reverse().Take(2).Select(c => c.Id).ToArray();
        Check(hidden.Length == 2, "privacy fixture has two deck entities");
        f.Process(PlayCardTask.Any(opponent, source));
        Check(opponent.HandZone.Count == hand + 1 && hidden.All(id => f.Game.IdEntityDic[id].Zone == opponent.HandZone),
            "opponent draws real deck entities");
        foreach (var entry in f.Entries)
        {
            if (entry.HasTagChange && hidden.Contains(entry.TagChange.Entity))
                Check(entry.TagChange.Tag == (int)GameTag.ENTITY_ID || entry.TagChange.Tag == (int)GameTag.CONTROLLER
                    || entry.TagChange.Tag == (int)GameTag.ZONE || entry.TagChange.Tag == (int)GameTag.ZONE_POSITION,
                    "opponent draw tag changes never expose hidden stats");
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity
                : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (entity == null || !hidden.Contains(entity.Entity)) continue;
            Check(string.IsNullOrEmpty(entity.Name), "opponent Picker draws never expose identities");
            Check(entity.Tags.All(t => t.Name == (int)GameTag.ENTITY_ID || t.Name == (int)GameTag.CONTROLLER
                || t.Name == (int)GameTag.ZONE || t.Name == (int)GameTag.ZONE_POSITION), "opponent draws expose only hidden tags");
        }
        var own = new Fixture(); own.Player[GameTag.IMBUE] = 2;
        var picker = own.Draw("FIR_921");
        own.Player.DeckZone.DrawWithRandom = false;
        int[] drawn = own.Player.DeckZone.Reverse().Take(2).Select(c => c.Id).ToArray();
        own.Process(PlayCardTask.Any(own.Player, picker));
        Check(drawn.All(id => own.Entries.Any(e => e.HasShowEntity && e.ShowEntity.Entity == id
            && !string.IsNullOrEmpty(e.ShowEntity.Name))), "owner sees both drawn identities");
    }
}
