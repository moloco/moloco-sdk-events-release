// Moloco Events SDK iOS — Unity bridge.
//
// Ships inside the com.moloco.eventssdk UPM package under Runtime/Plugins/iOS
// alongside MolocoEventsSDK.xcframework; Unity compiles it into UnityFramework
// and links it against the framework automatically. (It is NOT part of the SPM
// target — SPM doesn't compile .mm, and the SPM/CocoaPods channels ship the
// xcframework alone.)

#import <Foundation/Foundation.h>
#import <objc/message.h>

// Forward declarations of the Swift API. The auto-generated header would be
// "MolocoEventsSDK-Swift.h", but we forward-declare here to avoid having to
// resolve the build path of that header in non-SPM projects.
@interface MolocoEventsSDK : NSObject
+ (void)initializeWithAppKey:(NSString * _Nonnull)appKey;
+ (void)trackEventWithName:(NSString * _Nonnull)name data:(NSDictionary * _Nullable)data;
+ (NSString * _Nonnull)_currentStateForTesting;
@end

// Provided at link time by Unity's iOS runtime. Used to push SDK log lines from
// the native side back into managed code (where the demo's `Application.logMessageReceived`
// can then surface them in the on-device log view, matching SwiftDemo behavior).
extern "C" void UnitySendMessage(const char* gameObject, const char* method, const char* message);

extern "C" {

void _MolocoEventsSDK_initialize(const char* appKey) {
    if (appKey == NULL) return;
    // +stringWithUTF8String: returns nil on invalid UTF-8. The Swift facade's
    // parameter is non-optional `String`, so passing nil through would crash
    // inside the bridging thunk. Bail out instead.
    NSString* key = [NSString stringWithUTF8String:appKey];
    if (key == nil) return;
    if (@available(iOS 13.0, *)) {
        [MolocoEventsSDK initializeWithAppKey:key];
    }
}

void _MolocoEventsSDK_trackEvent(const char* name, const char* dataJson) {
    if (name == NULL) return;
    NSString* eventName = [NSString stringWithUTF8String:name];
    if (eventName == nil) return;
    NSDictionary* data = nil;
    if (dataJson != NULL) {
        NSString* jsonString = [NSString stringWithUTF8String:dataJson];
        if (jsonString != nil) {
            NSData* jsonData = [jsonString dataUsingEncoding:NSUTF8StringEncoding];
            NSError* err = nil;
            id parsed = [NSJSONSerialization JSONObjectWithData:jsonData options:0 error:&err];
            if ([parsed isKindOfClass:[NSDictionary class]]) {
                data = (NSDictionary*)parsed;
            }
        }
    }
    if (@available(iOS 13.0, *)) {
        [MolocoEventsSDK trackEventWithName:eventName data:data];
    }
}

/// Demo-only: returns a UTF-8 C string snapshot of current SDK state for
/// the Unity demo's "Print SDK State" button. Mirrors SwiftDemo's behavior.
///
/// Uses a static buffer (bounded ~4KB, sufficient for the multi-line snapshot)
/// so we don't have to teach C# how to free the result. Not thread-safe —
/// fine for a UI button handler since the user only taps one at a time.
const char* _MolocoEventsSDK_currentState(void) {
    static char buffer[4096];
    if (@available(iOS 13.0, *)) {
        NSString* state = [MolocoEventsSDK _currentStateForTesting];
        strncpy(buffer, [state UTF8String], sizeof(buffer) - 1);
        buffer[sizeof(buffer) - 1] = '\0';
        return buffer;
    }
    return "<iOS 13+ required>";
}

/// Demo-only: hook the SDK's internal log mirror so every log line emitted by
/// the SDK (debug + info + error) is forwarded to a hidden Unity GameObject
/// named `__MolocoSdkLogReceiver`, which the C# wrapper creates and which
/// re-emits via `Debug.Log("[SDK] " + msg)`. From there the host scene's
/// `Application.logMessageReceived` subscriber picks it up and renders it
/// into the on-device log view, matching SwiftDemo behavior.
///
/// The SDK's `_setLogHandlerForTesting:` is always compiled in (the SDK ships
/// only for testing, so the hook is not stripped from release). We still use a
/// runtime selector check rather than a link-time symbol so this bridge stays
/// resilient if a future SDK build ever drops the hook — the install simply
/// becomes a no-op instead of failing to link.
void _MolocoEventsSDK_installUnityLogMirror(void) {
    if (@available(iOS 13.0, *)) {
        SEL sel = NSSelectorFromString(@"_setLogHandlerForTesting:");
        if ([MolocoEventsSDK respondsToSelector:sel]) {
            // UnitySendMessage is documented as thread-safe — Unity buffers the
            // call and dispatches the receiver on the main thread, so it's OK
            // to invoke this from whichever thread the SDK logger fires on.
            void (^handler)(NSString *) = ^(NSString * _Nonnull message) {
                UnitySendMessage("__MolocoSdkLogReceiver", "OnSdkLog", [message UTF8String]);
            };
            // Call via objc_msgSend cast — `performSelector:withObject:` doesn't
            // play well with block arguments because ARC can't bridge them.
            ((void (*)(id, SEL, id))objc_msgSend)([MolocoEventsSDK class], sel, handler);
        }
    }
}

}
