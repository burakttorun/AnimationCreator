using System.Collections.Generic;
using UnityEngine;

namespace AnimationCreator.Playback
{
  public class JointCoordinateLabels : MonoBehaviour
  {
    readonly List<LabelEntry> _labels = new List<LabelEntry>();
    GUIStyle _style;
    Camera _camera;

    struct LabelEntry
    {
      public string Text;
      public Vector3 WorldPos;
      public bool Highlight;
    }

    public void Clear() => _labels.Clear();

    public void AddLabel(string jointName, Vector3 worldPos, bool highlight = false)
    {
      _labels.Add(new LabelEntry
      {
        Text = $"X: {worldPos.x:F3}\nY: {worldPos.y:F3}\nZ: {worldPos.z:F3}",
        WorldPos = worldPos + Vector3.up * (highlight ? 0.35f : 0.22f),
        Highlight = highlight,
      });
    }

    void EnsureStyle()
    {
      if (_style != null)
        return;

      _style = new GUIStyle(GUI.skin.label)
      {
        fontSize = 11,
        alignment = TextAnchor.MiddleLeft,
        richText = true,
        normal = { textColor = Color.white },
      };
    }

    void OnGUI()
    {
      if (_labels.Count == 0)
        return;

      if (_camera == null)
        _camera = Camera.main;
      if (_camera == null)
        return;

      EnsureStyle();

      foreach (var label in _labels)
      {
        var screen = _camera.WorldToScreenPoint(label.WorldPos);
        if (screen.z < 0f)
          continue;

        var rect = new Rect(screen.x + 8f, Screen.height - screen.y - 36f, 140f, 52f);
        var prefix = label.Highlight ? "<color=#FFEE22><b>" : "";
        var suffix = label.Highlight ? "</b></color>" : "";
        GUI.Label(rect, prefix + label.Text + suffix, _style);
      }
    }
  }
}
