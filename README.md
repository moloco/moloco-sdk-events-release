# Moloco Events SDK for iOS

A lightweight, privacy-first native event tracking SDK for iOS. Enables advertisers
to share first-party in-app event signals — installs, sessions, in-app purchases,
and custom events — directly with Moloco for improved campaign optimization.

> **Status:** v0.1.0. Public API is `initialize(appKey:)` and `trackEvent(name:data:)`.

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
.package(url: "https://github.com/moloco/moloco-sdk-events-release.git", from: "0.1.0")
```

### CocoaPods

The podspec is published to Moloco's private spec repo, so add it as a source:

```ruby
source 'https://github.com/moloco/moloco-sdk-events-release.git'

pod 'MolocoEventsSDK', '~> 0.1'
```

### Unity (iOS)

In Unity, open **Window → Package Manager → + → Add package from git URL** and enter:

```
https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.1.0
```

Or add it to `Packages/manifest.json`:

```json
"com.moloco.eventssdk": "https://github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.1.0"
```

The package vendors `MolocoEventsSDK.xcframework` plus the C# wrapper, and embeds the
framework into your Xcode project on build. Requires Unity 2021.3+.

Unity resolves git packages with your machine's `git`, so it must be able to
authenticate to GitHub. If you use SSH keys rather than an HTTPS credential helper,
use `ssh://git@github.com/moloco/moloco-sdk-events-release.git?path=/unity/com.moloco.eventssdk#0.1.0`
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
- **in_app_purchase** — completed App Store purchases (observed via StoreKit)
- **init_complete** — emitted once initialization succeeds

### 3. Custom events

```swift
MolocoEventsSDK.trackEvent(name: "level_complete", data: ["level": 4, "score": 1200])
```

```objc
[MolocoEventsSDK trackEventWithName:@"level_complete" data:@{@"level": @4}];
```

```csharp
Moloco.MolocoEventsSDK.TrackEvent("level_complete", new Dictionary<string, object> { {"level", 4} });
```

Rules (events that break a rule are dropped — logged in DEBUG, never a crash):

- **name** must match `^[a-zA-Z][a-zA-Z0-9_]{0,63}$` (start with a letter; letters,
  digits, underscore; ≤ 64 chars)
- **reserved names** are rejected: `install`, `session`, `session_start`,
  `session_end`, `in_app_purchase`, `init_complete`
- **data** must be JSON-serializable; serialized size ≤ 50 KB; nesting ≤ 5 levels

## Verifying your integration

The SDK logs its lifecycle — init result, and events queued / sent / dropped — via
Apple unified logging (`os.log`, subsystem `com.moloco.eventssdk`). Filter Console.app
or Xcode's console by that subsystem to confirm init succeeds and events flow (and to
keep SDK output out of the rest of your logs).

## Support

For integration help, contact your Moloco representative.

## License

Proprietary. See [`LICENSE.md`](LICENSE.md).
