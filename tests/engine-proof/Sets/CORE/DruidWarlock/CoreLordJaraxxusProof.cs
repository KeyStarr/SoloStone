using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz; retain existing current-text power.
internal static class CoreLordJaraxxusProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_323", (g, p, id) => {
        p.Hero.Damage=11; p.Hero.Armor=3; var location=LocationProofs.Location(p); var left=Summon(p,"CS2_182");
        Play(g,p,id);
        Core2026Proof.Check(p.Hero.Card.Id==id && p.Hero.Health==19 && p.Hero.Armor==8,"hero replacement preserves current Health and adds five Armor");
        var weapon=p.Hero.Weapon;
        Core2026Proof.Check(weapon.Card.Id=="EX1_323w" && weapon.Card.AssetId==1660 && weapon.AttackDamage==3 && weapon.Durability==8,"exact 3/8 Blood Fury equipped");
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id=="EX1_tk33" && p.Hero.HeroPower.Card.AssetId==1178 && p.Hero.HeroPower.Cost==2,"exact Inferno power");
        p.UsedMana=0; Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)),"Inferno accepted");
        var token=p.BoardZone.Single(m=>m.Card.Id=="EX1_tk34");
        Core2026Proof.Check(token.Card.AssetId==1143 && token.AttackDamage==6 && token.Health==6 && token.IsRace(Race.DEMON),"exact 6/6 Infernal token");
        Core2026Proof.Check(p.BoardZone.Slots.Select(m=>m.Id).SequenceEqual(new[]{location.Id,left.Id,token.Id}),"hero power summon appended after physical slots");
        while(!p.BoardZone.IsFull) Summon(p,"CS2_182"); int count=p.BoardZone.SlotCount;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p,p.Opponent.Hero)),"Blood Fury attack accepted");
        Core2026Proof.Check(weapon.Durability==7 && p.Opponent.Hero.Damage==3,"attack consumes original weapon durability");
        p.Hero.Damage=0; Play(g,p,id);
        Core2026Proof.Check(p.BoardZone.SlotCount==count && p.Hero.Health==30 && p.Hero.Armor==13,"hero playable with full board; no healing above max; Armor additive");
        Core2026Proof.Check(p.Hero.Weapon.Id!=weapon.Id && p.Hero.Weapon.Card.Id=="EX1_323w" && p.Hero.Weapon.Durability==8
            && weapon.Zone==p.GraveyardZone,"new Blood Fury restores eight durability and removes old weapon");
        p.UsedMana=0; p.Hero.HeroPower.IsExhausted=false;
        Core2026Proof.Check(!p.Options().Any(t=>t.PlayerTaskType==PlayerTaskType.HERO_POWER),"Inferno unavailable on seven occupied slots");
        Core2026Proof.Check(p.Opponent.Hero.Card.Id!="CORE_EX1_323" && p.Opponent.Hero.Weapon==null && p.Opponent.Hero.Armor==0,"opponent hero untouched");
    }, legacyPoolMember: true);
}
