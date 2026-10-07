const M = JSON.parse(document.getElementById("messages").textContent);
const $ = (id) => document.getElementById(id);
const fill = (text, values) => text.replace(/\{(\w+)\}/g, (_, key) => values[key]);
const say = (text) => { $("status").textContent = text; };
const MAX_SECONDS = 10;
const SAMPLE_RATE = 22050;
const FRAME_PERIOD = 5;

if (!("DecompressionStream" in window) || !("Worker" in window)) {
  say(M.unsupported);
  throw new Error(M.unsupported);
}

const worker = new Worker(new URL("worker.js", import.meta.url), { type: "module" });
const pending = new Map();
let nextId = 0;
let info = null;
let f0 = [];
let analysisMilliseconds = [];
let estimators = null;

const CONTROLS = ["pitch", "speed", "formant", "breath", "mode", "reset", "dimensions", "frame", "compare", "file"];
const enable = (flag) => { for (const id of CONTROLS) $(id).disabled = !flag; };

const call = (name, ...args) => new Promise((resolve, reject) => {
  const id = nextId++;
  pending.set(id, { resolve, reject });
  worker.postMessage({ id, name, args });
});

worker.onmessage = (event) => {
  const data = event.data;
  if (data.progress) {
    const [loaded, total] = data.progress;
    const megabytes = (value) => (value / 1048576).toFixed(1);
    say(total > 0 ? fill(M.loadingOf, { loaded: megabytes(loaded), total: megabytes(total) }) : fill(M.loading, { loaded: megabytes(loaded) }));
  } else if (data.ready) {
    start();
  } else if (data.failed) {
    say(M.fail + data.failed);
  } else if ("id" in data) {
    const entry = pending.get(data.id);
    pending.delete(data.id);
    if (data.error) entry.reject(new Error(data.error));
    else entry.resolve(data);
  }
};

const fail = (error) => say(M.fail + error.message);

const latest = (task) => {
  let running = false;
  let again = false;
  return async () => {
    if (running) { again = true; return; }
    running = true;
    try {
      do { again = false; await task(); } while (again);
    } catch (error) {
      fail(error);
    } finally {
      running = false;
    }
  };
};

const setAudio = (id, bytes) => {
  const audio = $(id);
  if (audio.dataset.url) URL.revokeObjectURL(audio.dataset.url);
  audio.dataset.url = URL.createObjectURL(new Blob([bytes], { type: "audio/wav" }));
  audio.src = audio.dataset.url;
  audio.hidden = false;
};

const chart = (target, series, xEnd, xUnit, yUnit) => {
  const w = 600, h = 180, pad = 16;
  const finite = series.flatMap((s) => s.values.filter(Number.isFinite));
  if (finite.length === 0) { $(target).textContent = ""; return; }
  const lo = Math.min(...finite), hi = Math.max(...finite);
  const X = (i, n) => pad + (i / Math.max(n - 1, 1)) * (w - 2 * pad);
  const Y = (v) => h - pad - ((v - lo) / ((hi - lo) || 1)) * (h - 2 * pad);
  let body = "";
  series.forEach((s, index) => {
    let d = "", pen = false;
    s.values.forEach((v, i) => {
      if (Number.isFinite(v)) { d += (pen ? "L" : "M") + X(i, s.values.length).toFixed(1) + " " + Y(v).toFixed(1); pen = true; } else { pen = false; }
    });
    const y = 12 + index * 13;
    body += '<path d="' + d + '" fill="none" stroke="currentColor" stroke-width="1.5" stroke-dasharray="' + s.dash + '"/>'
      + '<line x1="' + (w - 150) + '" x2="' + (w - 122) + '" y1="' + (y - 4) + '" y2="' + (y - 4) + '" stroke="currentColor" stroke-width="1.5" stroke-dasharray="' + s.dash + '"/>'
      + '<text x="' + (w - 118) + '" y="' + y + '">' + s.label + "</text>";
  });
  $(target).innerHTML = '<svg viewBox="0 0 ' + w + " " + h + '" role="img" aria-label="' + M.chart + '" font-size="11" fill="currentColor">'
    + '<rect x="0.5" y="0.5" width="' + (w - 1) + '" height="' + (h - 1) + '" fill="none" stroke="currentColor" opacity="0.4"/>'
    + body
    + '<text x="4" y="' + (h - 4) + '">' + lo.toFixed(0) + " " + yUnit + "</text>"
    + '<text x="4" y="' + (h - 18) + '">' + hi.toFixed(0) + " " + yUnit + "</text>"
    + '<text x="' + (w - 4) + '" y="' + (h - 4) + '" text-anchor="end">' + xEnd.toFixed(1) + " " + xUnit + "</text></svg>";
};

const voicedOnly = (values) => Array.from(values, (v) => (v > 0 ? v : NaN));
const DASHES = ["", "6 3", "2 3"];

const drawF0 = () => {
  const series = [{ values: voicedOnly(f0), dash: DASHES[0], label: "Harvest" }];
  if (estimators) {
    series.push({ values: voicedOnly(estimators.dio), dash: DASHES[1], label: "Dio" });
    series.push({ values: voicedOnly(estimators.stone), dash: DASHES[2], label: "Dio + StoneMask" });
  }
  chart("f0chart", series, f0.length * FRAME_PERIOD / 1000, M.secs, M.hz);
};

const convert = latest(async () => {
  const { result, milliseconds } = await call("Convert",
    Number($("pitch").value), Number($("speed").value), Number($("formant").value), Number($("breath").value), Number($("mode").value));
  setAudio("converted", result);
  $("convertNote").textContent = fill(M.synthesized, { t: milliseconds.toFixed(0) });
});

const drawEnvelope = async () => {
  const frame = Number($("frame").value);
  const original = (await call("GetEnvelope", frame, 0)).result;
  const decoded = (await call("GetEnvelope", frame, 1)).result;
  const db = (values) => Array.from(values, (v) => 10 * Math.log10(Math.max(v, 1e-30)));
  chart("envelope", [{ values: db(original), dash: DASHES[0], label: M.original }, { values: db(decoded), dash: DASHES[1], label: M.decoded }], info.fs / 2000, M.khz, M.db);
};

const code = latest(async () => {
  const dimensions = Number($("dimensions").value);
  const { result, milliseconds } = await call("ConvertCoded", dimensions);
  setAudio("coded", result);
  $("codedNote").textContent = fill(M.coded, { bins: info.spectrumLength, dims: dimensions, t: milliseconds.toFixed(0) });
  await drawEnvelope();
});

const compare = latest(async () => {
  $("compare").disabled = true;
  say(M.running);
  const dio = await call("EstimateF0", 0);
  const stone = await call("EstimateF0", 1);
  estimators = { dio: dio.result, stone: stone.result };
  drawF0();
  $("estimators").textContent = [["Harvest", analysisMilliseconds[0]], ["Dio", dio.milliseconds], ["Dio + StoneMask", stone.milliseconds]].map(([name, ms]) => name + " " + ms.toFixed(0) + " " + M.ms + "\n").join("");
  say(M.ready);
  $("compare").disabled = false;
});

const showAnalysis = () => {
  const [harvest, cheapTrick, d4c] = analysisMilliseconds;
  $("analysis").textContent = fill(M.analysis, { fs: info.fs, seconds: (info.samples / info.fs).toFixed(1), harvest: harvest.toFixed(0), cheapTrick: cheapTrick.toFixed(0), d4c: d4c.toFixed(0) });
};

const afterAnalysis = async () => {
  f0 = (await call("GetF0")).result;
  const voiced = [];
  f0.forEach((v, i) => { if (v > 0) voiced.push(i); });
  const frame = voiced.length > 0 ? voiced[Math.floor(voiced.length / 2)] : 0;
  $("frame").max = String(Math.max(f0.length - 1, 0));
  $("frame").value = String(frame);
  estimators = null;
  $("estimators").textContent = "";
  showAnalysis();
  drawF0();
  await convert();
  await code();
  enable(true);
  say(M.ready);
};

const analyzeSample = async () => {
  const bytes = new Uint8Array(await (await fetch($("original").getAttribute("src"))).arrayBuffer());
  say(M.analyzing);
  const response = await call("AnalyzeWave", bytes);
  analysisMilliseconds = response.result;
  info = (await call("GetInfo")).result;
  info = { fs: info[0], frames: info[1], spectrumLength: info[2], fftSize: info[3], samples: info[4] };
  await afterAnalysis();
};

const decodeFile = async (file) => {
  let context;
  try { context = new AudioContext({ sampleRate: SAMPLE_RATE }); } catch { context = new AudioContext(); }
  try {
    const buffer = await context.decodeAudioData(await file.arrayBuffer());
    const length = Math.min(buffer.length, Math.floor(buffer.sampleRate * MAX_SECONDS));
    const mono = new Float64Array(length);
    for (let c = 0; c < buffer.numberOfChannels; c++) {
      const channel = buffer.getChannelData(c);
      for (let i = 0; i < length; i++) mono[i] += channel[i] / buffer.numberOfChannels;
    }
    return { mono, fs: buffer.sampleRate, truncated: buffer.length > length };
  } finally {
    context.close();
  }
};

const analyzeFile = async (file) => {
  enable(false);
  try {
    say(M.decoding);
    const decoded = await decodeFile(file);
    const original = $("original");
    if (original.dataset.url) URL.revokeObjectURL(original.dataset.url);
    original.dataset.url = URL.createObjectURL(file);
    original.src = original.dataset.url;
    $("sampleNote").hidden = true;
    say(M.analyzing);
    const response = await call("AnalyzeSamples", new Uint8Array(decoded.mono.buffer), decoded.fs);
    analysisMilliseconds = response.result;
    const values = (await call("GetInfo")).result;
    info = { fs: values[0], frames: values[1], spectrumLength: values[2], fftSize: values[3], samples: values[4] };
    await afterAnalysis();
    if (decoded.truncated) say(fill(M.truncated, { s: MAX_SECONDS }));
  } catch (error) {
    fail(error);
    enable(info !== null);
    $("file").disabled = false;
  }
};

const start = () => {
  analyzeSample().catch(fail);
};

const bind = (id, output, format) => {
  const input = $(id);
  const show = () => { $(output).textContent = format(Number(input.value)); };
  show();
  input.addEventListener("input", show);
};

bind("pitch", "pitchOut", (v) => (v > 0 ? "+" : "") + v);
bind("speed", "speedOut", (v) => "×" + v.toFixed(2));
bind("formant", "formantOut", (v) => "×" + v.toFixed(2));
bind("breath", "breathOut", (v) => "×" + v.toFixed(1));
bind("dimensions", "dimensionsOut", (v) => String(v));

for (const id of ["pitch", "speed", "formant", "breath", "mode"]) $(id).addEventListener("input", convert);
$("dimensions").addEventListener("input", code);
$("frame").addEventListener("input", () => drawEnvelope().catch(fail));
$("compare").addEventListener("click", compare);
$("file").addEventListener("change", () => { const file = $("file").files[0]; if (file) analyzeFile(file); });
$("reset").addEventListener("click", () => {
  $("pitch").value = "0"; $("speed").value = "1"; $("formant").value = "1"; $("breath").value = "1"; $("mode").value = "0";
  for (const id of ["pitch", "speed", "formant", "breath"]) $(id).dispatchEvent(new Event("input"));
});

say(fill(M.loading, { loaded: "0.0" }));
