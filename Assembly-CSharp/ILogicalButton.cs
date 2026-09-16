public interface ILogicalButton : ILogicalElement
{
	bool JustPressed();

	bool JustReleased();

	bool HasUnclaimedPressEvent();

	void ClaimPressEvent();

	bool HasUnclaimedReleaseEvent();

	void ClaimReleaseEvent();

	float GetHeldTimeLength();

	bool IsDown();
}
