using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TutorialPopup
{
	[AssignResource("TutorialSplash", Editorbility.Editable)]
	public GameObject Prefab;

	public string Title;

	public string Description;

	public string LabelText = "Text.Tutorial.NewRecipe";

	public Sprite SplashImage;

	public bool CanSpawn()
	{
		return SplashImage != null;
	}

	public GameObject Spawn()
	{
		if (CanSpawn())
		{
			GameObject gameObject = GameUtils.InstantiateUIController(Prefab, "UICanvas");
			GameObject gameObject2 = gameObject.RequireChild("Title");
			if (gameObject2 != null)
			{
				T17Text t17Text = gameObject2.RequireComponent<T17Text>();
				t17Text.SetNewPlaceHolder(Title);
				t17Text.SetNewLocalizationTag(Title);
			}
			GameObject gameObject3 = gameObject.RequestChild("Description");
			if (gameObject3 != null)
			{
				T17Text t17Text2 = gameObject3.RequireComponent<T17Text>();
				t17Text2.SetNewPlaceHolder(Description);
				t17Text2.SetNewLocalizationTag(Description);
			}
			GameObject obj = gameObject.RequestChild("Label");
			if (gameObject3 != null)
			{
				T17Text t17Text3 = obj.RequireComponent<T17Text>();
				t17Text3.SetNewPlaceHolder(LabelText);
				t17Text3.SetNewLocalizationTag(LabelText);
			}
			GameObject obj2 = gameObject.RequireChild("StepsImage");
			obj2.RequireComponent<Image>().sprite = SplashImage;
			return gameObject;
		}
		return null;
	}
}
