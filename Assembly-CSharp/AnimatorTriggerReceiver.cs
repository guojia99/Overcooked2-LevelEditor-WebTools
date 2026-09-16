using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/AnimatorTriggerReceiver")]
public class AnimatorTriggerReceiver : MonoBehaviour, ITriggerReceiver
{
	public void OnTrigger(string _name)
	{
		if ((bool)base.gameObject)
		{
			base.gameObject.SendTrigger(_name);
		}
	}
}
