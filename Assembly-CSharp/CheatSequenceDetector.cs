using System;
using System.Collections.Generic;
using UnityEngine;

public class CheatSequenceDetector : MonoBehaviour
{
	[Serializable]
	private class Sequence
	{
		public ControlPadInput.Button[] Definition = new ControlPadInput.Button[0];

		public string Message = string.Empty;

		public event VoidGeneric<int> OnProgressChange = delegate
		{
		};

		public void SetProgressChange(int _value)
		{
			this.OnProgressChange(_value);
		}
	}

	[SerializeField]
	private Sequence[] m_sequences = new Sequence[0];

	private ControlPadInput.Button[] m_buttonRingBuffer;

	private int m_buttonRingBufferCaret;

	private Dictionary<ControlPadInput.Button, ILogicalButton> m_buttons = new Dictionary<ControlPadInput.Button, ILogicalButton>();

	private static void _AOT()
	{
		KeyValuePair<int, Sequence> keyValuePair = default(KeyValuePair<int, Sequence>);
		Generic<float, Sequence> generic = (Sequence s) => 0f;
		if (keyValuePair.Key == 0)
		{
		}
		if (generic == null)
		{
		}
	}

	private void AvoidWarnings()
	{
		if (m_sequences != null)
		{
		}
		if (m_buttonRingBuffer != null)
		{
		}
		if (m_buttonRingBufferCaret == 0)
		{
		}
		if (m_buttons == null)
		{
		}
	}
}
