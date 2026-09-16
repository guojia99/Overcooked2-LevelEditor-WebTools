using UnityEngine;

public class ChefColourData : ScriptableObject
{
	public Material ChefMaterial;

	[Header("Colour Mask")]
	public Color MaskColour;

	[Header("UI")]
	public Color UIColour;

	public Color DarkUIColour;

	public Color PadUIColour;

	public Color PadBarColour;

	[Space]
	public Sprite Background;
}
