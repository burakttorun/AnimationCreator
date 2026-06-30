/** Smooth root (x,z) tracks before joint synthesis. */

const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

function lerpAngle(a, b, t) {
  let d = b - a;
  while (d > Math.PI) d -= 2 * Math.PI;
  while (d < -Math.PI) d += 2 * Math.PI;
  return a + d * t;
}

function movingAverage(arr, radius) {
  const out = new Array(arr.length);
  for (let i = 0; i < arr.length; i++) {
    let sum = 0;
    let n = 0;
    const from = Math.max(0, i - radius);
    const to = Math.min(arr.length - 1, i + radius);
    for (let j = from; j <= to; j++) {
      sum += arr[j];
      n++;
    }
    out[i] = n > 0 ? sum / n : arr[i];
  }
  return out;
}

function smoothAngles(arr, radius) {
  const out = [...arr];
  for (let i = 0; i < arr.length; i++) {
    let sumSin = 0;
    let sumCos = 0;
    let n = 0;
    const from = Math.max(0, i - radius);
    const to = Math.min(arr.length - 1, i + radius);
    for (let j = from; j <= to; j++) {
      sumSin += Math.sin(arr[j]);
      sumCos += Math.cos(arr[j]);
      n++;
    }
    out[i] = n > 0 ? Math.atan2(sumSin / n, sumCos / n) : arr[i];
  }
  return out;
}

function clampStepPerFrame(values, maxStep) {
  if (values.length === 0) return values;
  const out = [values[0]];
  for (let i = 1; i < values.length; i++) {
    const prev = out[i - 1];
    const delta = values[i] - prev;
    out.push(prev + clamp(delta, -maxStep, maxStep));
  }
  return out;
}

/**
 * @param {string[]} playerIds
 * @param {(id: string, frame: number) => { x: number, z: number }} samplePosition
 * @param {number} totalFrames
 * @param {number} fps
 * @param {(id: string) => number} defaultYaw
 */
export function buildSmoothedRootTracks(playerIds, samplePosition, totalFrames, fps, defaultYaw) {
  const raw = {};
  for (const id of playerIds) {
    raw[id] = { x: [], z: [], yaw: [], speed: [], phase: [] };
  }

  const yawState = {};
  const phaseState = {};

  for (let f = 0; f < totalFrames; f++) {
    for (const id of playerIds) {
      const { x, z } = samplePosition(id, f);
      raw[id].x.push(x);
      raw[id].z.push(z);

      let vx = 0;
      let vz = 0;
      let speed = 0;
      if (f > 0) {
        vx = (x - raw[id].x[f - 1]) * fps;
        vz = (z - raw[id].z[f - 1]) * fps;
        speed = Math.hypot(vx, vz);
      }

      let yaw = yawState[id] ?? defaultYaw(id);
      if (speed > 0.06) {
        const target = Math.atan2(vx, vz);
        yaw = lerpAngle(yaw, target, 0.22);
      }
      yawState[id] = yaw;
      raw[id].yaw.push(yaw);
      raw[id].speed.push(speed);

      const phaseStep = speed > 0.06 ? (speed / fps) * (1.55 + speed * 0.1) : 0;
      const phase = (phaseState[id] ?? 0) + phaseStep;
      phaseState[id] = phase;
      raw[id].phase.push(phase);
    }
  }

  const window = Math.max(2, Math.round(fps * 0.14));
  const maxStep = 7.2 / fps;
  const maxSpeedStep = 4.5 / fps;

  for (const id of playerIds) {
    const t = raw[id];
    t.x = movingAverage(t.x, window);
    t.z = movingAverage(t.z, window);
    t.x = clampStepPerFrame(t.x, maxStep);
    t.z = clampStepPerFrame(t.z, maxStep);

    t.speed = [0];
    for (let f = 1; f < totalFrames; f++) {
      const vx = (t.x[f] - t.x[f - 1]) * fps;
      const vz = (t.z[f] - t.z[f - 1]) * fps;
      t.speed.push(Math.hypot(vx, vz));
    }
    t.speed = movingAverage(t.speed, window);
    t.speed = clampStepPerFrame(t.speed, maxSpeedStep);

    t.yaw = smoothAngles(t.yaw, window);
    for (let f = 1; f < t.yaw.length; f++) {
      if (t.speed[f] < 0.08) {
        t.yaw[f] = t.yaw[f - 1];
      }
    }

    t.phase = [0];
    for (let f = 1; f < totalFrames; f++) {
      const spd = t.speed[f];
      const step = spd > 0.06 ? (spd / fps) * (1.55 + spd * 0.1) : 0;
      t.phase.push(t.phase[f - 1] + step);
    }
  }

  return raw;
}

/** Minimal smoothing for faithful SoccerNet root replay. */
export function buildReplayRootTracks(playerIds, samplePosition, totalFrames, fps) {
  const raw = {};
  for (const id of playerIds) {
    raw[id] = { x: [], z: [], yaw: [], speed: [], phase: [] };
    let prev = null;
    let yaw = 0;
    for (let f = 0; f < totalFrames; f++) {
      const { x, z } = samplePosition(id, f);
      raw[id].x.push(x);
      raw[id].z.push(z);
      let speed = 0;
      if (prev) {
        const vx = (x - prev.x) * fps;
        const vz = (z - prev.z) * fps;
        speed = Math.hypot(vx, vz);
        if (speed > 0.2) yaw = Math.atan2(vx, vz);
      }
      prev = { x, z };
      raw[id].yaw.push(yaw);
      raw[id].speed.push(speed);
      raw[id].phase.push(0);
    }
    raw[id].x = movingAverage(raw[id].x, 1);
    raw[id].z = movingAverage(raw[id].z, 1);
    raw[id].yaw = smoothAngles(raw[id].yaw, 2);
  }
  return raw;
}

export function smoothBallTrack(positions, fps, opts = {}) {
  const window = opts.window ?? Math.max(2, Math.round(fps * 0.1));
  const maxStep = opts.maxStep ?? 14 / fps;
  const out = positions.map((p) => ({ ...p }));

  for (const axis of ["x", "y", "z"]) {
    const arr = out.map((p) => p[axis]);
    const smoothed = clampStepPerFrame(movingAverage(arr, window), axis === "y" ? maxStep * 1.4 : maxStep);
    for (let i = 0; i < out.length; i++) out[i][axis] = +smoothed[i].toFixed(3);
  }

  return out;
}

/** Faithful ball replay — outlier reject + gap fill only, no speed clamp. */
export function buildReplayBallTrack(positions, fps, opts = {}) {
  const window = opts.window ?? 0;
  const out = positions.map((p) => ({ ...p }));
  if (window <= 0) {
    return out.map((p) => ({
      x: +p.x.toFixed(3),
      y: +p.y.toFixed(3),
      z: +p.z.toFixed(3),
    }));
  }

  for (const axis of ["x", "y", "z"]) {
    const arr = out.map((p) => p[axis]);
    const smoothed = movingAverage(arr, window);
    for (let i = 0; i < out.length; i++) out[i][axis] = +smoothed[i].toFixed(3);
  }
  return out;
}
