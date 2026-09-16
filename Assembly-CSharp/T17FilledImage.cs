using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("T17_UI/Filled Image", 29)]
public class T17FilledImage : T17Image
{
	protected override void Awake()
	{
		base.Awake();
		base.type = Type.Filled;
	}

	public void SetFilledAmount(float fProgress)
	{
		fProgress = Mathf.Clamp(fProgress, 0f, 1f);
		base.fillAmount = fProgress;
	}
}
