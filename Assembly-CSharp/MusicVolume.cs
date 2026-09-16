internal class MusicVolume : AudioVolume
{
	public override string Label
	{
		get
		{
			return "OptionType.MusicVolume";
		}
	}

	protected override string MixerControlName
	{
		get
		{
			return "MusicVolume";
		}
	}

	protected override float MaxAudioVolume
	{
		get
		{
			return 0f;
		}
	}
}
