using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class FragmentofNothingProof
{
    internal static void Run() => StandardSetsProof.Case("END_026", (g, p, id) => {
        var redirected = g.CloneExact(history: true);
        Summon(redirected.Player1, id); Summon(redirected.Player2, "CFM_670");
        var redirectTarget = Summon(redirected.Player2, "CS2_200");
        bool minionToHero = false, heroToMinion = false;
        bool minionToHeroWrong = false, heroToMinionWrong = false;
        for (int seed = 1; seed <= 16; seed++)
        {
            foreach (bool startOnMinion in new[] { true, false })
            {
                var trial = redirected.CloneExact(history: true); trial.SetRandomSeed(seed);
                var owner = trial.Player1; int before = owner.HandZone.Count;
                var spell = Play(trial, owner, "CS2_008", startOnMinion ? (ICharacter)trial.IdEntityDic[redirectTarget.Id] : owner.Opponent.Hero);
                if (startOnMinion && trial.IdEntityDic[spell.CardTarget] is Hero)
                {
                    minionToHero = true; minionToHeroWrong |= owner.HandZone.Count != before;
                }
                if (!startOnMinion && trial.IdEntityDic[spell.CardTarget] is Minion)
                {
                    heroToMinion = true; heroToMinionWrong |= owner.HandZone.Count != before + 1;
                }
            }
        }
        Core2026Proof.Check(minionToHero && heroToMinion, "Mayor redirects spells in both directions");
        Core2026Proof.Check(!minionToHeroWrong && !heroToMinionWrong,
            $"resolved target controls draw: minion-to-hero wrong={minionToHeroWrong}; hero-to-minion wrong={heroToMinionWrong}");
        var held = Add(p, id); var source = (Minion)Play(g, p, id); var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        int hand = p.HandZone.Count; Play(g, p, "CS2_008", friendly); Core2026Proof.Check(p.HandZone.Count == hand + 1, "friendly targeted spell draws one");
        hand = p.HandZone.Count; Play(g, p, "CS2_008", enemy); Core2026Proof.Check(p.HandZone.Count == hand + 1, "enemy targeted spell also draws one");
        hand = p.HandZone.Count; Play(g, p, "GAME_005"); Core2026Proof.Check(p.HandZone.Count == hand, "untargeted spell does not draw");
        Play(g, p, "CS2_008", p.Opponent.Hero); Core2026Proof.Check(p.HandZone.Count == hand, "hero target does not draw");
        var clone = g.CloneExact(history: true); hand = p.HandZone.Count; Play(clone, clone.Player1, "CS2_008", (Minion)clone.IdEntityDic[friendly.Id]);
        Core2026Proof.Check(clone.Player1.HandZone.Count == hand + 1 && p.HandZone.Count == hand, "clone trigger independent");
        End(g); hand = p.HandZone.Count; Play(g, p.Opponent, "CS2_008", friendly); Core2026Proof.Check(p.HandZone.Count == hand, "opponent spell does not trigger");
        End(g); source.Silence(); hand = p.HandZone.Count; Play(g, p, "CS2_008", friendly); Core2026Proof.Check(p.HandZone.Count == hand, "Silence removes trigger");
        Kill(g, source); source = Summon(p, id); Kill(g, source); hand = p.HandZone.Count;
        Play(g, p, "CS2_008", enemy); Core2026Proof.Check(p.HandZone.Count == hand, "dead source and hand copy do not trigger");
        source = Summon(p, id); hand = p.HandZone.Count; Play(g, p, "CS2_029", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.HandZone.Count == hand, "lethal targeted spell removes source before after-cast");
    });
}
