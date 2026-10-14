using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MobileWebBuild
{
    [Serializable]
    private class WebManifest
    {
        public string loaderUrl;
        public string dataUrl;
        public string frameworkUrl;
        public string codeUrl;
        public string companyName = "Grid Caster";
        public string productName = "Grid Caster · Playtest";
        public string productVersion = "0.1";
    }

    [MenuItem("Tools/Build/Mobile Safari Playtest")]
    public static void Build()
    {
        string output = Argument("-mobileWebOutput") ?? Path.GetFullPath("Builds/MobileWeb");
        string sourceRoot = Argument("-mobileWebSourceRoot") ?? Directory.GetCurrentDirectory();
        // 플랫폼/품질 설정과 저장 씬은 변경하지 않는다. 빌드만 Web 대상으로 수행한다.
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Mobile Web build failed: " + report.summary.result);
        string[] files = Directory.GetFiles(Path.Combine(output, "Build"));
        string Url(string suffix)
        {
            string file = Path.GetFileName(files.Single(path =>
                Path.GetFileName(path).EndsWith(suffix) || Path.GetFileName(path).EndsWith(suffix + ".br")
                || Path.GetFileName(path).EndsWith(suffix + ".gz") || Path.GetFileName(path).EndsWith(suffix + ".unityweb")));
            // HTTP 로컬 서버가 압축을 풀어 전달하므로 로더에도 비압축 URL을 제공한다.
            // Unity의 .unityweb fallback은 로더가 직접 해제하므로 접미사를 유지한다.
            if (file.EndsWith(".br")) file = file.Substring(0, file.Length - 3);
            else if (file.EndsWith(".gz")) file = file.Substring(0, file.Length - 3);
            return "Build/" + file;
        }
        var manifest = new WebManifest { loaderUrl = Url(".loader.js"), dataUrl = Url(".data"),
            frameworkUrl = Url(".framework.js"), codeUrl = Url(".wasm") };
        File.WriteAllText(Path.Combine(output, "build-manifest.json"), JsonUtility.ToJson(manifest, true));
        File.Copy(Path.Combine(sourceRoot, "Tools/mobile-playtest/index.html"), Path.Combine(output, "index.html"), true);
        Debug.Log($"MOBILE_WEB_BUILD_SUCCEEDED {output} ({report.summary.totalSize} bytes)");
    }

    private static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
