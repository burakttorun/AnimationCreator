/** Synthesize FRDS joints from root (x,z) tracking + heading. */



export const BONE = {

  pelvisH: 0.92,

  thigh: 0.42,

  shin: 0.4,

  torso: 0.52,

  neck: 0.14,

  head: 0.14,

  shoulderHalfW: 0.22,

  upperArm: 0.28,

  hipHalfW: 0.14,

};



export const GROUND_Y = 0.04;



const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

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

const lerp = (a, b, t) => a + (b - a) * t;



function rotYaw([x, y, z], yaw) {

  const c = Math.cos(yaw);

  const s = Math.sin(yaw);

  return [x * c + z * s, y, -x * s + z * c];

}



function worldFromLocal(root, yaw, local) {

  const [wx, wy, wz] = rotYaw(local, yaw);

  return [root[0] + wx, root[1] + wy, root[2] + wz];

}



function clampReach(from, to, maxDist) {

  const d = sub(to, from);

  const l = len(d);

  if (l <= maxDist) return [...to];

  return add(from, scale(norm(d), maxDist * 0.98));

}



function ikLeg(hip, foot, thighLen, shinLen, fwd) {

  const maxReach = thighLen + shinLen - 0.03;

  foot = clampReach(hip, foot, maxReach);

  const d = sub(foot, hip);

  let dist = len(d);

  dist = clamp(dist, 0.08, thighLen + shinLen - 0.02);

  const dir = norm(d);

  const a = (thighLen * thighLen - shinLen * shinLen + dist * dist) / (2 * dist);

  const h = Math.sqrt(Math.max(0, thighLen * thighLen - a * a));

  const mid = add(hip, scale(dir, a));

  let perp = cross(dir, [0, 1, 0]);

  if (len(perp) < 0.05) perp = cross(dir, fwd);

  perp = norm(perp);

  return { knee: add(mid, scale(perp, h)), foot: [...foot] };

}



function idleFootLocal(side) {
  const sign = side === "left" ? -1 : 1;
  return { fwd: sign * 0.1, y: GROUND_Y };
}



function gaitFootLocal(legPhase, speed, side) {
  if (speed < 0.18) {
    return idleFootLocal(side);
  }



  const jog = clamp(speed / 5.5, 0, 1);

  const stride = 0.22 + jog * 0.34;

  const ph = legPhase % 1;

  const stanceEnd = 0.58 - (1 - jog) * 0.06;

  const lift = 0.04 + jog * 0.12;



  if (ph < stanceEnd) {

    const t = ph / stanceEnd;

    const fwd = lerp(stride * 0.38, -stride * 0.36, t * t * (3 - 2 * t));

    return { fwd, y: GROUND_Y + (jog > 0.3 ? 0.006 * Math.sin(t * Math.PI) : 0) };

  }



  const t = (ph - stanceEnd) / (1 - stanceEnd);

  const fwd = lerp(-stride * 0.36, stride * 0.42, t * t);

  return { fwd, y: GROUND_Y + lift * Math.sin(t * Math.PI) };

}



function solveLegChain(pelvis, yaw, side, legPhase, speed, fwd3) {

  const sign = side === "left" ? -1 : 1;

  const hip = worldFromLocal(pelvis, yaw, [BONE.hipHalfW * sign, 0, 0]);

  const localFoot = gaitFootLocal(legPhase, speed, side);

  const foot = worldFromLocal(pelvis, yaw, [localFoot.fwd, localFoot.y - pelvis[1], 0]);

  return ikLeg(hip, foot, BONE.thigh, BONE.shin, fwd3);

}



function armSwing(phase, side, speed) {

  const sign = side === "left" ? -1 : 1;

  const amp = speed < 0.18 ? 0.02 : 0.1 + clamp(speed / 6, 0, 1) * 0.18;

  const fwd = Math.sin(phase + (side === "left" ? Math.PI : 0)) * amp;

  return [fwd, -0.08 - clamp(speed / 8, 0, 1) * 0.06, sign * 0.07];

}



function elbowFromShoulder(shoulder, yaw, swing, upperLen) {

  return add(shoulder, scale(norm(rotYaw(swing, yaw)), upperLen));

}



export function buildJointsFromRoot(px, pz, yaw, speed, phase, role = "player") {

  const jog = clamp(speed / 5.5, 0, 1);

  const pelvisBob = speed > 0.15 ? 0.012 * Math.sin(phase * 2 * Math.PI) : 0.004 * Math.sin(phase * 0.5);

  const pelvis = [px, BONE.pelvisH + pelvisBob, pz];

  const fwd3 = norm([Math.sin(yaw), 0, Math.cos(yaw)]);

  const lean = clamp(speed / 7, 0, 0.1);

  const torso = add(pelvis, [fwd3[0] * lean * 4, BONE.torso * 0.92, fwd3[2] * lean * 4]);

  const chest = add(torso, scale(fwd3, 0.04));

  const head = add(chest, [0, BONE.neck + BONE.head, 0]);



  const ls = add(chest, rotYaw([-BONE.shoulderHalfW, 0.02, 0], yaw));

  const rs = add(chest, rotYaw([BONE.shoulderHalfW, 0.02, 0], yaw));



  const leftPhase = phase;

  const rightPhase = phase + 0.5;



  let leftLeg = solveLegChain(pelvis, yaw, "left", leftPhase, speed, fwd3);

  let rightLeg = solveLegChain(pelvis, yaw, "right", rightPhase, speed, fwd3);



  if (role === "goalkeeper" && speed < 0.4) {

    const spread = 0.36;

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

    pelvis[1] -= 0.05;

  }



  const le = elbowFromShoulder(ls, yaw, armSwing(leftPhase, "left", speed), BONE.upperArm);

  const re = elbowFromShoulder(rs, yaw, armSwing(rightPhase, "right", speed), BONE.upperArm);



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



/** Blend pass/kick pose into joints without snapping. kickT: 0..1 */

export function applyKickBlend(joints, yaw, kickT, foot = "right") {

  if (kickT <= 0) return joints;



  const pelvis = joints.pelvis;

  const px = pelvis.x;

  const py = pelvis.y;

  const pz = pelvis.z;

  const fwd = [Math.sin(yaw), 0, Math.cos(yaw)];

  const side = foot === "left" ? -1 : 1;



  const swing = kickT * 0.55;

  const footLocal = [

    fwd[0] * (0.25 + swing * 0.55) + side * 0.08,

    GROUND_Y - py + swing * 0.12,

    fwd[2] * (0.25 + swing * 0.55),

  ];

  const footWorld = [px + footLocal[0], py + footLocal[1], pz + footLocal[2]];



  const hip = [px + side * BONE.hipHalfW * Math.cos(yaw), py, pz - side * BONE.hipHalfW * Math.sin(yaw)];

  const kneeMid = [

    lerp(hip[0], footWorld[0], 0.52),

    lerp(hip[1], footWorld[1], 0.35) + 0.08,

    lerp(hip[2], footWorld[2], 0.52),

  ];



  const out = { ...joints };

  if (foot === "right") {

    out.rightFoot = vec3(...footWorld);

    out.rightKnee = vec3(...kneeMid);

  } else {

    out.leftFoot = vec3(...footWorld);

    out.leftKnee = vec3(...kneeMid);

  }

  return out;

}



/** SoccerNet GSR pitch → FRDS. GSR origin is pitch center: x = length (±52.5), y = width (±34). */
export function pitchToFrds(gsrLengthX, gsrWidthY) {
  return { x: gsrWidthY, z: gsrLengthX };
}

