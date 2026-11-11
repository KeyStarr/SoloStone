using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class WishoftheNewMoonProof
{
    // Pinned rule, tiers C/E: wiki Notes (only spells cast while held), independently corroborated by
    // Vicious Syndicate, Emerald Dream Preview p8 (2025-03): held card upgrades after three other spells.
    // https://hearthstone.wiki.gg/wiki/Wish_of_the_New_Moon
    // https://www.vicioussyndicate.com/the-comprehensive-into-the-emerald-dream-preview/8/
    // Wiki patch history through build 37.0 lists only its 32.0 introduction; no intervening rule change found.
    internal static void Run() => StandardSetsProof.Case("EDR_460", (g, p, id) => {
        Play(g, p, "GAME_005");
        var held = Add(p, id);
        var inDeck = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
        var aside = Entity.FromCard(p, Cards.FromId(id), zone: p.SetasideZone);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && !held.HasLifeSteal,
            "fresh held card displays three remaining spells; earlier cast excluded");
        Play(g, p, "CS2_182");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "minion play does not progress upgrade");
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held.Card.Id == id && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && !held.HasLifeSteal,
            "first friendly spell updates client-visible remaining count only in hand");
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && inDeck[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3
            && aside[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "opponent spell, turn changes, deck and setaside do not progress");
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held.Card.Id == id && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && !held.HasLifeSteal,
            "two spells remain below three-spell threshold");

        var clone = g.CloneExact(history: true);
        var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var next in new[] { clone, loaded })
        {
            var owner = next.Player1;
            var upgraded = (IPlayable)next.IdEntityDic[held.Id];
            int position = upgraded.ZonePosition;
            Play(next, owner, "GAME_005");
            Core2026Proof.Check(upgraded.Card.Id == "EDR_460t" && upgraded.Card.AssetId == 114297 && upgraded.Card.Implemented
                && upgraded.Cost == 3 && upgraded.HasLifeSteal && upgraded[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0
                && upgraded.Controller == owner && upgraded.Zone == owner.HandZone && upgraded.ZonePosition == position
                && upgraded.ActivatedTrigger == null, "third spell transforms same held entity into exact Full Moon and removes upgrade trigger");
            var enemy = Summon(owner.Opponent, "CS2_186");
            var friendly = Summon(owner, "CS2_186");
            TargetRules(next, owner, "EDR_460t", enemy, friendly, false);
            owner.Hero.Damage = 15;
            PlayEntity(next, owner, upgraded, enemy);
            Core2026Proof.Check(enemy.Damage == 6 && owner.Hero.Damage == 9,
                "upgraded spell deals six and heals six");
            Play(next, owner, "GAME_005");
            Core2026Proof.Check(upgraded.Zone == owner.GraveyardZone && upgraded.ActivatedTrigger == null,
                "played source no longer listens for casts");
            Summon(owner, "EX1_012");
            Play(next, owner, "EDR_460t", friendly);
            Core2026Proof.Check(friendly.Zone == owner.GraveyardZone && owner.Hero.Damage == 2,
                "friendly target allowed; Spell Damage increases damage and Lifesteal to seven");
            var shielded = Summon(owner.Opponent, "CS2_186"); shielded.HasDivineShield = true;
            Play(next, owner, "EDR_460t", shielded);
            Core2026Proof.Check(shielded.Damage == 0 && !shielded.HasDivineShield && owner.Hero.Damage == 2,
                "shield prevents damage and Lifesteal");
            shielded.IsImmune = true;
            var spell = Add(owner, "EDR_460t"); owner.UsedMana = 0;
            Core2026Proof.Check(!owner.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == shielded.Id),
                "immune enemy cannot be targeted");
        }
        Core2026Proof.Check(Canonical.State(clone) == Canonical.State(loaded) && held.Card.Id == id
            && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "clone/snapshot resume identical; original held progress isolated");

        var target = Summon(p.Opponent, "CS2_186"); var friend = Summon(p, "CS2_186");
        TargetRules(g, p, id, target, friend, false);
        p.Hero.Damage = 15;
        PlayEntity(g, p, held, target);
        Core2026Proof.Check(target.Damage == 6 && p.Hero.Damage == 15 && held.Card.Id == id
            && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && held.ActivatedTrigger == null,
            "casting at two spells deals six without upgrading itself or healing");
        var fresh = Add(p, id);
        Core2026Proof.Check(fresh[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && !fresh.HasLifeSteal,
            "separate entity starts at three despite prior casts");
        PoolPilotProof.EmptyHand(p); ClearBoard(g, p); ClearBoard(g, p.Opponent);
        var blocked = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id)
            && !g.Process(PlayCardTask.Any(p, blocked)) && blocked.Zone == p.HandZone && p.UsedMana == 0,
            "without minions spell cannot target a hero or be played untargeted");
        PoolPilotProof.EmptyHand(p); EmeraldDreamBatch3Proof.EmptyDeck(p);
        var drawn = Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, "CS2_023");
        Core2026Proof.Check(drawn.Zone == p.HandZone && drawn[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "spell drawing Wish does not count as a spell cast while it was held");
    });
}
