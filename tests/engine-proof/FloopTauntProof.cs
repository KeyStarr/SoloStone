using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class FloopTauntProof
{
    internal static void Run()
    {
        foreach (bool history in new[] { true, false })
        foreach (bool bounced in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = 7, StartPlayer = 1, SkipMulligan = true,
                History = history, Logging = false, FormatType = FormatType.FT_WILD });
            game.StartGame();
            var player = game.Player1;
            var floop = (Minion)Add(player, "BOT_434");
            Core2026Proof.Check(!floop.HasTaunt, "Floop initially has no Taunt (read before changing card)");
            if (bounced)
            {
                PlayEntity(game, player, floop);
                Core2026Proof.Check(Generic.ReturnToHandBlock(player, floop)
                    && player.HandZone.Contains(floop) && floop.Card.Id == "BOT_434" && !floop.HasTaunt,
                    "Played Floop bounces to hand without Taunt");
            }
            Play(game, player, "JAIL_942");
            Core2026Proof.Check(floop.Card.Id == "JAIL_942" && floop.AttackDamage == 3 && floop.BaseHealth == 4,
                "Floop becomes a 3/4 Specter of Despair");
            Core2026Proof.Check(floop.HasTaunt && floop[GameTag.TAUNT] == 1,
                $"Floop gains Specter's printed Taunt after its previous non-Taunt state was read (bounced={bounced}, history={history})");
            Play(game, player, "CS2_231");
            Core2026Proof.Check(floop.Card.Id == "CS2_231" && !floop.HasTaunt,
                "Floop loses previous printed Taunt when copying a non-Taunt minion");
            floop.HasTaunt = true;
            Play(game, player, "CS2_172");
            Core2026Proof.Check(floop.Card.Id == "CS2_172" && floop.HasTaunt,
                "Floop preserves explicitly granted Taunt across hand transforms");
        }
        Console.WriteLine("PASS Floop Taunt: printed keyword follows hand transforms after reads and bounce; explicit grant preserved; history on/off");
    }
}
