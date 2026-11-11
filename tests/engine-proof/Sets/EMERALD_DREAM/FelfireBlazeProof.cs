using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FelfireBlazeProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_904", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var friend = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p.Opponent, "CS2_171"); shield.HasDivineShield = true;
        Play(g, p, "GAME_005"); Core2026Proof.Check(source.Zone == p.BoardZone && enemy.Damage == 0, "schoolless spell excluded");
        End(g); Play(g, p.Opponent, "EX1_596", enemy); End(g);
        Core2026Proof.Check(source.Zone == p.BoardZone && p.Opponent.Hero.Damage == 0, "opponent Fel excluded");
        var clone = g.Clone(); Play(clone, clone.Player1, "EX1_596", (ICharacter)clone.IdEntityDic[friend.Id]);
        Core2026Proof.Check(clone.IdEntityDic[source.Id].Zone.Type == Zone.GRAVEYARD && source.Zone == p.BoardZone && p.Opponent.Hero.Damage == 0, "Fel trigger clone isolated");
        Play(g, p, "EX1_596", friend);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && friend.Damage == 2 && enemy.Damage == 4 && shield.Damage == 0 && !shield.HasDivineShield && p.Opponent.Hero.Damage == 2, "own legacy Fel resolves, destroys source, deals two to every enemy; shield absorbs");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source);
        Play(g, p, "EX1_596", friend);
        Core2026Proof.Check(source.Zone == p.BoardZone && enemy.Damage == 4 && source.ActivatedTrigger == null, "Silence removes Fel trigger");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source); Play(g, p, "EX1_596", friend);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2, "removed source cannot trigger");
        source = (Minion)Play(g, p, id); source.Damage = 1; Play(g, p, "EX1_596", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.Opponent.Hero.Damage == 2, "spell killing source prevents after-cast trigger");
    });
}
