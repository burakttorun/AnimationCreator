using System.Collections.Generic;
using AnimationCreator.Actors;
using AnimationCreator.Data;
using UnityEngine;

namespace AnimationCreator.Playback
{
  public class SaotVerdictPresenter : MonoBehaviour
  {
    SituationInfo _situation;
    MatchClock _clock;
    Dictionary<string, IPlayerVisual> _players;
    SituationCameraController _camera;
    OffsidePlaneVisualizer _offsidePlane;
    BallActor _ball;

    SaotSkeletonOverlay _focusSkeleton;
    SaotSkeletonOverlay _defenderSkeleton;
    JointCoordinateLabels _labels;
    DecisionPointGlow _decisionGlow;
    AnalysisEnvironment _environment;

    bool _verdictActive;
  readonly HashSet<string> _verdictPlayerIds = new HashSet<string>();

    public bool IsVerdictActive => _verdictActive;

    public void Initialize(
      SituationInfo situation,
      MatchClock clock,
      Dictionary<string, IPlayerVisual> players,
      SituationCameraController camera,
      OffsidePlaneVisualizer offsidePlane,
      BallActor ball,
      Transform environmentRoot)
    {
      _situation = situation;
      _clock = clock;
      _players = players;
      _camera = camera;
      _offsidePlane = offsidePlane;
      _ball = ball;

      if (situation?.offsideLine != null)
      {
        _verdictPlayerIds.Add(situation.offsideLine.offsidePlayerId);
        _verdictPlayerIds.Add(situation.offsideLine.lastDefenderPlayerId);
        _verdictPlayerIds.Add(situation.offsideLine.secondLastDefenderPlayerId);
      }

      _labels = gameObject.AddComponent<JointCoordinateLabels>();
      _decisionGlow = new GameObject("DecisionGlow").AddComponent<DecisionPointGlow>();
      _decisionGlow.transform.SetParent(transform, false);
      _decisionGlow.Initialize();

      _environment = gameObject.AddComponent<AnalysisEnvironment>();
      _environment.Bind(environmentRoot);

      var overlayRoot = new GameObject("SaotOverlays").transform;
      overlayRoot.SetParent(transform, false);

      var focusGo = new GameObject("FocusSkeleton");
      focusGo.transform.SetParent(overlayRoot, false);
      _focusSkeleton = focusGo.AddComponent<SaotSkeletonOverlay>();
      _focusSkeleton.Initialize(focusGo.transform);

      var defGo = new GameObject("DefenderSkeleton");
      defGo.transform.SetParent(overlayRoot, false);
      _defenderSkeleton = defGo.AddComponent<SaotSkeletonOverlay>();
      _defenderSkeleton.Initialize(defGo.transform);
    }

    public void UpdatePresentation(int frameIndex)
    {
      if (_situation == null)
        return;

      var atKey = frameIndex == _situation.keyFrameIndex && !_clock.IsPlaying;
      if (atKey && !_verdictActive)
        EnterVerdict();
      else if (!atKey && _verdictActive)
        ExitVerdict();

      if (!_verdictActive)
        return;

      RefreshVerdictVisuals();
    }

    void EnterVerdict()
    {
      _verdictActive = true;
      _environment.SetActive(true);
      _camera?.SetVerdictMode(true, GetFocusPosition());

      foreach (var pair in _players)
        pair.Value.SetAnalysisMode(true, pair.Key == _situation.offsideLine.offsidePlayerId);

      _focusSkeleton.SetVisible(true);
      _defenderSkeleton.SetVisible(true);

      var attackerJoint = _situation.verdict?.attackerPoint?.joint ?? "leftKnee";
      _focusSkeleton.SetHighlightJoint(attackerJoint);

      var defenderJoint = _situation.verdict?.defenderPoint?.joint ?? "torso";
      _defenderSkeleton.SetHighlightJoint(defenderJoint);

      _decisionGlow.SetVisible(true);
      _ball?.gameObject.SetActive(false);
    }

    void ExitVerdict()
    {
      _verdictActive = false;
      _environment.SetActive(false);
      _camera?.SetVerdictMode(false, Vector3.zero);

      foreach (var pair in _players)
        pair.Value.SetAnalysisMode(false, false);

      _focusSkeleton.SetVisible(false);
      _defenderSkeleton.SetVisible(false);
      _decisionGlow.SetVisible(false);
      _labels.Clear();
      _ball?.gameObject.SetActive(true);
    }

    void RefreshVerdictVisuals()
    {
      _labels.Clear();

      var line = _situation.offsideLine;
      if (_players.TryGetValue(line.offsidePlayerId, out var attacker))
      {
        var joints = attacker.GetJointWorldPositions();
        _focusSkeleton.UpdateFromJoints(joints);

        foreach (var pair in joints)
        {
          var highlight = pair.Key == (_situation.verdict?.attackerPoint?.joint ?? "leftKnee");
          if (highlight || ShouldLabelJoint(pair.Key, true))
            _labels.AddLabel(pair.Key, pair.Value, highlight);
        }
      }

      if (_players.TryGetValue(line.secondLastDefenderPlayerId, out var defender))
      {
        var joints = defender.GetJointWorldPositions();
        _defenderSkeleton.UpdateFromJoints(joints);

        foreach (var pair in joints)
        {
          var highlight = pair.Key == (_situation.verdict?.defenderPoint?.joint ?? "torso");
          if (highlight || ShouldLabelJoint(pair.Key, false))
            _labels.AddLabel(pair.Key, pair.Value, highlight);
        }
      }

      var decisionPos = GetDecisionPosition();
      _decisionGlow.SetPosition(decisionPos);
    }

    static bool ShouldLabelJoint(string joint, bool isAttacker)
    {
      if (isAttacker)
        return joint is "head" or "torso" or "pelvis" or "leftKnee" or "rightKnee" or "leftFoot" or "rightFoot";

      return joint is "torso" or "pelvis" or "leftKnee" or "rightKnee";
    }

    Vector3 GetDecisionPosition()
    {
      if (_situation.verdict?.attackerPoint?.position != null)
        return _situation.verdict.attackerPoint.position.ToUnity();

      if (_players.TryGetValue(_situation.offsideLine.offsidePlayerId, out var attacker))
      {
        var joint = _situation.verdict?.attackerPoint?.joint ?? "leftKnee";
        if (attacker.GetJointWorldPositions().TryGetValue(joint, out var pos))
          return pos;
      }

      return GetFocusPosition();
    }

    Vector3 GetFocusPosition()
    {
      if (_players.TryGetValue(_situation.offsideLine.offsidePlayerId, out var attacker) &&
          attacker is MonoBehaviour mb)
        return mb.transform.position;

      return Vector3.zero;
    }

    public void DrawVerdictHud(int currentFrame, int totalFrames)
    {
      if (!_verdictActive || _situation == null)
        return;

      var margin = _situation.verdict?.marginM ?? ComputeMargin();
      var outcome = _situation.verdict?.outcome ?? "offside";
      var outcomeTr = outcome == "offside" ? "OFSAYT" : "OYNANABİLİR";
      var outcomeColor = outcome == "offside" ? "#FF3344" : "#33FF88";

      var bannerStyle = new GUIStyle(GUI.skin.box)
      {
        fontSize = 22,
        alignment = TextAnchor.MiddleCenter,
        richText = true,
        normal = { textColor = Color.white },
      };

      var w = Screen.width;
      var h = Screen.height;

      GUI.Box(new Rect(w * 0.5f - 160f, 24f, 320f, 56f),
        $"<color={outcomeColor}><b><size=26>{outcomeTr}</size></b></color>\n" +
        $"<size=14>Karar mesafesi: <b>{margin:F3} m</b>  |  Çizgi Z = {_situation.offsideLine.z:F3}</size>",
        bannerStyle);

      var barH = 6f;
      var barY = h - 48f;
      var barW = w - 80f;
      var barX = 40f;

      GUI.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);
      GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

      var keyT = _situation.keyFrameIndex / (float)(totalFrames - 1);
      var playT = currentFrame / (float)(totalFrames - 1);

      GUI.color = new Color(0.85f, 0.15f, 0.15f, 0.95f);
      GUI.DrawTexture(new Rect(barX, barY, barW * playT, barH), Texture2D.whiteTexture);

      GUI.color = new Color(1f, 0.92f, 0.15f, 1f);
      GUI.DrawTexture(new Rect(barX + barW * keyT - 3f, barY - 5f, 6f, barH + 10f), Texture2D.whiteTexture);

      GUI.color = Color.white;
      var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
      GUI.Label(new Rect(barX, barY + 10f, 300f, 20f), "SAOT — Kesin sonuç görünümü  |  Space: devam", labelStyle);
      GUI.color = Color.white;
    }

    float ComputeMargin()
    {
      var attackerZ = _situation.verdict?.attackerPoint?.position?.z ?? 0f;
      return attackerZ - _situation.offsideLine.z;
    }
  }
}
