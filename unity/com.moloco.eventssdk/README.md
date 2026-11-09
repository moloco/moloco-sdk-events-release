# Moloco Events SDK for Unity

iOS-only Unity plugin. Auto-collects install / session / in-app-purchase events it
observes, and supports custom events and reported purchases. Off-iOS (Editor, Android) the API compiles and no-ops.

## Install

Package Manager → **+** → **Add package from git URL**:

```
https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.2.0
```

Or in `Packages/manifest.json`:

```json
"com.moloco.eventssdk": "https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.2.0"
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

### Purchases

On iOS 15+ Unity IAP 5 uses StoreKit 2, whose purchases the SDK may not observe. Report
each order:

```csharp
// Unity IAP 5 (compiled only when Unity IAP 5 is installed)
var store = UnityIAPServices.StoreController();
store.OnPurchasePending += order => {
    // Grant the item, then:
    store.ConfirmPurchase(order);
};
store.OnPurchaseConfirmed += order => MolocoEventsSDK.TrackPurchase(order);

// Any other purchase flow
MolocoEventsSDK.TrackPurchase("gem_pack", 4.99m, "USD", transactionId, quantity: 1);
```

Report from `OnPurchaseConfirmed`: Unity IAP redelivers unconfirmed orders to
`OnPurchasePending` on every launch. If the app is terminated after the order is
confirmed but before `OnPurchaseConfirmed` runs, that purchase is not reported.
`TrackPurchase(order)` sends the price charged, read from Apple's signed transaction, so
a free trial is sent as 0; without one (StoreKit 1: iOS 14 and below, or when Unity IAP
is set to StoreKit 1) it sends the listed price. `price` is the price of one unit. A
purchase that the SDK also captured automatically is sent once, matched by transaction
ID, as long as both happen while the app stays running. Orders with more than one item
(possible only with Unity IAP payment providers) are skipped with a warning, as are
orders with neither a signed nor a listed price (a product missing from the fetched
catalog, on the StoreKit 1 path); automatic capture reports those.
