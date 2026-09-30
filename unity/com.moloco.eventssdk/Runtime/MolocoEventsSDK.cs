using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Moloco {
    public static class MolocoEventsSDK {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _MolocoEventsSDK_initialize(string appKey);
        [DllImport("__Internal")] private static extern void _MolocoEventsSDK_trackEvent(string name, string dataJson);
        [DllImport("__Internal")] private static extern void _MolocoEventsSDK_installUnityLogMirror();
        [DllImport("__Internal")] private static extern System.IntPtr _MolocoEventsSDK_currentState();
#endif

        public static void Initialize(string appKey) {
#if UNITY_IOS && !UNITY_EDITOR
            _MolocoEventsSDK_initialize(appKey);
#endif
        }

        public static void TrackEvent(string name, Dictionary<string, object> data = null) {
#if UNITY_IOS && !UNITY_EDITOR
            string json = data != null ? DictToJson(data) : null;
            _MolocoEventsSDK_trackEvent(name, json);
#endif
        }

        /// <summary>
        /// Demo-only: install a side-channel that surfaces SDK internal log
        /// lines (init success / failure / event retries / drops / etc.) into
        /// Unity's logging pipeline. Each native log line arrives at a hidden
        /// MonoBehaviour <c>SdkLogReceiver</c> via Unity's
        /// <c>UnitySendMessage</c>, which re-emits via <c>Debug.Log("[SDK] " +
        /// msg)</c>. Host scenes subscribing to
        /// <c>Application.logMessageReceived</c> then see SDK traces alongside
        /// their own logs. Call once at startup, before any SDK invocation.
        ///
        /// No-op outside iOS device builds (Editor, other platforms).
        /// </summary>
        public static void InstallLogMirror() {
#if UNITY_IOS && !UNITY_EDITOR
            SdkLogReceiver.Install();
            _MolocoEventsSDK_installUnityLogMirror();
#endif
        }

        /// <summary>
        /// Returns a multi-line human-readable snapshot of current SDK state
        /// (state machine, applied config, kill switches, pre-init buffer
        /// count). Mirrors SwiftDemo's "Print SDK State" button. Format is
        /// best-effort and not stable across SDK versions.
        ///
        /// Returns a placeholder string outside iOS device builds (Editor,
        /// other platforms).
        /// </summary>
        public static string CurrentStateForTesting() {
#if UNITY_IOS && !UNITY_EDITOR
            var ptr = _MolocoEventsSDK_currentState();
            return Marshal.PtrToStringAnsi(ptr) ?? "<empty>";
#else
            return "<Editor mode — SDK is iOS-only>";
#endif
        }

        /// <summary>
        /// Manual JSON-object serializer for <c>Dictionary&lt;string, object&gt;</c>.
        /// Unity's <c>JsonUtility.ToJson</c> can't serialize a Dictionary directly
        /// (it requires <c>[Serializable]</c> POCOs with fixed fields), so the
        /// earlier <c>SerializableMap</c> wrapper produced the wrong wire shape:
        /// <c>{"keys":["level"],"values":["3"]}</c> instead of <c>{"level":3}</c>.
        /// This builder produces a correct JSON object preserving user keys
        /// and value types (int / long / float / double / bool / string / null).
        /// Nested dictionaries / arrays are not supported in v1 — unrecognized
        /// values are stringified. ~30 lines, zero new dependencies.
        /// </summary>
        internal static string DictToJson(Dictionary<string, object> data) {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in data) {
                if (!first) sb.Append(',');
                first = false;
                AppendJsonString(sb, kv.Key);
                sb.Append(':');
                AppendJsonValue(sb, kv.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendJsonValue(StringBuilder sb, object value) {
            switch (value) {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: AppendJsonString(sb, s); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                default: AppendJsonString(sb, value.ToString()); break;
            }
        }

        private static void AppendJsonString(StringBuilder sb, string s) {
            sb.Append('"');
            foreach (char c in s) {
                switch (c) {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:X4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }

    /// <summary>
    /// Hidden GameObject + MonoBehaviour that receives SDK log lines from the
    /// native bridge via <c>UnitySendMessage</c>. Re-emits each line via
    /// <c>Debug.Log</c> so any host scene already subscribing to
    /// <c>Application.logMessageReceived</c> picks them up without further
    /// wiring. Installed lazily by <c>MolocoEventsSDK.InstallLogMirror()</c>.
    /// Marked <c>DontDestroyOnLoad</c> so scene reloads don't drop it.
    /// </summary>
    internal class SdkLogReceiver : MonoBehaviour {
        private const string GameObjectName = "__MolocoSdkLogReceiver";

        internal static void Install() {
            if (GameObject.Find(GameObjectName) != null) return;  // idempotent
            var go = new GameObject(GameObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<SdkLogReceiver>();
        }

        // Method name MUST match the second argument of `UnitySendMessage` on
        // the native side ("OnSdkLog"). Signature must take a single `string`.
        // Called on Unity's main thread by Unity's runtime.
        public void OnSdkLog(string message) {
            Debug.Log("[SDK] " + message);
        }
    }
}
