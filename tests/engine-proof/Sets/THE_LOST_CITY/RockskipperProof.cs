using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RockskipperProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_427", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var rock = p.HandZone.Single();
        Core2026Proof.Check(source.IsRace(Race.MURLOC) && rock.Card.Id == "WW_001t" && rock.Cost == 1 && rock.Card.Implemented, "Murloc gets pinned 1-Cost Rock");
        var enemy = Summon(p.Opponent, "CS2_182"); PlayEntity(g, p, rock, enemy); Core2026Proof.Check(enemy.Health == 2, "Rock deals three to minion");
        rock = Play(g, p, id) is Minion ? p.HandZone.Single() : null; PlayEntity(g, p, rock, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Health == 27, "Rock targets hero");
        p.Hero[GameTag.SPELLPOWER] = 1;
        Play(g, p, id); rock = p.HandZone.Single(); int before = p.Opponent.Hero.Health;
        PlayEntity(g, p, rock, p.Opponent.Hero);
        Core2026Proof.Check(p.CurrentSpellPower == 1 && p.Opponent.Hero.Health == before - 4, "Rock deals four with Spell Damage +1");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack()); Core2026Proof.Check(p.HandZone.Count == 10 && !p.HandZone.Any(c => c.Card.Id == "WW_001t"), "full hand receives no Rock");
    });
}
