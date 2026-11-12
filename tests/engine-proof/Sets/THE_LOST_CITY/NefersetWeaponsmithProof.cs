using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class NefersetWeaponsmithProof
{
    // repeat-effects/brann-opu-weaponsmith.yaml: STILL-UNPINNED, owner assumption (D export, C wiki/E discussion).
    internal static void Run() => StandardSetsProof.Case("TLC_516", Unpinned_NefersetWeaponsmith);

    private static IPlayable[] Weapons(Game g, Controller p, IPlayable source, int count, int bonus)
    {
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, LostCityPoolProof.Filter(source.Card.Id), source.Card.AssetId);
        var made = p.HandZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        Core2026Proof.Check(made.Length == count, "exact weapon acquisition count");
        foreach (var card in made)
            Core2026Proof.Check(card is Weapon w && pool.Any(c => c.Id == card.Card.Id) && card.Card.Type == CardType.WEAPON
                && card.Card.Class != CardClass.NEUTRAL && !CardClassRules.IsAllowedInDeck(card.Card, p.HeroClass)
                && card.Card.Id != source.Card.Id && w.AttackDamage == w.Card.ATK + bonus
                && w.Durability == w.Card[GameTag.DURABILITY] && card.Controller == p && card.Cost == card.Card.Cost
                && (card.AppliedEnchantments?.Count(e => e.Card.Id == "TLC_516e") ?? 0) == (bonus == 0 ? 0 : 1),
                "weapon filter, exact identity/stats, persistent +2 enchantment and owner");
        return made;
    }

    private static void Unpinned_NefersetWeaponsmith(Game g, Controller p, string id)
    {
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }).ValidateRegistered();
        Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, LostCityPoolProof.Filter(id), Cards.FromId(id).AssetId).Count >= 3,
            "Rogue other-class weapon pool admitted");
        p.IsComboActive = false;
        var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Weapons(g, p, source, 1, 0);

        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        Play(g, p, "GAME_005"); source = Play(g, p, id);
        var weapon = Weapons(g, p, source, 1, 2).Single();
        ((Minion)source).Silence(); Kill(g, (Minion)source);
        Core2026Proof.Check(((Weapon)weapon).AttackDamage == weapon.Card.ATK + 2, "source Silence/death does not remove acquired buff");
        End(g); End(g);
        Core2026Proof.Check(((Weapon)weapon).AttackDamage == weapon.Card.ATK + 2, "weapon buff survives turn end");
        PlayEntity(g, p, weapon);
        // Bladed Gauntlet overwrites weapon modifiers with Armor while equipped.
        int equippedAttack = weapon.Card.Id == "CORE_LOOT_044" ? p.Hero.Armor : weapon.Card.ATK + 2;
        Core2026Proof.Check(p.Hero.Weapon == weapon && p.Hero.Weapon.AttackDamage == equippedAttack
            && weapon.AppliedEnchantments.Count(e => e.Card.Id == "TLC_516e") == 1,
            "equipping retains Combo enchantment and respects weapon's own Attack rule");

        ParkBoard(p); PoolPilotProof.EmptyHand(p); Summon(p, "LOE_077");
        p.IsComboActive = false; source = Play(g, p, id); Weapons(g, p, source, 2, 0);
        ParkBoard(p); PoolPilotProof.EmptyHand(p); Summon(p, "LOE_077");
        Play(g, p, "GAME_005"); source = Play(g, p, id); Weapons(g, p, source, 2, 2);

        ParkBoard(p); PoolPilotProof.EmptyHand(p); Summon(p, "LOE_077"); PoolPilotProof.FillHand(p, 9);
        source = Play(g, p, id); Weapons(g, p, source, 1, 2);
        Core2026Proof.Check(p.HandZone.IsFull, "Brann at one free hand slot grants only one weapon");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        PoolPilotProof.Pool(g, id, 0, () => {
            var rng = g.Random.CaptureState(); source = Play(g, p, id);
            Weapons(g, p, source, 0, 2);
            Core2026Proof.Check(rng.Equals(g.Random.CaptureState()), "empty pool: no weapon or RNG");
        });
        foreach (int count in new[] { 1, 2 })
        {
            PoolPilotProof.Pool(g, id, count, () => { source = Play(g, p, id); Weapons(g, p, source, 1, 2); });
            ParkBoard(p); PoolPilotProof.EmptyHand(p);
        }
    }
}
