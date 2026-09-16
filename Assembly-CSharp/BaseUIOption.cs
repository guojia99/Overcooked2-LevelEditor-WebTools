using System;
using UnityEngine;

public abstract class BaseUIOption<T> : MonoBehaviour, ISyncUIWithOption where T : IOption
{
	protected T m_Option = default(T);

	[SerializeField]
	private OptionsData.OptionType m_OptionType;

	protected virtual void Awake()
	{
		if (Enum.IsDefined(typeof(OptionsData.OptionType), m_OptionType))
		{
			IOption option = GameUtils.GetMetaGameProgress().AccessOptionsData.GetOption(m_OptionType);
			if (option != null)
			{
				m_Option = (T)option;
			}
			SyncUIWithOption();
		}
	}

	public virtual void SetValue(int _Value)
	{
		if (m_Option != null)
		{
			m_Option.SetOption(_Value);
		}
	}

	public virtual int GetValue()
	{
		if (m_Option != null)
		{
			return m_Option.GetOption();
		}
		return 0;
	}

	public virtual void SyncUIWithOption()
	{
	}
}
