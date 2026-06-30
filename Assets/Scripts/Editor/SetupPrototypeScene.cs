using AnimationCreator.Playback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimationCreator.Editor
{
  public static class SetupPrototypeScene
  {
    [MenuItem("AnimationCreator/Setup Prototype Scene")]
    public static void Setup()
    {
      foreach (var director in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
      {
        if (director is MatchDirector or SituationDirector)
          Object.DestroyImmediate(director.gameObject);
      }

      var bootstrapGo = new GameObject("SituationDirector");
      bootstrapGo.AddComponent<SituationDirector>();

      EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
      Debug.Log("FRDS Situation Replay hazır. Play'e basın — offside_demo yüklenecek.");
    }
  }
}
