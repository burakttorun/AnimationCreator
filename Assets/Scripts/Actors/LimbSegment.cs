using UnityEngine;

namespace AnimationCreator.Actors
{
  public class LimbSegment : MonoBehaviour
  {
    [SerializeField] float radius = 0.06f;

    Transform _mesh;
    Material _material;

    public void Initialize(Color color, float segmentRadius)
    {
      radius = segmentRadius;
      _mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
      _mesh.SetParent(transform, false);
      _mesh.localScale = new Vector3(radius * 2f, 0.5f, radius * 2f);
      Destroy(_mesh.GetComponent<Collider>());

      _material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      _material.color = color;
      _mesh.GetComponent<Renderer>().sharedMaterial = _material;
    }

    public void SetEndpoints(Vector3 start, Vector3 end)
    {
      var dir = end - start;
      var length = dir.magnitude;
      if (length < 0.001f)
        return;

      transform.position = (start + end) * 0.5f;
      transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
      _mesh.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
    }

    public void SetColor(Color color)
    {
      if (_material != null)
        _material.color = color;
    }

    public void SetAlpha(float alpha)
    {
      if (_material == null)
        return;

      var c = _material.color;
      c.a = alpha;
      _material.color = c;
    }

    public Renderer MeshRenderer => _mesh != null ? _mesh.GetComponent<Renderer>() : null;

    void OnDestroy()
    {
      if (_material != null)
        Destroy(_material);
    }
  }
}
