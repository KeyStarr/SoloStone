using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Auras
{
    // Module usage: Power.Aura = new HandAdjacencyCostAura(1); activates on entering hand.
    public sealed class HandAdjacencyCostAura : IAura
    {
        private readonly int _amount;
        private readonly string _enchantmentCardId;
        private IPlayable _owner;
        private readonly List<int> _targets = new List<int>();
        private readonly Dictionary<int, int> _presentations = new Dictionary<int, int>();
        public IPlayable Owner => _owner;
        public HandAdjacencyCostAura(int amount, string enchantmentCardId = null)
        { _amount = amount; _enchantmentCardId = enchantmentCardId; }

        public void Activate(IPlayable owner)
        {
            var instance = new HandAdjacencyCostAura(_amount, _enchantmentCardId) { _owner = owner };
            owner.OngoingEffect = instance;
            owner.Game.Auras.Add(instance);
            instance.Update();
        }

        internal void ApplyCostToProbe(Playable probe)
        {
            var hand = _owner.Controller.HandZone;
            if (probe.Controller == _owner.Controller && _owner.Zone == hand && _owner.ZonePosition == hand.Count - 1)
                CostRules.Manager(probe).AddCostAura(EffectOperator.ADD, _amount);
        }

        public void Update()
        {
            var hand = _owner.Controller.HandZone;
            if (_owner.Zone != hand || !hand.Contains(_owner)) { Remove(); return; }
            int position = _owner.ZonePosition;
            var desired = new List<int>(2);
            if (position > 0) desired.Add(hand[position - 1].Id);
            if (position + 1 < hand.Count) desired.Add(hand[position + 1].Id);
            foreach (int id in _targets.Where(id => !desired.Contains(id)).ToArray()) DeApply(id);
            foreach (int id in desired)
            {
                var target = (Playable)_owner.Game.IdEntityDic[id];
                var manager = CostRules.Manager(target);
                if (!manager.AdjacencySources.Contains(_owner.Id))
                {
                    manager.AddCostAura(EffectOperator.ADD, _amount);
                    manager.AdjacencySources.Add(_owner.Id);
                }
                if (_enchantmentCardId == null) continue;
                if (_presentations.TryGetValue(id, out int actorId)
                    && _owner.Game.IdEntityDic.TryGetValue(actorId, out var entity) && entity is Enchantment actor
                    && ReferenceEquals(actor.Target, target) && (target.AppliedEnchantments?.Contains(actor) ?? false)) continue;
                RetirePresentation(id);
                // Presentation has no cost recipe; the aura alone owns the numeric effect.
                _presentations[id] = Enchantment.GetInstance(target.Controller, _owner, target, Cards.FromId(_enchantmentCardId)).Id;
            }
            _targets.Clear(); _targets.AddRange(desired);
        }

        private void DeApply(int id)
        {
            if (_owner.Game.IdEntityDic.TryGetValue(id, out var target) && target is Playable p)
                if (p._costManager?.AdjacencySources.Remove(_owner.Id) ?? false)
                    p._costManager.RemoveCostAura(EffectOperator.ADD, _amount);
            RetirePresentation(id);
        }

        internal bool OwnsPresentation(Enchantment actor)
            => _presentations.TryGetValue(actor.Target.Id, out int id) && id == actor.Id;

        private void RetirePresentation(int id)
        {
            if (!_presentations.TryGetValue(id, out int actorId)) return;
            if (_owner.Game.IdEntityDic.TryGetValue(actorId, out var entity) && entity is Enchantment actor
                && (actor.Target.AppliedEnchantments?.Contains(actor) ?? false)) actor.Remove();
            _presentations.Remove(id);
        }

        public void Remove()
        {
            foreach (int id in _targets) DeApply(id);
            _targets.Clear();
            _owner.Game.Auras.Remove(this);
            if (_owner.OngoingEffect == this) _owner.OngoingEffect = null;
        }

        public void Clone(IPlayable clone)
        {
            if (clone.Id != _owner.Id || clone.Game == _owner.Game) { Activate(clone); return; }
            var instance = new HandAdjacencyCostAura(_amount, _enchantmentCardId) { _owner = clone };
            instance._targets.AddRange(_targets);
            foreach (var pair in _presentations) instance._presentations.Add(pair.Key, pair.Value);
            clone.OngoingEffect = instance;
            clone.Game.Auras.Add(instance);
        }
    }
}
