using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class EntomologistToruProof
{
    internal static void Run() => Unpinned_PayloadLifecycle();
    private static void Unpinned_PayloadLifecycle() => StandardSetsProof.Case("TLC_841", (g, p, id) => {
        var held = (Minion)Add(p, "CS2_182"); held.AttackDamage = 10; held.Cost = 0;
        Play(g, p, id); var jar = p.HandZone.Single();
        Core2026Proof.Check(jar.Card.Id == "TLC_841t" && jar.Cost == 1 && jar[GameTag.TAG_SCRIPT_DATA_NUM_6] == Cards.FromId("CS2_182").AssetId, "Jar identity and sixth display reference");
        var duplicate = Generic.Copy(p, p.Hero, jar, Zone.HAND);
        p.UsedMana = 0; Core2026Proof.Check(g.Process(PlayCardTask.Any(p, jar)), "play Jar");
        Kill(g, (Minion)jar); var released = p.BoardZone.Single(m => m.Card.Id == "CS2_182");
        Core2026Proof.Check(released.AttackDamage == 4 && released.Health == 5, "Unpinned_BasePayload releases fresh unbuffed minion");
        p.UsedMana = 0; Core2026Proof.Check(g.Process(PlayCardTask.Any(p, duplicate)), "play copied Jar");
        ((Minion)duplicate).Silence(); Kill(g, (Minion)duplicate);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "CS2_182") == 1, "Silence removes copied payload");
        StandardSetsProof.ClearBoard(g, p); Add(p, "CS2_120"); Play(g, p, id); Play(g, p, id);
        var outer = p.HandZone.Single(); p.UsedMana = 0; Core2026Proof.Check(g.Process(PlayCardTask.Any(p, outer)), "play nested Jar");
        Kill(g, (Minion)outer); var inner = p.BoardZone.Single(m => m.Card.Id == "TLC_841t");
        Kill(g, inner); Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "CS2_120"), "Unpinned_NestedJar releases empty inner Jar");
    });
}
