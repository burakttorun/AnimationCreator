import fs from "fs";
import { pitchToFrds } from "./lib/pose_from_root.mjs";

const PITCH_HALF_W = 34;
const PITCH_HALF_L = 52.5;
const FPS = 25;

const data = JSON.parse(
  fs.readFileSync("ExternalData/SoccerNetGS/test/SNGS-116/Labels-GameState.json", "utf8"),
);
const anns = data.annotations.filter((a) => a.attributes?.role === "ball");
function fi(id) {
  return parseInt(String(id).slice(-4), 10) - 1;
}

const byFrame = new Map();
for (const b of anns) {
  const f = fi(b.image_id);
  const bp = b.bbox_pitch;
  const fr = pitchToFrds(bp.x_bottom_middle, bp.y_bottom_middle);
  byFrame.set(f, fr);
}

function inPitch(p, margin = 1.5) {
  return Math.abs(p.x) <= PITCH_HALF_W + margin && Math.abs(p.z) <= PITCH_HALF_L + margin;
}

let best = null;
const window = 200;
for (let start = 0; start <= 750 - window; start += 25) {
  let valid = 0;
  let maxJump = 0;
  let prev = null;
  for (let f = start; f < start + window; f++) {
    const p = byFrame.get(f);
    if (!p || !inPitch(p)) continue;
    valid++;
    if (prev) maxJump = Math.max(maxJump, Math.hypot(p.x - prev.x, p.z - prev.z));
    prev = p;
  }
  const score = valid - maxJump * 2;
  if (!best || score > best.score) {
    const mid = byFrame.get(start + window / 2);
    best = { start, end: start + window - 1, valid, maxJump, score, mid };
  }
}

console.log("best 8s window", best);

// show ball path for current clip 50-249
let outliers = 0;
prev = null;
for (let f = 50; f <= 249; f++) {
  const p = byFrame.get(f);
  if (!p) continue;
  if (!inPitch(p, 0)) outliers++;
  if (prev && Math.hypot(p.x - prev.x, p.z - prev.z) > 4) {
    console.log("big jump at frame", f, "from", prev, "to", p);
  }
  prev = p;
}
console.log("out of pitch frames in 50-249:", outliers, "/ 200");
