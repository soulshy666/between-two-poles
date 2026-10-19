using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BetweenPoles {
public static class AndroidReleaseBuild {
    public const string PackageName = "com.betweentwopoles.game";
    // Increase VersionCode for every new upload; retain the same signing key.
    public const string VersionName = "0.1.0";
    public const int VersionCode = 1;

    [Serializable] sealed class Signing {
        public string keystorePath;
        public string keystorePassword;
        public string alias;
        public string aliasPassword;
    }

    [MenuItem("Between Poles/Build/Android signed release APK")]
    public static void Build() {
        var configPath = Environment.GetEnvironmentVariable("BETWEEN_POLES_SIGNING_CONFIG");
        if(string.IsNullOrWhiteSpace(configPath))
            configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BetweenTwoPoles", "AndroidSigning", "signing.json");
        if(!File.Exists(configPath))throw new Exception("Release signing config missing. See Docs/AndroidRelease.md. Never generate a replacement key for an existing release.");
        var signing = JsonUtility.FromJson<Signing>(File.ReadAllText(configPath));
        if(signing == null || string.IsNullOrEmpty(signing.keystorePath) || !File.Exists(signing.keystorePath)
            || string.IsNullOrEmpty(signing.keystorePassword) || string.IsNullOrEmpty(signing.alias)
            || string.IsNullOrEmpty(signing.aliasPassword))throw new Exception("Release signing config is incomplete.");
        const string settingsPath = "ProjectSettings/ProjectSettings.asset";
        var serializedSettings = File.ReadAllBytes(settingsPath);
        const BuildTargetGroup group = BuildTargetGroup.Android;
        var identifier = PlayerSettings.GetApplicationIdentifier(group);
        var backend = PlayerSettings.GetScriptingBackend(group);
        var architectures = PlayerSettings.Android.targetArchitectures;
        var product = PlayerSettings.productName;
        var version = PlayerSettings.bundleVersion;
        int code = PlayerSettings.Android.bundleVersionCode;
        bool custom = PlayerSettings.Android.useCustomKeystore;
        var storeName = PlayerSettings.Android.keystoreName;
        var storePass = PlayerSettings.Android.keystorePass;
        var alias = PlayerSettings.Android.keyaliasName;
        var aliasPass = PlayerSettings.Android.keyaliasPass;
        bool bundle = EditorUserBuildSettings.buildAppBundle;
        bool development = EditorUserBuildSettings.development;
        bool debugging = EditorUserBuildSettings.allowDebugging;
        bool profiler = EditorUserBuildSettings.connectProfiler;
        try {
            PlayerSettings.SetApplicationIdentifier(group, PackageName);
            PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.productName = "两极之间";
            PlayerSettings.bundleVersion = VersionName;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(signing.keystorePath);
            PlayerSettings.Android.keystorePass = signing.keystorePassword;
            PlayerSettings.Android.keyaliasName = signing.alias;
            PlayerSettings.Android.keyaliasPass = signing.aliasPassword;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if(scenes.Length == 0 || scenes[0] != "Assets/BetweenPoles/Scenes/Cover.unity")
                throw new Exception("Release must start with the Cover scene.");
            foreach(var scene in scenes)if(!File.Exists(scene))throw new Exception("Missing build scene: " + scene);
            Directory.CreateDirectory("output/android");
            string output = "output/android/between-two-poles-release-" + VersionName + ".apk";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = output, target = BuildTarget.Android, options = BuildOptions.None
            });
            File.WriteAllText("output/android/release-build-result.txt", report.summary.result + "\nErrors: "
                + report.summary.totalErrors + "\nPackage: " + PackageName + "\nVersion: " + VersionName
                + "\nVersionCode: " + VersionCode + "\nDuration: " + report.summary.totalTime);
            if(report.summary.result != BuildResult.Succeeded)throw new Exception("Android release build failed: " + report.summary.result);
            Debug.Log("Signed release APK ready: " + output);
        } finally {
            PlayerSettings.SetApplicationIdentifier(group, identifier);
            PlayerSettings.SetScriptingBackend(group, backend);
            PlayerSettings.Android.targetArchitectures = architectures;
            PlayerSettings.productName = product;
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = code;
            PlayerSettings.Android.useCustomKeystore = custom;
            PlayerSettings.Android.keystoreName = storeName;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            EditorUserBuildSettings.buildAppBundle = bundle;
            EditorUserBuildSettings.development = development;
            EditorUserBuildSettings.allowDebugging = debugging;
            EditorUserBuildSettings.connectProfiler = profiler;
            try { AssetDatabase.SaveAssets(); }
            finally { File.WriteAllBytes(settingsPath, serializedSettings); }
        }
    }
}
}
