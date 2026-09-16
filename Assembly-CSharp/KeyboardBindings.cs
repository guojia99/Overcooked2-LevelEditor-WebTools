public class KeyboardBindings
{
	public KeyboardBindingSet m_CombinedKeyboard = new KeyboardBindingSet();

	public KeyboardBindingSet m_SplitKeyboard = new KeyboardBindingSet();

	private string m_Name = "UNSET";

	public KeyboardBindings(string name)
	{
		m_Name = name;
	}

	public void CopyFrom(KeyboardBindings original)
	{
		m_CombinedKeyboard.CopyFrom(original.m_CombinedKeyboard);
		m_SplitKeyboard.CopyFrom(original.m_SplitKeyboard);
	}

	public void Save(GlobalSave saveData)
	{
		m_CombinedKeyboard.Save(saveData, m_Name + "_Combined");
		m_SplitKeyboard.Save(saveData, m_Name + "_Split");
	}

	public bool Load(GlobalSave saveData)
	{
		bool flag = true;
		flag &= m_CombinedKeyboard.Load(saveData, m_Name + "_Combined");
		return flag & m_SplitKeyboard.Load(saveData, m_Name + "_Split");
	}
}
