using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using UnityEngine;

namespace InControl
{
	public abstract class PlayerActionSet
	{
		public BindingSourceType LastInputType;

		private List<PlayerAction> actions = new List<PlayerAction>();

		private List<PlayerOneAxisAction> oneAxisActions = new List<PlayerOneAxisAction>();

		private List<PlayerTwoAxisAction> twoAxisActions = new List<PlayerTwoAxisAction>();

		private Dictionary<string, PlayerAction> actionsByName = new Dictionary<string, PlayerAction>();

		private BindingListenOptions listenOptions = new BindingListenOptions();

		internal PlayerAction listenWithAction;

		public InputDevice Device { get; set; }

		public ReadOnlyCollection<PlayerAction> Actions { get; private set; }

		public ulong UpdateTick { get; protected set; }

		public bool Enabled { get; set; }

		public BindingListenOptions ListenOptions
		{
			get
			{
				return listenOptions;
			}
			set
			{
				listenOptions = value ?? new BindingListenOptions();
			}
		}

		protected PlayerActionSet()
		{
			Actions = new ReadOnlyCollection<PlayerAction>(actions);
			Enabled = true;
			InputManager.AttachPlayerActionSet(this);
		}

		public void Destroy()
		{
			InputManager.DetachPlayerActionSet(this);
		}

		protected PlayerAction CreatePlayerAction(string name)
		{
			PlayerAction playerAction = new PlayerAction(name, this);
			playerAction.Device = Device ?? InputManager.ActiveDevice;
			if (actionsByName.ContainsKey(name))
			{
				throw new InControlException("Action '" + name + "' already exists in this set.");
			}
			actions.Add(playerAction);
			actionsByName.Add(name, playerAction);
			return playerAction;
		}

		protected void ClearActions()
		{
			actions.Clear();
			actionsByName.Clear();
			oneAxisActions.Clear();
		}

		protected PlayerOneAxisAction CreateOneAxisPlayerAction(PlayerAction negativeAction, PlayerAction positiveAction)
		{
			PlayerOneAxisAction playerOneAxisAction = new PlayerOneAxisAction(negativeAction, positiveAction);
			oneAxisActions.Add(playerOneAxisAction);
			return playerOneAxisAction;
		}

		protected PlayerTwoAxisAction CreateTwoAxisPlayerAction(PlayerAction negativeXAction, PlayerAction positiveXAction, PlayerAction negativeYAction, PlayerAction positiveYAction)
		{
			PlayerTwoAxisAction playerTwoAxisAction = new PlayerTwoAxisAction(negativeXAction, positiveXAction, negativeYAction, positiveYAction);
			twoAxisActions.Add(playerTwoAxisAction);
			return playerTwoAxisAction;
		}

		internal void Update(ulong updateTick, float deltaTime)
		{
			InputDevice device = Device ?? InputManager.ActiveDevice;
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				PlayerAction playerAction = actions[i];
				playerAction.Update(updateTick, deltaTime, device);
				if (playerAction.UpdateTick > UpdateTick)
				{
					UpdateTick = playerAction.UpdateTick;
					LastInputType = playerAction.LastInputType;
				}
			}
			int count2 = oneAxisActions.Count;
			for (int j = 0; j < count2; j++)
			{
				oneAxisActions[j].Update(updateTick, deltaTime);
			}
			int count3 = twoAxisActions.Count;
			for (int k = 0; k < count3; k++)
			{
				twoAxisActions[k].Update(updateTick, deltaTime);
			}
		}

		public void Reset()
		{
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				actions[i].ResetBindings();
			}
		}

		public void ClearInputState()
		{
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				actions[i].ClearInputState();
			}
			int count2 = oneAxisActions.Count;
			for (int j = 0; j < count2; j++)
			{
				oneAxisActions[j].ClearInputState();
			}
			int count3 = twoAxisActions.Count;
			for (int k = 0; k < count3; k++)
			{
				twoAxisActions[k].ClearInputState();
			}
		}

		internal bool HasBinding(BindingSource binding)
		{
			if (binding == null)
			{
				return false;
			}
			int count = actions.Count;
			for (int i = 0; i < count; i++)
			{
				if (actions[i].HasBinding(binding))
				{
					return true;
				}
			}
			return false;
		}

		internal void RemoveBinding(BindingSource binding)
		{
			if (!(binding == null))
			{
				int count = actions.Count;
				for (int i = 0; i < count; i++)
				{
					actions[i].FindAndRemoveBinding(binding);
				}
			}
		}

		public string Save()
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream, Encoding.UTF8))
				{
					binaryWriter.Write((byte)66);
					binaryWriter.Write((byte)73);
					binaryWriter.Write((byte)78);
					binaryWriter.Write((byte)68);
					binaryWriter.Write((ushort)1);
					int count = actions.Count;
					binaryWriter.Write(count);
					for (int i = 0; i < count; i++)
					{
						actions[i].Save(binaryWriter);
					}
				}
				return Convert.ToBase64String(memoryStream.ToArray());
			}
		}

		public void Load(string data)
		{
			if (data == null)
			{
				return;
			}
			try
			{
				using (MemoryStream input = new MemoryStream(Convert.FromBase64String(data)))
				{
					using (BinaryReader binaryReader = new BinaryReader(input))
					{
						if (binaryReader.ReadUInt32() != 1145981250)
						{
							throw new Exception("Unknown data format.");
						}
						if (binaryReader.ReadUInt16() != 1)
						{
							throw new Exception("Unknown data version.");
						}
						int num = binaryReader.ReadInt32();
						for (int i = 0; i < num; i++)
						{
							PlayerAction value;
							if (actionsByName.TryGetValue(binaryReader.ReadString(), out value))
							{
								value.Load(binaryReader);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("Provided state could not be loaded:\n" + ex.Message);
				Reset();
			}
		}
	}
}
