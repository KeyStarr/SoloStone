using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CaptainCrowleyProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_106", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Crowley play");
        var cannons = p.BoardZone.Where(m => m.Card.Id == "CAP_107t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(source is Minion m && m.AttackDamage == 4 && m.Health == 5 && m.IsRace(Race.PIRATE), "printed Crowley stats/type");
        Core2026Proof.Check(cannons.Length == 2 && cannons.All(m => m.AttackDamage == 1 && m.Health == 1)
            && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, cannons[0].Id, cannons[1].Id, right.Id }), "two exact tokens beside source in creation order");
        End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "both Cannoneers fire extra shot");
        int damage = p.Opponent.Hero.Damage; End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == damage, "opponent turn does not fire");
        var clone = g.CloneExact(history: true); ((Minion)clone.IdEntityDic[source.Id]).Silence(); End(clone);
        Core2026Proof.Check(clone.Player2.Hero.Damage == 6 && p.Opponent.Hero.Damage == 4, "Silence removes extra shot, clone isolated");
        ((Minion)source).Silence(); End(g); End(g); Kill(g, (Minion)source); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 8, "departed Crowley gives no extra shot");
        End(g); var second = Summon(p, id); second[GameTag.DORMANT] = 1; End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 10, "Dormant Crowley gives no extra shot");
        End(g); ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CAP_107t") == 1, "Battlecry respects board cap");
    });
}
