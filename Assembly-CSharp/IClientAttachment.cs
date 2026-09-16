using UnityEngine;

public interface IClientAttachment
{
	bool IsAttached();

	GameObject AccessGameObject();

	Rigidbody AccessRigidbody();

	void RegisterAttachChangedCallback(AttachChangedCallback _callback);

	void UnregisterAttachChangedCallback(AttachChangedCallback _callback);

	IClientSidePredicted GetClientSidePrediction();

	void SetClientSidePrediction(CreateClientSidePredictionCallback prediction);
}
