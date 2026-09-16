internal class HasSetSafeArea : BoolOption
{
	public override string Label
	{
		get
		{
			return "HasSetSafeArea";
		}
	}

	public override OptionsData.Categories Category
	{
		get
		{
			return OptionsData.Categories.SafeArea;
		}
	}

	public override void Commit()
	{
	}
}
