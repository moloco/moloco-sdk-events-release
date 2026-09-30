# Moloco Events SDK for Unity

iOS-only Unity plugin. Auto-collects install / session / in-app-purchase events and
supports custom events. Off-iOS (Editor, Android) the API compiles and no-ops.

## Install

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.1.0
```

Or in `Packages/manifest.json`:

```json
"com.moloco.eventssdk": "https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.1.0"
```

Requires Unity 2021.3+. Unity resolves git packages with your machine's `git`, so it
must be able to authenticate to GitHub — use the `ssh://git@github.com/...` form if you
authenticate with SSH keys rather than an HTTPS credential helper.

The package vendors `MolocoEventsSDK.xcframework` + the native bridge, so no CocoaPods/SPM
step is required. Set your own `DEVELOPMENT_TEAM` in the generated Xcode project (signing is
not baked in).

## Usage
```csharp
using System.Collections.Generic;
using Moloco;

MolocoEventsSDK.Initialize("MLC:yourAppKey");
MolocoEventsSDK.TrackEvent("level_complete", new Dictionary<string, object> { { "level", 3 } });
```
