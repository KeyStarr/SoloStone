#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCore.Kettle
	#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
{
	//message AllOptions
	//{
	//    enum PacketID
	//    {
	//        ID_a74d = 14;
	//    }
	//    required int32 id = 1;
	//    repeated Option options = 2;
	//}
	public class PowerAllOptions
	{
		private static int _index = 1;
		public int Index { get; set; }

		public List<PowerOption> PowerOptionList { get; set; }

		public List<PlayerTask> PlayerTaskList { get; set; }

		public PowerAllOptions(Game game)
		{
			Index = _index++;
			game.AllOptionsMap.Add(Index, this);
			PowerOptionList = new List<PowerOption>();
		}

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"PowerOption index={Index}");
			for (int i = 0; i < PowerOptionList.Count; i++)
			{
				str.Append($"option {i} {PowerOptionList[i].Print()}");
			}

			return str.ToString();
		}
	}

	public class PowerOptionsBuilder
	{
		public static PowerAllOptions AllOptions(Game game, List<PlayerTask> list)
		{
			var result = new PowerAllOptions(game)
			{
				PlayerTaskList = list
			};

			foreach (PlayerTask option in list.Where(p => p.PlayerTaskType == PlayerTaskType.END_TURN))
			{
				result.PowerOptionList.Add(new PowerOption { OptionType = OptionType.END_TURN });
			}

			var playCards = list.Where(p => p.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToList();
			foreach (int sourceId in playCards.Select(p => p.Source.Id).Distinct())
			{
				IEnumerable<PlayerTask> targets = playCards.Where(p => p.Source.Id == sourceId && p.ActionTarget != null && p.ChooseOne == 0);
				var mainOption = new PowerOption
				{
					OptionType = OptionType.POWER,
					MainOption = new PowerSubOption
					{
						EntityId = sourceId,
						Targets = targets.Any() ? targets.Select(p => p.ActionTarget.Id).Distinct().ToList() : null
					}
				};
				result.PowerOptionList.Add(mainOption);

				for (int i = 1; i < 3; i++)
				{
					var subOptions = playCards.Where(p => p.Source.Id == sourceId && p.ChooseOne == i).ToList();
					if (subOptions.Any())
					{
						int refCardId = subOptions.First().Source.ChooseOnePlayables[i - 1].Id;
						var refCardTargets = subOptions.Where(p => p.ActionTarget != null).Select(p => p.ActionTarget).ToList();
						mainOption.SubOptions.Add(new PowerSubOption
						{
							EntityId = refCardId,
							Targets = refCardTargets.Any() ? refCardTargets.Select(p => p.Id).ToList() : null
						});
					}
				}
			}

			foreach (PlayerTask trade in list.Where(p => p.PlayerTaskType == PlayerTaskType.TRADE_CARD))
			{
				result.PowerOptionList.Add(new PowerOption
				{
					OptionType = OptionType.POWER,
					MainOption = new PowerSubOption
					{
						EntityId = trade.Source.Id,
						PowerKeyword = (int)GameTag.TRADEABLE
					}
				});
			}

            foreach (PlayerTask prepare in list.Where(p => p.PlayerTaskType == PlayerTaskType.PREPARE_CARD))
                result.PowerOptionList.Add(new PowerOption { OptionType = OptionType.POWER,
                    MainOption = new PowerSubOption { EntityId = prepare.Source.Id, PowerKeyword = (int)GameTag.PREPARE } });

			var minionAttacks = list.Where(p => p.PlayerTaskType == PlayerTaskType.MINION_ATTACK).ToList();
			foreach (int sourceId in minionAttacks.Select(p => p.Source.Id).Distinct())
			{
				IEnumerable<PlayerTask> targets = minionAttacks.Where(p => p.Source.Id == sourceId);

				result.PowerOptionList.Add(
					new PowerOption
					{
						OptionType = OptionType.POWER,
						MainOption = new PowerSubOption
						{
							EntityId = sourceId,
							Targets = targets.Select(p => p.ActionTarget.Id).Distinct().ToList()
						}
					});
			}

			var heroAttacks = list.Where(p => p.PlayerTaskType == PlayerTaskType.HERO_ATTACK).ToList();
			if (heroAttacks.Any())
			{
				IEnumerable<PlayerTask> targets = heroAttacks.Where(p => p.ActionTarget != null);
				var mainOption = new PowerOption
				{
					OptionType = OptionType.POWER,
					MainOption = new PowerSubOption
					{
						EntityId = heroAttacks.First().Controller.Hero.Id,
						Targets = targets.Any() ? targets.Select(p => p.ActionTarget.Id).Distinct().ToList() : null
					}
				};
				result.PowerOptionList.Add(mainOption);
			}

			var heroPowers = list.Where(p => p.PlayerTaskType == PlayerTaskType.HERO_POWER).ToList();
			foreach (var group in heroPowers.GroupBy(p => p.Source?.Id ?? p.Controller.Hero.HeroPower.Id))
			{
				IEnumerable<PlayerTask> targets = group.Where(p => p.ActionTarget != null);
				var mainOption = new PowerOption
				{
					OptionType = OptionType.POWER,
					MainOption = new PowerSubOption
					{
						EntityId = group.Key,
						Targets = targets.Any() ? targets.Select(p => p.ActionTarget.Id).Distinct().ToList() : null
					}
				};
				result.PowerOptionList.Add(mainOption);
			}

            foreach (var group in list.OfType<ActivateLocationTask>().GroupBy(task => task.Source.Id))
                result.PowerOptionList.Add(new PowerOption { OptionType = OptionType.POWER,
                    MainOption = new PowerSubOption { EntityId = group.Key,
                        PowerKeyword = Actions.QuestRules.IsRift(group.First().Source) ? 4089 : 0,
                        Targets = group.Where(task => task.ActionTarget != null).Select(task => task.ActionTarget.Id).Distinct().ToList() } });

            foreach (var activation in list.OfType<ActivateOldGodTask>())
                result.PowerOptionList.Add(new PowerOption { OptionType = OptionType.POWER,
                    MainOption = new PowerSubOption { EntityId = activation.Source.Id } });
			return result;
		}
	}

	//message Option
	//{
	//    enum Type
	//    {
	//        PASS = 1;
	//        END_TURN = 2;
	//        POWER = 3;
	//    }
	//    required Type type = 1;
	//    optional SubOption main_option = 2;
	//    repeated SubOption sub_options = 3;
	//}
	public class PowerOption
	{
		public OptionType OptionType { get; set; }

		public PowerSubOption MainOption { get; set; }

		public List<PowerSubOption> SubOptions { get; set; } = new List<PowerSubOption>();

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"type={OptionType} mainEntity=[{MainOption?.EntityId}]");
			for (int i = 0; i < MainOption?.Targets?.Count; i++)
			{
				str.AppendLine($" target {i} entity=[{MainOption.Targets[i]}] ");
			}
			for (int i = 0; i < SubOptions.Count; i++)
			{
				PowerSubOption subOption = SubOptions[i];
				str.AppendLine($" subOption {i} entity=[{subOption.EntityId}] ");
				for (int j = 0; j < subOption.Targets?.Count; j++)
				{
					str.AppendLine($"  target {j} entity=[{subOption.Targets[j]}] ");
				}
			}
			return str.ToString();
		}
	}

	//message SubOption
	//{
	//    required int32 id = 1;
	//    repeated int32 targets = 3 [packed=true];
	//}
	public class PowerSubOption
	{
		public int EntityId { get; set; }
		public List<int> Targets { get; set; }
		public int PowerKeyword { get; set; }
	}
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
