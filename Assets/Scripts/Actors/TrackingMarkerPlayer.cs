using System.Collections.Generic;
using AnimationCreator.Data;
using UnityEngine;

namespace AnimationCreator.Actors
{
  /// <summary>
  /// SoccerNet-style minimap marker: team disc + jersey label at tracked root.
  /// </summary>
  public class TrackingMarkerPlayer : MonoBehaviour, IPlayerVisual
  {
    Transform _body;
    Transform _head;
    Transform _labelAnchor;
    TextMesh _label;
    Renderer _bodyRenderer;
    Material _bodyMaterial;
    PlayerJoints _currentJoints;
    Color _baseColor;

    public string PlayerId { get; private set; }
    public int JerseyNumber { get; private set; }
    public PlayerJoints CurrentJoints => _currentJoints;

    public void Initialize(PlayerInfo info, Color teamColor)
    {
      PlayerId = info.playerId;
      JerseyNumber = info.jerseyNumber;
      _baseColor = teamColor;

      var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      bodyGo.name = "Body";
      bodyGo.transform.SetParent(transform, false);
      bodyGo.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
      bodyGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
      Destroy(bodyGo.GetComponent<Collider>());
      _body = bodyGo.transform;
      _bodyRenderer = bodyGo.GetComponent<Renderer>();
      _bodyMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      _bodyMaterial.color = teamColor;
      _bodyRenderer.sharedMaterial = _bodyMaterial;

      var headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      headGo.name = "Head";
      headGo.transform.SetParent(transform, false);
      headGo.transform.localScale = Vector3.one * 0.28f;
      headGo.transform.localPosition = new Vector3(0f, 1.72f, 0f);
      Destroy(headGo.GetComponent<Collider>());
      _head = headGo.transform;
      headGo.GetComponent<Renderer>().sharedMaterial = _bodyMaterial;

      var labelGo = new GameObject("JerseyLabel");
      labelGo.transform.SetParent(transform, false);
      _labelAnchor = labelGo.transform;
      _labelAnchor.localPosition = new Vector3(0f, 2.05f, 0f);
      _label = labelGo.AddComponent<TextMesh>();
      _label.text = info.jerseyNumber > 0 ? info.jerseyNumber.ToString() : info.displayName;
      _label.fontSize = 48;
      _label.characterSize = 0.04f;
      _label.anchor = TextAnchor.MiddleCenter;
      _label.alignment = TextAlignment.Center;
      _label.color = Color.white;
    }

    void LateUpdate()
    {
      if (_labelAnchor != null && Camera.main != null)
        _labelAnchor.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
    }

    public void ApplyJoints(PlayerJoints joints)
    {
      _currentJoints = joints;
      if (joints?.pelvis == null)
        return;

      var pelvis = joints.pelvis.ToUnity();
      var yaw = 0f;
      if (joints.torso != null)
      {
        var torso = joints.torso.ToUnity();
        var fwd = torso - pelvis;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.01f)
          yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
      }

      transform.position = pelvis;
      transform.rotation = Quaternion.Euler(0f, yaw, 0f);
      if (joints.head != null)
        _head.position = joints.head.ToUnity();
    }

    public IReadOnlyDictionary<string, Vector3> GetJointWorldPositions()
    {
      var map = new Dictionary<string, Vector3>();
      if (_currentJoints?.pelvis != null)
        map["pelvis"] = _currentJoints.pelvis.ToUnity();
      return map;
    }

    public void SetAnalysisMode(bool active, bool isFocusPlayer, float dimAlpha = 0.15f) { }

    public void SetSituationHighlight(SituationHighlight highlight)
    {
      if (_bodyMaterial == null)
        return;

      _bodyMaterial.color = highlight switch
      {
        SituationHighlight.OffsidePlayer => new Color(1f, 0.25f, 0.25f),
        SituationHighlight.LastDefender => new Color(1f, 0.9f, 0.15f),
        SituationHighlight.SecondLastDefender => new Color(0.95f, 0.75f, 0.1f),
        _ => _baseColor,
      };
    }

    public void SetFootHighlight(string foot, bool active) { }

    public void ClearHighlight()
    {
      if (_bodyMaterial != null)
        _bodyMaterial.color = _baseColor;
    }
  }
}
