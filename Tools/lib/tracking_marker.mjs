/** Upright marker joints from tracked root — no gait synthesis. */

import { GROUND_Y } from "./pose_from_root.mjs";

const vec3 = (x, y, z) => ({ x: +x.toFixed(3), y: +y.toFixed(3), z: +z.toFixed(3) });

function rotYaw([x, y, z], yaw) {
  const c = Math.cos(yaw);
  const s = Math.sin(yaw);
  return [x * c + z * s, y, -x * s + z * c];
}

export function buildTrackingMarkerJoints(px, pz, yaw, role = "player") {
  const bodyH = role === "goalkeeper" ? 1.88 : 1.82;
  const pelvisY = GROUND_Y + bodyH * 0.48;
  const pelvis = [px, pelvisY, pz];
  const torso = [px, pelvisY + bodyH * 0.28, pz];
  const head = [px, pelvisY + bodyH * 0.52, pz];
  const hipW = 0.22;
  const footSpread = 0.14;

  const lFoot = rotYaw([-footSpread, GROUND_Y - pelvisY, 0.04], yaw);
  const rFoot = rotYaw([footSpread, GROUND_Y - pelvisY, 0.04], yaw);
  const lKnee = rotYaw([-footSpread * 0.7, GROUND_Y + 0.42 - pelvisY, 0.02], yaw);
  const rKnee = rotYaw([footSpread * 0.7, GROUND_Y + 0.42 - pelvisY, 0.02], yaw);
  const lSh = rotYaw([-hipW - 0.08, bodyH * 0.22, 0], yaw);
  const rSh = rotYaw([hipW + 0.08, bodyH * 0.22, 0], yaw);
  const lEl = rotYaw([-hipW - 0.1, bodyH * 0.08, 0.06], yaw);
  const rEl = rotYaw([hipW + 0.1, bodyH * 0.08, 0.06], yaw);

  const add = (base, local) => [base[0] + local[0], base[1] + local[1], base[2] + local[2]];

  return {
    pelvis: vec3(...pelvis),
    torso: vec3(...torso),
    head: vec3(...head),
    leftShoulder: vec3(...add(pelvis, lSh)),
    rightShoulder: vec3(...add(pelvis, rSh)),
    leftElbow: vec3(...add(pelvis, lEl)),
    rightElbow: vec3(...add(pelvis, rEl)),
    leftKnee: vec3(...add(pelvis, lKnee)),
    rightKnee: vec3(...add(pelvis, rKnee)),
    leftFoot: vec3(...add(pelvis, lFoot)),
    rightFoot: vec3(...add(pelvis, rFoot)),
  };
}
