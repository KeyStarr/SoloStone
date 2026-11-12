using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class SpectralSightProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_491", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).AssetId == 69684, "Spectral Sight dbfId");
        var recast = PlayerDecisionProof.Fixture(); var caster = recast.Player1;
        Play(recast, caster, "BOT_411"); // Electra's AFTER_CAST uses PlayTask on the original entity.
        var originalSpell = Add(caster, id);
        int beforeRecast = caster.DeckZone.Count, beforePlays = caster.NumCardsPlayedThisTurn;
        PlayEntity(recast, caster, originalSpell);
        Core2026Proof.Check(caster.DeckZone.Count == beforeRecast - 3 && caster.HandZone.Count == 3
            && caster.NumCardsPlayedThisTurn == beforePlays + 1,
            $"R3 Electra recasts same Spectral Sight entity: primary draws two, recast one; got {beforeRecast - caster.DeckZone.Count}");
        OutcastProof.DrawCases(g, p, id, 1);
        OutcastProof.DrawBurn(g, p, id, 2);
        OutcastProof.EmptyHand(p);
        var spell = Add(p, id); int deck = p.DeckZone.Count, plays = p.NumCardsPlayedThisTurn;
        // R3 tier C: https://hearthstone.wiki.gg/wiki/Outcast#Notes, Cast spell effects do not trigger Outcast.
        new PlayTask(PlayType.SPELL).Process(g, p, p.Hero, null,
            new TaskStack { Playables = new List<IPlayable> { spell } });
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.NumCardsPlayedThisTurn == plays
            && !OutcastRules.PlayedFromEdge(spell), "R3 PlayTask casts edge card without Outcast or play");
        foreach (int position in new[] { 0, 1, 2 })
        {
            var empty = PlayerDecisionProof.Fixture(); var player = empty.Player1;
            PoolPilotProof.ClassDeck(player);
            IPlayable source = null;
            for (int i = 0; i < 3; i++)
            {
                var card = Add(player, i == position ? id : "CS2_182");
                if (i == position) source = card;
            }
            PlayEntity(empty, player, source);
            int draws = position == 1 ? 1 : 2;
            Core2026Proof.Check(player.Hero.Fatigue == draws && player.Hero.Damage == draws * (draws + 1) / 2
                && player.DeckZone.Count == 0 && player.HandZone.Count == 2,
                $"empty deck position {position}: {draws} independent fatigue hits");
        }
    });
}
