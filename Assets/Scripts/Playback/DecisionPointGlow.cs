using UnityEngine;

namespace AnimationCreator.Playback
{
  public class DecisionPointGlow : MonoBehaviour
  {
    Transform _core;
    Transform _halo;
    float _pulse;

    public void Initialize()
    {
      _core = CreateSphere(transform, 0.18f, new Color(1f, 0.95f, 0.15f, 1f));
      _halo = CreateSphere(transform, 0.32f, new Color(1f, 0.9f, 0.1f, 0.35f));
      SetVisible(false);
    }

    public void SetPosition(Vector3 worldPos)
    {
      transform.position = worldPos;
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    void Update()
    {
      _pulse += Time.deltaTime * 3.5f;
      var s = 1f + Mathf.Sin(_pulse) * 0.15f;
      _core.localScale = Vector3.one * 0.18f * s;
      _halo.localScale = Vector3.one * 0.38f * (1f + Mathf.Sin(_pulse * 0.8f) * 0.2f);
    }

    static Transform CreateSphere(Transform parent, float size, Color color)
    {
      var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      Destroy(go.GetComponent<Collider>());
      go.transform.SetParent(parent, false);
      go.transform.localScale = Vector3.one * size;
      var mat = new Material(Shader.Find("Sprites/Default"));
      mat.color = color;
      go.GetComponent<Renderer>().sharedMaterial = mat;
      return go.transform;
    }
  }
}
