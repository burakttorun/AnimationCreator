using UnityEngine;

namespace AnimationCreator.Actors
{
  public class BallActor : MonoBehaviour
  {
    Transform _mesh;

    public void Initialize()
    {
      var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      go.name = "BallMesh";
      go.transform.SetParent(transform, false);
      go.transform.localScale = Vector3.one * 0.32f;
      Destroy(go.GetComponent<Collider>());

      var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      mat.color = new Color(1f, 0.92f, 0.15f);
      go.GetComponent<Renderer>().sharedMaterial = mat;
      _mesh = go.transform;
    }

    public void SetPosition(Vector3 position)
    {
      transform.position = position;
    }
  }
}
