using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VibeRun.Editor
{
    /// <summary>Scene setup, player settings and batch builds. All menu items live under Tools ▸ Vibe Run.</summary>
    public static class BuildTools
    {
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        static string[] Scenes => new[] { ScenePath };
        static string Root => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Tools/Vibe Run/Build Main Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.07f, 0.11f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[VibeRun] Main scene built at " + ScenePath);
        }

        [MenuItem("Tools/Vibe Run/Apply Player Settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Emergent";
            PlayerSettings.productName = "Vibe Run";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.emergent.viberun");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.emergent.viberun");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            Debug.Log("[VibeRun] player settings applied");
        }

        static void Build(BuildTarget target, string relPath, BuildOptions opts, bool runInBackground = false)
        {
            if (!File.Exists(Path.Combine(Root, ScenePath))) BuildScene();
            ApplyPlayerSettings();
            PlayerSettings.runInBackground = runInBackground;
            string path = Path.Combine(Root, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = Scenes, locationPathName = path, target = target, options = opts });
            Debug.Log("[VibeRun] " + target + " build: " + report.summary.result + " -> " + path);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Tools/Vibe Run/Build macOS (dev)")]
        public static void BuildMac()
        {
            // dev build keeps running unfocused so the -vr-shot harness works from a terminal
            Build(BuildTarget.StandaloneOSX, "Builds/mac/VibeRun.app", BuildOptions.Development, runInBackground: true);
        }

        [MenuItem("Tools/Vibe Run/Build Android (apk)")]
        public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/android/VibeRun.apk", BuildOptions.None);

        [MenuItem("Tools/Vibe Run/Build iOS (Xcode project)")]
        public static void BuildIOS() => Build(BuildTarget.iOS, "Builds/ios", BuildOptions.None);

        [MenuItem("Tools/Vibe Run/Build WebGL")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/web", BuildOptions.None);

        [MenuItem("Tools/Vibe Run/Reset Save Data")]
        public static void ResetSave()
        {
            Services.SaveService.Reset();
            Debug.Log("[VibeRun] save data cleared");
        }
    }
}
