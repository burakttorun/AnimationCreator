using System.Collections.Generic;
using AnimationCreator.Data;
using UnityEngine;

namespace AnimationCreator.Actors
{
  public class StickFigurePlayer : MonoBehaviour, IPlayerVisual
  {
    Transform _head;
    LimbSegment _torso;
    LimbSegment _leftUpperArm;
    LimbSegment _leftForearm;
    LimbSegment _rightUpperArm;
    LimbSegment _rightForearm;
    LimbSegment _leftThigh;
    LimbSegment _leftShin;
    LimbSegment _rightThigh;
    LimbSegment _rightShin;

    Color _baseColor;
    Renderer _headRenderer;
    Material _headMaterial;
    PlayerJoints _currentJoints;
    bool _analysisMode;
    bool _isFocusPlayer;

    static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f);
    static readonly Color OffsideColor = new Color(1f, 0.25f, 0.25f);
    static readonly Color DefenderColor = new Color(1f, 0.9f, 0.15f);
    static readonly Color SecondDefenderColor = new Color(0.95f, 0.75f, 0.1f);
    static readonly Color AnalysisBodyColor = new Color(1f, 0.45f, 0.12f, 0.55f);
    static readonly Color AnalysisFocusColor = new Color(1f, 0.55f, 0.18f, 0.75f);

    public string PlayerId { get; private set; }
    public int JerseyNumber { get; private set; }
    public PlayerJoints CurrentJoints => _currentJoints;

    public bool TryGetPelvisWorld(out Vector3 pelvis)
    {
      if (_currentJoints?.pelvis == null)
      {
        pelvis = transform.position;
        return false;
      }

      pelvis = _currentJoints.pelvis.ToUnity();
      return true;
    }

    public void Initialize(PlayerInfo info, Color teamColor)
    {
      PlayerId = info.playerId;
      JerseyNumber = info.jerseyNumber;
      _baseColor = teamColor;

      _head = CreateJointSphere("Head", teamColor, 0.14f);
      _torso = CreateSegment("Torso", teamColor, 0.09f);
      _leftUpperArm = CreateSegment("LeftUpperArm", teamColor, 0.055f);
      _leftForearm = CreateSegment("LeftForearm", teamColor, 0.05f);
      _rightUpperArm = CreateSegment("RightUpperArm", teamColor, 0.055f);
      _rightForearm = CreateSegment("RightForearm", teamColor, 0.05f);
      _leftThigh = CreateSegment("LeftThigh", teamColor, 0.07f);
      _leftShin = CreateSegment("LeftShin", teamColor, 0.06f);
      _rightThigh = CreateSegment("RightThigh", teamColor, 0.07f);
      _rightShin = CreateSegment("RightShin", teamColor, 0.06f);
    }

    public void ApplyJoints(PlayerJoints joints)
    {
      _currentJoints = PlayerJointsGrounding.Sanitize(joints);

      var pelvis = _currentJoints.pelvis.ToUnity();
      var torso = _currentJoints.torso.ToUnity();
      var head = _currentJoints.head.ToUnity();
      var leftKnee = _currentJoints.leftKnee.ToUnity();
      var rightKnee = _currentJoints.rightKnee.ToUnity();
      var leftFoot = _currentJoints.leftFoot.ToUnity();
      var rightFoot = _currentJoints.rightFoot.ToUnity();

      transform.position = pelvis;
      _head.position = head;
      _torso.SetEndpoints(pelvis, torso);
      _leftThigh.SetEndpoints(pelvis, leftKnee);
      _leftShin.SetEndpoints(leftKnee, leftFoot);
      _rightThigh.SetEndpoints(pelvis, rightKnee);
      _rightShin.SetEndpoints(rightKnee, rightFoot);

      if (PlayerJointsUtility.HasArmData(_currentJoints))
      {
        var ls = _currentJoints.leftShoulder.ToUnity();
        var le = _currentJoints.leftElbow.ToUnity();
        var rs = _currentJoints.rightShoulder.ToUnity();
        var re = _currentJoints.rightElbow.ToUnity();

        _leftUpperArm.SetEndpoints(ls, le);
        _leftForearm.SetEndpoints(le, EstimateWrist(ls, le));
        _rightUpperArm.SetEndpoints(rs, re);
        _rightForearm.SetEndpoints(re, EstimateWrist(rs, re));
      }
    }

    public IReadOnlyDictionary<string, Vector3> GetJointWorldPositions()
    {
      var map = new Dictionary<string, Vector3>();
      if (_currentJoints == null)
        return map;

      map["pelvis"] = _currentJoints.pelvis.ToUnity();
      map["torso"] = _currentJoints.torso.ToUnity();
      map["head"] = _currentJoints.head.ToUnity();
      map["leftShoulder"] = _currentJoints.leftShoulder.ToUnity();
      map["rightShoulder"] = _currentJoints.rightShoulder.ToUnity();
      map["leftElbow"] = _currentJoints.leftElbow.ToUnity();
      map["rightElbow"] = _currentJoints.rightElbow.ToUnity();
      map["leftKnee"] = _currentJoints.leftKnee.ToUnity();
      map["rightKnee"] = _currentJoints.rightKnee.ToUnity();
      map["leftFoot"] = _currentJoints.leftFoot.ToUnity();
      map["rightFoot"] = _currentJoints.rightFoot.ToUnity();
      return map;
    }

    static Vector3 EstimateWrist(Vector3 shoulder, Vector3 elbow, float forearmLen = 0.26f)
    {
      var dir = elbow - shoulder;
      return dir.sqrMagnitude < 1e-8f ? elbow + Vector3.forward * forearmLen : elbow + dir.normalized * forearmLen;
    }

    public void SetAnalysisMode(bool active, bool isFocusPlayer, float dimAlpha = 0.15f)
    {
      _analysisMode = active;
      _isFocusPlayer = isFocusPlayer;

      if (!active)
      {
        RestoreBaseColors();
        return;
      }

      var color = isFocusPlayer
        ? AnalysisFocusColor
        : new Color(0.1f, 0.12f, 0.16f);
      ApplyColorToAll(color, 1f);
    }

    public void SetSituationHighlight(SituationHighlight highlight)
    {
      if (_analysisMode)
        return;

      var color = highlight switch
      {
        SituationHighlight.OffsidePlayer => OffsideColor,
        SituationHighlight.LastDefender => DefenderColor,
        SituationHighlight.SecondLastDefender => SecondDefenderColor,
        _ => _baseColor,
      };

      _torso.SetColor(color);
      _leftThigh.SetColor(color);
      _rightThigh.SetColor(color);
      if (_headMaterial != null)
        _headMaterial.color = color;
    }

    public void SetFootHighlight(string foot, bool active)
    {
      if (_analysisMode)
        return;

      _leftShin.SetColor(foot == "left" && active ? HighlightColor : _baseColor);
      _rightShin.SetColor(foot == "right" && active ? HighlightColor : _baseColor);
    }

    public void ClearHighlight()
    {
      if (_analysisMode)
        return;

      RestoreBaseColors();
    }

    void RestoreBaseColors()
    {
      ApplyColorToAll(_baseColor, 1f);
    }

    void ApplyColorToAll(Color color, float alpha)
    {
      _torso.SetColor(color);
      _leftThigh.SetColor(color);
      _rightThigh.SetColor(color);
      _leftShin.SetColor(color);
      _rightShin.SetColor(color);
      _leftUpperArm.SetColor(color);
      _leftForearm.SetColor(color);
      _rightUpperArm.SetColor(color);
      _rightForearm.SetColor(color);

      _torso.SetAlpha(alpha);
      _leftThigh.SetAlpha(alpha);
      _rightThigh.SetAlpha(alpha);
      _leftShin.SetAlpha(alpha);
      _rightShin.SetAlpha(alpha);
      _leftUpperArm.SetAlpha(alpha);
      _leftForearm.SetAlpha(alpha);
      _rightUpperArm.SetAlpha(alpha);
      _rightForearm.SetAlpha(alpha);

      if (_headMaterial != null)
      {
        _headMaterial.color = color;
        var c = _headMaterial.color;
        c.a = alpha;
        _headMaterial.color = c;
      }
    }

    Transform CreateJointSphere(string name, Color color, float size)
    {
      var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      go.name = name;
      go.transform.SetParent(transform, false);
      go.transform.localScale = Vector3.one * size;
      Destroy(go.GetComponent<Collider>());

      _headMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      _headMaterial.color = color;
      _headRenderer = go.GetComponent<Renderer>();
      _headRenderer.sharedMaterial = _headMaterial;
      return go.transform;
    }

    LimbSegment CreateSegment(string name, Color color, float radius)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      var segment = go.AddComponent<LimbSegment>();
      segment.Initialize(color, radius);
      return segment;
    }
  }
}
