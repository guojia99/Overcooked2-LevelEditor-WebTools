using UnityEngine;

public interface IAttachment
{
	void Attach(IParentable _parentable);

	void Detach();

	bool IsAttached();

	GameObject AccessGameObject();

	Rigidbody AccessRigidbody();

	RigidbodyMotion AccessMotion();

	void RegisterAttachChangedCallback(AttachChangedCallback _callback);

	void UnregisterAttachChangedCallback(AttachChangedCallback _callback);
}
