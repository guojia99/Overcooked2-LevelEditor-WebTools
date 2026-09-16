using System;
using UnityEngine;

namespace GameModes
{
	[Serializable]
	public struct ModeUIData
	{
		public string m_nameLocalisationKey;

		public string m_descriptionLocalisationKey;

		public Sprite m_previewImage;

		public SettingKind[] m_supportedSettings;

		public WorldMapLevelIconUI m_levelPreview;
	}
}
