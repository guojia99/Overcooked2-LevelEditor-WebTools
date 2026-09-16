using System;
using Team17.Online.Multiplayer.Messaging;
using UnityEngine;

public class TriggerAttachedSpawn : MonoBehaviour
{
	[Serializable]
	public class WeightedPrefab : IWeight
	{
		public GameObject AttachmentPrefab;

		[SerializeField]
		private float m_weight = 1f;

		public float Weight
		{
			get
			{
				return m_weight;
			}
		}
	}

	[SerializeField]
	public WeightedPrefab[] m_attachmentPrefabs = new WeightedPrefab[0];

	[SerializeField]
	public Transform m_gridPointSelector;

	[SerializeField]
	public bool m_spawnInOrder;

	[SerializeField]
	public string m_trigger;

	[SerializeField]
	public int m_maxNumber = 50;

	private void Awake()
	{
		if (m_attachmentPrefabs.Length > 0)
		{
			SpawnableEntityCollection spawnableEntityCollection = base.gameObject.AddComponent<SpawnableEntityCollection>();
			for (int i = 0; i < m_attachmentPrefabs.Length; i++)
			{
				GameObject attachmentPrefab = m_attachmentPrefabs[i].AttachmentPrefab;
				spawnableEntityCollection.RegisterSpawnable(attachmentPrefab, null);
			}
		}
	}
}
