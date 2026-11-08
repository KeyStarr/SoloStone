using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Auras
{
    // Module usage: new ConditionalHeroPowerCostAura(maxHandCount: 3, cost: 0).Activate(source).
    public sealed class ConditionalHeroPowerCostAura : IAura
    {
        private readonly int _maxHandCount;
        private readonly int _cost;
        private readonly string _enchantmentCardId;
        private IPlayable _owner;
        private int _targetId;
        private int _presentationId;
        public IPlayable Owner => _owner;
        public ConditionalHeroPowerCostAura(int maxHandCount, int cost, string enchantmentCardId = null)
        { _maxHandCount = maxHandCount; _cost = cost; _enchantmentCardId = enchantmentCardId; }

        public void Activate(IPlayable owner)
        {
            var instance = new ConditionalHeroPowerCostAura(_maxHandCount, _cost, _enchantmentCardId) { _owner = owner };
            owner.OngoingEffect = instance;
            owner.Game.Auras.Add(instance);
            instance.Update();
        }

        private const GameTag SecondaryTarget = (GameTag)31291;
        private const GameTag SecondaryPresentation = (GameTag)31292;
        private void UpdateSecondary(int desired)
        {
            int old = _owner[SecondaryTarget];
            if (old == desired) return;
            if (old != 0 && _owner.Game.IdEntityDic.TryGetValue(old, out var previous) && previous is Playable p)
                p._costManager?.RemoveCostAura(EffectOperator.SET, _cost, _owner.Id);
            if (_owner[SecondaryPresentation] != 0 && _owner.Game.IdEntityDic.TryGetValue(_owner[SecondaryPresentation], out var entity)
                && entity is Enchantment actor && (actor.Target.AppliedEnchantments?.Contains(actor) ?? false)) actor.Remove();
            _owner[SecondaryTarget] = desired; _owner[SecondaryPresentation] = 0;
            if (desired == 0) return;
            var power = (HeroPower)_owner.Game.IdEntityDic[desired];
            CostRules.Manager(power).AddCostAura(EffectOperator.SET, _cost, _owner.Id);
            if (_enchantmentCardId != null)
                _owner[SecondaryPresentation] = Enchantment.GetInstance(power.Controller, _owner, power, Cards.FromId(_enchantmentCardId)).Id;
        }

        public void Update()
        {
            if (_owner.Zone?.Type != Zone.PLAY || _owner.ToBeDestroyed || _owner is Minion m && m.IsSilenced)
            { Remove(); return; }
            UpdateSecondary(_owner.Controller.HandZone.Count <= _maxHandCount ? _owner.Controller.SecondaryHeroPower?.Id ?? 0 : 0);
            var power = _owner.Controller.Hero.HeroPower;
            int desired = _owner.Controller.HandZone.Count <= _maxHandCount ? power.Id : 0;
            if (_targetId == desired) return;
            DeApply();
            UpdateSecondary(_owner.Controller.HandZone.Count <= _maxHandCount ? _owner.Controller.SecondaryHeroPower?.Id ?? 0 : 0);
            _targetId = desired;
            if (desired != 0)
            {
                CostRules.Manager(power).AddCostAura(EffectOperator.SET, _cost, _owner.Id);
                // Presentation has no cost recipe; the aura alone owns the numeric effect.
                if (_enchantmentCardId != null)
                    _presentationId = Enchantment.GetInstance(power.Controller, _owner, power, Cards.FromId(_enchantmentCardId)).Id;
            }
        }

        private void DeApply()
        {
            UpdateSecondary(0);
            if (_targetId != 0 && _owner.Game.IdEntityDic.TryGetValue(_targetId, out var target) && target is Playable p)
                p._costManager?.RemoveCostAura(EffectOperator.SET, _cost, _owner.Id);
            if (_presentationId != 0 && _owner.Game.IdEntityDic.TryGetValue(_presentationId, out var entity)
                && entity is Enchantment actor && (actor.Target.AppliedEnchantments?.Contains(actor) ?? false))
                actor.Remove();
            _targetId = 0;
            _presentationId = 0;
        }

        public void Remove()
        {
            DeApply();
            _owner.Game.Auras.Remove(this);
            if (_owner.OngoingEffect == this) _owner.OngoingEffect = null;
        }

        public void Clone(IPlayable clone)
        {
            if (clone.Id != _owner.Id || clone.Game == _owner.Game) { Activate(clone); return; }
            var instance = new ConditionalHeroPowerCostAura(_maxHandCount, _cost, _enchantmentCardId) { _owner = clone, _targetId = _targetId, _presentationId = _presentationId };
            clone.OngoingEffect = instance;
            clone.Game.Auras.Add(instance);
        }
    }
}
