using System.Collections.Generic;
using System.Linq;
using InControl;
using UnityEngine;

public class KeyboardRebindValueElement : KeyboardRebindElement
{
	[SerializeField]
	private PlayerInputLookup.LogicalValueID m_ValueID;

	[SerializeField]
	private bool m_Positive;

	public override void SetBinding(Key key)
	{
		ControlPadInput.Value[] realValues = GetRealValues();
		if (realValues.Length <= 0)
		{
			return;
		}
		List<Key> list = new List<Key>();
		for (int i = 0; i < realValues.Length; i++)
		{
			List<Key> bindings = PCPadInputProvider.GetBindings(realValues[i], m_Positive, m_Side != PadSide.Both);
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
		for (int j = 0; j < realValues.Length; j++)
		{
			PCPadInputProvider.SetBindings(realValues[j], list, m_Positive, m_Side != PadSide.Both);
		}
	}

	public override void UnsetBinding(Key key)
	{
		ControlPadInput.Value[] realValues = GetRealValues();
		for (int i = 0; i < realValues.Length; i++)
		{
			List<Key> bindings = PCPadInputProvider.GetBindings(realValues[i], m_Positive, m_Side != PadSide.Both);
			if (bindings != null && bindings.Count > 0)
			{
				int num = bindings.RemoveAll((Key x) => x == key);
				if (num > 0)
				{
					PCPadInputProvider.SetBindings(realValues[i], bindings, m_Positive, m_Side != PadSide.Both);
				}
			}
		}
	}

	public override bool HasAnyBindings()
	{
		ControlPadInput.Value[] realValues = GetRealValues();
		for (int i = 0; i < realValues.Length; i++)
		{
			List<Key> bindings = PCPadInputProvider.GetBindings(realValues[i], m_Positive, m_Side != PadSide.Both);
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
		ControlPadInput.Value[] realValues = GetRealValues();
		if (realValues.Length > 0)
		{
			List<Key> list = new List<Key>();
			for (int i = 0; i < realValues.Length; i++)
			{
				List<Key> bindings = PCPadInputProvider.GetBindings(realValues[i], m_Positive, m_Side != PadSide.Both);
				if (bindings != null)
				{
					list.AddRange(bindings);
				}
			}
			keyBindingsText = KeysToString(list);
		}
		SetKeyBindingsText(keyBindingsText);
	}

	private ControlPadInput.Value[] GetRealValues()
	{
		PlayerManager playerManager = GameUtils.RequireManager<PlayerManager>();
		PlayerGameInput playerGameInput = new PlayerGameInput(ControlPadInput.PadNum.One, m_Side, (m_Side != PadSide.Both) ? playerManager.SidedAmbiMapping : playerManager.UnsidedAmbiMapping);
		ControlPadInput.Value[] array = new ControlPadInput.Value[0];
		AmbiPadValue[] array2 = PlayerInputLookup.LogicalToAmbiValue(m_ValueID);
		if (array2 != null)
		{
			for (int i = 0; i < array2.Length; i++)
			{
				ControlPadInput.ValueIdentifier[] realValues = PlayerInputLookup.GetInputConfig().GetRealValues(playerGameInput, array2[i]);
				array = array.Union(realValues.ConvertAll((ControlPadInput.ValueIdentifier x) => x.value));
			}
			return array;
		}
		return new ControlPadInput.Value[0];
	}
}
