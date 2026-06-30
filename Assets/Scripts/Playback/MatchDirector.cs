using System.Collections.Generic;
using AnimationCreator.Actors;
using AnimationCreator.Data;
using UnityEngine;

namespace AnimationCreator.Playback
{
  public class MatchDirector : MonoBehaviour
  {
    [SerializeField] string sequenceId = "sngs116_real";
    [SerializeField] float pitchLengthM = 105f;
    [SerializeField] float pitchWidthM = 68f;
    [SerializeField] bool smoothJoints = true;
    [SerializeField] float smoothWindowSeconds = 0.1f;

    [SerializeField] bool showBroadcastVideo = true;
    [SerializeField] string broadcastClipId = "SNGS-116";
    [SerializeField] string broadcastSplit = "test";
    [SerializeField] int broadcastSourceStartFrame = 375;
    [SerializeField] bool broadcastPretrimmedClip;

    MatchDataset _dataset;
    MatchClock _clock;
    BallActor _ball;
    BroadcastVideoPanel _broadcast;
    readonly Dictionary<string, IPlayerVisual> _players = new Dictionary<string, IPlayerVisual>();
    readonly Dictionary<int, List<MatchEvent>> _eventsByFrame = new Dictionary<int, List<MatchEvent>>();

    void Awake()
    {
      var environment = new GameObject("Environment");
      PitchBuilder.Build(environment.transform, pitchLengthM, pitchWidthM);

      _clock = gameObject.AddComponent<MatchClock>();
      _dataset = LoadDataset();
      SyncBroadcastFromManifest();
      IndexEvents();
      SpawnActors();
      _clock.Configure(_dataset.Manifest.timing.frameRateHz, _dataset.Frames.Count);
      _clock.OnFrameChanged += OnFrameChanged;
      ApplyInterpolatedFrame(_clock.CurrentFrame, 0f);

      if (showBroadcastVideo)
        SetupBroadcastPanel();

      EnsureCameraAndLight();
    }

    void SetupBroadcastPanel()
    {
      _broadcast = gameObject.AddComponent<BroadcastVideoPanel>();
      _broadcast.Configure(
        broadcastClipId,
        broadcastSplit,
        broadcastSourceStartFrame,
        sequenceId,
        broadcastPretrimmedClip);
      _broadcast.Initialize(_clock);
    }

    void SyncBroadcastFromManifest()
    {
      var source = _dataset.Manifest.dataSource;
      if (source == null)
        return;

      if (!string.IsNullOrEmpty(source.clipId))
        broadcastClipId = source.clipId;

      if (source.type == "skillcorner_tracking")
      {
        broadcastPretrimmedClip = true;
        broadcastSourceStartFrame = 0;
      }
      else if (!broadcastPretrimmedClip && source.frameRange != null && source.frameRange.start >= 0)
      {
        broadcastSourceStartFrame = source.frameRange.start;
      }
    }

    MatchDataset LoadDataset()
    {
      var manifest = LoadManifestHint();
      var trackingReplay = manifest?.dataSource?.jointSynthesis != null
                           && manifest.dataSource.jointSynthesis.Contains("tracking");

      if (trackingReplay || !smoothJoints)
        return MatchDataLoader.LoadRaw(sequenceId);

      var settings = FrdsJointSmoother.Settings.Default;
      settings.WindowSeconds = smoothWindowSeconds;
      settings.Enabled = true;
      return MatchDataLoader.Load(sequenceId, settings);
    }

    bool UsesTrackingReplay()
    {
      var synthesis = _dataset?.Manifest?.dataSource?.jointSynthesis;
      return synthesis != null && synthesis.Contains("tracking");
    }

    MatchManifest LoadManifestHint()
    {
      try
      {
        var path = System.IO.Path.Combine(Application.streamingAssetsPath, sequenceId, "manifest.json");
        if (!System.IO.File.Exists(path))
          return null;
        return JsonUtility.FromJson<MatchManifest>(System.IO.File.ReadAllText(path));
      }
      catch
      {
        return null;
      }
    }

    void OnDestroy()
    {
      if (_clock != null)
        _clock.OnFrameChanged -= OnFrameChanged;
    }

    void OnFrameChanged(int frameIndex) => ApplyEventHighlights(frameIndex);

    void Update()
    {
      if (MatchPlaybackInput.TogglePlayPressed)
        _clock.TogglePlay();
      if (MatchPlaybackInput.StepBackPressed)
        _clock.StepFrame(-1);
      if (MatchPlaybackInput.StepForwardPressed)
        _clock.StepFrame(1);
      if (MatchPlaybackInput.RestartPressed)
        _clock.SeekFrame(0);

      var cam = Camera.main != null ? Camera.main.GetComponent<MatchCameraController>() : null;
      if (MatchPlaybackInput.CycleCameraPressed && cam != null)
        cam.CycleMode();

      var alpha = _clock.IsPlaying ? _clock.InterpolationAlpha : 0f;
      ApplyInterpolatedFrame(_clock.CurrentFrame, alpha);
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
        IPlayerVisual visual = UsesTrackingReplay()
          ? playerGo.AddComponent<TrackingMarkerPlayer>()
          : playerGo.AddComponent<StickFigurePlayer>();
        if (visual is TrackingMarkerPlayer tracker)
          tracker.Initialize(playerInfo, color);
        else if (visual is StickFigurePlayer stick)
          stick.Initialize(playerInfo, color);
        _players[playerInfo.playerId] = visual;
      }
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
        actor.ClearHighlight();

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

    void EnsureCameraAndLight()
    {
      var mainCam = Camera.main;
      if (mainCam == null)
      {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        mainCam = camGo.AddComponent<Camera>();
      }

      if (mainCam.GetComponent<MatchCameraController>() == null)
        mainCam.gameObject.AddComponent<MatchCameraController>();

      if (FindAnyObjectByType<Light>() == null)
      {
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
      }
    }

    void OnGUI()
    {
      var y = 10f;
      GUI.Label(new Rect(10, y, 640, 24), $"FRDS Sim — {_dataset.Manifest.matchId}" + (UsesTrackingReplay() ? " [tracking replay]" : ""));
      y += 22;
      var broadcastFrame = broadcastPretrimmedClip
        ? _clock.CurrentFrame
        : broadcastSourceStartFrame + _clock.CurrentFrame;
      var videoNote = _broadcast != null && _broadcast.HasVideo
        ? "broadcast synced"
        : broadcastPretrimmedClip
          ? "video: Assets/Resources/clip.mp4"
          : "video: drop clip into ExternalData/SoccerNetGS/test/SNGS-116/";
      GUI.Label(new Rect(10, y, 900, 24), $"Frame {_clock.CurrentFrame + 1}/{_dataset.Frames.Count}  |  broadcast {broadcastFrame}  |  {videoNote}  |  Space: Play  |  C: Camera");
      y += 22;

      foreach (var evt in _dataset.Events)
      {
        var marker = _clock.CurrentFrame == evt.frameIndex ? ">>" : "  ";
        GUI.Label(new Rect(10, y, 700, 20), $"{marker} F{evt.frameIndex} {evt.type} {evt.actorPlayerId} ({evt.foot})");
        y += 18;
      }
    }
  }
}
