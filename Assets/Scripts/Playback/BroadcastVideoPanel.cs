using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace AnimationCreator.Playback
{
  /// <summary>
  /// Optional broadcast panel synced to <see cref="MatchClock"/>.
  /// Plays video.mkv if present, otherwise JPG frame sequence from frames/.
  /// </summary>
  public class BroadcastVideoPanel : MonoBehaviour
  {
    [SerializeField] string clipId = "SNGS-116";
    [SerializeField] string split = "test";
    [SerializeField] string sequenceId = "";
    [SerializeField] int sourceStartFrame = 50;
    [SerializeField] bool pretrimmedClip;
    [SerializeField] float panelWidth = 0.38f;
    [SerializeField] Color placeholderColor = new Color(0.08f, 0.09f, 0.11f, 1f);

    MatchClock _clock;
    VideoPlayer _player;
    RawImage _image;
    RenderTexture _renderTexture;
    Texture2D _frameTexture;
    Text _placeholder;
    string _framesDir;
    string _loadedVideoPath;
    int _lastFrameIndex = -1;
    bool _useVideo;
    bool _useFrames;
    bool _videoPrepared;

    public bool HasVideo => _useVideo || _useFrames;

    public void Configure(string newClipId, string newSplit, int frameStart, string frdsSequenceId = null, bool isPretrimmedClip = false)
    {
      clipId = newClipId;
      split = newSplit;
      sequenceId = frdsSequenceId ?? "";
      pretrimmedClip = isPretrimmedClip;
      sourceStartFrame = isPretrimmedClip ? 0 : frameStart;
    }

    public void Initialize(MatchClock clock)
    {
      _clock = clock;
      _clock.OnFrameChanged += OnClockFrameChanged;
      BuildUi();
      TryLoadBroadcast();
      if (!HasVideo)
      {
        _placeholder.text = $"Video bekleniyor\n{clipId}";
        _image.color = new Color(0.12f, 0.13f, 0.16f, 1f);
      }

      ApplyLayout();
    }

    void OnClockFrameChanged(int _) => SyncVideoToClock(force: true);

    void BuildUi()
    {
      var canvasGo = new GameObject("BroadcastCanvas");
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 20;
      canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      canvasGo.AddComponent<GraphicRaycaster>();

      var panelGo = new GameObject("BroadcastPanel");
      panelGo.transform.SetParent(canvasGo.transform, false);
      var panelRect = panelGo.AddComponent<RectTransform>();
      panelRect.anchorMin = new Vector2(1f - panelWidth, 0f);
      panelRect.anchorMax = Vector2.one;
      panelRect.offsetMin = Vector2.zero;
      panelRect.offsetMax = Vector2.zero;
      var panelImage = panelGo.AddComponent<Image>();
      panelImage.color = placeholderColor;

      var videoGo = new GameObject("Video");
      videoGo.transform.SetParent(panelGo.transform, false);
      var videoRect = videoGo.AddComponent<RectTransform>();
      videoRect.anchorMin = Vector2.zero;
      videoRect.anchorMax = Vector2.one;
      videoRect.offsetMin = new Vector2(8f, 8f);
      videoRect.offsetMax = new Vector2(-8f, -48f);
      _image = videoGo.AddComponent<RawImage>();
      _image.color = Color.white;

      var labelGo = new GameObject("Label");
      labelGo.transform.SetParent(panelGo.transform, false);
      var labelRect = labelGo.AddComponent<RectTransform>();
      labelRect.anchorMin = new Vector2(0f, 1f);
      labelRect.anchorMax = new Vector2(1f, 1f);
      labelRect.pivot = new Vector2(0.5f, 1f);
      labelRect.sizeDelta = new Vector2(0f, 36f);
      labelRect.anchoredPosition = new Vector2(0f, -4f);
      _placeholder = labelGo.AddComponent<Text>();
      _placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      _placeholder.fontSize = 14;
      _placeholder.alignment = TextAnchor.MiddleCenter;
      _placeholder.color = new Color(0.85f, 0.88f, 0.92f, 1f);
      _placeholder.text = "Broadcast";

      var playerGo = new GameObject("VideoPlayer");
      playerGo.transform.SetParent(transform, false);
      _player = playerGo.AddComponent<VideoPlayer>();
      _player.playOnAwake = false;
      _player.waitForFirstFrame = true;
      _player.skipOnDrop = true;
      _player.isLooping = false;
      _player.audioOutputMode = VideoAudioOutputMode.None;
      _player.renderMode = VideoRenderMode.RenderTexture;
    }

    void TryLoadBroadcast()
    {
      if (_useVideo || _useFrames || _videoPrepared)
        return;

      _framesDir = ResolveFramesDir();
      if (TryUseFrameSequence())
      {
        ApplyLayout();
        return;
      }

      var videoPath = ResolveVideoPath();
      if (string.IsNullOrEmpty(videoPath))
        return;

      _loadedVideoPath = videoPath;
      _renderTexture = new RenderTexture(1280, 720, 0);
      _player.targetTexture = _renderTexture;
      _image.texture = _renderTexture;
      _player.url = ToFileUrl(videoPath);
      _player.prepareCompleted += OnVideoPrepared;
      _player.errorReceived += OnVideoError;
      _player.Prepare();
      _placeholder.text = pretrimmedClip ? $"{clipId} (clip)" : clipId;
    }

    void OnVideoError(VideoPlayer source, string message)
    {
      Debug.LogWarning($"Broadcast video error ({_loadedVideoPath}): {message}");
    }

    bool TryUseFrameSequence()
    {
      if (string.IsNullOrEmpty(_framesDir) || !Directory.Exists(_framesDir))
        return false;

      var firstFrame = Path.Combine(_framesDir, FrameFileName(sourceStartFrame));
      if (!File.Exists(firstFrame))
        return false;

      _useFrames = true;
      _frameTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
      _image.texture = _frameTexture;
      _placeholder.text = $"{clipId} (frames)";
      ShowFrame(sourceStartFrame, force: true);
      return true;
    }

    void OnVideoPrepared(VideoPlayer source)
    {
      _videoPrepared = true;
      _useVideo = true;
      _image.color = Color.white;
      ApplyLayout();
      SyncVideoToClock(force: true);
      if (_clock != null && _clock.IsPlaying)
        source.Play();
    }

    void Update()
    {
      if (_clock == null)
        return;

      if (_useVideo)
      {
        if (!_videoPrepared)
          return;

        SyncVideoToClock(force: false);
        return;
      }

      if (_useFrames)
      {
        var sourceFrame = sourceStartFrame + _clock.CurrentFrame;
        ShowFrame(sourceFrame, force: false);
        return;
      }

      // Retry if download finished after scene start
      if (Time.frameCount % 120 == 0)
        TryLoadBroadcast();
    }

    float ComputeTargetTime()
    {
      var fps = Mathf.Max(1f, _clock.FrameRateHz);
      var simTime = (sourceStartFrame + _clock.CurrentFrame) / fps;
      if (_clock.IsPlaying)
        simTime += _clock.InterpolationAlpha / fps;

      var simDuration = _clock.TotalFrames / fps;
      var videoDuration = (float)_player.length;
      if (simDuration > 0.01f && videoDuration > 0.01f)
        simTime *= videoDuration / simDuration;

      return Mathf.Clamp(simTime, 0f, Mathf.Max(0f, videoDuration - 0.001f));
    }

    void SyncVideoToClock(bool force)
    {
      if (!_player.isPrepared || _clock == null)
        return;

      var targetTime = ComputeTargetTime();
      var drift = Mathf.Abs((float)(_player.time - targetTime));

      if (pretrimmedClip)
      {
        if (!force && drift < 0.001f)
          return;

        _player.time = targetTime;
        _player.Play();
        _player.Pause();
        return;
      }

      if (_clock.IsPlaying)
      {
        if (force || drift > 0.15f)
          _player.time = targetTime;
        return;
      }

      if (!force && drift < 0.02f)
        return;

      _player.time = targetTime;
    }

    void ShowFrame(int sourceFrame, bool force)
    {
      if (!force && sourceFrame == _lastFrameIndex)
        return;

      var path = Path.Combine(_framesDir, FrameFileName(sourceFrame));
      if (!File.Exists(path))
        return;

      var bytes = File.ReadAllBytes(path);
      if (!_frameTexture.LoadImage(bytes))
        return;

      _lastFrameIndex = sourceFrame;
      _image.color = Color.white;
    }

    static string FrameFileName(int frameIndex) => (frameIndex + 1).ToString("D6") + ".jpg";

    void ApplyLayout()
    {
      var cam = Camera.main;
      if (cam == null)
        return;

      cam.rect = HasVideo ? new Rect(0f, 0f, 1f - panelWidth, 1f) : new Rect(0f, 0f, 1f, 1f);
    }

    string ResolveVideoPath()
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
      var candidates = new List<string>();

      if (!string.IsNullOrEmpty(sequenceId))
      {
        candidates.Add(Path.Combine(Application.streamingAssetsPath, sequenceId, "broadcast.mp4"));
        candidates.Add(Path.Combine(Application.streamingAssetsPath, sequenceId, "broadcast.mkv"));
        candidates.Add(Path.Combine(Application.streamingAssetsPath, sequenceId, "clip.mp4"));
      }

      candidates.Add(Path.Combine(Application.dataPath, "Resources", "clip.mp4"));

      var skillCornerDir = Path.Combine(projectRoot, "ExternalData", "SkillCorner", "matches", clipId);
      candidates.Add(Path.Combine(skillCornerDir, "clip.mp4"));
      candidates.Add(Path.Combine(skillCornerDir, "broadcast.mp4"));
      candidates.Add(Path.Combine(skillCornerDir, "video.mp4"));
      candidates.Add(Path.Combine(skillCornerDir, "video.mkv"));

      candidates.Add(Path.Combine(projectRoot, "ExternalData", "SoccerNetGS", split, clipId, "video.mkv"));
      candidates.Add(Path.Combine(projectRoot, "ExternalData", "SoccerNetGS", split, clipId, "video.mp4"));
      candidates.Add(Path.Combine(Application.streamingAssetsPath, "match_clip_demo", "broadcast.mp4"));
      candidates.Add(Path.Combine(Application.streamingAssetsPath, "match_clip_demo", "broadcast.mkv"));

      foreach (var candidate in candidates)
      {
        if (File.Exists(candidate))
          return candidate;
      }

      return null;
    }

    string ResolveFramesDir()
    {
      var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
      return Path.Combine(projectRoot, "ExternalData", "SoccerNetGS", split, clipId, "frames");
    }

    static string ToFileUrl(string path) => new System.Uri(path).AbsoluteUri;

    void OnDestroy()
    {
      if (_clock != null)
        _clock.OnFrameChanged -= OnClockFrameChanged;

      if (_player != null)
      {
        _player.prepareCompleted -= OnVideoPrepared;
        _player.errorReceived -= OnVideoError;
      }
      if (_renderTexture != null)
        _renderTexture.Release();
      if (_frameTexture != null)
        Destroy(_frameTexture);
    }
  }
}
