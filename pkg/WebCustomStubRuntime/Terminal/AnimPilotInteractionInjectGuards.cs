namespace CustomStub
{
    /// <summary>InteractionObjects 注入共用护栏（摇杆 Terminal / 摇杆组成员）。</summary>
    internal static class AnimPilotInteractionInjectGuards
    {
        internal static bool HasPickupOrPlacement(object interactionObjects)
        {
            if (interactionObjects == null)
                return false;
            try
            {
                if (GameApi.InteractionObjectsPickupField != null)
                {
                    if (GameApi.InteractionObjectsPickupField.GetValue(interactionObjects) != null)
                        return true;
                }
                if (GameApi.InteractionObjectsPlacementField != null)
                {
                    if (GameApi.InteractionObjectsPlacementField.GetValue(interactionObjects) != null)
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
