public interface IOption
{
	OptionsData.Categories Category { get; }

	string Label { get; }

	void SetOption(int _value);

	int GetOption();

	void Commit();
}
