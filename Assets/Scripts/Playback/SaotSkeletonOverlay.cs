using System.Collections.Generic;
using AnimationCreator.Data;
using UnityEngine;

namespace AnimationCreator.Playback
{
  public class SaotSkeletonOverlay : MonoBehaviour
  {
    static readonly (string a, string b)[] BonePairs =
    {
      ("pelvis", "torso"),
      ("torso", "head"),
      ("torso", "leftShoulder"),
      ("leftShoulder", "leftElbow"),
      ("torso", "rightShoulder"),
      ("rightShoulder", "rightElbow"),
      ("pelvis", "leftKnee"),
      ("leftKnee", "leftFoot"),
      ("pelvis", "rightKnee"),
      ("rightKnee", "rightFoot"),
    };

    static readonly string[] JointNames =
    {
      "pelvis", "torso", "head",
      "leftShoulder", "rightShoulder", "leftElbow", "rightElbow",
      "leftKnee", "rightKnee", "leftFoot", "rightFoot",
    };

    readonly List<LineRenderer> _bones = new List<LineRenderer>();
    readonly List<Transform> _nodes = new List<Transform>();
    Material _lineMaterial;
    Material _nodeMaterial;
    string _highlightJoint;

    public void Initialize(Transform parent)
    {
      transform.SetParent(parent, false);

      _lineMaterial = new Material(Shader.Find("Sprites/Default"));
      _lineMaterial.color = Color.white;

      _nodeMaterial = new Material(Shader.Find("Sprites/Default"));
      _nodeMaterial.color = Color.white;

      foreach (var _ in BonePairs)
      {
        var lr = CreateLineRenderer();
        _bones.Add(lr);
      }

      foreach (var _ in JointNames)
      {
        var node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        node.name = "JointNode";
        Destroy(node.GetComponent<Collider>());
        node.transform.SetParent(transform, false);
        node.transform.localScale = Vector3.one * 0.11f;
        node.GetComponent<Renderer>().sharedMaterial = _nodeMaterial;
        _nodes.Add(node.transform);
      }

      SetVisible(false);
    }

    public void SetHighlightJoint(string jointName) => _highlightJoint = jointName;

    public void SetVisible(bool visible)
    {
      gameObject.SetActive(visible);
    }

    public void UpdateFromJoints(IReadOnlyDictionary<string, Vector3> joints)
    {
      for (var i = 0; i < BonePairs.Length; i++)
      {
        var (a, b) = BonePairs[i];
        if (!joints.TryGetValue(a, out var p0) || !joints.TryGetValue(b, out var p1))
          continue;

        var lr = _bones[i];
        lr.SetPosition(0, p0);
        lr.SetPosition(1, p1);
      }

      for (var i = 0; i < JointNames.Length; i++)
      {
        if (!joints.TryGetValue(JointNames[i], out var p))
          continue;

        var node = _nodes[i];
        node.position = p;
        var isHighlight = JointNames[i] == _highlightJoint;
        node.localScale = Vector3.one * (isHighlight ? 0.22f : 0.11f);

        var rend = node.GetComponent<Renderer>();
        rend.sharedMaterial.color = isHighlight ? new Color(1f, 0.92f, 0.1f) : Color.white;
      }
    }

    LineRenderer CreateLineRenderer()
    {
      var go = new GameObject("Bone");
      go.transform.SetParent(transform, false);
      var lr = go.AddComponent<LineRenderer>();
      lr.material = _lineMaterial;
      lr.startWidth = 0.045f;
      lr.endWidth = 0.045f;
      lr.positionCount = 2;
      lr.useWorldSpace = true;
      lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      lr.receiveShadows = false;
      return lr;
    }

    void OnDestroy()
    {
      if (_lineMaterial != null)
        Destroy(_lineMaterial);
      if (_nodeMaterial != null)
        Destroy(_nodeMaterial);
    }
  }
}
