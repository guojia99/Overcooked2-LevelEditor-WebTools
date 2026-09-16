using System;
using System.Collections.Generic;

[Serializable]
public class OptionsData
{
	public enum Categories
	{
		Graphics = 0,
		Audio = 1,
		SafeArea = 2,
		Controls = 3,
		System = 4
	}

	public interface IEnabled
	{
		bool IsEnabled();
	}

	public interface IInit
	{
		void Init();
	}

	public interface IUnloadable
	{
		void Unload();
	}

	public interface IUpdatable
	{
		void Update();
	}

	public enum OptionType
	{
		Resolution = 0,
		Windowed = 1,
		VSync = 2,
		Quality = 3,
		MusicVolume = 4,
		SFXVolume = 5,
		SafeAreaX = 6,
		SafeAreaY = 7,
		HasSetSafeArea = 8,
		Count = 11
	}

	[AssignResource("SidedAmbiControlsMappingData", Editorbility.NonEditable)]
	public AmbiControlsMappingData m_sidedMappingData;

	[AssignResource("UnsidedAmbiControlsMappingData", Editorbility.NonEditable)]
	public AmbiControlsMappingData m_unsidedMappingData;

	private IOption[] m_options;

	public OptionsData()
	{
		m_options = new IOption[11];
		m_options[0] = new Resolution();
		m_options[2] = new VSync();
		m_options[1] = new Windowed();
		m_options[3] = new Quality();
		m_options[4] = new MusicVolume();
		m_options[5] = new SFXVolume();
		m_options[6] = new SafeAreaX();
		m_options[7] = new SafeAreaY();
		m_options[8] = new HasSetSafeArea();
	}

	public void OnAwake()
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			if (m_options[i] != null)
			{
				IInit init = m_options[i] as IInit;
				if (init != null)
				{
					init.Init();
				}
			}
		}
	}

	public void Unload()
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			if (m_options[i] != null)
			{
				IUnloadable unloadable = m_options[i] as IUnloadable;
				if (unloadable != null)
				{
					unloadable.Unload();
				}
			}
		}
	}

	public void Update()
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			if (m_options[i] != null)
			{
				IUpdatable updatable = m_options[i] as IUpdatable;
				if (updatable != null)
				{
					updatable.Update();
				}
			}
		}
	}

	public void AddToSave()
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			if (m_options[i] != null)
			{
				m_options[i].SetOption(m_options[i].GetOption());
				m_options[i].Commit();
				Prefs.SetInt(m_options[i].Label, m_options[i].GetOption());
			}
		}
		Prefs.Save();
	}

	public void LoadFromSave()
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			if (m_options[i] != null)
			{
				Prefs.Setup(m_options[i].Label);
				if (Prefs.HasKey(m_options[i].Label))
				{
					int option = Prefs.GetInt(m_options[i].Label, 0);
					m_options[i].SetOption(option);
					m_options[i].Commit();
				}
			}
		}
	}

	public bool AnyChangesToCommit()
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			if (m_options[i] != null)
			{
				int option = m_options[i].GetOption();
				int num = Prefs.GetInt(m_options[i].Label, 0);
				if (option != num)
				{
					return true;
				}
			}
		}
		return false;
	}

	public IOption GetOption(OptionType _type)
	{
		return m_options[(int)_type];
	}

	public int GetOptionsInCategory(Categories _categories)
	{
		int num = 0;
		foreach (IOption item in IterateOverCategory(_categories))
		{
			num++;
		}
		return num;
	}

	public IEnumerable<IOption> IterateOverCategory(Categories _category)
	{
		for (int i = 0; i < m_options.Length; i++)
		{
			if (m_options[i] != null && m_options[i].Category == _category)
			{
				yield return m_options[i];
			}
		}
	}
}
