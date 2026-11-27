using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("repentant-prince", ProofKind.Native)]
    private static void RepentantPrincePlaytest()
    {
        var fixture = new PoweredUpGlowTests.Fixture();
        var player = fixture.Player;
        string cardId = RepentantPrinceRules.CardId;
        var loaner = LocalDecks.Get(LocalDecks.PrincePlaytestDeck);
        Check(loaner.Build().Count(card => card.Id == cardId) == 1
            && NativeUIProjection.IsExactLoaner(NativeUIProjection.DeckId(loaner.Id), loaner.Hero,
                loaner.CardIds.Select(LocalDecks.PrinceNativeId))
            && !NativeUIProjection.PlayerLoanerKeys.Contains(loaner.Id), "sandbox loaner uses one custom card through its native alias");

        var opening = new NativeMatch((_, _) => { }, loaner.Id, "mage", 322);
        opening.Start();
        Check(opening.Game.Player1.HandZone.Any(card => card.Card.Id == cardId), "playtest loaner opens with Kael’thas in hand");

        var prince = (Minion)fixture.Draw(cardId);
        Check(LocalNativeText.TryGetNameOverride(true, LocalDecks.PrincePresentationCard, value => value, out string name)
            && name == prince.Card.Name
            && LocalNativeText.TryGetOverride(true, LocalDecks.PrincePresentationCard, value => value, null, out string text)
            && text == prince.Card.Text, "native alias uses custom name and rules text");
        var third = fixture.Draw("CS2_182");
        void Play(IPlayable card) => fixture.Process(player.Options().First(option =>
            option.PlayerTaskType == PlayerTaskType.PLAY_CARD && option.Source?.Id == card.Id));
        CostPaymentKind Kind(IPlayable card) => CostRules.PaymentKind(card);
        Check(prince.Cost == 7 && prince.AttackDamage == 4 && prince.Health == 7 && prince.Card.Races.Contains(Race.UNDEAD)
            && prince.Card.Rarity == Rarity.LEGENDARY && !prince.Card.Collectible, "custom printed definition");
        Play(prince);
        Check(fixture.Entries.Any(row => SnapshotEntity(row) == prince.Id && SnapshotName(row) == LocalDecks.PrincePresentationCard
            && SnapshotTag(row, GameTag.CARDRACE) == (int)Race.UNDEAD), "custom card rendered with existing art and Undead tag");

        Play(fixture.Draw("GAME_005"));
        Check(Kind(third) == CostPaymentKind.Mana, "playing the Prince doesn't advance his own counter");
        fixture.Game.Process(EndTurnTask.Any(player));
        fixture.Game.Process(EndTurnTask.Any(player.Opponent));
        fixture.Mana(10);
        Play(fixture.Draw("GAME_005"));
        Check(Kind(third) == CostPaymentKind.Corpses && fixture.Entries.Any(row =>
            row.HasTagChange && row.TagChange.Entity == third.Id && row.TagChange.Tag == (int)GameTag.CARD_ALTERNATE_COST
                && row.TagChange.Value == 3), "third play carries across turns and switches the hand display");

        CorpseRules.Gain(player, third.Cost - 1);
        Check(!player.Options().Any(option => option.Source?.Id == third.Id), "enough Mana cannot pay a Corpse cost");
        CorpseRules.Gain(player, 1);
        fixture.Mana(0);
        Play(third);
        Check(CorpseRules.Available(player) == 0 && player.UsedMana == 0, "third play pays only Corpses");

        var fourth = fixture.Draw("CS2_182");
        Check(Kind(fourth) == CostPaymentKind.Mana, "corpse play restarts the counter");
        Play(fixture.Draw("GAME_005"));
        Play(fixture.Draw("GAME_005"));
        Check(Kind(fourth) == CostPaymentKind.Corpses, "conversion repeats on the sixth play");
        prince.Silence();
        fixture.Flush();
        Check(Kind(fourth) == CostPaymentKind.Mana, "silence ends replacement");

        AdmitFixture("custom-playtest", cardId, player.PlayerId, fixture.Packets, fixture.Game);
        Console.WriteLine("PASS Repentant Prince playtest: loaner, native identity, Corpse payment, turn carry, repeat and silence");
    }
}
