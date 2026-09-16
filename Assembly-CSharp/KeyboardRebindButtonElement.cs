using System.Collections.Generic;
using System.Linq;
using InControl;
using UnityEngine;

public class KeyboardRebindButtonElement : KeyboardRebindElement
{
	[SerializeField]
	private PlayerInputLookup.LogicalButtonID m_ButtonID;

	public override void SetBinding(Key key)
	{
		ControlPadInput.Button[] realButtons = PlayerInputLookup.GetRealButtons(m_ButtonID, m_Side);
		if (realButtons.Length <= 0)
		{
			return;
		}
		List<Key> list = new List<Key>();
		for (int i = 0; i < realButtons.Length; i++)
		{
			List<Key> bindings = PCPadInputProvider.GetBindings(realButtons[i], m_Side != PadSide.Both);
			if (bindings != null && bindings.Count > 0)
			{
				list.AddRange(bindings);
			}
		}
		list = list.Distinct().ToList();
		if (m_AllowSecondaryKey && list.Count == 1)
		{
			List<Key> list2 = new List<Key>();
			list2.Add(list[0]);
			list2.Add(key);
			list = list2;
		}
		else
		{
			List<Key> list2 = new List<Key>();
			list2.Add(key);
			list = list2;
		}
		for (int j = 0; j < realButtons.Length; j++)
		{
			PCPadInputProvider.SetBindings(realButtons[j], list, m_Side != PadSide.Both);
		}
	}

	public override void UnsetBinding(Key key)
	{
		ControlPadInput.Button[] realButtons = PlayerInputLookup.GetRealButtons(m_ButtonID, m_Side);
		for (int i = 0; i < realButtons.Length; i++)
		{
			List<Key> bindings = PCPadInputProvider.GetBindings(realButtons[i], m_Side != PadSide.Both);
			if (bindings != null && bindings.Count > 0)
			{
				int num = bindings.RemoveAll((Key x) => x == key);
				if (num > 0)
				{
					PCPadInputProvider.SetBindings(realButtons[i], bindings, m_Side != PadSide.Both);
				}
			}
		}
	}

	public override bool HasAnyBindings()
	{
		ControlPadInput.Button[] realButtons = PlayerInputLookup.GetRealButtons(m_ButtonID, m_Side);
		for (int i = 0; i < realButtons.Length; i++)
		{
			List<Key> bindings = PCPadInputProvider.GetBindings(realButtons[i], m_Side != PadSide.Both);
			if (bindings != null && bindings.Count > 0)
			{
				return true;
			}
		}
		return false;
	}

	public override void RefreshBindingText()
	{
		string keyBindingsText = string.Empty;
		ControlPadInput.Button[] realButtons = PlayerInputLookup.GetRealButtons(m_ButtonID, m_Side);
		if (realButtons.Length > 0)
		{
			List<Key> list = new List<Key>();
			for (int i = 0; i < realButtons.Length; i++)
			{
				List<Key> bindings = PCPadInputProvider.GetBindings(realButtons[i], m_Side != PadSide.Both);
				if (bindings != null)
				{
					list.AddRange(bindings);
				}
			}
			keyBindingsText = KeysToString(list);
		}
		SetKeyBindingsText(keyBindingsText);
	}
}
