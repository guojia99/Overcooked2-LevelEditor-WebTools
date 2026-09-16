using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public abstract class ServerHazardBase : ServerSynchroniserBase, IHandleGridTransfer
{
	private GridManager m_gridManager;

	private IGridLocation m_gridLocation;

	protected virtual void Awake()
	{
	}

	protected virtual void Start()
	{
		m_gridLocation = base.gameObject.RequireInterface<IGridLocation>();
		m_gridManager = m_gridLocation.AccessGridManager;
	}

	public abstract void HandleTransfer(GridIndex _index, GameObject _object);

	public bool CanHandleTransfer(GridIndex _index, GameObject _object)
	{
		return true;
	}
}
