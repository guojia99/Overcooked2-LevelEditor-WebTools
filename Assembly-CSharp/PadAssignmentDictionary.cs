using System.Collections.Generic;

public class PadAssignmentDictionary
{
	private Dictionary<ControlPadInput.PadNum, ControlPadInput.PadNum> s_padMapping = new Dictionary<ControlPadInput.PadNum, ControlPadInput.PadNum>();

	public PadAssignmentDictionary()
	{
		for (int i = 0; i < 15; i++)
		{
			ControlPadInput.PadNum padNum = (ControlPadInput.PadNum)i;
			s_padMapping.Add(padNum, padNum);
		}
	}

	public void RemapPad(ControlPadInput.PadNum _oldPadNum, ControlPadInput.PadNum _newPadNum)
	{
		ControlPadInput.PadNum value = s_padMapping[_oldPadNum];
		ControlPadInput.PadNum value2 = s_padMapping[_newPadNum];
		s_padMapping[_oldPadNum] = value2;
		s_padMapping[_newPadNum] = value;
	}

	public ControlPadInput.PadNum GetMappedPad(ControlPadInput.PadNum _requestedPadNum)
	{
		return s_padMapping[_requestedPadNum];
	}
}
