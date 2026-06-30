using System;
using UnityEngine;

namespace AnimationCreator.Playback
{
  public class MatchClock : MonoBehaviour
  {
    public int FrameRateHz { get; private set; } = 25;
    public int TotalFrames { get; private set; }
    public int CurrentFrame { get; private set; }
    public bool IsPlaying { get; private set; } = true;
    public float PlaybackSpeed { get; set; } = 1f;

    float _accumulator;

    /// <summary>0..1 blend toward the next frame while playing.</summary>
    public float InterpolationAlpha
    {
      get
      {
        var frameDuration = 1f / Mathf.Max(1, FrameRateHz);
        return Mathf.Clamp01(_accumulator / frameDuration);
      }
    }

    public event Action<int> OnFrameChanged;

    public void Configure(int frameRateHz, int totalFrames)
    {
      FrameRateHz = Mathf.Max(1, frameRateHz);
      TotalFrames = Mathf.Max(1, totalFrames);
      CurrentFrame = 0;
      _accumulator = 0f;
      OnFrameChanged?.Invoke(CurrentFrame);
    }

    void Update()
    {
      if (!IsPlaying || TotalFrames <= 0)
        return;

      _accumulator += Time.deltaTime * PlaybackSpeed;
      var frameDuration = 1f / FrameRateHz;

      while (_accumulator >= frameDuration)
      {
        _accumulator -= frameDuration;
        if (CurrentFrame >= TotalFrames - 1)
        {
          IsPlaying = false;
          break;
        }

        CurrentFrame++;
        OnFrameChanged?.Invoke(CurrentFrame);
      }
    }

    public void TogglePlay()
    {
      IsPlaying = !IsPlaying;
    }

    public void Pause() => IsPlaying = false;

    public void Play() => IsPlaying = true;

    public void SeekFrame(int frameIndex)
    {
      CurrentFrame = Mathf.Clamp(frameIndex, 0, TotalFrames - 1);
      _accumulator = 0f;
      OnFrameChanged?.Invoke(CurrentFrame);
    }

    public void StepFrame(int delta)
    {
      Pause();
      SeekFrame(CurrentFrame + delta);
    }
  }
}
