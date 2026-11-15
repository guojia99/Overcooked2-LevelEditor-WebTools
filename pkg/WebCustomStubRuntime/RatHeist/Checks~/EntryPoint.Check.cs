#if RATHEIST_ISOLATED_CHECK
namespace CustomStub
{
    // Check the rat module alone; the main agent validates the real core wiring.
    internal static class EntryPoint
    {
        internal static void EnsurePotAssemblyPatches() { }
    }
}
#endif
