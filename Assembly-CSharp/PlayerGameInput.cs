using System;

[Serializable]
public class PlayerGameInput
{
	public ControlPadInput.PadNum Pad;

	public PadSide Side = PadSide.Both;

	public AmbiControlsMappingData AmbiControlsMapping;

	public PlayerGameInput(ControlPadInput.PadNum _pad, PadSide _side, AmbiControlsMappingData _mappingData)
	{
		Pad = _pad;
		Side = _side;
		AmbiControlsMapping = _mappingData;
	}
}
