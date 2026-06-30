using System.Collections.Generic;
using AnimationCreator.Data;
using UnityEngine;

namespace AnimationCreator.Actors
{
  public enum SituationHighlight
  {
    None,
    OffsidePlayer,
    LastDefender,
    SecondLastDefender,
  }

  public interface IPlayerVisual
  {
    string PlayerId { get; }
    void ApplyJoints(PlayerJoints joints);
    void SetFootHighlight(string foot, bool active);
    void SetSituationHighlight(SituationHighlight highlight);
    void SetAnalysisMode(bool active, bool isFocusPlayer, float dimAlpha = 0.15f);
    void ClearHighlight();
    PlayerJoints CurrentJoints { get; }
    IReadOnlyDictionary<string, Vector3> GetJointWorldPositions();
  }
}
