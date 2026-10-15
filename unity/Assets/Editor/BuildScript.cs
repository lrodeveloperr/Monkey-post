using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MonkeyPost.EditorTools
{
    /// <summary>
    /// Configures the project and builds the Android APK. Used by CI (GameCI, via -executeMethod)
    /// and available in the editor under the "Monkey Post" menu.
    /// The game builds itself from code, so the scene and material are generated here rather
    /// than hand-authored.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string MaterialPath = "Assets/Resources/MonkeyPostFlat.mat";
        const string ApplicationId = "com.worksbienstudios.monkeypost";

        static BuildScript()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return;
                EnsureMaterial();
                EnsureScene();
            };
        }

        [MenuItem("Monkey Post/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigurePlayer();
            EnsureMaterial();
            EnsureScene();

            string path = Argument("-customBuildPath") ?? "Builds/Android/MonkeyPost.apk";
            if (!path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "MonkeyPost.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[MonkeyPost] Build {summary.result}: {path}, {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalTime}");
            if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }

        static void ConfigurePlayer()
        {
            int build = BuildNumber();
            PlayerSettings.companyName = "Worksbien Studios Inc.";
            PlayerSettings.productName = "Monkey Post";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, ApplicationId);
            PlayerSettings.bundleVersion = $"0.1.{build}";
            PlayerSettings.Android.bundleVersionCode = build;

            // Android 8.0+, 64-bit and 32-bit ARM so it installs on every phone in use in Nigeria.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SplashScreen.show = false;
            UseClassicInput();
        }

        /// <summary>The prototype reads touches through the classic Input class.</summary>
        static void UseClassicInput()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var settings = new SerializedObject(assets[0]);
            var handler = settings.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue == 0) return;
            handler.intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            var shader = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Standard");
            AssetDatabase.CreateAsset(new Material(shader), MaterialPath);
            AssetDatabase.SaveAssets();
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static int BuildNumber()
        {
            string run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            return int.TryParse(run, out int n) && n > 0 ? n : 1;
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
