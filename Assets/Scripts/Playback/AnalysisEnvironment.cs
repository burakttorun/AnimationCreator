using UnityEngine;

namespace AnimationCreator.Playback
{
  public class AnalysisEnvironment : MonoBehaviour
  {
    Transform _environmentRoot;
    Color _originalAmbient;
    bool _active;

    public void Bind(Transform environmentRoot) => _environmentRoot = environmentRoot;

    public void SetActive(bool active)
    {
      if (_active == active)
        return;

      _active = active;

      if (active)
      {
        _originalAmbient = RenderSettings.ambientLight;
        RenderSettings.ambientLight = new Color(0.08f, 0.1f, 0.18f);
        Camera.main.backgroundColor = new Color(0.04f, 0.06f, 0.14f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.14f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.012f;
      }
      else
      {
        RenderSettings.ambientLight = _originalAmbient;
        Camera.main.backgroundColor = new Color(0.55f, 0.7f, 0.85f);
        RenderSettings.fog = false;
      }

      if (_environmentRoot != null)
      {
        foreach (var rend in _environmentRoot.GetComponentsInChildren<Renderer>())
        {
          if (rend.gameObject.name == "Pitch")
            rend.material.color = active ? new Color(0.06f, 0.12f, 0.2f) : new Color(0.18f, 0.55f, 0.22f);
          else if (rend.gameObject.name == "Line")
            rend.material.color = active ? new Color(0.25f, 0.35f, 0.55f, 0.6f) : Color.white;
        }
      }
    }
  }
}
