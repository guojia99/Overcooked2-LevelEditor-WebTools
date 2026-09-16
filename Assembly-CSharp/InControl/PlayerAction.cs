using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEngine;

namespace InControl
{
	public class PlayerAction : InputControlBase
	{
		public BindingListenOptions ListenOptions;

		public BindingSourceType LastInputType;

		private List<BindingSource> defaultBindings = new List<BindingSource>();

		private List<BindingSource> regularBindings = new List<BindingSource>();

		private List<BindingSource> visibleBindings = new List<BindingSource>();

		private readonly ReadOnlyCollection<BindingSource> bindings;

		private static readonly BindingSourceListener[] bindingSourceListeners = new BindingSourceListener[4]
		{
			new DeviceBindingSourceListener(),
			new UnknownDeviceBindingSourceListener(),
			new KeyBindingSourceListener(),
			new MouseBindingSourceListener()
		};

		private InputDevice device;

		public string Name { get; private set; }

		public PlayerActionSet Owner { get; private set; }

		public bool IsListeningForBinding
		{
			get
			{
				return Owner.listenWithAction == this;
			}
		}

		public ReadOnlyCollection<BindingSource> Bindings
		{
			get
			{
				return bindings;
			}
		}

		internal InputDevice Device
		{
			get
			{
				if (device == null)
				{
					device = Owner.Device;
					UpdateVisibleBindings();
				}
				return device;
			}
			set
			{
				if (device != value)
				{
					device = value;
					UpdateVisibleBindings();
				}
			}
		}

		public PlayerAction(string name, PlayerActionSet owner)
		{
			Raw = true;
			Name = name;
			Owner = owner;
			bindings = new ReadOnlyCollection<BindingSource>(visibleBindings);
		}

		public void AddDefaultBinding(BindingSource binding)
		{
			if (binding == null)
			{
				return;
			}
			if (binding.BoundTo != null)
			{
				throw new InControlException("Binding source is already bound to action " + binding.BoundTo.Name);
			}
			if (!defaultBindings.Contains(binding))
			{
				defaultBindings.Add(binding);
				binding.BoundTo = this;
			}
			if (!regularBindings.Contains(binding))
			{
				regularBindings.Add(binding);
				binding.BoundTo = this;
				if (binding.IsValid)
				{
					visibleBindings.Add(binding);
				}
			}
		}

		public void AddDefaultBinding(params Key[] keys)
		{
			AddDefaultBinding(new KeyBindingSource(keys));
		}

		public void AddDefaultBinding(Mouse control)
		{
			AddDefaultBinding(new MouseBindingSource(control));
		}

		public void AddDefaultBinding(InputControlType control)
		{
			AddDefaultBinding(new DeviceBindingSource(control));
		}

		public bool AddBinding(BindingSource binding)
		{
			if (binding == null)
			{
				return false;
			}
			if (binding.BoundTo != null)
			{
				Debug.LogWarning("Binding source is already bound to action " + binding.BoundTo.Name);
				return false;
			}
			if (regularBindings.Contains(binding))
			{
				return false;
			}
			regularBindings.Add(binding);
			binding.BoundTo = this;
			if (binding.IsValid)
			{
				visibleBindings.Add(binding);
			}
			return true;
		}

		public bool InsertBindingAt(int index, BindingSource binding)
		{
			if (index < 0 || index > visibleBindings.Count)
			{
				throw new InControlException("Index is out of range for bindings on this action.");
			}
			if (index == visibleBindings.Count)
			{
				return AddBinding(binding);
			}
			if (binding == null)
			{
				return false;
			}
			if (binding.BoundTo != null)
			{
				Debug.LogWarning("Binding source is already bound to action " + binding.BoundTo.Name);
				return false;
			}
			if (regularBindings.Contains(binding))
			{
				return false;
			}
			int index2 = ((index != 0) ? regularBindings.IndexOf(visibleBindings[index]) : 0);
			regularBindings.Insert(index2, binding);
			binding.BoundTo = this;
			if (binding.IsValid)
			{
				visibleBindings.Insert(index, binding);
			}
			return true;
		}

		public bool ReplaceBinding(BindingSource findBinding, BindingSource withBinding)
		{
			if (findBinding == null || withBinding == null)
			{
				return false;
			}
			if (withBinding.BoundTo != null)
			{
				Debug.LogWarning("Binding source is already bound to action " + withBinding.BoundTo.Name);
				return false;
			}
			int num = regularBindings.IndexOf(findBinding);
			if (num < 0)
			{
				Debug.LogWarning("Binding source to replace is not present in this action.");
				return false;
			}
			Debug.Log("index = " + num);
			findBinding.BoundTo = null;
			regularBindings[num] = withBinding;
			withBinding.BoundTo = this;
			num = visibleBindings.IndexOf(findBinding);
			if (num >= 0)
			{
				visibleBindings[num] = withBinding;
			}
			return true;
		}

		internal bool HasBinding(BindingSource binding)
		{
			if (binding == null)
			{
				return false;
			}
			BindingSource bindingSource = FindBinding(binding);
			if (bindingSource == null)
			{
				return false;
			}
			return bindingSource.BoundTo == this;
		}

		internal BindingSource FindBinding(BindingSource binding)
		{
			if (binding == null)
			{
				return null;
			}
			int num = regularBindings.IndexOf(binding);
			if (num >= 0)
			{
				return regularBindings[num];
			}
			return null;
		}

		internal void FindAndRemoveBinding(BindingSource binding)
		{
			if (binding == null)
			{
				return;
			}
			int num = regularBindings.IndexOf(binding);
			if (num >= 0)
			{
				BindingSource bindingSource = regularBindings[num];
				if (bindingSource.BoundTo == this)
				{
					bindingSource.BoundTo = null;
					regularBindings.RemoveAt(num);
					UpdateVisibleBindings();
				}
			}
		}

		internal int CountBindingsOfType(BindingSourceType bindingSourceType)
		{
			int num = 0;
			int count = regularBindings.Count;
			for (int i = 0; i < count; i++)
			{
				BindingSource bindingSource = regularBindings[i];
				if (bindingSource.BoundTo == this && bindingSource.BindingSourceType == bindingSourceType)
				{
					num++;
				}
			}
			return num;
		}

		internal void RemoveFirstBindingOfType(BindingSourceType bindingSourceType)
		{
			int count = regularBindings.Count;
			for (int i = 0; i < count; i++)
			{
				BindingSource bindingSource = regularBindings[i];
				if (bindingSource.BoundTo == this && bindingSource.BindingSourceType == bindingSourceType)
				{
					bindingSource.BoundTo = null;
					regularBindings.RemoveAt(i);
					break;
				}
			}
		}

		internal int IndexOfFirstInvalidBinding()
		{
			int count = regularBindings.Count;
			for (int i = 0; i < count; i++)
			{
				if (!regularBindings[i].IsValid)
				{
					return i;
				}
			}
			return -1;
		}

		public void RemoveBinding(BindingSource binding)
		{
			if (!(binding == null))
			{
				if (binding.BoundTo != this)
				{
					throw new InControlException("Cannot remove a binding source not bound to this action.");
				}
				binding.BoundTo = null;
			}
		}

		public void RemoveBindingAt(int index)
		{
			if (index < 0 || index >= regularBindings.Count)
			{
				throw new InControlException("Index is out of range for bindings on this action.");
			}
			regularBindings[index].BoundTo = null;
		}

		public void ClearBindings()
		{
			int count = regularBindings.Count;
			for (int i = 0; i < count; i++)
			{
				regularBindings[i].BoundTo = null;
			}
			regularBindings.Clear();
			visibleBindings.Clear();
		}

		public void ResetBindings()
		{
			ClearBindings();
			regularBindings.AddRange(defaultBindings);
			int count = regularBindings.Count;
			for (int i = 0; i < count; i++)
			{
				BindingSource bindingSource = regularBindings[i];
				bindingSource.BoundTo = this;
				if (bindingSource.IsValid)
				{
					visibleBindings.Add(bindingSource);
				}
			}
		}

		public void ListenForBinding()
		{
			ListenForBindingReplacing(null);
		}

		public void ListenForBindingReplacing(BindingSource binding)
		{
			BindingListenOptions bindingListenOptions = ListenOptions ?? Owner.ListenOptions;
			bindingListenOptions.ReplaceBinding = binding;
			Owner.listenWithAction = this;
			int num = bindingSourceListeners.Length;
			for (int i = 0; i < num; i++)
			{
				bindingSourceListeners[i].Reset();
			}
		}

		public void StopListeningForBinding()
		{
			if (IsListeningForBinding)
			{
				Owner.listenWithAction = null;
			}
		}

		private void RemoveOrphanedBindings()
		{
			int count = regularBindings.Count;
			for (int num = count - 1; num >= 0; num--)
			{
				if (regularBindings[num].BoundTo != this)
				{
					regularBindings.RemoveAt(num);
				}
			}
		}

		internal void Update(ulong updateTick, float deltaTime, InputDevice device)
		{
			Device = device;
			UpdateBindings(updateTick, deltaTime);
			DetectBindings();
		}

		private void UpdateBindings(ulong updateTick, float deltaTime)
		{
			int count = regularBindings.Count;
			for (int num = count - 1; num >= 0; num--)
			{
				BindingSource bindingSource = regularBindings[num];
				if (bindingSource.BoundTo != this)
				{
					regularBindings.RemoveAt(num);
					visibleBindings.Remove(bindingSource);
				}
				else
				{
					float value = bindingSource.GetValue(Device);
					if (UpdateWithValue(value, updateTick, deltaTime))
					{
						LastInputType = bindingSource.BindingSourceType;
					}
				}
			}
			Commit();
			Enabled = Owner.Enabled;
		}

		private void DetectBindings()
		{
			if (!IsListeningForBinding)
			{
				return;
			}
			BindingSource bindingSource = null;
			BindingListenOptions bindingListenOptions = ListenOptions ?? Owner.ListenOptions;
			int num = bindingSourceListeners.Length;
			for (int i = 0; i < num; i++)
			{
				bindingSource = bindingSourceListeners[i].Listen(bindingListenOptions, device);
				if (bindingSource != null)
				{
					break;
				}
			}
			if (bindingSource == null)
			{
				return;
			}
			Func<PlayerAction, BindingSource, bool> onBindingFound = bindingListenOptions.OnBindingFound;
			if (onBindingFound != null && !onBindingFound(this, bindingSource))
			{
				return;
			}
			if (HasBinding(bindingSource))
			{
				Action<PlayerAction, BindingSource, BindingSourceRejectionType> onBindingRejected = bindingListenOptions.OnBindingRejected;
				if (onBindingRejected != null)
				{
					onBindingRejected(this, bindingSource, BindingSourceRejectionType.DuplicateBindingOnAction);
				}
				return;
			}
			if (bindingListenOptions.UnsetDuplicateBindingsOnSet)
			{
				Owner.RemoveBinding(bindingSource);
			}
			if (!bindingListenOptions.AllowDuplicateBindingsPerSet && Owner.HasBinding(bindingSource))
			{
				Action<PlayerAction, BindingSource, BindingSourceRejectionType> onBindingRejected2 = bindingListenOptions.OnBindingRejected;
				if (onBindingRejected2 != null)
				{
					onBindingRejected2(this, bindingSource, BindingSourceRejectionType.DuplicateBindingOnActionSet);
				}
				return;
			}
			StopListeningForBinding();
			if (bindingListenOptions.ReplaceBinding == null)
			{
				if (bindingListenOptions.MaxAllowedBindingsPerType != 0)
				{
					while (CountBindingsOfType(bindingSource.BindingSourceType) >= bindingListenOptions.MaxAllowedBindingsPerType)
					{
						RemoveFirstBindingOfType(bindingSource.BindingSourceType);
					}
				}
				else if (bindingListenOptions.MaxAllowedBindings != 0)
				{
					while (regularBindings.Count >= bindingListenOptions.MaxAllowedBindings)
					{
						int index = Mathf.Max(0, IndexOfFirstInvalidBinding());
						regularBindings.RemoveAt(index);
					}
				}
				AddBinding(bindingSource);
			}
			else
			{
				ReplaceBinding(bindingListenOptions.ReplaceBinding, bindingSource);
			}
			UpdateVisibleBindings();
			Action<PlayerAction, BindingSource> onBindingAdded = bindingListenOptions.OnBindingAdded;
			if (onBindingAdded != null)
			{
				onBindingAdded(this, bindingSource);
			}
		}

		private void UpdateVisibleBindings()
		{
			visibleBindings.Clear();
			int count = regularBindings.Count;
			for (int i = 0; i < count; i++)
			{
				BindingSource bindingSource = regularBindings[i];
				if (bindingSource.IsValid)
				{
					visibleBindings.Add(bindingSource);
				}
			}
		}

		internal void Load(BinaryReader reader)
		{
			ClearBindings();
			int num = reader.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				BindingSourceType bindingSourceType = (BindingSourceType)reader.ReadInt32();
				BindingSource bindingSource;
				switch (bindingSourceType)
				{
				case BindingSourceType.DeviceBindingSource:
					bindingSource = new DeviceBindingSource();
					break;
				case BindingSourceType.KeyBindingSource:
					bindingSource = new KeyBindingSource();
					break;
				case BindingSourceType.MouseBindingSource:
					bindingSource = new MouseBindingSource();
					break;
				case BindingSourceType.UnknownDeviceBindingSource:
					bindingSource = new UnknownDeviceBindingSource();
					break;
				default:
					throw new InControlException("Don't know how to load BindingSourceType: " + bindingSourceType);
				}
				bindingSource.Load(reader);
				AddBinding(bindingSource);
			}
		}

		internal void Save(BinaryWriter writer)
		{
			RemoveOrphanedBindings();
			writer.Write(Name);
			int count = regularBindings.Count;
			writer.Write(count);
			for (int i = 0; i < count; i++)
			{
				BindingSource bindingSource = regularBindings[i];
				writer.Write((int)bindingSource.BindingSourceType);
				bindingSource.Save(writer);
			}
		}
	}
}
