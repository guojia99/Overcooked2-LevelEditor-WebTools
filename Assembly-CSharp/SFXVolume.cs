internal class SFXVolume : AudioVolume
{
	public override string Label
	{
		get
		{
			return "OptionType.SFXVolume";
		}
	}

	protected override string MixerControlName
	{
		get
		{
			return "SFXVolume";
		}
	}
}
