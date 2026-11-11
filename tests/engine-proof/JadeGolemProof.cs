using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class JadeGolemProof
{
    [GroupProof("jade-golem")]
    internal static void Run()
    {
        var game = ImbueProof.Make(CardClass.ROGUE);
        var player = game.Player1;
        for (int size = 1; size <= 5; size++)
        {
            player.BaseMana = 10; player.UsedMana = 0; player.TemporaryMana = 0;
            var coin = Add(player, "JAIL_504t");
            Core2026Proof.Check(game.Process(PlayCardTask.Any(player, coin)), "Jade Coin play accepted");
            var golem = player.BoardZone.Last();
            Core2026Proof.Check(golem.Card.Id == "CFM_712_t" + size.ToString("D2")
                && golem.Card.ATK == size && golem.Card.Health == size
                && golem.AttackDamage == size && golem.Health == size
                && (golem.AppliedEnchantments?.Count ?? 0) == 0,
                "Jade Coin " + size + " summons size-specific base token without stat enchantment");
        }
        Console.WriteLine("PASS Jade Golems: Coins summon t01..t05, base 1/1..5/5, no enchantments");
    }
}
