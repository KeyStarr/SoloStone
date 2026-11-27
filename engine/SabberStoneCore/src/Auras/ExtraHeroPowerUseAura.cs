using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Auras
{
    /// <summary>
    /// Garrison Commander grants a second Hero Power use. Only readiness granted by this aura ends with the last source.
    /// </summary>
    public sealed class ExtraHeroPowerUseAura : IAura
    {
        private IPlayable _owner;
        public IPlayable Owner => _owner;

        public void Activate(IPlayable owner)
        {
            var instance = new ExtraHeroPowerUseAura { _owner = owner };
            owner.OngoingEffect = instance;
            owner.Game.Auras.Add(instance);
            instance.Update();
        }

        public void Update()
        {
            if (_owner.Zone?.Type != Zone.PLAY || _owner is Minion m && (m.IsSilenced || m.IsDormant))
            { Remove(); return; }
            // A mortally wounded source can still be rescued before death creation.
            if (_owner.ToBeDestroyed) return;
            var c = _owner.Controller;
            var power = c.Hero?.HeroPower;
            if (power == null || power.IsPassiveHeroPower || c.Game.CurrentPlayer != c) return;
            if (power.IsExhausted && c.HeroPowerActivationsThisTurn == 1) power.ReadyForExtraUse();
        }

        public void Remove()
        {
            var c = _owner.Controller;
            _owner.Game.Auras.Remove(this);
            if (_owner.OngoingEffect == this) _owner.OngoingEffect = null;
            if (!c.BoardZone.AnyInteractable(m => m.OngoingEffect is ExtraHeroPowerUseAura && !m.IsSilenced && !m.IsDormant))
                c.Hero?.HeroPower?.RevokeExtraUse();
        }

        public void Clone(IPlayable clone)
        {
            if (clone.Id != _owner.Id || clone.Game == _owner.Game) { Activate(clone); return; }
            var instance = new ExtraHeroPowerUseAura { _owner = clone };
            clone.OngoingEffect = instance;
            clone.Game.Auras.Add(instance);
        }
    }
}
