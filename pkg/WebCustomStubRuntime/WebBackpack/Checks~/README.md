# Backpack verification and integration

Release stays **4.0.0**. No shared core, upstream source, editor export code or scene data changed.

## Review Corrections

### Zero-cooking Ingredients

`WebBackpackCookingPolicy.cs` replaces the blanket CookableIngredient rejection. Known physical
prefabs and their Workable chain members may enter with exactly zero ServerCookingHandler
progress. Any nonzero value (including still-Raw partial cooking), NaN, infinity or unreadable
live cooking state is rejected. No cooked dish support or nonzero cooking-state persistence added.

Runtime capture reads the authoritative ServerCookableIngredient order node, validates its Raw
CookedComposite wrapper, zero recorded progress, cooking-step reference and single ingredient,
and retains that original node. Prefab initial fills build the identical native Raw wrapper from
CookableIngredient.m_ingredientOrderNode and CookingHandler.m_cookingType; source validation and
initial fill use the same zero-cooking policy. Incorrect cooked/flattened initial nodes reject
rather than silently changing identity. Simple-node fallback never flattens a cookable wrapper.

TryDispense uses the exact physical Entry, so Raw CookedComposite ingredient slots are allowed
by hand and respawn with the same cut/raw prefab at zero cooking progress. Plate transfer keeps
the original wrapper for chopped items with no remaining Workable. Whole/partly chopped items
with Workable remain hand-only so form and chopping progress cannot be discarded.

Read-only native audit covered CookableIngredient, ServerCookableIngredient, CookingHandler and
ServerCookingHandler. CookingHandler reports Raw even for nonzero partial progress; therefore
the cooked enum alone is insufficient. UnityPy bundle47 audit confirmed Meat -> ChoppedMeat,
Chicken -> ChoppedChicken and Burrito/Stew/Nugget chains; these specific cut prefabs use
IngredientPropertiesComponent (not CookableIngredient). bundle421 DLC10 chopped meat also uses
IngredientPropertiesComponent. These audited assets continue through the existing simple-node
path. The new zero-cooking tests additionally cover actual CookableIngredient component semantics.

Repeat the read-only asset audit with:

```sh
python3 "Assets/WebCustomStubRuntime/WebBackpack/Checks~/audit-cookable-assets.py" bundle47
python3 "Assets/WebCustomStubRuntime/WebBackpack/Checks~/audit-cookable-assets.py" bundle421
```

### In-flight Spawn and Attached Carry P1 Corrections

- Server initiation permission retains the 3-second peer lease. Client decode permission is a
  separate confirmed-directory digest, valid for this scene while the local immutable table is
  still valid. Lease expiry and temporary peer readiness loss do not discard reliable in-flight
  Spawn messages. Scene reset, changed digest or invalid local table invalidates the decoder.
- Both native ClientSynchronisationReceiver Spawn entrypoints are guarded before SpawnEntity.
  A truly unavailable page decoder skips the entire receive handler, before native RegisterObject
  and ComponentCacheRegistry see a null. SpawnEntity itself never implements refusal by returning
  null; an unexpected unguarded invalid page call raises an explicit diagnostic exception.
- CarryItem exceptions after confirmed hand assignment and ServerPhysicalAttachment.IsAttached
  commit the restored FIFO slot immediately if slot/node order still matches. No TakeItem or
  destruction occurs. The native ChefCarryMessage is repaired without replaying carry callbacks.
- Only uncommittable failures enter recovery. Exact reserved-object guards cover native carrier
  take/destroy/carry, server chef dispatch/update, pickup/place/throw/catch, container transfer,
  station AddItem and physical Attach. Other items/carriers pass unchanged. Cleanup bypass is
  restricted to its exact carrier's TakeItem/DestroyCarriedItem, not a global reentrant bypass.
  Before detach/destruction, the native entity registry is checked for a different actual carrier;
  IsAttached also prevents destroying an item now attached to a station or other owner.
- `WebBackpackProtocolLoader.cs` supports editor Play without Loader.dll: loaded assemblies first,
  then AssetDatabase.LoadAssetAtPath via reflection for
  `Assets/WebCustomStubRuntime/RuntimeDll/BackpackProtocol.dll.bytes`, then TextAsset lookup in
  already-loaded non-scene bundles. Loaded assembly identity is checked, failures are throttled.
  Staging the module TextAsset remains the main agent's responsibility; no staged asset means
  online initiation remains disabled.

Current native source audit: ClientSynchronisationReceiver calls RegisterObject immediately after
SpawnEntity (normal and physical paths); ServerPlayerAttachmentCarrier assigns its hand before
Attach and OnCarryBegun notifications, then sends ChefCarryMessage last.
ServerPlayerControlsImpl_Default.OnChefEvent directly calls ReceivePlace/Throw/Take even when
Behaviour.enabled is false, so recovery guards the actual dispatcher rather than toggling it.

- `WebBackpackRecovery.cs` retains pending store/rollback transactions. A hidden object is not
  evidence of hand-slot clearance. Successful explicit InspectCarriedItem()==null and a completed
  destruction call are required to unlock a store. Failed TakeItem retains the visible rollback
  object and inventory lock, retries every 0.5 seconds, and never destroys a still-held object.
  Inspection failure is not treated as empty hands. Reserved objects cannot enter another backpack.
- Pre-scan `WebBackpackAuthoredSource.cs` reads exact `pseudoPrefabSO` fields from components in
  the shared LevelEditorStub assembly and maps their unique direct physical child to the loaded
  prefab reference. This requires no LevelEditor host types. Missing/ambiguous source-child links
  are not guessed. The actual shared DLL audit found PseudoPrefabStub.pseudoPrefabSO and no
  PrefabReference type; alternate contracts need an explicit audited adapter before use.
- Node-only simple Ingredient initial fills bind to a unique exact simple-node prefab in the
  frozen directory. Multiple physical candidates warn and remain node-only/plate-accessible;
  composite initial recipes retain their plate path.
- `WebBackpackDirectoryHandshake.cs` computes aggregate SHA256 across stable owner keys, directory
  content and actual page entity IDs. Physical store and spawn require protocol permission in a
  session; module missing, local failure, timeout or mismatching peer directory cannot generate.
  Owner keys are captured before scan and do not change when the backpack is reparented to a chef.

## Protocol Module / Staging

`Modules~/BackpackProtocol/BackpackProtocol.csproj` builds **BackpackProtocol.dll**, C#4/net35.
Sources use explicit `.cs.txt` Compile items so broad offline C# globs cannot accidentally include
these Assembly-CSharp-dependent sources in the asmdef build. No new EntityType or synchronizer is
registered, so AsyncScanEntities -> EntitySerialisationRegistry.Clear cannot erase this protocol.

The Release build stages the built DLL as `RuntimeDll/BackpackProtocol.dll.bytes`. Its committed
`.meta` sets `assetBundleName=webcustomstub_runtime`, alongside IngredientSplitterRuntime. The existing Loader
loads all `*.dll.bytes`; the backpack locates Bootstrap through reflection once assemblies load.
No new EntryPoint/HarmonyPatches/GameApi wire-up is required. LayoutStubDllBuilder verifies the
staged module exists and is not older than the module sources before export. **Until the module
is staged, online physical backpack operations intentionally fail closed.** Module build command:

```sh
dotnet build "Assets/WebCustomStubRuntime/WebBackpack/Modules~/BackpackProtocol/BackpackProtocol.csproj" --configuration Release --nologo -v:q
```

Audited native path: Message serializes 8-bit MessageType but rejects values >=42 on read;
Mailbox and NetworkMessageTracker use fixed 42-entry arrays. Dedicated global ID **201** is
outside native MessageType 0..41, distinct from IngredientSplitter EntityType 200 (separate enum).
Runtime modules contained no global MessageType 201 allocation in the source audit. Install checks
the live global serializer dictionary and refuses conflicts without replacing another serializer.
There is no way to certify IDs chosen by every external mod; conflicts keep the feature disabled.

The module adds only its own message deserialization postfix, dedicated mailbox interception
before fixed-array indexing, and filters that ID out of native debug tracker arrays. It uses the
native reliable Client.SendMessageToServer and Server.BroadcastMessageToAll transport. Other
messages retain original deserialization, mailbox, sequence and tracker behavior.

Server challenge -> ready/hash replies from every actual remote connection -> server commit.
Fresh nonce on scene/content/connection-roster change; leases expire after 3 seconds, challenges
repeat at 1 second. Replies are checked against actual sender connection identity, not a payload
claim. Missing peer, not-ready peer, wrong SHA256 or old nonce prevents permission. Client page
SpawnEntity is also gated. Reset unpatches this module's hooks and clears its own registry slot.
Harmony detours and real transport still require dual-machine acceptance before distribution.

## Implementation

- `WebBackpackPrefabDirectory.cs`: pre-scan runtime source closure from native PickupItemSpawner,
  existing SpawnableEntityCollection tables (including TriggerAttachedSpawn.Awake), RandomCrate
  candidates and backpack initial-fill SOs; traverse exact Workable next-prefab references.
- Stable keys use source hierarchy/array position plus processing-chain stage. Duplicate prefab
  references choose the ordinal-minimum source key. UID and cloned object names are never used
  to identify the physical source. Root ordering is excluded from keys; duplicate keys reject.
- Allocate exactly eight native ObjectContainer page entities per backpack BEFORE source loading
  and BEFORE ScanEntities traverses roots. Each isolated page has at most 16 prefabs, preserving
  the 4-bit SpawnEntityMessage wire format (also used by SpawnPhysicalAttachmentMessage).
  The original backpack's native default registration and callback table remain untouched.
- Track actual prefab references at SpawnableEntityCollection.SpawnEntity, covering crate spawn,
  workstation transforms and backpack respawn. Unknown/untracked sources reject storage.
- `WebBackpackStoredSlot.cs`: independent FIFO records contain prefab identity, chain stage,
  original order node, Workable progress and subprogress. Restore via ServerWorkableItem plus its
  native SynchroniseClientState before carry. Debit only after InspectCarriedItem confirms.
  Failed carry detaches/destroys the attempted spawn and retains the stored slot.
- Mutations check node references against the native container. Rollback uses exact List.RemoveAt,
  since vanilla RemoveIngredient(index) actually calls semantic List.Remove(node).
- `[BackpackNet]`: initialization/hash and page/local-ID maps once; actual generation IDs,
  store/dispense states, unknown-source and carry failures limited to one per key per five seconds.
  Hook and provenance caches are cleared via existing WebBackpackStorage.OnSceneChanged.

## Shared Wiring

Existing core wiring must be retained by the main agent (module staging above is additional):

1. `HarmonyPatches.MultiplayerScanEntitiesPrefix` calls
   `WebBackpackStorage.FlushPendingAssemblies()` synchronously BEFORE the native ScanEntities body.
   It must run even when no PushablePot exists. This is now the sole page-creation path.
2. Keep `EntryPoint.ResetSceneTickers -> WebBackpackStorage.OnSceneChanged()`.
3. Keep current WebBackpackStorage PrepareOnWrapper and pickup/placement prefixes. The shared
   prefixes now consume failed worn-backpack operations instead of falling through to vanilla.

No new GameApi/EntryPoint/StubVersion method is required. Main agent owns full compilation,
Unity recompile, runtime staging and deployment. Preserve release version 4.0.0.

`ServerBackpackDispenserHandlePickupPrefix` and `ServerPlacementHandlePlacementPrefix` retain
ownership after identifying worn storage, including their outer exception fallback.

## Automated Checks

Run from repository root:

```sh
dotnet build "Assets/WebCustomStubRuntime/WebBackpack/Checks~/Backpack.Check.csproj" --nologo -v:q
dotnet run --project "Assets/WebCustomStubRuntime/WebBackpack/Checks~/Behavior/Backpack.Behavior.csproj" --nologo -v:q
git diff --check -- Assets/WebCustomStubRuntime/WebBackpack
```

The first project compiles all real backpack files plus the actual GameApi/StubLog against
Unity assemblies, C# 4 / net35. Only EntryPoint's shared installer call is stubbed for isolation.
The second executes the actual directory/slot/source/recovery/isolation/cooking-policy code on a small managed host (64
assertions). Tests
cover >16-source pagination, raw/chopped identities, partial progress, four-bit progress bounds,
FIFO, carry refusal/exception rollback, restoration broadcast, unknown source, late registration,
deterministic dedup, exact-index removal, original native default preservation, source overflow
and missing dependency topology.
Additional native checks (23 assertions) use actual shipped Assembly-CSharp Message/BitStreamReader/Writer and
actual protocol DLL, including extension decode, magic rejection, ID conflict and the production
DirectoryAgreement acceptance state (all peers, mismatch, stale nonce, expiry, disconnected sender).

```sh
dotnet run --project "Assets/WebCustomStubRuntime/WebBackpack/Checks~/Native/Backpack.Native.csproj" --nologo -v:q
```

The tests do **not** emulate Unity entity scanning, Harmony detours or actual network transport.

Latest review correction checks: runtime slice C#4/net35 build and protocol C#4/net35 build both
pass with zero warnings/errors. Behavioral assertions include destroy hiding before clear,
TakeItem failing before slot clear, inspection throwing, destruction broadcast failing after
hide, pending retry/unlock, full preplaced directory mapping, ambiguous child/source rejection
and unique/ambiguous simple-node initial fill binding. Native protocol assertions exercise the
actual game wire implementations and production DirectoryAgreement, not a replacement codec.

## Runtime Acceptance / Limits

- On two machines compare `[BackpackNet] directory ... hash=` and page/local-ID maps, and the
  existing `[Net]` entity fingerprints. Exercise host and guest placement/pickup.
- Mix raw, partially chopped and completely chopped versions of the same ingredient in FIFO;
  verify visual forms and continued chopping progress after several store/dispense cycles.
- Repeat without visible inventory UI; test >16 prefabs, random candidates, button food spawner,
  failed carry, water clear and scene reload. Both machines need the rebuilt runtime bundle.
- 128 distinct prefab references per backpack, eight extra entities each. These share the native
  scene's overall 1022-entity budget; densely populated maps require an actual entity-count check.
- Missing source bundle, cyclic chain, ambiguous keys, overflow, missing network identity or
  mutated registration tables reject physical operations. No late retry mutates network topology.
- Preplaced items with audited shared SO/unique-child provenance are supported. Other untracked
  items and sources added after scan are safely rejected. No UID/name guess for carried objects.
- No cooked dishes/filled containers or nonzero cooking progress support added. Zero-cooking
  CookableIngredient physical sources are supported without flattening their Raw wrapper.
  Node-only composite recipe initial fills retain the existing empty-plate path.
- No scene-schema upgrade, export prepare hook or additional bundle dependency is required:
  closure only follows scene references and already-declared SO/bundle dependencies. Rebuild and
  stage runtime; a normal re-export is needed only for scenes whose existing dependencies/config
  were missing. A missing dependency must be fixed at its source, not guessed at runtime.
- Directory hash comparison is now a real cross-peer protocol, not a log-only claim. The protocol
  module is staged; Unity main-runtime staging, bundle export and real dual-machine
  Harmony/transport validation remain outstanding.
