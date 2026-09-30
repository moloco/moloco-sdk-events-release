#if UNITY_IOS
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;

namespace Moloco.Editor {
    /// <summary>
    /// Embeds MolocoEventsSDK.xcframework (a dynamic Swift framework) into the
    /// generated Xcode app so it loads at runtime, and ensures the Swift standard
    /// libraries are embedded. The .mm bridge and the xcframework are added to the
    /// build automatically by Unity from Runtime/Plugins/iOS; this only flips the
    /// embed/sign + Swift-runtime settings that a dynamic framework needs.
    /// Intentionally does NOT set DEVELOPMENT_TEAM — that's the integrator's choice.
    /// </summary>
    public static class MolocoEventsSDKPostProcessBuild {
        [PostProcessBuild(1100)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath) {
            if (target != BuildTarget.iOS) return;

            var projPath = PBXProject.GetPBXProjectPath(buildPath);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            var mainTarget = proj.GetUnityMainTargetGuid();
            var frameworkTarget = proj.GetUnityFrameworkTargetGuid();

            // Swift runtime must travel with a Swift dynamic framework.
            foreach (var t in new[] { mainTarget, frameworkTarget }) {
                proj.SetBuildProperty(t, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
                proj.AddBuildProperty(t, "LD_RUNPATH_SEARCH_PATHS",
                    "@executable_path/Frameworks @loader_path/Frameworks");
            }

            // Embed the xcframework so it's present in the .app bundle. Unity adds a
            // package's iOS plugin under Frameworks/<package-path>/, and registers the
            // .xcframework itself (not its inner .framework slice) — so we look it up
            // at the package-relative path. The Assets-relative path is kept as a
            // fallback in case the framework is ever vendored under Assets/ instead.
            string[] candidates = {
                "Frameworks/com.moloco.eventssdk/Runtime/Plugins/iOS/MolocoEventsSDK.xcframework",
                "Frameworks/MolocoEventsSDK.xcframework",
            };
            string fileGuid = null;
            foreach (var path in candidates) {
                fileGuid = proj.FindFileGuidByProjectPath(path);
                if (fileGuid != null) break;
            }
            if (fileGuid == null) {
                // Fail loud. A null guid means Unity never added the xcframework —
                // usually because it wasn't built (it's gitignored; see README) or the
                // plugin's iOS import setting is off. Silently skipping embed would link
                // fine but crash at launch on the missing dynamic framework.
                throw new BuildFailedException(
                    "MolocoEventsSDK: MolocoEventsSDK.xcframework was not found in the generated "
                    + "Xcode project. Build the xcframework first (scripts/build-xcframework.sh) so "
                    + "it is bundled under unity/com.moloco.eventssdk/Runtime/Plugins/iOS/, and make "
                    + "sure the plugin's iOS platform is enabled.");
            }
            proj.AddFileToEmbedFrameworks(mainTarget, fileGuid);

            proj.WriteToFile(projPath);
        }
    }
}
#endif
