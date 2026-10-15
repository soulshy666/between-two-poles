using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BetweenPoles {
public static class AndroidPlaytestBuild {
    [MenuItem("Between Poles/Build/Android playtest APK")]
    public static void Build() {
        const BuildTargetGroup group = BuildTargetGroup.Android;
        var identifier = PlayerSettings.GetApplicationIdentifier(group);
        var backend = PlayerSettings.GetScriptingBackend(group);
        var architectures = PlayerSettings.Android.targetArchitectures;
        var product = PlayerSettings.productName;
        bool bundle = EditorUserBuildSettings.buildAppBundle;
        // Preserve absent/default entries too: Unity's setters otherwise serialize
        // new identifier overrides even when restoring their original values.
        const string settingsPath = "ProjectSettings/ProjectSettings.asset";
        var serializedSettings = File.ReadAllBytes(settingsPath);
        Directory.CreateDirectory("output/android");
        try {
            // This identity is for local testing only, not a store registration.
            PlayerSettings.SetApplicationIdentifier(group,"com.betweentwopoles.playtest");
            PlayerSettings.SetScriptingBackend(group,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARMv7|AndroidArchitecture.ARM64;
            PlayerSettings.productName="Between Two Poles Test";
            EditorUserBuildSettings.buildAppBundle=false;
            var scenes=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();
            foreach(var scene in scenes)if(!File.Exists(scene))throw new Exception("Missing build scene: "+scene);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=scenes,locationPathName="output/android/between-two-poles-test.apk",
                target=BuildTarget.Android,options=BuildOptions.Development
            });
            File.WriteAllText("output/android/build-result.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nSize: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Android playtest build failed: "+report.summary.result);
            Debug.Log("Android playtest APK ready: output/android/between-two-poles-test.apk");
        } finally {
            PlayerSettings.SetApplicationIdentifier(group,identifier);
            PlayerSettings.SetScriptingBackend(group,backend);
            PlayerSettings.Android.targetArchitectures=architectures;
            PlayerSettings.productName=product;
            EditorUserBuildSettings.buildAppBundle=bundle;
            AssetDatabase.SaveAssets();
            File.WriteAllBytes(settingsPath,serializedSettings);
        }
    }
}
}
