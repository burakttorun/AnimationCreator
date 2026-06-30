using System.IO;
using AnimationCreator.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimationCreator.Editor
{
  public static class BakeMocapDemo
  {
    const string PresentationSequenceId = "presentation_demo";
    const string MatchClipSequenceId = "match_clip_demo";
    const string SkillCornerSequenceId = "skillcorner_demo";
    const string DefaultSkillCornerMatchDir = "ExternalData/SkillCorner/matches/1886347";
    const int SkillCornerClipStartFrame = 5000;
    const string MocapSequenceId = "mocap_run_demo";
    const string DefaultBvh = "Tools/sample_data/09_01/09_01.bvh";
    const string DefaultSoccerNetClip = "ExternalData/SoccerNetGS/test/SNGS-116/Labels-GameState.json";
    const string FallbackSoccerNetClip = "ExternalData/SoccerNetGS-test/tracklab/SNGS-116.json";

    static string BuildAlignedClipArgs(string projectRoot)
    {
      var input = ResolveSoccerNetInput(projectRoot) ?? FallbackSoccerNetClip;
      return $"--input \"{input}\" --sequence-id {MatchClipSequenceId} --start-frame {AlignedClipStartFrame} --duration-sec 8 --max-players 22 --joint-mode tracking";
    }

    static string ResolveSoccerNetInput(string projectRoot)
    {
      var primary = Path.Combine(projectRoot, DefaultSoccerNetClip);
      if (File.Exists(primary))
        return Path.GetRelativePath(projectRoot, primary).Replace('\\', '/');

      var fallback = Path.Combine(projectRoot, FallbackSoccerNetClip);
      if (File.Exists(fallback))
        return Path.GetRelativePath(projectRoot, fallback).Replace('\\', '/');

      return null;
    }

    const int AlignedClipStartFrame = 375;

    [MenuItem("AnimationCreator/Bake presentation_demo (clean procedural)")]
    public static void BakePresentation()
    {
      RunDotnetTool(
        Path.Combine("Tools", "GeneratePresentationDemo", "GeneratePresentationDemo.csproj"),
        string.Empty,
        "presentation_demo baked.");
    }

    [MenuItem("AnimationCreator/Download SNGS-116 broadcast frames")]
    public static void DownloadBroadcastFrames()
    {
      RunNodeTool(
        Path.Combine("Tools", "download_sngs116_video.mjs"),
        $"--start {AlignedClipStartFrame} --count 200",
        "SNGS-116 broadcast frames downloaded.");
    }

    [MenuItem("AnimationCreator/Bake match_clip_demo from SoccerNet (real match)")]
    public static void BakeMatchClip()
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
      var inputPath = ResolveSoccerNetInput(projectRoot);
      if (inputPath == null)
      {
        EditorUtility.DisplayDialog(
          "SoccerNet clip missing",
          $"Expected SoccerNet labels or tracking JSON.\n{DefaultSoccerNetClip}\nor\n{FallbackSoccerNetClip}",
          "OK");
        return;
      }

      RunNodeTool(
        Path.Combine("Tools", "convert_soccernet_gs_to_frds.mjs"),
        BuildAlignedClipArgs(projectRoot),
        "match_clip_demo baked from real SoccerNet tracking.");
    }

    [MenuItem("AnimationCreator/Download SkillCorner match (1886347)")]
    public static void DownloadSkillCornerMatch()
    {
      RunNodeTool(
        Path.Combine("Tools", "download_skillcorner.mjs"),
        "--match-id 1886347",
        "SkillCorner open-data match downloaded.");
    }

    [MenuItem("AnimationCreator/Bake skillcorner_demo from SkillCorner")]
    public static void BakeSkillCornerDemo()
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
      var matchDir = Path.Combine(projectRoot, DefaultSkillCornerMatchDir);
      var tracking = Path.Combine(matchDir, "1886347_tracking_extrapolated.jsonl");
      if (!File.Exists(tracking))
      {
        EditorUtility.DisplayDialog(
          "SkillCorner data missing",
          $"Expected tracking JSONL at:\n{tracking}\n\nUse AnimationCreator → Download SkillCorner match first.",
          "OK");
        return;
      }

      var relDir = Path.GetRelativePath(projectRoot, matchDir).Replace('\\', '/');
      RunNodeTool(
        Path.Combine("Tools", "convert_skillcorner_to_frds.mjs"),
        $"--match-dir \"{relDir}\" --sequence-id {SkillCornerSequenceId} --start-frame {SkillCornerClipStartFrame} --duration-sec 8 --fps 10",
        "skillcorner_demo baked from SkillCorner tracking.");
    }

    [MenuItem("AnimationCreator/Setup SkillCorner Scene")]
    public static void SetupSkillCornerScene()
    {
      var scenePath = "Assets/Scenes/SkillCornerDemo.unity";
      var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

      foreach (var existing in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
      {
        if (existing is MatchDirector or SituationDirector)
          Object.DestroyImmediate(existing.gameObject);
      }

      var bootstrapGo = new GameObject("MatchDirector");
      var matchDirector = bootstrapGo.AddComponent<MatchDirector>();

      var so = new SerializedObject(matchDirector);
      so.FindProperty("sequenceId").stringValue = SkillCornerSequenceId;
      so.FindProperty("pitchLengthM").floatValue = 104f;
      so.FindProperty("pitchWidthM").floatValue = 68f;
      so.FindProperty("smoothJoints").boolValue = false;
      so.FindProperty("showBroadcastVideo").boolValue = true;
      so.FindProperty("broadcastClipId").stringValue = "1886347";
      so.FindProperty("broadcastSourceStartFrame").intValue = 0;
      so.FindProperty("broadcastPretrimmedClip").boolValue = true;
      so.ApplyModifiedPropertiesWithoutUndo();

      EditorSceneManager.SaveScene(scene, scenePath);
      EditorSceneManager.OpenScene(scenePath);
      Debug.Log($"Saved {scenePath}. Play to preview {SkillCornerSequenceId} (Auckland FC vs Newcastle, 10 Hz tracking).");
    }

    [MenuItem("AnimationCreator/Setup SkillCorner (download + bake + scene)")]
    public static void SetupSkillCornerFull()
    {
      DownloadSkillCornerMatch();
      BakeSkillCornerDemo();
      SetupSkillCornerScene();
    }

    [MenuItem("AnimationCreator/Setup Aligned Comparison (bake + download + scene)")]
    public static void SetupAlignedComparison()
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;

      RunNodeTool(
        Path.Combine("Tools", "download_soccernet_labels.mjs"),
        "SNGS-116",
        "Official SoccerNet labels downloaded.");

      if (ResolveSoccerNetInput(projectRoot) == null && !File.Exists(Path.Combine(projectRoot, FallbackSoccerNetClip)))
      {
        EditorUtility.DisplayDialog(
          "SoccerNet clip missing",
          $"Download failed. Expected:\n{DefaultSoccerNetClip}\nor\n{FallbackSoccerNetClip}",
          "OK");
        return;
      }

      RunNodeTool(
        Path.Combine("Tools", "convert_soccernet_gs_to_frds.mjs"),
        BuildAlignedClipArgs(projectRoot),
        "match_clip_demo baked.");

      RunNodeTool(
        Path.Combine("Tools", "download_sngs116_video.mjs"),
        $"--start {AlignedClipStartFrame} --count 200",
        "Broadcast frames downloaded.");

      SetupMatchClipScene();
    }

    [MenuItem("AnimationCreator/Setup Real Match Scene")]
    public static void SetupMatchClipScene()
    {
      var scenePath = "Assets/Scenes/MatchClipDemo.unity";
      var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

      foreach (var existing in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
      {
        if (existing is MatchDirector or SituationDirector)
          Object.DestroyImmediate(existing.gameObject);
      }

      var bootstrapGo = new GameObject("MatchDirector");
      var matchDirector = bootstrapGo.AddComponent<MatchDirector>();

      var so = new SerializedObject(matchDirector);
      so.FindProperty("sequenceId").stringValue = MatchClipSequenceId;
      so.FindProperty("smoothJoints").boolValue = true;
      so.FindProperty("showBroadcastVideo").boolValue = true;
      so.FindProperty("broadcastClipId").stringValue = "SNGS-116";
      so.FindProperty("broadcastSplit").stringValue = "test";
      so.FindProperty("broadcastSourceStartFrame").intValue = AlignedClipStartFrame;
      so.ApplyModifiedPropertiesWithoutUndo();

      EditorSceneManager.SaveScene(scene, scenePath);
      EditorSceneManager.OpenScene(scenePath);
      Debug.Log($"Saved {scenePath}. Play to preview {MatchClipSequenceId}. Add video to ExternalData/SoccerNetGS/test/SNGS-116/video.mkv");
    }

    [MenuItem("AnimationCreator/Bake mocap_run_demo from CMU BVH (experimental)")]
    public static void BakeMocap()
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
      var bvhPath = Path.Combine(projectRoot, DefaultBvh);
      if (!File.Exists(bvhPath))
      {
        EditorUtility.DisplayDialog(
          "BVH missing",
          $"Expected sample clip at:\n{bvhPath}",
          "OK");
        return;
      }

      var toolProject = Path.Combine(projectRoot, "Tools", "BvhToFrds", "BvhToFrds.csproj");
      RunDotnetTool(toolProject, $"\"{bvhPath}\" {MocapSequenceId} 4", "mocap_run_demo baked (experimental).");
    }

    [MenuItem("AnimationCreator/Setup Presentation Scene")]
    public static void SetupPresentationScene()
    {
      foreach (var existing in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
      {
        if (existing is MatchDirector or SituationDirector)
          Object.DestroyImmediate(existing.gameObject);
      }

      var bootstrapGo = new GameObject("MatchDirector");
      var matchDirector = bootstrapGo.AddComponent<MatchDirector>();

      var so = new SerializedObject(matchDirector);
      so.FindProperty("sequenceId").stringValue = PresentationSequenceId;
      so.FindProperty("smoothJoints").boolValue = true;
      so.ApplyModifiedPropertiesWithoutUndo();

      EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
      Debug.Log($"Presentation scene ready. Play to preview {PresentationSequenceId}.");
    }

    static void RunNodeTool(string scriptRelativePath, string arguments, string successTitle)
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
      var scriptPath = Path.Combine(projectRoot, scriptRelativePath);
      var nodePath = ResolveExecutable(
        "node",
        System.Environment.GetEnvironmentVariable("ANIMATIONCREATOR_NODE"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe"),
        Path.Combine(
          System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
          "Programs",
          "cursor",
          "resources",
          "app",
          "resources",
          "helpers",
          "node.exe"));

      if (!File.Exists(nodePath))
      {
        EditorUtility.DisplayDialog(
          "Node.js not found",
          "Install Node.js from https://nodejs.org or add node.exe to PATH, then restart Unity.",
          "OK");
        return;
      }

      var psi = new System.Diagnostics.ProcessStartInfo
      {
        FileName = nodePath,
        Arguments = $"\"{scriptPath}\" {arguments}",
        WorkingDirectory = projectRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };

      System.Diagnostics.Process process;
      try
      {
        process = System.Diagnostics.Process.Start(psi);
      }
      catch (System.ComponentModel.Win32Exception ex)
      {
        Debug.LogError($"Failed to start node ({nodePath}): {ex.Message}");
        EditorUtility.DisplayDialog("Bake failed", $"Could not run node:\n{nodePath}\n\n{ex.Message}", "OK");
        return;
      }

      if (process == null)
      {
        Debug.LogError("Failed to start node tool.");
        return;
      }

      using (process)
      {
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
          Debug.LogError($"Bake failed (exit {process.ExitCode}):\n{stderr}\n{stdout}");
          EditorUtility.DisplayDialog("Bake failed", stderr + stdout, "OK");
          return;
        }

        AssetDatabase.Refresh();
        Debug.Log($"{successTitle}\n{stdout}");
        EditorUtility.DisplayDialog("Bake complete", stdout.Trim(), "OK");
      }
    }

    static string ResolveExecutable(string name, params string[] extraCandidates)
    {
      foreach (var candidate in extraCandidates)
      {
        if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
          return candidate;
      }

      var pathEnv = System.Environment.GetEnvironmentVariable("PATH");
      if (!string.IsNullOrEmpty(pathEnv))
      {
        foreach (var dir in pathEnv.Split(';'))
        {
          if (string.IsNullOrWhiteSpace(dir))
            continue;

          var trimmed = dir.Trim();
          var withExe = name.EndsWith(".exe") ? name : name + ".exe";
          var candidate = Path.Combine(trimmed, withExe);
          if (File.Exists(candidate))
            return candidate;
        }
      }

      return name;
    }

    static void RunDotnetTool(string projectRelativePath, string arguments, string successTitle)
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
      var toolProject = Path.Combine(projectRoot, projectRelativePath);
      var dotnetPath = ResolveExecutable(
        "dotnet",
        System.Environment.GetEnvironmentVariable("ANIMATIONCREATOR_DOTNET"),
        Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"));

      if (!File.Exists(dotnetPath))
      {
        EditorUtility.DisplayDialog(
          ".NET SDK not found",
          "Install the .NET SDK from https://dotnet.microsoft.com or add dotnet.exe to PATH, then restart Unity.",
          "OK");
        return;
      }

      var psi = new System.Diagnostics.ProcessStartInfo
      {
        FileName = dotnetPath,
        Arguments = string.IsNullOrEmpty(arguments)
          ? $"run --project \"{toolProject}\""
          : $"run --project \"{toolProject}\" -- {arguments}",
        WorkingDirectory = projectRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };

      System.Diagnostics.Process process;
      try
      {
        process = System.Diagnostics.Process.Start(psi);
      }
      catch (System.ComponentModel.Win32Exception ex)
      {
        Debug.LogError($"Failed to start dotnet ({dotnetPath}): {ex.Message}");
        EditorUtility.DisplayDialog("Bake failed", $"Could not run dotnet:\n{dotnetPath}\n\n{ex.Message}", "OK");
        return;
      }

      if (process == null)
      {
        Debug.LogError("Failed to start dotnet tool.");
        return;
      }

      using (process)
      {
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
          Debug.LogError($"Bake failed (exit {process.ExitCode}):\n{stderr}\n{stdout}");
          EditorUtility.DisplayDialog("Bake failed", stderr + stdout, "OK");
          return;
        }

        AssetDatabase.Refresh();
        Debug.Log($"{successTitle}\n{stdout}");
        EditorUtility.DisplayDialog("Bake complete", stdout.Trim(), "OK");
      }
    }
  }
}
