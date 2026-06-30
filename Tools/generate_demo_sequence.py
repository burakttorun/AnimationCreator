#!/usr/bin/env python3
"""Generate FRDS v0.1 demo_sequence: 4 players, 2 passes, 1 shot."""

import json
import math
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUTPUT_DIRS = [
    ROOT / "SampleData" / "demo_sequence",
    ROOT / "Assets" / "StreamingAssets" / "demo_sequence",
]

FPS = 25
DURATION_SEC = 16.0
TOTAL_FRAMES = int(DURATION_SEC * FPS)

PLAYERS = [
    {"playerId": "h10", "teamId": "home", "jerseyNumber": 10, "displayName": "Playmaker"},
    {"playerId": "h9", "teamId": "home", "jerseyNumber": 9, "displayName": "Midfielder"},
    {"playerId": "h7", "teamId": "home", "jerseyNumber": 7, "displayName": "Striker"},
    {"playerId": "h1", "teamId": "home", "jerseyNumber": 1, "displayName": "Goalkeeper"},
    {"playerId": "a4", "teamId": "away", "jerseyNumber": 4, "displayName": "Defender"},
    {"playerId": "a5", "teamId": "away", "jerseyNumber": 5, "displayName": "Defender"},
]

EVENTS = [
    {"eventId": "evt_001", "type": "pass", "frameIndex": 80, "actorPlayerId": "h10", "targetPlayerId": "h9", "foot": "right"},
    {"eventId": "evt_002", "type": "pass", "frameIndex": 180, "actorPlayerId": "h9", "targetPlayerId": "h7", "foot": "right"},
    {"eventId": "evt_003", "type": "shot", "frameIndex": 320, "actorPlayerId": "h7", "foot": "left"},
]


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def smoothstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def vec3(x: float, y: float, z: float) -> dict:
    return {"x": round(x, 3), "y": round(y, 3), "z": round(z, 3)}


def player_path(frame: int, player_id: str) -> tuple[float, float, float]:
    """Returns (x, z, facing_rad) for player root at given frame."""
    t = frame / FPS

    if player_id == "h10":
        if frame < 80:
            x = lerp(-12.0, -8.0, smoothstep(frame / 80))
            z = lerp(-20.0, -14.0, smoothstep(frame / 80))
            face = math.atan2(1.0, 2.0)
        else:
            x, z = -8.0, -14.0
            face = math.atan2(1.0, 1.5)
    elif player_id == "h9":
        if frame < 80:
            x, z, face = -4.0, -6.0, math.atan2(1.0, 0.5)
        elif frame < 140:
            p = smoothstep((frame - 80) / 60)
            x = lerp(-4.0, -2.0, p)
            z = lerp(-6.0, 2.0, p)
            face = math.atan2(z - (-6.0), x - (-4.0) + 0.001)
        else:
            x, z = -2.0, 2.0
            face = math.atan2(1.0, 0.8)
    elif player_id == "h7":
        if frame < 180:
            x, z, face = 8.0, 28.0, math.atan2(1.0, 0.0)
        elif frame < 260:
            p = smoothstep((frame - 180) / 80)
            x = lerp(8.0, 6.0, p)
            z = lerp(28.0, 38.0, p)
            face = math.atan2(1.0, -0.2)
        else:
            x, z = 6.0, 38.0
            face = math.atan2(1.0, 0.0)
    elif player_id == "h1":
        x, z, face = 0.0, 50.0, math.atan2(-1.0, 0.0)
    elif player_id == "a4":
        x = lerp(3.0, 2.0, smoothstep(min(frame, 200) / 200))
        z = lerp(32.0, 36.0, smoothstep(min(frame, 200) / 200))
        face = math.atan2(-0.5, -1.0)
    elif player_id == "a5":
        x = lerp(-2.0, -1.0, smoothstep(min(frame, 200) / 200))
        z = lerp(30.0, 35.0, smoothstep(min(frame, 200) / 200))
        face = math.atan2(-0.5, 1.0)
    else:
        x, z, face = 0.0, 0.0, 0.0

    return x, z, face


def ball_pos(frame: int) -> tuple[float, float, float]:
    h10_80 = (-8.0, -14.0)
    h9_140 = (-2.0, 2.0)
    h7_320 = (6.0, 38.0)
    goal = (0.5, 51.5)

    if frame < 80:
        bx, bz = lerp(-12.0, h10_80[0], smoothstep(frame / 80)), lerp(-20.0, h10_80[1], smoothstep(frame / 80))
        bx += 0.35
        bz += 0.2
    elif frame < 140:
        p = smoothstep((frame - 80) / 60)
        bx = lerp(h10_80[0] + 0.35, h9_140[0] + 0.25, p)
        bz = lerp(h10_80[1] + 0.2, h9_140[1] + 0.15, p)
    elif frame < 180:
        bx, bz = h9_140[0] + 0.25, h9_140[1] + 0.15
    elif frame < 260:
        p = smoothstep((frame - 180) / 80)
        bx = lerp(h9_140[0] + 0.25, h7_320[0] - 0.2, p)
        bz = lerp(h9_140[1] + 0.15, h7_320[1] - 0.3, p)
    elif frame < 320:
        bx, bz = h7_320[0] - 0.2, h7_320[1] - 0.3
    elif frame < 400:
        p = smoothstep((frame - 320) / 80)
        arc = math.sin(p * math.pi) * 1.8
        bx = lerp(h7_320[0], goal[0], p)
        bz = lerp(h7_320[1], goal[1], p)
        by = 0.11 + arc
        return bx, by, bz
    else:
        bx, bz = goal[0], goal[1]

    return bx, 0.11, bz


def make_joints(px: float, pz: float, face: float, frame: int, player_id: str) -> dict:
    speed = 1.2 if player_id in ("h10", "h9", "h7") and frame < 320 else 0.3
    phase = frame * 0.35 * speed + hash(player_id) % 7

    stride = 0.22 * math.sin(phase)
    lateral = 0.12 * math.sin(phase + math.pi * 0.5)

    cos_f, sin_f = math.cos(face), math.sin(face)
    forward = (sin_f, cos_f)
    right = (cos_f, -sin_f)

    py = 0.9
    pelvis_x = px + right[0] * lateral
    pelvis_z = pz + right[1] * lateral

    torso_x = pelvis_x + forward[0] * 0.05
    torso_z = pelvis_z + forward[1] * 0.05
    head_x = torso_x + forward[0] * 0.08
    head_z = torso_z + forward[1] * 0.08

    lk_x = pelvis_x + right[0] * (-0.12) + forward[0] * stride * 0.3
    lk_z = pelvis_z + right[1] * (-0.12) + forward[1] * stride * 0.3
    rk_x = pelvis_x + right[0] * 0.12 + forward[0] * (-stride) * 0.3
    rk_z = pelvis_z + right[1] * 0.12 + forward[1] * (-stride) * 0.3

    lf_x = lk_x + forward[0] * stride
    lf_z = lk_z + forward[1] * stride
    rf_x = rk_x + forward[0] * (-stride)
    rf_z = rk_z + forward[1] * (-stride)

    # Kick pose near events
    if player_id == "h10" and 75 <= frame <= 85:
        rf_x, rf_z = ball_pos(frame)[0] - 0.1, ball_pos(frame)[2] - 0.15
    if player_id == "h9" and 175 <= frame <= 185:
        rf_x, rf_z = ball_pos(frame)[0] - 0.1, ball_pos(frame)[2] - 0.15
    if player_id == "h7" and 315 <= frame <= 330:
        lf_x, lf_z = ball_pos(frame)[0] + 0.05, ball_pos(frame)[2] - 0.2

    return {
        "pelvis": vec3(pelvis_x, py, pelvis_z),
        "torso": vec3(torso_x, py + 0.5, torso_z),
        "head": vec3(head_x, py + 0.85, head_z),
        "leftKnee": vec3(lk_x, 0.45, lk_z),
        "rightKnee": vec3(rk_x, 0.45, rk_z),
        "leftFoot": vec3(lf_x, 0.05, lf_z),
        "rightFoot": vec3(rf_x, 0.05, rf_z),
    }


def build_manifest() -> dict:
    return {
        "schemaVersion": "0.1.0",
        "matchId": "demo_sequence",
        "pitch": {"lengthM": 105.0, "widthM": 68.0},
        "timing": {"frameRateHz": FPS, "durationSeconds": DURATION_SEC},
        "teams": [
            {"teamId": "home", "name": "Home", "color": "#E63946"},
            {"teamId": "away", "name": "Away", "color": "#457B9D"},
        ],
        "players": PLAYERS,
    }


def build_events() -> dict:
    events = []
    for e in EVENTS:
        events.append({**e, "timestampMs": int(e["frameIndex"] * 1000 / FPS)})
    return {"events": events}


def build_frames() -> list[str]:
    lines = []
    for frame in range(TOTAL_FRAMES):
        bx, by, bz = ball_pos(frame)
        players = []
        for p in PLAYERS:
            pid = p["playerId"]
            px, pz, face = player_path(frame, pid)
            players.append({
                "playerId": pid,
                "joints": make_joints(px, pz, face, frame, pid),
            })
        line = {
            "frameIndex": frame,
            "timestampMs": int(frame * 1000 / FPS),
            "ball": {"pos": vec3(bx, by, bz)},
            "players": players,
        }
        lines.append(json.dumps(line, separators=(",", ":")))
    return lines


def main() -> None:
    manifest = build_manifest()
    events = build_events()
    frames = build_frames()

    for out_dir in OUTPUT_DIRS:
        out_dir.mkdir(parents=True, exist_ok=True)
        (out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        (out_dir / "events.json").write_text(json.dumps(events, indent=2), encoding="utf-8")
        (out_dir / "frames.jsonl").write_text("\n".join(frames) + "\n", encoding="utf-8")
        print(f"Wrote {out_dir} ({len(frames)} frames)")


if __name__ == "__main__":
    main()
