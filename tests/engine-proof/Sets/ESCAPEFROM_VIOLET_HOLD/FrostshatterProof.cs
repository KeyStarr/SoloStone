using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FrostshatterProof
{
    private const string Leans = "Unpinned_HandCastScope Unpinned_HandUpgradeTiming Unpinned_HandFresh Unpinned_HandUpgradeCost Unpinned_HandProjection";
    internal static void Run() => StandardSetsProof.Case("JAIL_803", (g, p, id) => {
        foreach (string form in new[] { id, "JAIL_803t" })
        {
            var fatigueGame = HeroDivineShieldProof.NewGame(true); var owner = fatigueGame.CurrentPlayer;
            EmeraldDreamBatch3Proof.EmptyDeck(owner); owner.Hero.Fatigue = 0; owner.Hero.Damage = 0;
            Play(fatigueGame, owner, form, owner.Opponent.Hero);
            Core2026Proof.Check(owner.Hero.Fatigue == 2 && owner.Hero.Damage == 3 && owner.Opponent.Hero.IsFrozen,
                form + ": two independent empty-deck fatigue hits (1+2), after Freeze");
        }
        var token = Cards.FromId("JAIL_803t");
        Core2026Proof.Check(token.Implemented && token.AssetId == 126411 && token.Type == CardType.MINION && token.Cost == 5 &&
            token.ATK == 5 && token.Health == 5 && token.IsRace(Race.ELEMENTAL) && token[GameTag.BATTLECRY] == 1 &&
            token.Text == "<b>Battlecry:</b> <b>Freeze</b> an enemy. Draw two cards.", "exact Frostshatter Elemental closure");
        Play(g, p, "GAME_005");
        var held = Add(p, id); int entity = held.Id, slot = held.ZonePosition;
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, Leans + ": no prior-cast credit");
        var friendly = (Minion)Play(g, p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "minion play excluded");
        var options = p.Options().Where(t => t.Source?.Id == held.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == enemy.Id) && options.Any(t => t.Target?.Id == p.Opponent.Hero.Id) &&
            options.All(t => t.Target?.Controller == p.Opponent), "spell requires enemy character, never friendly");
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "opponent casts excluded");
        Play(g, p, "GAME_005"); Play(g, p, "GAME_005");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && held.Card.Id == id, Leans + ": counter before third cast");
        Effects.ReduceCost(2).ApplyTo((Playable)held);
        Core2026Proof.Check(held.Cost == 3 && held[GameTag.COST] == 3, "held cost visible before upgrade");
        var copy = Generic.Copy(p, p.Hero, held, Zone.HAND);
        Core2026Proof.Check(copy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, Leans + ": gameplay copy fresh");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            var owner = next.Player1; int deck = owner.DeckZone.Count, hand = owner.HandZone.Count;
            Play(next, owner, "GAME_005");
            var upgraded = next.IdEntityDic[entity];
            Core2026Proof.Check(upgraded is Minion && upgraded.Card == token && upgraded.ZonePosition == slot && upgraded.Cost == 5 &&
                upgraded[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && !ReferenceEquals(upgraded, held) && owner.DeckZone.Count == deck &&
                owner.HandZone.Count == hand && !owner.Opponent.Hero.IsFrozen, Leans + ": same-id upgrade resets cost, no Battlecry at upgrade");
            Core2026Proof.Check(next.IdEntityDic[copy.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "copy counter independent");
            var choices = owner.Options().Where(t => t.Source?.Id == entity && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
            Core2026Proof.Check(choices.Any(t => t.Target?.Id == owner.Opponent.Hero.Id) && choices.All(t => t.Target?.Controller == owner.Opponent), "Battlecry enemy character targets");
            PlayEntity(next, owner, upgraded, (Minion)next.IdEntityDic[enemy.Id]);
            var minion = (Minion)upgraded;
            Core2026Proof.Check(((Minion)next.IdEntityDic[enemy.Id]).IsFrozen && !((Minion)next.IdEntityDic[friendly.Id]).IsFrozen &&
                owner.DeckZone.Count == deck - 2 && owner.HandZone.Count == hand + 1 && minion.AttackDamage == 5 && minion.BaseHealth == 5,
                "Battlecry freezes enemy then draws exactly two");
            minion.Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(minion.IsSilenced && minion[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "upgraded minion ordinary Silence, no counter");
            Generic.ReturnToHandBlock(owner, minion);
            Core2026Proof.Check(minion.Card.Id == "JAIL_803t" && minion.Zone == owner.HandZone, "bounce retains elemental identity");
        }
        StandardSetsProof.ParkBoard(p);
        PoolPilotProof.EmptyHand(p);
        var spell = Add(p, id); spell[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        int before = p.DeckZone.Count;
        PlayEntity(g, p, spell, p.Opponent.Hero);
        Core2026Proof.Check(spell is Spell && spell.Card.Id == id && spell.Zone == p.GraveyardZone && p.Opponent.Hero.IsFrozen &&
            p.DeckZone.Count == before - 2 && p.HandZone.Count == 2, Leans + ": self-cast freezes hero, draws two, never upgrades itself");
        PoolPilotProof.EmptyHand(p);
        for (int i = 0; i < 9; i++) Add(p, "CS2_182");
        spell = Add(p, id); before = p.DeckZone.Count;
        PlayEntity(g, p, spell, enemy);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == before - 2, "spell draws through one free slot; second card burns");
        PoolPilotProof.EmptyHand(p);
        for (int i = 0; i < 9; i++) Add(p, "CS2_182");
        var full = Add(p, "JAIL_803t"); before = p.DeckZone.Count;
        PlayEntity(g, p, full, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == before - 2, "Battlecry hand overflow burns second draw");
        StandardSetsProof.ParkBoard(p);
        PoolPilotProof.EmptyHand(p);
        LocationProofs.Location(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        full = Add(p, "JAIL_803t");
        Core2026Proof.Check(p.BoardZone.IsFull && !p.Options().Any(t => t.Source?.Id == full.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "location plus six minions block elemental play");
        PoolPilotProof.EmptyHand(p);
        held = Add(p, id);
        var automatic = (Spell)Entity.FromCard(p, Cards.FromId("GAME_005"));
        Generic.CastSpell(p, g, automatic, null, 0);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, Leans + ": effect casts excluded");
        End(g); Play(g, p.Opponent, "EX1_287"); End(g);
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, Leans + ": countered casts excluded");
        held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        p.DeckZone.Add(p.HandZone.Remove(held));
        Play(g, p, id, enemy);
        Core2026Proof.Check(held.Zone == p.HandZone && held.Card.Id == id && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            Leans + ": new residence resets progress; drawing during spell body earns no creator-cast credit");
    });
}
