import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");
const OUTPUT_DIRS = [
  path.join(ROOT, "SampleData", "demo_sequence"),
  path.join(ROOT, "Assets", "StreamingAssets", "demo_sequence"),
];

const FPS = 25;
const DURATION_SEC = 18.0;
const TOTAL_FRAMES = Math.floor(DURATION_SEC * FPS);

const PLAYERS = [
  { playerId: "h10", teamId: "home", jerseyNumber: 10, displayName: "Playmaker" },
  { playerId: "h9", teamId: "home", jerseyNumber: 9, displayName: "Midfielder" },
  { playerId: "h7", teamId: "home", jerseyNumber: 7, displayName: "Striker" },
  { playerId: "h1", teamId: "home", jerseyNumber: 1, displayName: "Goalkeeper" },
  { playerId: "a4", teamId: "away", jerseyNumber: 4, displayName: "Defender" },
  { playerId: "a5", teamId: "away", jerseyNumber: 5, displayName: "Defender" },
];

const EVENTS = [
  { eventId: "evt_001", type: "pass", frameIndex: 100, actorPlayerId: "h10", targetPlayerId: "h9", foot: "right" },
  { eventId: "evt_002", type: "pass", frameIndex: 210, actorPlayerId: "h9", targetPlayerId: "h7", foot: "right" },
  { eventId: "evt_003", type: "shot", frameIndex: 360, actorPlayerId: "h7", foot: "left" },
];

// Anthropometry (metres)
const BONE = {
  pelvisH: 0.92,
  thigh: 0.42,
  shin: 0.40,
  torso: 0.52,
  neck: 0.14,
  head: 0.14,
  shoulderHalfW: 0.22,
  upperArm: 0.28,
  forearm: 0.26,
  hipHalfW: 0.14,
};

const GROUND_Y = 0.04;
const BALL_R = 0.11;

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const lerp = (a, b, t) => a + (b - a) * t;
const easeInOut = (t) => {
  t = clamp(t, 0, 1);
  return t * t * (3 - 2 * t);
};
const easeOut = (t) => 1 - (1 - clamp(t, 0, 1)) ** 2;
const vec3 = (x, y, z) => ({ x: +x.toFixed(3), y: +y.toFixed(3), z: +z.toFixed(3) });

const add = (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const scale = (a, s) => [a[0] * s, a[1] * s, a[2] * s];
const len = (a) => Math.hypot(a[0], a[1], a[2]);
const norm = (a) => {
  const l = len(a);
  return l < 1e-8 ? [0, 0, 1] : scale(a, 1 / l);
};
const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

function rotYaw([x, y, z], yaw) {
  const c = Math.cos(yaw);
  const s = Math.sin(yaw);
  return [x * c + z * s, y, -x * s + z * c];
}

function worldFromLocal(root, yaw, local) {
  const [lx, ly, lz] = local;
  const [wx, wy, wz] = rotYaw([lx, ly, lz], yaw);
  return [root[0] + wx, root[1] + wy, root[2] + wz];
}

/** Catmull-Rom spline through keyframes [{frame,x,z}, ...]. */
function samplePath(keyframes, frame) {
  if (keyframes.length === 1) return [keyframes[0].x, keyframes[0].z];
  if (frame <= keyframes[0].frame) return [keyframes[0].x, keyframes[0].z];
  const last = keyframes[keyframes.length - 1];
  if (frame >= last.frame) return [last.x, last.z];

  let i = 0;
  while (i < keyframes.length - 1 && keyframes[i + 1].frame < frame) i++;

  const p0 = keyframes[Math.max(0, i - 1)];
  const p1 = keyframes[i];
  const p2 = keyframes[i + 1];
  const p3 = keyframes[Math.min(keyframes.length - 1, i + 2)];

  const t = easeInOut((frame - p1.frame) / (p2.frame - p1.frame));
  const t2 = t * t;
  const t3 = t2 * t;

  const x =
    0.5 *
    (2 * p1.x +
      (-p0.x + p2.x) * t +
      (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 +
      (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3);
  const z =
    0.5 *
    (2 * p1.z +
      (-p0.z + p2.z) * t +
      (2 * p0.z - 5 * p1.z + 4 * p2.z - p3.z) * t2 +
      (-p0.z + 3 * p1.z - 3 * p2.z + p3.z) * t3);
  return [x, z];
}

const PLAYER_PATHS = {
  h10: [
    { frame: 0, x: -14, z: -28 },
    { frame: 70, x: -11, z: -18 },
    { frame: 95, x: -10.5, z: -14 },
    { frame: 120, x: -10.5, z: -14 },
  ],
  h9: [
    { frame: 0, x: -6, z: -8 },
    { frame: 90, x: -5, z: -4 },
    { frame: 120, x: -3.5, z: 0 },
    { frame: 170, x: -3, z: 2 },
    { frame: 205, x: -2.5, z: 4 },
    { frame: 240, x: -2, z: 6 },
  ],
  h7: [
    { frame: 0, x: 5, z: 22 },
    { frame: 200, x: 6, z: 26 },
    { frame: 250, x: 5.5, z: 32 },
    { frame: 300, x: 4, z: 38 },
    { frame: 340, x: 3, z: 42 },
    { frame: 380, x: 2.5, z: 44 },
  ],
  h1: [
    { frame: 0, x: 0, z: 50.5 },
    { frame: 300, x: 0.8, z: 50.5 },
    { frame: 380, x: -0.5, z: 50.5 },
  ],
  a4: [
    { frame: 0, x: 4, z: 30 },
    { frame: 120, x: 3.5, z: 32 },
    { frame: 220, x: 2.5, z: 35 },
    { frame: 320, x: 1.5, z: 38 },
  ],
  a5: [
    { frame: 0, x: -3, z: 28 },
    { frame: 120, x: -2.5, z: 31 },
    { frame: 220, x: -1.5, z: 34 },
    { frame: 320, x: -0.5, z: 37 },
  ],
};

const rootCache = {};
function getRootMotion(frame, playerId) {
  const path = PLAYER_PATHS[playerId];
  const [px, pz] = samplePath(path, frame);

  const prev = rootCache[playerId];
  let vx = 0;
  let vz = 0;
  let speed = 0;
  let yaw = prev?.yaw ?? 0;

  if (prev) {
    vx = (px - prev.x) * FPS;
    vz = (pz - prev.z) * FPS;
    speed = Math.hypot(vx, vz);
    if (speed > 0.08) yaw = lerpAngle(yaw, Math.atan2(vx, vz), 0.35);
  }

  rootCache[playerId] = { x: px, z: pz, yaw, speed };
  return { px, pz, yaw, speed, vx, vz };
}

function lerpAngle(a, b, t) {
  let d = b - a;
  while (d > Math.PI) d -= 2 * Math.PI;
  while (d < -Math.PI) d += 2 * Math.PI;
  return a + d * t;
}

function clampReach(from, to, maxDist) {
  const d = sub(to, from);
  const l = len(d);
  if (l <= maxDist) return [...to];
  return add(from, scale(norm(d), maxDist * 0.98));
}

/** Two-bone IK; knee bends toward body forward. */
function ikLeg(hip, foot, thighLen, shinLen, fwd) {
  const maxReach = thighLen + shinLen - 0.03;
  foot = clampReach(hip, foot, maxReach);
  const d = sub(foot, hip);
  let dist = len(d);
  dist = clamp(dist, 0.08, thighLen + shinLen - 0.02);

  const dir = norm(d);
  const a = (thighLen * thighLen - shinLen * shinLen + dist * dist) / (2 * dist);
  const hSq = Math.max(0, thighLen * thighLen - a * a);
  const h = Math.sqrt(hSq);

  const mid = add(hip, scale(dir, a));
  let perp = cross(dir, [0, 1, 0]);
  if (len(perp) < 0.05) perp = cross(dir, fwd);
  perp = norm(perp);
  const knee = add(mid, scale(perp, h));
  return { knee, foot: [...foot] };
}

/** Gait phase advances with speed; returns foot targets in body-local space. */
function gaitFootLocal(legPhase, speed, strideScale = 1) {
  const jog = clamp(speed / 5.5, 0, 1);
  const walkBlend = 1 - jog;
  const cadence = 1.4 + jog * 1.6;
  const stride = (0.32 + jog * 0.42) * strideScale;

  const ph = legPhase % 1;
  const stanceEnd = 0.62 - walkBlend * 0.08;
  const lift = 0.05 + jog * 0.14;

  if (ph < stanceEnd) {
    const t = ph / stanceEnd;
    const fwd = lerp(stride * 0.45, -stride * 0.42, easeInOut(t));
    const y = GROUND_Y + (jog > 0.35 ? 0.008 * Math.sin(t * Math.PI) : 0);
    return { fwd, y, grounded: true };
  }

  const t = (ph - stanceEnd) / (1 - stanceEnd);
  const fwd = lerp(-stride * 0.42, stride * 0.48, easeOut(t));
  const y = GROUND_Y + lift * Math.sin(t * Math.PI);
  return { fwd, y, grounded: t > 0.92 };
}

function solveLegChain(pelvis, yaw, side, legPhase, speed, fwd3) {
  const sign = side === "left" ? -1 : 1;
  const hipLocal = [BONE.hipHalfW * sign, 0, 0];
  const hip = worldFromLocal(pelvis, yaw, hipLocal);
  const localFoot = gaitFootLocal(legPhase, speed);
  const foot = worldFromLocal(pelvis, yaw, [localFoot.fwd, localFoot.y - pelvis[1], 0]);
  return ikLeg(hip, foot, BONE.thigh, BONE.shin, fwd3);
}

function armSwing(phase, side, speed) {
  const sign = side === "left" ? -1 : 1;
  const amp = 0.12 + clamp(speed / 6, 0, 1) * 0.22;
  const fwd = Math.sin(phase + (side === "left" ? Math.PI : 0)) * amp;
  const down = -0.08 - clamp(speed / 8, 0, 1) * 0.06;
  const out = sign * 0.07;
  return [fwd, down, out];
}

function elbowFromShoulder(shoulder, yaw, swing, upperLen) {
  const dir = norm(rotYaw(swing, yaw));
  const elbow = add(shoulder, scale(dir, upperLen));
  return elbow;
}

/** Kick overlay on planting foot; returns normalized phase or null. */
function kickOverlay(playerId, frame, foot) {
  const kicks = {
    h10: { start: 96, end: 108, foot: "right" },
    h9: { start: 204, end: 214, foot: "right" },
    h7: { start: 354, end: 368, foot: "left" },
  };
  const k = kicks[playerId];
  if (!k || k.foot !== foot) return null;
  if (frame < k.start || frame > k.end + 6) return null;
  const dur = k.end - k.start;
  const t = clamp((frame - k.start) / dur, 0, 1);
  const recover = frame > k.end ? clamp((frame - k.end) / 6, 0, 1) : 0;
  return { t, recover };
}

function applyKick(defaultLeg, overlay, pelvis, yaw, side, fwd3, ball) {
  if (!overlay) return defaultLeg;
  const { t, recover } = overlay;
  if (recover >= 1) return defaultLeg;

  const sign = side === "left" ? -1 : 1;
  const hip = worldFromLocal(pelvis, yaw, [BONE.hipHalfW * sign, 0, 0]);
  const toBall = norm([ball[0] - pelvis[0], 0, ball[2] - pelvis[2]]);

  let foot;
  if (t < 0.3) {
    const w = t / 0.3;
    foot = add(hip, scale(toBall, -0.22 - w * 0.18));
    foot[1] = GROUND_Y + 0.05 * w;
  } else if (t < 0.55) {
    foot = [
      ball[0] - toBall[0] * 0.1,
      GROUND_Y,
      ball[2] - toBall[2] * 0.1 - 0.06,
    ];
  } else {
    const f = (t - 0.55) / 0.45;
    foot = add(hip, scale(toBall, 0.2 + f * 0.35));
    foot[1] = GROUND_Y;
  }

  const kicked = ikLeg(hip, foot, BONE.thigh, BONE.shin, fwd3);
  if (recover <= 0) return kicked;

  return {
    knee: [
      lerp(kicked.knee[0], defaultLeg.knee[0], recover),
      lerp(kicked.knee[1], defaultLeg.knee[1], recover),
      lerp(kicked.knee[2], defaultLeg.knee[2], recover),
    ],
    foot: [
      lerp(kicked.foot[0], defaultLeg.foot[0], recover),
      lerp(kicked.foot[1], defaultLeg.foot[1], recover),
      lerp(kicked.foot[2], defaultLeg.foot[2], recover),
    ],
  };
}

function ballTrajectory(frame) {
  const p1From = [-10.5, -14];
  const p1To = [-3, 2];
  const p2From = [-2, 6];
  const p2To = [3, 42];
  const goal = [0.4, 51.2];

  // Dribble with h10
  if (frame < 96) {
    const root = samplePath(PLAYER_PATHS.h10, frame);
    const phase = frame * 0.22;
    const side = Math.sin(phase) > 0 ? 1 : -1;
    return vec3(root[0] + 0.35, BALL_R, root[1] + 0.55 + side * 0.12);
  }

  // Pass 1 rolling (after h10 strike)
  if (frame < 160) {
    const t = easeInOut((frame - 100) / 60);
    const x = lerp(p1From[0], p1To[0], t);
    const z = lerp(p1From[1], p1To[1], t);
    const wobble = Math.sin(t * Math.PI * 5) * 0.015 * (1 - t);
    return vec3(x + wobble, BALL_R, z);
  }

  // h9 control / dribble
  if (frame < 210) {
    const root = samplePath(PLAYER_PATHS.h9, frame);
    const phase = (frame - 100) * 0.19;
    const side = Math.sin(phase) > 0 ? 1 : -1;
    return vec3(root[0] + 0.28, BALL_R, root[1] + 0.48 + side * 0.1);
  }

  // Pass 2 rolling
  if (frame < 290) {
    const t = easeInOut((frame - 210) / 80);
    const x = lerp(p2From[0], p2To[0], t);
    const z = lerp(p2From[1], p2To[1], t);
    return vec3(x, BALL_R, z);
  }

  // h7 control + shot setup
  if (frame < 360) {
    const root = samplePath(PLAYER_PATHS.h7, frame);
    return vec3(root[0] + 0.25, BALL_R, root[1] + 0.45);
  }

  // Shot arc
  if (frame < 430) {
    const t = (frame - 360) / 70;
    const x = lerp(p2To[0], goal[0], easeInOut(t));
    const z = lerp(p2To[1], goal[1], easeInOut(t));
    const arc = Math.sin(t * Math.PI) * 2.2;
    return vec3(x, BALL_R + arc, z);
  }

  return vec3(goal[0], BALL_R, goal[1]);
}

function makePose(frame, playerId) {
  const { px, pz, yaw, speed, vx, vz } = getRootMotion(frame, playerId);
  const ball = ballTrajectory(frame);

  const jog = clamp(speed / 5.5, 0, 1);
  const pelvisBob = 0.018 * Math.sin(frame * (0.24 + jog * 0.18) * 2 + playerId.length);
  const lean = clamp(speed / 7, 0, 0.12);

  const pelvis = [px, BONE.pelvisH + pelvisBob, pz];
  const fwd3 = norm([Math.sin(yaw), 0, Math.cos(yaw)]);
  const torso = add(pelvis, scale(fwd3, lean * 4));
  torso[1] = pelvis[1] + BONE.torso * 0.92;
  const chest = add(torso, scale(fwd3, 0.04));

  const lookYaw = playerId === "h1" ? yaw : lerpAngle(yaw, Math.atan2(ball.x - px, ball.z - pz), 0.25);
  const head = add(chest, [0, BONE.neck + BONE.head, 0]);
  head[0] += Math.sin(lookYaw) * 0.06;
  head[2] += Math.cos(lookYaw) * 0.06;

  const ls = add(chest, rotYaw([-BONE.shoulderHalfW, 0.02, 0], yaw));
  const rs = add(chest, rotYaw([BONE.shoulderHalfW, 0.02, 0], yaw));

  const cadence = 1.4 + jog * 1.6;
  const phase = frame * (cadence / FPS) + (playerId.charCodeAt(0) % 7) * 0.31;
  const leftPhase = phase;
  const rightPhase = phase + 0.5;

  let leftLeg = solveLegChain(pelvis, yaw, "left", leftPhase, speed, fwd3);
  let rightLeg = solveLegChain(pelvis, yaw, "right", rightPhase, speed, fwd3);

  const ballPt = [ball.x, ball.y, ball.z];
  const kickL = kickOverlay(playerId, frame, "left");
  const kickR = kickOverlay(playerId, frame, "right");
  if (kickL) leftLeg = applyKick(leftLeg, kickL, pelvis, yaw, "left", fwd3, ballPt);
  if (kickR) rightLeg = applyKick(rightLeg, kickR, pelvis, yaw, "right", fwd3, ballPt);

  // GK ready stance
  if (playerId === "h1" && speed < 0.5) {
    const gPhase = frame * 0.08;
    const spread = 0.38 + Math.sin(gPhase) * 0.04;
    leftLeg = ikLeg(
      worldFromLocal(pelvis, yaw, [-BONE.hipHalfW, 0, 0]),
      worldFromLocal(pelvis, yaw, [-spread * 0.4, GROUND_Y - pelvis[1], spread * 0.15]),
      BONE.thigh,
      BONE.shin,
      fwd3,
    );
    rightLeg = ikLeg(
      worldFromLocal(pelvis, yaw, [BONE.hipHalfW, 0, 0]),
      worldFromLocal(pelvis, yaw, [spread * 0.4, GROUND_Y - pelvis[1], spread * 0.15]),
      BONE.thigh,
      BONE.shin,
      fwd3,
    );
    pelvis[1] -= 0.06;
  }

  const la = armSwing(leftPhase, "left", speed);
  const ra = armSwing(rightPhase, "right", speed);
  if (kickR && playerId !== "h1") {
    ra[0] = 0.35;
    ra[1] = 0.1;
  }
  if (kickL && playerId !== "h1") {
    la[0] = 0.35;
    la[1] = 0.1;
  }

  const le = elbowFromShoulder(ls, yaw, la, BONE.upperArm);
  const re = elbowFromShoulder(rs, yaw, ra, BONE.upperArm);

  return {
    pelvis: vec3(...pelvis),
    torso: vec3(...chest),
    head: vec3(...head),
    leftShoulder: vec3(...ls),
    rightShoulder: vec3(...rs),
    leftElbow: vec3(...le),
    rightElbow: vec3(...re),
    leftKnee: vec3(...leftLeg.knee),
    rightKnee: vec3(...rightLeg.knee),
    leftFoot: vec3(...leftLeg.foot),
    rightFoot: vec3(...rightLeg.foot),
  };
}

const manifest = {
  schemaVersion: "0.2.0",
  matchId: "demo_sequence",
  pitch: { lengthM: 105, widthM: 68 },
  timing: { frameRateHz: FPS, durationSeconds: DURATION_SEC },
  teams: [
    { teamId: "home", name: "Home", color: "#E63946" },
    { teamId: "away", name: "Away", color: "#457B9D" },
  ],
  players: PLAYERS,
};

const events = {
  events: EVENTS.map((e) => ({ ...e, timestampMs: Math.floor((e.frameIndex * 1000) / FPS) })),
};

const frames = [];
for (let frame = 0; frame < TOTAL_FRAMES; frame++) {
  const ball = { pos: ballTrajectory(frame) };
  const players = PLAYERS.map((p) => ({
    playerId: p.playerId,
    joints: makePose(frame, p.playerId),
  }));
  frames.push(JSON.stringify({ frameIndex: frame, timestampMs: Math.floor((frame * 1000) / FPS), ball, players }));
}

for (const outDir of OUTPUT_DIRS) {
  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(path.join(outDir, "manifest.json"), JSON.stringify(manifest, null, 2));
  fs.writeFileSync(path.join(outDir, "events.json"), JSON.stringify(events, null, 2));
  fs.writeFileSync(path.join(outDir, "frames.jsonl"), frames.join("\n") + "\n");
  console.log(`Wrote ${outDir} (${frames.length} frames)`);
}
