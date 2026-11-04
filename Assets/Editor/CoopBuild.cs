using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the co-op test: only the menu, the lobby and the Arena, without FirebaseConfig.json, so it can't touch
// accounts or the ranking.
// Unity.exe -batchmode -quit -projectPath <proj> -buildTarget <Win64|WebGL> -executeMethod CoopBuild.Build -coopOut <dir>
public static class CoopBuild {

    private const string LobbyScene = "Assets/Scripts/Coop/Coop Lobby.unity";
    private const string ArenaScene = "Assets/Scenes/ZE Arena.unity";
    private const string MenuScene = "Assets/Scenes/UI - Main Menu.unity";
    private const string MenuFont = "Assets/Sprites/Interface/Resources/Fonts/PressStart2P.ttf";

    public static void Build() {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-coopOut");
        string outDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/Coop";
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

        MakeLobby();
        TrueTypeFontImporter font = null;
        FontRenderingMode originalMode = FontRenderingMode.Smooth;
        string location = outDir + "/Freny Coop.exe";
        if (target == BuildTarget.WebGL) {
            location = outDir;
            // itch.io doesn't send Content-Encoding, so the loader decompresses the Gzip itself
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            font = (TrueTypeFontImporter)AssetImporter.GetAtPath(MenuFont);
            originalMode = font.fontRenderingMode;
            SetFontMode(font, FontRenderingMode.Smooth);
        }
        PlayerSettings.runInBackground = true;

        BuildReport report;
        try {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { MenuScene, LobbyScene, ArenaScene },
                locationPathName = location,
                target = target,
                options = BuildOptions.None,
            });
        } finally {
            if (font != null) {
                SetFontMode(font, originalMode);
            }
        }
        Debug.Log($"[CoopBuild] {report.summary.result}, {report.summary.totalSize} bytes, {report.summary.totalErrors} errors");
        if (report.summary.result != BuildResult.Succeeded) {
            EditorApplication.Exit(1);
        }
    }

    private static void MakeLobby() {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject camera = new GameObject("Camera");
        Camera cam = camera.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.03f, 0.05f);
        new GameObject("Lobby").AddComponent<CoopLobby>();
        EditorSceneManager.SaveScene(scene, LobbyScene);
    }

    private static void SetFontMode(TrueTypeFontImporter font, FontRenderingMode mode) {
        if (font.fontRenderingMode != mode) {
            font.fontRenderingMode = mode;
            font.SaveAndReimport();
        }
    }
}
