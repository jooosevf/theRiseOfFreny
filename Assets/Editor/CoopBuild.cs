using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the co-op test: only the menu and the Arena, without FirebaseConfig.json, so it can't touch
// accounts or the ranking.
// Unity.exe -batchmode -quit -projectPath <proj> -buildTarget <Win64|WebGL> -executeMethod CoopBuild.Build -coopOut <dir>
public static class CoopBuild {

    private const string ArenaScene = "Assets/Scenes/ZE Arena.unity";
    private const string MenuScene = "Assets/Scenes/UI - Main Menu.unity";
    private const string MenuFont = "Assets/Sprites/Interface/Resources/Fonts/PressStart2P.ttf";

    public static void Build() {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-coopOut");
        string outDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/Coop";
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        TrueTypeFontImporter font = null;
        FontRenderingMode originalMode = FontRenderingMode.Smooth;
        string location = outDir + "/Freny Coop.exe";
        if (target == BuildTarget.WebGL) {
            location = outDir;
            // itch.io doesn't send Content-Encoding, so the loader decompresses the Gzip itself
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            font = (TrueTypeFontImporter)AssetImporter.GetAtPath(MenuFont);
            originalMode = font.fontRenderingMode;
            SetFontMode(font, FontRenderingMode.Smooth);
        }
        PlayerSettings.runInBackground = true;

        BuildReport report;
        try {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { MenuScene, ArenaScene },
                locationPathName = location,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = new[] { "FRENY_COOP_BUILD" },   // Features.Coop on here, whatever the release has
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
        if (target == BuildTarget.WebGL) {
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "index.html"), Page(System.IO.Path.GetFileName(outDir)));
        }
    }

    // The arena's camera shows the whole map only at 16:9 (ArenaBounds: the screen is the arena), and
    // Unity's default page is 16:10, which cuts the towers. This one keeps 16:9 at any window size.
    private static string Page(string name) {
        return (@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>The Rise of Freny: co-op</title>
<style>
html, body { margin: 0; height: 100%; background: #0a0810; overflow: hidden; }
#game { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); display: block; background: #0a0810; }
#loading { position: absolute; left: 0; right: 0; top: 50%; text-align: center; color: #b39ad1; font: 16px monospace; }
</style>
</head>
<body>
<canvas id=""game"" tabindex=""-1""></canvas>
<div id=""loading"">Carregando...</div>
<script src=""Build/NAME.loader.js""></script>
<script>
var canvas = document.getElementById('game');
function fit() {
  var w = Math.min(window.innerWidth, window.innerHeight * 16 / 9);
  canvas.style.width = Math.floor(w) + 'px';
  canvas.style.height = Math.floor(w * 9 / 16) + 'px';
}
window.addEventListener('resize', fit);
fit();
createUnityInstance(canvas, {
  dataUrl: 'Build/NAME.data.unityweb',
  frameworkUrl: 'Build/NAME.framework.js.unityweb',
  codeUrl: 'Build/NAME.wasm.unityweb',
  streamingAssetsUrl: 'StreamingAssets',
  companyName: 'DefaultCompany',
  productName: 'The Rise of Freny',
  productVersion: '" + Application.version + @"'
}, function (progress) {
  document.getElementById('loading').textContent = 'Carregando... ' + Math.round(progress * 100) + '%';
}).then(function () {
  document.getElementById('loading').remove();
}).catch(function (message) { alert(message); });
</script>
</body>
</html>
").Replace("NAME", name);
    }

    private static void SetFontMode(TrueTypeFontImporter font, FontRenderingMode mode) {
        if (font.fontRenderingMode != mode) {
            font.fontRenderingMode = mode;
            font.SaveAndReimport();
        }
    }
}
