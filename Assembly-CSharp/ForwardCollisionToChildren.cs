using UnityEngine;

[AddComponentMenu("Scripts/Core/Components/ForwardCollisionToChildren")]
public class ForwardCollisionToChildren : MonoBehaviour
{
	private void OnCollisionEnter(Collision other)
	{
		for (int i = 0; i < base.gameObject.transform.childCount; i++)
		{
			Transform child = base.gameObject.transform.GetChild(i);
			child.gameObject.SendMessage("OnCollisionEnter", other, SendMessageOptions.DontRequireReceiver);
		}
	}
}
