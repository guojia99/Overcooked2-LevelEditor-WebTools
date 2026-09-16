using UnityEngine;

[ExecuteInEditMode]
public class EditorPrefabAutoEncasement : MonoBehaviour
{
	[SerializeField]
	private GameObject m_owningPrefab;

	private bool m_reimport;

	public GameObject EncasingPrefab
	{
		get
		{
			return m_owningPrefab;
		}
		set
		{
			m_owningPrefab = value;
		}
	}

	public void Refresh()
	{
		m_reimport = true;
	}

	private void Awake()
	{
		if (Application.isPlaying)
		{
			Object.Destroy(this);
			return;
		}
		Transform parent = base.transform.parent;
		Transform transform = base.transform;
		GameObject gameObject = null;
		if (parent != null && parent.gameObject.GetComponent<SpawnedEncaserComponent>() != null)
		{
			transform = parent;
			gameObject = transform.gameObject;
			parent = transform.transform.parent;
		}
		GameObject gameObject2 = m_owningPrefab.InstantiateOnParent(parent);
		if (gameObject2.GetComponent<SpawnedEncaserComponent>() == null)
		{
			gameObject2.AddComponent<SpawnedEncaserComponent>();
		}
		gameObject2.transform.localPosition = transform.localPosition;
		gameObject2.transform.localRotation = transform.localRotation;
		gameObject2.transform.localScale = transform.localScale;
		base.transform.SetParent(gameObject2.transform);
		if (gameObject != null)
		{
			Object.DestroyImmediate(gameObject);
		}
		base.enabled = true;
	}

	private void Update()
	{
		if (m_reimport)
		{
			Awake();
			m_reimport = false;
		}
	}
}
