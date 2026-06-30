using System.Collections.Generic;
using AnimationCreator.Actors;
using AnimationCreator.Data;
using UnityEngine;

namespace AnimationCreator.Playback
{
  public class SituationDirector : MonoBehaviour
  {
    [SerializeField] string sequenceId = "offside_demo";
    [SerializeField] float pitchLengthM = 105f;
    [SerializeField] float pitchWidthM = 68f;
    [SerializeField] bool smoothJoints = true;
    [SerializeField] float smoothWindowSeconds = 0.2f;
    [SerializeField] bool autoPauseAtKeyFrame = true;
    [SerializeField] int keyFramePauseRadius = 3;

    MatchDataset _dataset;
    MatchClock _clock;
    BallActor _ball;
    SituationCameraController _situationCamera;
    OffsidePlaneVisualizer _offsidePlane;
    SaotVerdictPresenter _verdictPresenter;
    Transform _kickMarker;
    Transform _environmentRoot;
    bool _keyFramePaused;

    readonly Dictionary<string, IPlayerVisual> _players = new Dictionary<string, IPlayerVisual>();
    readonly Dictionary<int, List<MatchEvent>> _eventsByFrame = new Dictionary<int, List<MatchEvent>>();

    SituationInfo Situation => _dataset?.Manifest?.situation;

    void Awake()
    {
      var environment = new GameObject("Environment");
      _environmentRoot = environment.transform;
      PitchBuilder.Build(_environmentRoot, pitchLengthM, pitchWidthM);

      _clock = gameObject.AddComponent<MatchClock>();
      _dataset = LoadDataset();
      IndexEvents();
      SpawnActors();
      SetupSituationVisuals();
      EnsureCameraAndLight();
      SetupVerdictPresenter();
      _clock.Configure(_dataset.Manifest.timing.frameRateHz, _dataset.Frames.Count);
      _clock.OnFrameChanged += OnFrameChanged;
      ApplyInterpolatedFrame(_clock.CurrentFrame, 0f);
      ApplySituationHighlights(_clock.CurrentFrame);

      // camera already set up above
    }

    MatchDataset LoadDataset()
    {
      if (!smoothJoints)
        return MatchDataLoader.LoadRaw(sequenceId);

      var settings = FrdsJointSmoother.Settings.Default;
      settings.WindowSeconds = smoothWindowSeconds;
      settings.Enabled = true;
      return MatchDataLoader.Load(sequenceId, settings);
    }

    void OnDestroy()
    {
      if (_clock != null)
        _clock.OnFrameChanged -= OnFrameChanged;
    }

    void OnFrameChanged(int frameIndex)
    {
      ApplyEventHighlights(frameIndex);
      ApplySituationHighlights(frameIndex);
      TryAutoPauseAtKeyFrame(frameIndex);
    }

    void Update()
    {
      if (MatchPlaybackInput.TogglePlayPressed)
      {
        _keyFramePaused = false;
        _clock.TogglePlay();
      }

      if (MatchPlaybackInput.StepBackPressed)
      {
        _keyFramePaused = false;
        _clock.StepFrame(-1);
      }

      if (MatchPlaybackInput.StepForwardPressed)
      {
        _keyFramePaused = false;
        _clock.StepFrame(1);
      }

      if (MatchPlaybackInput.RestartPressed)
      {
        _keyFramePaused = false;
        _clock.SeekFrame(0);
      }

      if (MatchPlaybackInput.SeekKeyFramePressed && Situation != null)
      {
        _keyFramePaused = true;
        _clock.Pause();
        _clock.SeekFrame(Situation.keyFrameIndex);
      }

      if (MatchPlaybackInput.CycleCameraPressed && _situationCamera != null)
        _situationCamera.CycleMode();

      var alpha = _clock.IsPlaying && !_keyFramePaused ? _clock.InterpolationAlpha : 0f;
      ApplyInterpolatedFrame(_clock.CurrentFrame, alpha);
      UpdateKickMarker();
      UpdateCameraFocus();
      _verdictPresenter?.UpdatePresentation(_clock.CurrentFrame);
    }

    void SetupVerdictPresenter()
    {
      if (Situation == null)
        return;

      _verdictPresenter = gameObject.AddComponent<SaotVerdictPresenter>();
      _verdictPresenter.Initialize(
        Situation,
        _clock,
        _players,
        _situationCamera,
        _offsidePlane,
        _ball,
        _environmentRoot);
    }

    void TryAutoPauseAtKeyFrame(int frameIndex)
    {
      if (!autoPauseAtKeyFrame || Situation == null || _keyFramePaused)
        return;

      if (frameIndex >= Situation.keyFrameIndex - 1 &&
          frameIndex <= Situation.keyFrameIndex + keyFramePauseRadius &&
          _clock.IsPlaying)
      {
        _clock.Pause();
        _clock.SeekFrame(Situation.keyFrameIndex);
        _keyFramePaused = true;
      }
    }

    void IndexEvents()
    {
      foreach (var evt in _dataset.Events)
      {
        if (!_eventsByFrame.TryGetValue(evt.frameIndex, out var list))
        {
          list = new List<MatchEvent>();
          _eventsByFrame[evt.frameIndex] = list;
        }

        list.Add(evt);
      }
    }

    void SpawnActors()
    {
      var ballGo = new GameObject("Ball");
      ballGo.transform.SetParent(transform, false);
      _ball = ballGo.AddComponent<BallActor>();
      _ball.Initialize();

      var root = new GameObject("Players");
      root.transform.SetParent(transform, false);

      foreach (var playerInfo in _dataset.Manifest.players)
      {
        var color = Color.white;
        if (_dataset.TeamColors.TryGetValue(playerInfo.teamId, out var hex))
          ColorUtility.TryParseHtmlString(hex, out color);

        var playerGo = new GameObject($"Player_{playerInfo.jerseyNumber}_{playerInfo.playerId}");
        playerGo.transform.SetParent(root.transform, false);
        var stick = playerGo.AddComponent<StickFigurePlayer>();
        stick.Initialize(playerInfo, color);
        _players[playerInfo.playerId] = stick;
      }
    }

    void SetupSituationVisuals()
    {
      if (Situation?.offsideLine == null)
        return;

      var planeGo = new GameObject("OffsideLine");
      planeGo.transform.SetParent(transform, false);
      _offsidePlane = planeGo.AddComponent<OffsidePlaneVisualizer>();
      _offsidePlane.Configure(Situation.offsideLine.z, pitchWidthM);

      if (Situation.kickPoint != null)
      {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "KickPoint";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = Vector3.one * 0.35f;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.5f, 0f, 0.9f);
        marker.GetComponent<Renderer>().sharedMaterial = mat;
        _kickMarker = marker.transform;
      }
    }

    void UpdateKickMarker()
    {
      if (_kickMarker == null || Situation?.kickPoint == null)
        return;

      var show = Mathf.Abs(_clock.CurrentFrame - Situation.keyFrameIndex) <= 8;
      _kickMarker.gameObject.SetActive(show);
      if (show)
        _kickMarker.position = Situation.kickPoint.ToUnity();
    }

    void UpdateCameraFocus()
    {
      if (_situationCamera == null)
        return;

      if (Situation?.offsideLine != null &&
          _players.TryGetValue(Situation.offsideLine.offsidePlayerId, out var striker) &&
          striker is MonoBehaviour mb)
        _situationCamera.SetFocus(mb.transform.position);
    }

    void ApplyInterpolatedFrame(int frameIndex, float alpha)
    {
      var current = _dataset.Frames[frameIndex];
      var nextIndex = Mathf.Min(frameIndex + 1, _dataset.Frames.Count - 1);
      var next = _dataset.Frames[nextIndex];
      var t = frameIndex == nextIndex ? 0f : alpha;

      _ball.SetPosition(Vector3.Lerp(current.ball.pos.ToUnity(), next.ball.pos.ToUnity(), t));

      var currentById = BuildPlayerLookup(current);
      var nextById = BuildPlayerLookup(next);

      foreach (var pair in _players)
      {
        if (!currentById.TryGetValue(pair.Key, out var jointsA))
          continue;

        if (t > 0f && nextById.TryGetValue(pair.Key, out var jointsB))
          pair.Value.ApplyJoints(PlayerJointsUtility.Lerp(jointsA, jointsB, t));
        else
          pair.Value.ApplyJoints(jointsA);
      }
    }

    static Dictionary<string, PlayerJoints> BuildPlayerLookup(MatchFrame frame)
    {
      var map = new Dictionary<string, PlayerJoints>();
      foreach (var pf in frame.players)
        map[pf.playerId] = pf.joints;
      return map;
    }

    void ApplyEventHighlights(int frameIndex)
    {
      foreach (var actor in _players.Values)
      {
        actor.SetFootHighlight("left", false);
        actor.SetFootHighlight("right", false);
      }

      if (!_eventsByFrame.TryGetValue(frameIndex, out var events))
        return;

      foreach (var evt in events)
      {
        if (!_players.TryGetValue(evt.actorPlayerId, out var actor))
          continue;

        if (!string.IsNullOrEmpty(evt.foot))
          actor.SetFootHighlight(evt.foot, true);
      }
    }

    void ApplySituationHighlights(int frameIndex)
    {
      if (_verdictPresenter != null && _verdictPresenter.IsVerdictActive)
        return;

      if (Situation?.offsideLine == null)
        return;

      var nearKey = Mathf.Abs(frameIndex - Situation.keyFrameIndex) <= 12;
      if (!nearKey)
      {
        foreach (var actor in _players.Values)
          actor.SetSituationHighlight(SituationHighlight.None);
        return;
      }

      var line = Situation.offsideLine;
      foreach (var pair in _players)
      {
        var highlight = SituationHighlight.None;
        if (pair.Key == line.offsidePlayerId)
          highlight = SituationHighlight.OffsidePlayer;
        else if (pair.Key == line.lastDefenderPlayerId)
          highlight = SituationHighlight.LastDefender;
        else if (pair.Key == line.secondLastDefenderPlayerId)
          highlight = SituationHighlight.SecondLastDefender;

        pair.Value.SetSituationHighlight(highlight);
      }
    }

    void EnsureCameraAndLight()
    {
      var mainCam = Camera.main;
      if (mainCam == null)
      {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        mainCam = camGo.AddComponent<Camera>();
      }

      if (mainCam.GetComponent<MatchCameraController>() != null)
        Destroy(mainCam.GetComponent<MatchCameraController>());

      if (Situation != null)
      {
        _situationCamera = mainCam.GetComponent<SituationCameraController>();
        if (_situationCamera == null)
          _situationCamera = mainCam.gameObject.AddComponent<SituationCameraController>();

        var kick = Situation.kickPoint?.ToUnity() ?? Vector3.zero;
        _situationCamera.Configure(Situation.offsideLine.z, kick);
      }
      else if (mainCam.GetComponent<MatchCameraController>() == null)
      {
        mainCam.gameObject.AddComponent<MatchCameraController>();
      }

      if (FindAnyObjectByType<Light>() == null)
      {
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        lightGo.transform.rotation = Quaternion.Euler(48f, -25f, 0f);
      }
    }

    void OnGUI()
    {
      if (_verdictPresenter != null && _verdictPresenter.IsVerdictActive)
      {
        _verdictPresenter.DrawVerdictHud(_clock.CurrentFrame, _dataset.Frames.Count);
        return;
      }

      var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
      var y = 12f;

      if (Situation != null)
      {
        GUI.Label(new Rect(12, y, 720, 28), $"<b>FRDS Situation Replay</b> — {Situation.title}", style);
        y += 26;
        GUI.Label(new Rect(12, y, 720, 22), Situation.description, style);
        y += 24;

        var atKey = _clock.CurrentFrame == Situation.keyFrameIndex;
        var keyLabel = atKey ? "<color=#FFAA00><b>▶ PAS ANI (kick point)</b></color>" : $"Pas anı: frame {Situation.keyFrameIndex + 1}";
        GUI.Label(new Rect(12, y, 720, 22), keyLabel, style);
        y += 24;

        GUI.Label(new Rect(12, y, 720, 22),
          $"Ofsayt çizgisi Z = {Situation.offsideLine.z:F1} m  |  " +
          $"Forvet: #{PlayerJersey(Situation.offsideLine.offsidePlayerId)}  |  " +
          $"Son savunmacı: #{PlayerJersey(Situation.offsideLine.lastDefenderPlayerId)}", style);
        y += 26;
      }
      else
      {
        GUI.Label(new Rect(12, y, 640, 24), $"FRDS Replay — {_dataset.Manifest.matchId}", style);
        y += 26;
      }

      var camHint = _situationCamera != null ? $"  |  C: Kamera ({_situationCamera.Mode})" : "";
      GUI.Label(new Rect(12, y, 900, 22),
        $"Frame {_clock.CurrentFrame + 1}/{_dataset.Frames.Count}  |  " +
        $"Space: Oynat  |  K: Pas anı (SAOT sonuç)  |  ←→: Adım  |  R: Baştan{camHint}", style);
      y += 28;

      DrawTimelineBar(y);
    }

    int PlayerJersey(string playerId)
    {
      return _dataset.PlayersById.TryGetValue(playerId, out var info) ? info.jerseyNumber : 0;
    }

    void DrawTimelineBar(float y)
    {
      if (Situation == null)
        return;

      const float barX = 12f;
      const float barW = 400f;
      const float barH = 10f;

      GUI.Box(new Rect(barX, y, barW, barH), "");

      var keyT = Situation.keyFrameIndex / (float)(_dataset.Frames.Count - 1);
      var playT = _clock.CurrentFrame / (float)(_dataset.Frames.Count - 1);

      GUI.color = new Color(1f, 0.85f, 0.1f, 0.5f);
      GUI.DrawTexture(new Rect(barX, y, barW * keyT, barH), Texture2D.whiteTexture);
      GUI.color = new Color(0.9f, 0.2f, 0.2f, 0.9f);
      GUI.DrawTexture(new Rect(barX + barW * keyT - 2f, y - 2f, 4f, barH + 4f), Texture2D.whiteTexture);
      GUI.color = Color.white;
      GUI.DrawTexture(new Rect(barX + barW * playT - 3f, y - 3f, 6f, barH + 6f), Texture2D.whiteTexture);
      GUI.color = Color.white;
    }
  }
}
