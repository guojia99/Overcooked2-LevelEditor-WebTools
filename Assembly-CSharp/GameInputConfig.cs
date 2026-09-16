using System;
using Team17.Online;

[Serializable]
public class GameInputConfig
{
	[Serializable]
	public class ConfigEntry
	{
		public PlayerInputLookup.Player Player;

		public ControlPadInput.PadNum Pad;

		public PadSide Side;

		public User.MachineID MachineId;

		public AmbiControlsMappingData AmbiControlsMapping;

		public PadSide UIHandedness = PadSide.Both;

		public ConfigEntry(PlayerInputLookup.Player _player, ControlPadInput.PadNum _pad, PadSide _side = PadSide.Both, User.MachineID _machineId = User.MachineID.Count, AmbiControlsMappingData _mappingData = null)
		{
			Player = _player;
			Pad = _pad;
			Side = _side;
			UIHandedness = _side;
			MachineId = _machineId;
			AmbiControlsMapping = _mappingData;
		}

		public bool IsLocal()
		{
			return MachineId == ClientUserSystem.s_LocalMachineId;
		}
	}

	public ConfigEntry[] m_playerConfigs = new ConfigEntry[11]
	{
		new ConfigEntry(PlayerInputLookup.Player.One, ControlPadInput.PadNum.One),
		new ConfigEntry(PlayerInputLookup.Player.Two, ControlPadInput.PadNum.Two),
		new ConfigEntry(PlayerInputLookup.Player.Three, ControlPadInput.PadNum.Three),
		new ConfigEntry(PlayerInputLookup.Player.Four, ControlPadInput.PadNum.Four),
		new ConfigEntry(PlayerInputLookup.Player.Five, ControlPadInput.PadNum.Five),
		new ConfigEntry(PlayerInputLookup.Player.Six, ControlPadInput.PadNum.Six),
		new ConfigEntry(PlayerInputLookup.Player.Seven, ControlPadInput.PadNum.Seven),
		new ConfigEntry(PlayerInputLookup.Player.Eight, ControlPadInput.PadNum.Eight),
		new ConfigEntry(PlayerInputLookup.Player.Nine, ControlPadInput.PadNum.Nine),
		new ConfigEntry(PlayerInputLookup.Player.Ten, ControlPadInput.PadNum.Ten),
		new ConfigEntry(PlayerInputLookup.Player.Eleven, ControlPadInput.PadNum.Eleven)
	};

	public GameInputConfig(ConfigEntry[] _entries)
	{
		m_playerConfigs = _entries;
	}

	public ControlPadInput.ButtonIdentifier[] GetRealButtons(PlayerInputLookup.Player _player, AmbiPadButton _gamepadButton)
	{
		PlayerGameInput inputData = GetInputData(_player);
		if (inputData == null || inputData.AmbiControlsMapping == null)
		{
			return new ControlPadInput.ButtonIdentifier[0];
		}
		return GetRealButtons(inputData, _gamepadButton);
	}

	public ControlPadInput.ButtonIdentifier[] GetRealButtons(PlayerGameInput _playerGameInput, AmbiPadButton _gamepadButton)
	{
		ControlPadInput.Button[] realButtons = _playerGameInput.AmbiControlsMapping.GetRealButtons(_gamepadButton);
		ControlPadInput.Button[] array = PadSidednessDefinition.FilterForSide(_playerGameInput.Side, realButtons);
		Converter<ControlPadInput.Button, ControlPadInput.ButtonIdentifier> converter = (ControlPadInput.Button input) => new ControlPadInput.ButtonIdentifier(_playerGameInput.Pad, input);
		return Array.ConvertAll(array, converter);
	}

	public ControlPadInput.ValueIdentifier[] GetRealValues(PlayerInputLookup.Player _player, AmbiPadValue _gamepadValue)
	{
		PlayerGameInput inputData = GetInputData(_player);
		if (inputData == null)
		{
			return new ControlPadInput.ValueIdentifier[0];
		}
		return GetRealValues(inputData, _gamepadValue);
	}

	public ControlPadInput.ValueIdentifier[] GetRealValues(PlayerGameInput _playerGameInput, AmbiPadValue _gamepadValue)
	{
		ControlPadInput.Value[] realValues = _playerGameInput.AmbiControlsMapping.GetRealValues(_gamepadValue);
		ControlPadInput.Value[] array = PadSidednessDefinition.FilterForSide(_playerGameInput.Side, realValues);
		Converter<ControlPadInput.Value, ControlPadInput.ValueIdentifier> converter = (ControlPadInput.Value input) => new ControlPadInput.ValueIdentifier(_playerGameInput.Pad, input);
		return Array.ConvertAll(array, converter);
	}

	public PlayerGameInput GetInputData(PlayerInputLookup.Player _player)
	{
		ConfigEntry configEntry = Array.Find(m_playerConfigs, (ConfigEntry x) => x.Player == _player);
		if (configEntry != null)
		{
			return new PlayerGameInput(configEntry.Pad, configEntry.Side, configEntry.AmbiControlsMapping);
		}
		return null;
	}

	public ConfigEntry GetInputConfigEntry(PlayerInputLookup.Player _player)
	{
		return Array.Find(m_playerConfigs, (ConfigEntry x) => x.Player == _player && x.IsLocal());
	}
}
