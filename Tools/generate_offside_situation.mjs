/**
 * FRDS-Situation offside demo — clear VAR-style scenario.
 * Home attacks +Z; offside line at OFFSIDE_Z; striker ahead at kick frame.
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { buildJointsFromRoot, applyKickBlend, GROUND_Y } from "./lib/pose_from_root.mjs";
import { buildSmoothedRootTracks, smoothBallTrack } from "./lib/root_track.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const OUTPUT_DIRS = [
  path.join(ROOT, "SampleData", "offside_demo"),
  path.join(ROOT, "Assets", "StreamingAssets", "offside_demo"),
];

const FPS = 25;
const DURATION_SEC = 5.0;
const TOTAL_FRAMES = Math.floor(DURATION_SEC * FPS);
const KEY_FRAME = 50;
const OFFSIDE_Z = 41.0;
const BALL_R = 0.11;

const HOME_NAMES = ["GK", "LB", "CB", "CB", "RB", "CM", "CM", "CM", "LW", "ST", "RW"];
const AWAY_NAMES = ["GK", "LB", "CB", "CB", "RB", "LM", "CM", "CM", "RM", "ST", "ST"];

function makePlayers() {
  const players = [];
  for (let i = 1; i <= 11; i++) {
    players.push({
      playerId: `h${i}`,
      teamId: "home",
      jerseyNumber: i,
      displayName: HOME_NAMES[i - 1],
    });
  }
  for (let i = 1; i <= 11; i++) {
    players.push({
      playerId: `a${i}`,
      teamId: "away",
      jerseyNumber: i,
      displayName: AWAY_NAMES[i - 1],
    });
  }
  return players;
}

const PLAYERS = makePlayers();

/** Static or keyed root positions. Attack toward +Z. */
const ROOT_AT_KICK = {
  h1: { x: 0, z: -48 },
  h2: { x: -22, z: 28 },
  h3: { x: -10, z: 30 },
  h4: { x: 8, z: 29 },
  h5: { x: 20, z: 28 },
  h6: { x: -14, z: 22 },
  h7: { x: 0, z: 24 },
  h8: { x: 14, z: 22 },
  h9: { x: 2, z: 43.8 },
  h10: { x: -8, z: 34.5 },
  h11: { x: 18, z: 36 },
  a1: { x: 0, z: 48 },
  a2: { x: -20, z: 40.2 },
  a3: { x: -8, z: 40.5 },
  a4: { x: 4, z: 40.3 },
  a5: { x: 16, z: 41.0 },
  a6: { x: -18, z: 34 },
  a7: { x: -6, z: 33 },
  a8: { x: 8, z: 33 },
  a9: { x: 18, z: 34 },
  a10: { x: -4, z: 44 },
  a11: { x: 10, z: 44.5 },
};

const MOTION = {
  h9: [
    { frame: 0, x: 2, z: 38.5 },
    { frame: 45, x: 2, z: 41.5 },
    { frame: KEY_FRAME, x: 2, z: 43.8 },
    { frame: 80, x: 2.5, z: 45.5 },
    { frame: TOTAL_FRAMES - 1, x: 3, z: 46 },
  ],
  h10: [
    { frame: 0, x: -10, z: 32 },
    { frame: 35, x: -9, z: 33.5 },
    { frame: KEY_FRAME, x: -8, z: 34.5 },
    { frame: TOTAL_FRAMES - 1, x: -8, z: 34.5 },
  ],
  h11: [
    { frame: 0, x: 16, z: 34 },
    { frame: KEY_FRAME, x: 18, z: 36 },
    { frame: TOTAL_FRAMES - 1, x: 19, z: 37 },
  ],
  a4: [
    { frame: 0, x: 3, z: 39.8 },
    { frame: KEY_FRAME, x: 4, z: 40.3 },
    { frame: TOTAL_FRAMES - 1, x: 4.2, z: 40.4 },
  ],
  a5: [
    { frame: 0, x: 15.5, z: 40.5 },
    { frame: KEY_FRAME, x: 16, z: 41.0 },
    { frame: TOTAL_FRAMES - 1, x: 16.2, z: 41.1 },
  ],
};

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const lerp = (a, b, t) => a + (b - a) * t;
const easeInOut = (t) => {
  t = clamp(t, 0, 1);
  return t * t * (3 - 2 * t);
};

function sampleMotion(playerId, frame) {
  const keys = MOTION[playerId];
  if (!keys) {
    const base = ROOT_AT_KICK[playerId];
    return { x: base.x, z: base.z };
  }
  if (frame <= keys[0].frame) return { x: keys[0].x, z: keys[0].z };
  const last = keys[keys.length - 1];
  if (frame >= last.frame) return { x: last.x, z: last.z };

  let i = 0;
  while (i < keys.length - 1 && keys[i + 1].frame < frame) i++;
  const p1 = keys[i];
  const p2 = keys[i + 1];
  const t = easeInOut((frame - p1.frame) / (p2.frame - p1.frame));
  return { x: lerp(p1.x, p2.x, t), z: lerp(p1.z, p2.z, t) };
}

function defaultYaw(playerId) {
  return playerId.startsWith("h") ? 0 : Math.PI;
}

const playerIds = PLAYERS.map((p) => p.playerId);
const rootTracks = buildSmoothedRootTracks(
  playerIds,
  (id, frame) => sampleMotion(id, frame),
  TOTAL_FRAMES,
  FPS,
  defaultYaw,
);

function buildRawBallPositions() {
  const raw = [];
  for (let f = 0; f < TOTAL_FRAMES; f++) {
    const h10 = rootTracks.h10;
    const h9 = rootTracks.h9;
    if (f < KEY_FRAME) {
      raw.push({
        x: h10.x[f] + 0.35,
        y: GROUND_Y + BALL_R,
        z: h10.z[f] + 0.2,
      });
      continue;
    }
    const t = easeInOut((f - KEY_FRAME) / 24);
    const start = { x: h10.x[KEY_FRAME], z: h10.z[KEY_FRAME] };
    const end = { x: h9.x[f] - 0.5, z: h9.z[f] - 1.2 };
    raw.push({
      x: lerp(start.x, end.x, t),
      y: GROUND_Y + BALL_R + Math.sin(t * Math.PI) * 1.0,
      z: lerp(start.z, end.z, t),
    });
  }
  return smoothBallTrack(raw, FPS);
}

const ballPositions = buildRawBallPositions();

const manifest = {
  schemaVersion: "0.3.0",
  matchId: "offside_demo",
  pitch: { lengthM: 105, widthM: 68 },
  timing: { frameRateHz: FPS, durationSeconds: DURATION_SEC },
  situation: {
    type: "offside",
    title: "VAR — Ofsayt incelemesi",
    description: "Pas anında forvet (9) ikinci son savunmacının gerisinde.",
    keyFrameIndex: KEY_FRAME,
    kickPoint: { x: -8.0, y: GROUND_Y + BALL_R, z: 34.5 },
    offsideLine: {
      z: OFFSIDE_Z,
      secondLastDefenderPlayerId: "a5",
      lastDefenderPlayerId: "a4",
      offsidePlayerId: "h9",
    },
    replayWindow: { startFrameIndex: 25, endFrameIndex: 85 },
  },
  dataSource: {
    positions: "authored",
    joints: "synthesized_from_root",
    note: "Demo senaryosu; gerçek tracking için SoccerTrack/SoccerNet kök pozisyonları kullanılabilir.",
  },
  teams: [
    { teamId: "home", name: "Kırmızı", color: "#E63946" },
    { teamId: "away", name: "Mavi", color: "#1D3557" },
  ],
  players: PLAYERS,
};

const events = {
  events: [
    {
      eventId: "evt_pass",
      type: "pass",
      frameIndex: KEY_FRAME,
      timestampMs: Math.round((KEY_FRAME / FPS) * 1000),
      actorPlayerId: "h10",
      targetPlayerId: "h9",
      foot: "right",
    },
    {
      eventId: "evt_offside",
      type: "offside_review",
      frameIndex: KEY_FRAME,
      timestampMs: Math.round((KEY_FRAME / FPS) * 1000),
      actorPlayerId: "h9",
      targetPlayerId: "a5",
      foot: "",
    },
  ],
};

const frames = [];
for (let f = 0; f < TOTAL_FRAMES; f++) {
  const ball = ballPositions[f];
  const playerFrames = PLAYERS.map((p) => {
    const id = p.playerId;
    const track = rootTracks[id];
    const px = track.x[f];
    const pz = track.z[f];
    const yaw = track.yaw[f];
    const speed = track.speed[f];
    const phase = track.phase[f];
    const role = id === "h1" || id === "a1" ? "goalkeeper" : "player";
    let joints = buildJointsFromRoot(px, pz, yaw, speed, phase, role);

    if (id === "h10" && f >= KEY_FRAME - 8 && f <= KEY_FRAME + 4) {
      const kickT = clamp((f - (KEY_FRAME - 8)) / 10, 0, 1);
      const envelope = Math.sin(kickT * Math.PI);
      joints = applyKickBlend(joints, yaw, envelope, "right");
    }

    return { playerId: id, joints };
  });

  frames.push({
    frameIndex: f,
    timestampMs: Math.round((f / FPS) * 1000),
    ball: { pos: { x: ball.x, y: ball.y, z: ball.z } },
    players: playerFrames,
  });
}

const PLAYABLE_JOINTS = ["head", "torso", "pelvis", "leftFoot", "rightFoot", "leftKnee", "rightKnee"];

function computeVerdict(frame) {
  const attacker = frame.players.find((p) => p.playerId === "h9");
  const defender = frame.players.find((p) => p.playerId === "a5");
  let maxZ = -Infinity;
  let maxJoint = "leftKnee";
  let maxPos = attacker.joints.leftKnee;

  for (const joint of PLAYABLE_JOINTS) {
    const pos = attacker.joints[joint];
    if (pos.z > maxZ) {
      maxZ = pos.z;
      maxJoint = joint;
      maxPos = pos;
    }
  }

  const margin = +(maxZ - OFFSIDE_Z).toFixed(3);
  return {
    outcome: margin > 0.001 ? "offside" : "onside",
    marginM: margin,
    attackerPoint: {
      playerId: "h9",
      joint: maxJoint,
      position: { x: maxPos.x, y: maxPos.y, z: maxPos.z },
    },
    defenderPoint: {
      playerId: "a5",
      joint: "torso",
      position: { ...defender.joints.torso },
    },
  };
}

manifest.situation.verdict = computeVerdict(frames[KEY_FRAME]);

for (const dir of OUTPUT_DIRS) {
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(path.join(dir, "manifest.json"), JSON.stringify(manifest, null, 2));
  fs.writeFileSync(path.join(dir, "events.json"), JSON.stringify(events, null, 2));
  fs.writeFileSync(
    path.join(dir, "frames.jsonl"),
    frames.map((fr) => JSON.stringify(fr)).join("\n") + "\n",
  );
  console.log(`Wrote ${dir} (${frames.length} frames, ${PLAYERS.length} players)`);
}
