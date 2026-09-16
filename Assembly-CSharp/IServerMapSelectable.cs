public interface IServerMapSelectable
{
	void AvatarEnteringSelectable(MapAvatarControls _avatar);

	void AvatarLeavingSelectable(MapAvatarControls _avatar);

	void OnSelected(MapAvatarControls _avatar);
}
