# Moloco Events SDK for iOS

A lightweight, privacy-first native event tracking SDK for iOS. Enables advertisers
to share first-party in-app event signals — installs, sessions, in-app purchases,
and custom events — directly with Moloco for improved campaign optimization.

> **Status:** v0.2.0. Public API is `initialize(appKey:)`, `trackEvent(name:data:)` and
> `trackPurchase`.

## Requirements

- iOS 13.0 or later at runtime (deployment target may be iOS 12.0; SDK no-ops on iOS 12)
- Swift 5.9+
- Xcode 15+ (built and verified with Xcode 26)

## Get your app key

Your **app key** identifies your app to Moloco and has the form
`<PlatformID>:<ProductID>` (e.g. `MLC:Dy0z…`). Your Moloco representative provides it
during onboarding. If the key is missing or invalid, the SDK disables itself for that
launch (and retries on the next launch for transient errors).

## Installation

### Swift Package Manager

Add the distribution repo to `Package.swift` (the signed binary is published
there, not in this source repo):

```swift
.package(url: "https://github.com/moloco/moloco-sdk-events-release.git", from: "0.2.0")
```

### CocoaPods

The podspec is published to Moloco's spec repo. Add it as a source — and keep the
CocoaPods trunk source too, because naming any `source` turns off the implicit default:

```ruby
source 'https://cdn.cocoapods.org/'
source 'https://github.com/moloco/moloco-sdk-events-release.git'

pod 'MolocoEventsSDK', '~> 0.2'
```

### Unity (iOS)

In Unity, open **Window → Package Manager → + → Add package from git URL** and enter:

```
https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.2.0
```

Or add it to `Packages/manifest.json`:

```json
"com.moloco.eventssdk": "https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.2.0"
```

The package vendors `MolocoEventsSDK.xcframework` plus the C# wrapper, and embeds the
framework into your Xcode project on build. Requires Unity 2021.3+.

Unity resolves git packages with your machine's `git`, so it must be able to
authenticate to GitHub. If you use SSH keys rather than an HTTPS credential helper,
use `ssh://git@github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.2.0`
instead.

## Usage

### 1. Initialize (once, as early as possible)

Call `initialize` early in app launch (e.g. `application(_:didFinishLaunchingWithOptions:)`).
You may call `trackEvent` before initialization finishes — those events are buffered
and sent once init resolves.

Swift:

```swift
import MolocoEventsSDK

MolocoEventsSDK.initialize(appKey: "<PlatformID>:<ProductID>")
```

Objective-C:

```objc
[MolocoEventsSDK initializeWithAppKey:@"<PlatformID>:<ProductID>"];
```

Unity (C#):

```csharp
Moloco.MolocoEventsSDK.Initialize("<PlatformID>:<ProductID>");
```

### 2. Automatically collected events

After `initialize`, the SDK tracks these for you — **no extra code**:

- **install** — first launch after install
- **session** — app foreground / background
- **in_app_purchase** — completed App Store purchases the SDK observes (see below for StoreKit 2)
- **init_complete** — emitted once initialization succeeds

### 3. StoreKit 2 purchases

A StoreKit 2 purchase made with `Product.purchase()` is returned only to your app,
so the SDK may not observe it. Report it after the purchase succeeds:

```swift
let result = try await product.purchase()
if case .success(.verified(let transaction)) = result {
    await transaction.finish()
    MolocoEventsSDK.trackPurchase(transaction: transaction)  // iOS 15+
}
```

If you don't have a StoreKit 2 `Transaction` (for example, purchases made through
another library), pass the values yourself:

```swift
MolocoEventsSDK.trackPurchase(productId: "gem_pack", price: 4.99, currency: "USD",
                              transactionId: transactionId, quantity: 1)
```

```objc
[MolocoEventsSDK trackPurchaseWithProductId:@"gem_pack"
                                      price:[NSDecimalNumber decimalNumberWithString:@"4.99"]
                                   currency:@"USD"
                              transactionId:transactionId
                                   quantity:1];
```

Unity:

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

- `price` is the price of one unit, in `currency` (ISO 4217 code). Send the count
  in `quantity`.
- A purchase that the SDK also captured automatically is sent once, matched by
  `transactionId`, as long as both happen while the app stays running.
- Report after `finish()` (in Unity, from `OnPurchaseConfirmed`). If the app is
  terminated between the two, that purchase is not reported; reporting earlier would
  instead send it twice after a relaunch.
- Unity's `TrackPurchase(order)` sends the price charged, so a free trial is sent as 0.
  On the StoreKit 1 path (iOS 14 and below) it sends the listed price.
- A purchase with an empty `transactionId` or `productId`, a `quantity` below 1, or
  a negative `price` is dropped and logged.

### 4. Custom events

```swift
MolocoEventsSDK.trackEvent(name: "level_complete", data: ["level": 4, "score": 1200])
```

```objc
[MolocoEventsSDK trackEventWithName:@"level_complete" data:@{@"level": @4}];
```

```csharp
Moloco.MolocoEventsSDK.TrackEvent("level_complete", new Dictionary<string, object> { {"level", 4} });
```

Rules (events that break a rule are dropped and logged; the SDK never crashes on them):

- **name** must match `^[a-zA-Z][a-zA-Z0-9_]{0,63}$` (start with a letter; letters,
  digits, underscore; ≤ 64 chars)
- **reserved names** are rejected: `install`, `session`, `session_start`,
  `session_end`, `in_app_purchase`, `init_complete`
- **data** must be JSON-serializable (no `Date`, `URL`, NaN or infinity; convert them
  to strings or numbers first); serialized size ≤ 50 KB; nesting ≤ 5 levels

## Verifying your integration

The SDK logs its lifecycle — init result, and events queued / sent / dropped — via
Apple unified logging (`os.log`, subsystem `com.moloco.eventssdk`) at the **info** and
**debug** levels. Both consoles hide those levels unless you opt in:

- **Xcode console:** filter by `com.moloco.eventssdk`; if nothing appears, set the
  console's log level to include Info and Debug.
- **Console.app:** enable *Action → Include Info Messages* and *Include Debug Messages*,
  then filter by the subsystem.
- **Terminal:** `log stream --level debug --predicate 'subsystem == "com.moloco.eventssdk"'`

A healthy launch shows `init success; state=ready, ingestionUrl=…` followed by
`event=… delivered (HTTP 200)` for each event.

## Support

For integration help, contact your Moloco representative.

