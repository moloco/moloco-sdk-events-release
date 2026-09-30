# Moloco Events SDK for Unity

iOS-only Unity plugin. Auto-collects install / session / in-app-purchase events and
supports custom events. Off-iOS (Editor, Android) the API compiles and no-ops.

## Install

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.1.1
```

Or in `Packages/manifest.json`:

```json
"com.moloco.eventssdk": "https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.1.1"
```

Declared minimum is Unity 2021.3 (`package.json`); verified on Unity 6000.3. Unity
resolves git packages with your machine's `git`, so it
must be able to authenticate to GitHub — use the `ssh://git@github.com/...` form if you
authenticate with SSH keys rather than an HTTPS credential helper.

The package vendors `MolocoEventsSDK.xcframework` + the native bridge, so no CocoaPods/SPM
step is required. Set your own `DEVELOPMENT_TEAM` in the generated Xcode project (signing is
not baked in).

**Simulator builds on Apple Silicon:** Unity's iOS Simulator target defaults to x86_64,
which current iOS simulator runtimes cannot run. Set *Player Settings → iOS → Target
SDK: Simulator SDK* and *Architecture: ARM64* (or build for a device).

**Seeing SDK logs:** the SDK logs at the info/debug level of Apple unified logging
(subsystem `com.moloco.eventssdk`). In Xcode's console include Info and Debug, or run
`log stream --level debug --predicate 'subsystem == "com.moloco.eventssdk"'`.

## Usage
```csharp
using System.Collections.Generic;
using Moloco;

MolocoEventsSDK.Initialize("MLC:yourAppKey");
MolocoEventsSDK.TrackEvent("level_complete", new Dictionary<string, object> { { "level", 3 } });
```
