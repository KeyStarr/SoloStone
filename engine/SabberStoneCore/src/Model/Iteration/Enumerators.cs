using System;
using System.Collections;
using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;

// Struct enumerators for direct foreach. They live outside the namespaces SnapshotSchema discovers
// (nested types count), so adding them never renumbers published snapshot type IDs.
namespace SabberStoneCore.Model.Iteration
{
	/// <summary>
	/// Allocation-free replacement of the former yield enumerator of <see cref="LimitedZone{T}"/>:
	/// the entity array is captured at the first MoveNext, the count is read live.
	/// </summary>
	public struct LimitedZoneEnumerator<T> : IEnumerator<T> where T : IPlayable
	{
		private readonly LimitedZone<T> _zone;
		private T[] _entities;
		private int _index;
		private bool _done;
		private T _current;

		internal LimitedZoneEnumerator(LimitedZone<T> zone)
		{
			_zone = zone;
			_entities = null;
			_index = -1;
			_done = false;
			_current = default;
		}

		public bool MoveNext()
		{
			if (_done)
				return false;
			if (_entities == null)
				_entities = _zone.Backing;
			int i = _index + 1;
			if (i < _zone.Count)
			{
				_index = i;
				_current = _entities[i];
				return true;
			}
			_done = true;
			_current = default;
			return false;
		}

		public T Current => _current;
		object IEnumerator.Current => _current;
		public void Reset() => throw new NotSupportedException();
		public void Dispose() { }
	}

	/// <summary>Enumerates a <see cref="SecretZone"/>: secrets, then the quest, modern quests and auras.</summary>
	public struct SecretZoneEnumerator : IEnumerator<Spell>
	{
		private readonly SecretZone _zone;
		private Spell[] _entities;
		private int _index;
		private int _phase;
		private List<Spell>.Enumerator _list;
		private Spell _current;

		internal SecretZoneEnumerator(SecretZone zone)
		{
			_zone = zone;
			_entities = null;
			_index = 0;
			_phase = 0;
			_list = default;
			_current = default;
		}

		public bool MoveNext()
		{
			switch (_phase)
			{
				case 0:
					if (_entities == null)
						_entities = _zone.Backing;
					if (_index < _zone.Count)
					{
						_current = _entities[_index++];
						return true;
					}
					_phase = 1;
					goto case 1;
				case 1:
					_phase = 2;
					if (_zone.Quest != null)
					{
						_current = _zone.Quest;
						return true;
					}
					goto case 2;
				case 2:
					_phase = 3;
					if (_zone.ModernQuestList != null)
					{
						_list = _zone.ModernQuestList.GetEnumerator();
						_phase = 4;
						goto case 4;
					}
					goto case 3;
				case 4:
					if (_list.MoveNext())
					{
						_current = _list.Current;
						return true;
					}
					_list.Dispose();
					_phase = 3;
					goto case 3;
				case 3:
					_phase = 5;
					if (_zone.AuraList != null)
					{
						_list = _zone.AuraList.GetEnumerator();
						_phase = 6;
						goto case 6;
					}
					goto default;
				case 6:
					if (_list.MoveNext())
					{
						_current = _list.Current;
						return true;
					}
					_list.Dispose();
					_phase = 5;
					goto default;
				default:
					_current = default;
					return false;
			}
		}

		public Spell Current => _current;
		object IEnumerator.Current => _current;
		public void Reset() => throw new NotSupportedException();
		public void Dispose() { }
	}

	/// <summary>The hero powers of a controller, evaluated lazily like the former yield property.</summary>
	public readonly struct HeroPowerSequence : IEnumerable<HeroPower>
	{
		private readonly Controller _controller;

		internal HeroPowerSequence(Controller controller) { _controller = controller; }

		public HeroPowerEnumerator GetEnumerator() => new HeroPowerEnumerator(_controller);
		IEnumerator<HeroPower> IEnumerable<HeroPower>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public struct HeroPowerEnumerator : IEnumerator<HeroPower>
	{
		private readonly Controller _controller;
		private int _state;
		private HeroPower _current;

		internal HeroPowerEnumerator(Controller controller)
		{
			_controller = controller;
			_state = 0;
			_current = null;
		}

		public bool MoveNext()
		{
			switch (_state)
			{
				case 0:
					_state = 1;
					_current = _controller.Hero.HeroPower;
					return true;
				case 1:
					_state = 2;
					if (_controller.SecondaryHeroPower is HeroPower second && second != _controller.Hero.HeroPower)
					{
						_current = second;
						return true;
					}
					goto default;
				default:
					_state = 2;
					_current = null;
					return false;
			}
		}

		public HeroPower Current => _current;
		object IEnumerator.Current => _current;
		public void Reset() => throw new NotSupportedException();
		public void Dispose() { }
	}

	/// <summary>Enumerates the used slots of an <see cref="EntityData"/> bucket array captured at creation.</summary>
	internal struct EntityDataEnumerator : IEnumerator<KeyValuePair<GameTag, int>>
	{
		private readonly int[] _buckets;
		private int _index;
		private KeyValuePair<GameTag, int> _current;

		internal EntityDataEnumerator(int[] buckets)
		{
			_buckets = buckets;
			_index = -2;
			_current = default;
		}

		public bool MoveNext()
		{
			int[] buckets = _buckets;
			for (int i = _index + 2; i < buckets.Length; i += 2)
			{
				if (buckets[i] > 0)
				{
					_index = i;
					_current = new KeyValuePair<GameTag, int>((GameTag)buckets[i], buckets[i + 1]);
					return true;
				}
			}
			_index = buckets.Length;
			_current = default;
			return false;
		}

		public KeyValuePair<GameTag, int> Current => _current;
		object IEnumerator.Current => _current;
		public void Reset() => throw new NotSupportedException();
		public void Dispose() { }
	}
}
