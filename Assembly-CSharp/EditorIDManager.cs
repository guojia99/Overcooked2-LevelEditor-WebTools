using UnityEngine;

[ExecuteInEditMode]
public class EditorIDManager : Manager
{
	[SerializeField]
	[ReadOnly]
	private EditorIDStore m_idStore;

	protected virtual void Awake()
	{
		if (m_idStore == null)
		{
			m_idStore = Object.FindObjectOfType<EditorIDStore>();
		}
	}

	public uint GetIDForObject(GameObject _object)
	{
		EnsureIDStoreAcquired();
		return m_idStore.GetIDForObject(_object);
	}

	protected void EnsureIDStoreAcquired()
	{
		if (m_idStore == null)
		{
			m_idStore = Object.FindObjectOfType<EditorIDStore>();
		}
	}

	public static uint GetUniqueID(GameObject _object)
	{
		EditorIDManager editorIDManager = GameUtils.RequireManager<EditorIDManager>();
		if (editorIDManager == null)
		{
			return 0u;
		}
		return editorIDManager.GetIDForObject(_object);
	}

	protected virtual void OnDestroy()
	{
		if (m_idStore != null)
		{
			Object.DestroyImmediate(m_idStore);
		}
	}
}
