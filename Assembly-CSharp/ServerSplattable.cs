using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class ServerSplattable : ServerSynchroniserBase
{
	private Splattable m_splattable;

	private LayerMask m_landscapeMask;

	private IThrowable m_throwable;

	private GridManager m_gridManager;

	private void Awake()
	{
		m_splattable = base.gameObject.RequireComponent<Splattable>();
		m_gridManager = GameUtils.GetGridManager(base.transform);
		m_landscapeMask = 1 << LayerMask.NameToLayer("Ground");
		m_throwable = base.gameObject.RequireInterface<IThrowable>();
		m_throwable.RegisterLandedCallback(OnThrowableLanded);
		m_splattable.m_prefabIndex = Random.Range(0, m_splattable.m_splatPrefab.Length - 1);
		NetworkUtils.RegisterSpawnablePrefab(base.gameObject, m_splattable.m_splatPrefab[m_splattable.m_prefabIndex]);
	}

	public override void UpdateSynchronising()
	{
	}

	private void Splat()
	{
		GridManager gridManager = GameUtils.GetGridManager(base.transform);
		GridIndex unclampedGridLocationFromPos = gridManager.GetUnclampedGridLocationFromPos(base.transform.position);
		GameObject gridOccupant = gridManager.GetGridOccupant(unclampedGridLocationFromPos);
		if (gridOccupant == null || gridOccupant.RequestInterface<HazardBase>() != null)
		{
			GameObject gameObject = NetworkUtils.ServerSpawnPrefab(base.gameObject, m_splattable.m_splatPrefab[m_splattable.m_prefabIndex], base.transform.position, Quaternion.identity);
			m_splattable.gameObject.AddComponent<StaticGridLocation>();
			if (m_splattable.m_alignToGrid)
			{
				gameObject.transform.position = gridManager.GetPosFromGridLocation(unclampedGridLocationFromPos);
			}
		}
		NetworkUtils.DestroyObject(base.gameObject);
	}

	private void OnThrowableLanded(GameObject _object)
	{
		if (((int)m_landscapeMask & (1 << _object.layer)) != 0)
		{
			Splat();
		}
	}
}
