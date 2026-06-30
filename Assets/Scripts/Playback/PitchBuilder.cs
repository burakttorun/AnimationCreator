using UnityEngine;

namespace AnimationCreator.Playback
{
  public static class PitchBuilder
  {
    public static void Build(Transform parent, float lengthM, float widthM)
    {
      var pitch = GameObject.CreatePrimitive(PrimitiveType.Plane);
      pitch.name = "Pitch";
      pitch.transform.SetParent(parent, false);
      pitch.transform.localScale = new Vector3(widthM / 10f, 1f, lengthM / 10f);
      Object.Destroy(pitch.GetComponent<Collider>());

      var grass = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      grass.color = new Color(0.18f, 0.55f, 0.22f);
      pitch.GetComponent<Renderer>().sharedMaterial = grass;

      CreateLine(parent, new Vector3(0f, 0.02f, 0f), new Vector3(widthM, 0.02f, 0f), 0.12f);
      CreateLine(parent, new Vector3(0f, 0.02f, lengthM * 0.5f), new Vector3(widthM, 0.02f, lengthM * 0.5f), 0.12f);
      CreateLine(parent, new Vector3(0f, 0.02f, -lengthM * 0.5f), new Vector3(widthM, 0.02f, -lengthM * 0.5f), 0.12f);
      CreateLine(parent, new Vector3(-widthM * 0.5f, 0.02f, 0f), new Vector3(-widthM * 0.5f, 0.02f, lengthM), 0.12f);
      CreateLine(parent, new Vector3(widthM * 0.5f, 0.02f, 0f), new Vector3(widthM * 0.5f, 0.02f, lengthM), 0.12f);

      CreateGoal(parent, new Vector3(0f, 1.2f, lengthM * 0.5f));
      CreateGoal(parent, new Vector3(0f, 1.2f, -lengthM * 0.5f));
    }

    static void CreateLine(Transform parent, Vector3 from, Vector3 to, float width)
    {
      var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
      go.name = "Line";
      go.transform.SetParent(parent, false);
      Object.Destroy(go.GetComponent<Collider>());

      var dir = to - from;
      var length = dir.magnitude;
      go.transform.position = (from + to) * 0.5f;
      go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
      go.transform.localScale = new Vector3(width, 0.02f, length);

      var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      mat.color = Color.white;
      go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void CreateGoal(Transform parent, Vector3 center)
    {
      var goal = new GameObject("Goal");
      goal.transform.SetParent(parent, false);
      goal.transform.position = center;

      CreatePost(goal.transform, new Vector3(-3.66f, 0f, 0f));
      CreatePost(goal.transform, new Vector3(3.66f, 0f, 0f));
      CreateLine(goal.transform, new Vector3(-3.66f, 2.44f, 0f), new Vector3(3.66f, 2.44f, 0f), 0.12f);
    }

    static void CreatePost(Transform parent, Vector3 localPos)
    {
      var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      post.transform.SetParent(parent, false);
      post.transform.localPosition = localPos + Vector3.up * 1.22f;
      post.transform.localScale = new Vector3(0.12f, 1.22f, 0.12f);
      Object.Destroy(post.GetComponent<Collider>());

      var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      mat.color = Color.white;
      post.GetComponent<Renderer>().sharedMaterial = mat;
    }
  }
}
