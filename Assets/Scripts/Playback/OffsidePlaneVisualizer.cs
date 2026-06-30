using UnityEngine;

namespace AnimationCreator.Playback
{
  public class OffsidePlaneVisualizer : MonoBehaviour
  {
    [SerializeField] float pitchWidthM = 68f;
    [SerializeField] float planeHeightM = 3.5f;
    [SerializeField] Color planeColor = new Color(1f, 0.92f, 0.1f, 0.35f);

    Material _material;
    MeshRenderer _renderer;

    public void Configure(float lineZ, float widthM)
    {
      pitchWidthM = widthM;
      transform.position = new Vector3(0f, planeHeightM * 0.5f, lineZ);
      transform.localScale = new Vector3(pitchWidthM, planeHeightM, 0.08f);
    }

    void Awake()
    {
      var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
      mesh.name = "OffsidePlane";
      mesh.transform.SetParent(transform, false);
      Destroy(mesh.GetComponent<Collider>());

      _renderer = mesh.GetComponent<MeshRenderer>();
      _material = new Material(Shader.Find("Sprites/Default"));
      _material.color = planeColor;
      _renderer.sharedMaterial = _material;
    }

    void OnDestroy()
    {
      if (_material != null)
        Destroy(_material);
    }
  }
}
