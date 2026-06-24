# Moloco Events SDK for iOS — Releases

Binary distribution of the Moloco Events SDK (advertiser-side event tracking).
Published per release; the source lives in the private `moloco-sdk-events-ios` repo.

## Swift Package Manager

Add `https://github.com/moloco/moloco-sdk-events-release` and pin a version (e.g. `0.1.0`).

## CocoaPods

```
pod 'MolocoEventsSDK', '0.1.0'
```

Each release tags `X.Y.Z` and publishes `Package.swift` (a binaryTarget pointing at the
hosted, signed `xcframework`) plus `MolocoEventsSDK/<version>/MolocoEventsSDK.podspec.json`.
