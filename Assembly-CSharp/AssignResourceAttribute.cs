using UnityEngine;

public class AssignResourceAttribute : PropertyAttribute
{
	public string AssetName;

	public Editorbility Editable = Editorbility.NonEditable;

	public AssignResourceAttribute(string _asset, Editorbility _editable = Editorbility.NonEditable)
	{
		AssetName = _asset;
		Editable = _editable;
	}
}
