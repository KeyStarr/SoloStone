using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EldritchTentaclesProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_491", Unpinned_Tentacles);
    private static void Unpinned_Tentacles(Game g, Controller p, string id)
    {
        var target = Summon(p.Opponent, "CS2_182"); target.BaseHealth = 100;
        var spell = Add(p, id); Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_209e"), spell, spell);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, (IPlayable)next.IdEntityDic[spell.Id]));
        Core2026Proof.Check(target.Damage == 10, "Unpinned_Tentacles persistent local +1 makes 4/3/2/1, applied once per wave");
        ParkBoard(p); ParkBoard(p.Opponent);
        target = Summon(p.Opponent, "CS2_182"); target.BaseHealth = 100;
        Summon(p, "CS2_052"); Play(g, p, id);
        Core2026Proof.Check(target.Damage == 7, "Unpinned_Tentacles aura dies in first wave; next waves use current bonus: 4/2/1");
        ParkBoard(p.Opponent);
        var golem = Summon(p.Opponent, "EX1_556"); Play(g, p, id);
        Core2026Proof.Check(golem.Zone.Type == Zone.GRAVEYARD && !p.Opponent.BoardZone.Any(), "Unpinned_Tentacles new Deathrattle bodies join later waves");
        var location = LocationProofs.Location(p.Opponent); var dormant = Summon(p.Opponent, "EDR_840t2");
        Play(g, p, id);
        Core2026Proof.Check(location[GameTag.DAMAGE] == 0 && dormant.Damage == 0, "locations/Dormant excluded");
    }
}
