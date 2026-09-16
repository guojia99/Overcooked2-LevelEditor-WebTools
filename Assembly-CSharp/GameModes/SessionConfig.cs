using System;

namespace GameModes
{
	[Serializable]
	public class SessionConfig
	{
		public Kind m_kind;

		public bool[] m_settings = new bool[3];

		public void Copy(SessionConfig config)
		{
			m_kind = config.m_kind;
			for (int i = 0; i < m_settings.Length; i++)
			{
				m_settings[i] = config.m_settings[i];
			}
		}

		public void Save(GlobalSave save)
		{
			save.Set("GameModeKind", (int)m_kind);
			for (int i = 0; i < 3; i++)
			{
				SettingKind settingKind = (SettingKind)i;
				save.Set("GameModeSetting " + settingKind, m_settings[i]);
			}
		}

		public void Load(GlobalSave save)
		{
			int value = 0;
			save.Get("GameModeKind", out value, 0);
			m_kind = (Kind)value;
			for (int i = 0; i < 3; i++)
			{
				SettingKind settingKind = (SettingKind)i;
				save.Get("GameModeSetting " + settingKind, out m_settings[i], false);
			}
		}
	}
}
