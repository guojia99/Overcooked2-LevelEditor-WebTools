# RatHeist Isolated Check

Run from the repository root:

```sh
dotnet build Assets/WebCustomStubRuntime/RatHeist/Checks~/RatHeist.Check.proj --nologo --verbosity minimal
```

This compiles the rat module with C# 4 and .NET 3.5 references. It reads the
existing GameApi, StubLog, and StubVersion sources without modifying them.
The tiny EntryPoint shim isolates the module from concurrent runtime edits.
It does not validate the real EntryPoint/HarmonyPatches wiring or run a game.
The main build must still compile `WebCustomStubRuntime.Check.csproj` and Unity
must refresh/stage the runtime bundle before multiplayer testing.

Diagnostics: set `OC2_RAT_DIAGNOSTICS=1` before launching, or use the existing
`OC2_STUB_VERBOSE=1` / loader `[Logging] Verbose=true`. Hooks only observe native
selection and TriggerInteract messages. Look for `[RatNet]` on both peers.
