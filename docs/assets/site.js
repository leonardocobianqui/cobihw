// cobihw.com script, shared by the Portuguese (/) and English (/en/) pages; the page language picks the texts below.
const REPO = "leonardocobianqui/cobihw";
// .NET Desktop Runtime linked by the compact package; update it with each .NET 10 release (and the href in the HTML).
const RUNTIME_VERSION = "10.0.13";
const WEB3FORMS_KEY = "68625da7-95d8-439f-a9e4-ae8d3cbd074d";
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

// Texts written by the script, per page language. The email sender name tells which page the message came from.
const TEXT = {
  pt: {
    locale: "pt-BR",
    disk: "Disco",
    network: "Rede",
    session: "Sessão",
    test: "Teste",
    pickOne: "Marque pelo menos um item.",
    files: ["atualizacao-do-jogo.pak", "aula-completa.mp4", "fotos-da-viagem.zip"],
    exporting: "Exportando em 4K",
    minutesLeft: n => `restam ${n} min`,
    frame: "Quadro",
    of: "de",
    lap: "Volta 2/3",
    noReleases: "Ainda não há versões publicadas. A primeira será a 1.0.0.",
    noNotes: "Sem notas para esta versão.",
    version: v => ` Versão ${v}.`,
    currentVersion: v => `Versão atual: ${v}`,
    releasesFailed: url => `Não foi possível carregar as versões agora. Veja na <a href="${url}" target="_blank" rel="noopener">página de versões no GitHub</a>.`,
    formInvalid: "Preencha nome, um e-mail válido e a mensagem.",
    sending: "Enviando…",
    send: "Enviar mensagem",
    sent: "Mensagem enviada. A resposta chega no seu e-mail.",
    sendFailed: "Não foi possível enviar agora. Tente de novo ou escreva para dev@cobihw.com.",
    fromName: "Site cobihw.com",
  },
  en: {
    locale: "en-US",
    disk: "Disk",
    network: "Network",
    session: "Session",
    test: "Test",
    pickOne: "Turn on at least one item.",
    files: ["game-update.pak", "full-lecture.mp4", "trip-photos.zip"],
    exporting: "Exporting in 4K",
    minutesLeft: n => `${n} min left`,
    frame: "Frame",
    of: "of",
    lap: "Lap 2/3",
    noReleases: "No versions published yet.",
    noNotes: "No notes for this version.",
    version: v => ` Version ${v}.`,
    currentVersion: v => `Current version: ${v}`,
    releasesFailed: url => `Couldn't load the versions right now. See the <a href="${url}" target="_blank" rel="noopener">releases page on GitHub</a>.`,
    formInvalid: "Fill in your name, a valid email address and the message.",
    sending: "Sending…",
    send: "Send message",
    sent: "Message sent. The answer will arrive at your email.",
    sendFailed: "Couldn't send it right now. Try again or write to dev@cobihw.com.",
    fromName: "Site cobihw.com (EN)",
  },
};
const T = TEXT[document.documentElement.lang.startsWith("pt") ? "pt" : "en"];

// Pages on other sites open in a new tab, so the visitor doesn't leave cobihw.com; file downloads stay in the same tab.
const openInNewTab = link => { link.target = "_blank"; link.rel = "noopener"; };

const fmt = (n, d) => n.toLocaleString(T.locale, { minimumFractionDigits: d, maximumFractionDigits: d });
const fmtMax2 = n => n.toLocaleString(T.locale, { maximumFractionDigits: 2 });
const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
const size = mb => mb >= 1024 ? `${fmtMax2(mb / 1024)} GB` : `${fmtMax2(mb)} MB`;

const toggle = document.querySelector(".menu-toggle");
const links = document.getElementById("nav-links");
toggle.addEventListener("click", () => {
  const open = links.classList.toggle("open");
  toggle.setAttribute("aria-expanded", String(open));
});
links.addEventListener("click", e => {
  if (e.target.closest("a")) { links.classList.remove("open"); toggle.setAttribute("aria-expanded", "false"); }
});
// Align the actual heading, rather than the section's outer padding.
const navigation = document.querySelector(".nav");
const updateNavigationHeight = () => document.documentElement.style.setProperty("--nav-h", `${navigation.getBoundingClientRect().height}px`);
updateNavigationHeight();
new ResizeObserver(updateNavigationHeight).observe(navigation);
// Sections of the menu, in page order; the first one (the hero) scrolls to the very top.
const sections = [...document.querySelectorAll("main > section[id]")];
function navigateToSection(hash, behavior = reduceMotion ? "instant" : "smooth") {
  const target = document.getElementById(hash.slice(1));
  if (!target) return;
  const heading = target.matches(".section") ? target.querySelector("h2") : target;
  const top = target === sections[0] ? 0 : heading.getBoundingClientRect().top + window.scrollY - navigation.getBoundingClientRect().height - 24;
  window.scrollTo({ top: Math.max(0, top), behavior });
}
document.addEventListener("click", event => {
  const anchor = event.target.closest('a[href^="#"]');
  if (!anchor || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
  const hash = anchor.getAttribute("href");
  if (!document.getElementById(hash.slice(1))) return;
  event.preventDefault();
  links.classList.remove("open");
  toggle.setAttribute("aria-expanded", "false");
  if (location.hash !== hash) history.pushState(null, "", hash);
  navigateToSection(hash);
});
window.addEventListener("hashchange", () => navigateToSection(location.hash, "instant"));
window.addEventListener("load", () => {
  if (location.hash) navigateToSection(location.hash, "instant");
});

const navObserver = new IntersectionObserver(entries => {
  entries.forEach(entry => {
    if (!entry.isIntersecting) return;
    links.querySelectorAll("a").forEach(a => a.setAttribute("aria-current", String(a.getAttribute("href") === "#" + entry.target.id)));
  });
}, { rootMargin: "-45% 0px -50% 0px" });
sections.forEach(section => navObserver.observe(section));

const SCENARIOS = {
  idle: { cpu: 5, gpu: 2, ram: 38, disk: 1, down: 0.05, up: 0.03, ping: 18, temp: 40 },
  game: { cpu: 55, gpu: 96, ram: 64, disk: 5, down: 2.5, up: 1.2, ping: 32, temp: 71 },
  render: { cpu: 98, gpu: 62, ram: 78, disk: 38, down: 0.1, up: 0.05, ping: 19, temp: 63 },
  download: { cpu: 12, gpu: 3, ram: 42, disk: 74, down: 420, up: 4, ping: 55, temp: 42 },
  speed: { cpu: 18, gpu: 4, ram: 40, disk: 3, down: 0.1, up: 0.05, ping: 24, temp: 41 },
};
const S = { ...SCENARIOS.idle, ghz: 1.9, sessionDown: 1980, sessionUp: 258, fps: null };
let scenario = "idle";
const speedTest = { stage: null, left: 0, value: 0 };

function tick() {
  const t = SCENARIOS[scenario];
  const toward = (k, noise, lo, hi) => { S[k] = clamp(S[k] + (t[k] - S[k]) * 0.35 + (Math.random() * 2 - 1) * noise, lo, hi); };
  toward("cpu", 5, 1, 100); toward("gpu", 6, 0, 100); toward("ram", 1, 20, 95); toward("disk", 4, 0, 100);
  toward("down", Math.max(0.02, t.down * 0.12), 0.01, 950); toward("up", Math.max(0.02, t.up * 0.15), 0.01, 90);
  toward("ping", 3, 8, 300); toward("temp", 1.2, 30, 90);
  S.ghz = clamp(1.8 + S.cpu / 40, 1.8, 4.4);
  if (scenario === "speed") stepSpeedTest();
  S.sessionDown += S.down / 8; S.sessionUp += S.up / 8;
}

function stepSpeedTest() {
  const order = [["latency", 2], ["down", 5], ["up", 4], ["pause", 3]];
  if (speedTest.left <= 0) {
    const i = order.findIndex(o => o[0] === speedTest.stage);
    const [stage, secs] = order[(i + 1) % order.length];
    speedTest.stage = stage; speedTest.left = secs;
  }
  if (speedTest.stage === "latency") speedTest.value = 10 + Math.random() * 6;
  if (speedTest.stage === "down") { speedTest.value = 250 + Math.random() * 90; S.down = speedTest.value; }
  if (speedTest.stage === "up") { speedTest.value = 32 + Math.random() * 12; S.up = speedTest.value; }
  speedTest.left -= 1;
}

const O = { theme: "dark", layout: "full", mode: "top", colors: true, transparency: 0, scale: 100, radius: 8, snap: true, lock: false, metrics: { cpu: true, gpu: true, ram: true, disk: true, net: true } };
const THEMES = {
  dark: { bg: "#1E1F22", text: "#E6E6E6", ok: "#6FD68A", warn: "#F0C44C", crit: "#FF6B63" },
  light: { bg: "#F3F4F6", text: "#1B1B1B", ok: "#1E8E3E", warn: "#B06000", crit: "#C5221F" },
};
function withAlpha(hex, a) {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16));
  return `rgba(${r}, ${g}, ${b}, ${a})`;
}

const widget = document.getElementById("widget"), screenEl = document.getElementById("screen"), desk = document.getElementById("desk");

function level(p) {
  if (!O.colors) return "";
  const v = Math.round(p);
  return v >= 75 ? "lv-crit" : v >= 50 ? "lv-warn" : "lv-ok";
}

function liveTestText() {
  if (speedTest.stage === "latency") return `${T.test} ${fmt(speedTest.value, 0)} ms`;
  if (speedTest.stage === "down") return `${T.test} ↓ ${fmt(speedTest.value, 1)} Mbps`;
  if (speedTest.stage === "up") return `${T.test} ↑ ${fmt(speedTest.value, 1)} Mbps`;
  return null;
}

function widgetHTML() {
  const m = O.metrics, live = scenario === "speed" ? liveTestText() : null, parts = [];
  const cpuPct = `${Math.round(S.cpu)}%`, ghz = `${fmt(S.ghz, 2)} GHz`;
  const gpuMem = `${fmt(0.8 + S.gpu / 9, 1)} / ${fmt(12, 1)} GB`, temp = `${Math.round(S.temp)} °C`;
  const ramAmt = `${fmt(S.ram * 0.16, 1)} / ${fmt(16, 1)} GB`;
  const down = `↓ ${fmt(S.down, 2)} Mbps`, up = `↑ ${fmt(S.up, 2)} Mbps`, ping = `Ping ${Math.round(S.ping)} ms`;
  if (O.layout === "full") {
    const sec = (label, name, p, details) => `<div><span class="w-label">${label}</span>${name}</div><div class="w-row ${level(p)}"><div class="w-bar"><div class="w-fill" style="width:${p.toFixed(1)}%"></div></div><span class="w-pct">${Math.round(p)}%</span>${details.map(d => `<span class="w-detail">${d}</span>`).join("")}</div>`;
    if (S.fps !== null) parts.push(`<div class="w-row" style="margin-top:0"><span class="w-label">FPS</span>${S.fps}</div>`);
    if (m.cpu) parts.push(sec("CPU", "Intel Core i5-12400F", S.cpu, [ghz]));
    if (m.gpu) parts.push(sec("GPU", "GeForce RTX 3060", S.gpu, [gpuMem, temp]));
    if (m.ram) parts.push(sec("RAM", "", S.ram, [ramAmt]));
    if (m.disk) parts.push(sec(T.disk, "0 (C:)", S.disk, []));
    if (m.net) parts.push(`<div class="w-label">${T.network}</div><div class="w-line">${live ? live : `${down}<span class="w-gap">${up}</span>`}<span class="w-gap">${ping}</span></div><div class="w-line" style="margin-top:4px">${T.session}&nbsp;&nbsp; ↓ ${size(S.sessionDown)}&nbsp;&nbsp; ↑ ${size(S.sessionUp)}</div>`);
    return parts.join('<div class="w-div"></div>') || `<span class="w-label">${T.pickOne}</span>`;
  }
  if (O.layout === "h") {
    const item = (label, p, details) => `<div class="c-item ${level(p)}"><span class="c-label">${label}</span><span class="c-pct">${Math.round(p)}%</span>${details.map(d => `<span>${d}</span>`).join("")}</div>`;
    if (S.fps !== null) parts.push(`<div class="c-item"><span class="c-label">FPS</span><span>${S.fps}</span></div>`);
    if (m.cpu) parts.push(item("CPU", S.cpu, [ghz]));
    if (m.gpu) parts.push(item("GPU", S.gpu, [gpuMem, temp]));
    if (m.ram) parts.push(item("RAM", S.ram, [ramAmt]));
    if (m.disk) parts.push(item(T.disk, S.disk, []));
    if (m.net) parts.push(`<div class="c-item">${live ? `<span>${live}</span>` : `<span>${down}</span><span>${up}</span>`}<span>${ping}</span></div>`);
    return parts.join('<div class="c-sep"></div>') || `<span class="c-label">${T.pickOne}</span>`;
  }
  const vitem = (label, p, details) => `<div class="${level(p)}"><div class="c-head"><span class="c-label">${label}</span><span class="c-pct">${Math.round(p)}%</span></div>${details.map(d => `<div>${d}</div>`).join("")}</div>`;
  if (S.fps !== null) parts.push(`<div class="c-head"><span class="c-label">FPS</span><span>${S.fps}</span></div>`);
  if (m.cpu) parts.push(vitem("CPU", S.cpu, [ghz]));
  if (m.gpu) parts.push(vitem("GPU", S.gpu, [gpuMem, temp]));
  if (m.ram) parts.push(vitem("RAM", S.ram, [ramAmt]));
  if (m.disk) parts.push(vitem(T.disk, S.disk, []));
  if (m.net) parts.push(live ? `<div>${T.test}</div><div>${live.replace(`${T.test} `, "")}</div><div>${ping}</div>` : `<div>${down}</div><div>${up}</div><div>${ping}</div>`);
  return parts.join('<div class="c-sep"></div>') || `<span class="c-label">${T.pickOne}</span>`;
}

function render() {
  const th = THEMES[O.theme];
  widget.style.setProperty("--w-bg", th.bg);
  widget.style.setProperty("--w-text", th.text);
  widget.style.setProperty("--w-label", withAlpha(th.text, 0.65));
  widget.style.setProperty("--w-line", withAlpha(th.text, 0.2));
  widget.style.setProperty("--w-ok", th.ok);
  widget.style.setProperty("--w-warn", th.warn);
  widget.style.setProperty("--w-crit", th.crit);
  widget.style.setProperty("--w-opacity", String(1 - O.transparency / 100));
  widget.style.setProperty("--w-radius", O.radius + "px");
  widget.style.transform = `scale(${O.scale / 100})`;
  widget.style.zIndex = O.mode === "top" ? 20 : 5;
  widget.className = "widget l-" + O.layout + (O.lock ? " locked" : "") + (dragging ? " dragging" : "");
  widget.innerHTML = widgetHTML();
  keepInside();
}

let scale = 1;
function fitScreen() {
  const w = desk.clientWidth;
  const vw = w < 600 ? 480 : 1000, vh = w < 600 ? 640 : 590;
  screenEl.style.width = vw + "px"; screenEl.style.height = vh + "px";
  screenEl.classList.toggle("narrow", vw < 1000);
  scale = w / vw;
  screenEl.style.transform = `scale(${scale})`;
  desk.style.height = vh * scale + "px";
  keepInside();
}
new ResizeObserver(fitScreen).observe(desk);

let dragging = false;
const widgetSize = () => ({ w: widget.offsetWidth * O.scale / 100, h: widget.offsetHeight * O.scale / 100 });
function keepInside() {
  const { w, h } = widgetSize(), maxX = screenEl.offsetWidth - w, maxY = screenEl.offsetHeight - 40 - h;
  if (!userMoved) { widget.style.left = Math.max(0, maxX - 16) + "px"; widget.style.top = "16px"; return; }
  widget.style.left = clamp(widget.offsetLeft, 0, Math.max(0, maxX)) + "px";
  widget.style.top = clamp(widget.offsetTop, 0, Math.max(0, maxY)) + "px";
}
let start = null;
let userMoved = false;
widget.addEventListener("pointerdown", e => {
  if (O.lock) return;
  start = { x: e.clientX, y: e.clientY, left: widget.offsetLeft, top: widget.offsetTop };
  widget.setPointerCapture(e.pointerId);
  dragging = true; widget.classList.add("dragging");
});
widget.addEventListener("pointermove", e => {
  if (!start) return;
  const { w, h } = widgetSize(), maxX = screenEl.offsetWidth - w, maxY = screenEl.offsetHeight - 40 - h;
  let x = clamp(start.left + (e.clientX - start.x) / scale, 0, maxX);
  let y = clamp(start.top + (e.clientY - start.y) / scale, 0, maxY);
  if (O.snap) { if (x < 20) x = 0; if (maxX - x < 20) x = maxX; if (y < 20) y = 0; if (maxY - y < 20) y = maxY; }
  widget.style.left = x + "px"; widget.style.top = y + "px"; userMoved = true;
});
const endDrag = () => { start = null; dragging = false; widget.classList.remove("dragging"); };
widget.addEventListener("pointerup", endDrag);
widget.addEventListener("pointercancel", endDrag);

document.querySelectorAll(".settings [data-opt]").forEach(group => {
  group.querySelectorAll("button").forEach(btn => btn.addEventListener("click", () => {
    group.querySelectorAll("button").forEach(b => b.setAttribute("aria-pressed", String(b === btn)));
    O[group.dataset.opt] = btn.dataset.v;
    if (group.dataset.opt === "theme") { screenEl.classList.toggle("os-dark", btn.dataset.v === "dark"); screenEl.classList.toggle("os-light", btn.dataset.v === "light"); }
    render();
  }));
});
document.querySelectorAll("[data-metric]").forEach(sw => sw.addEventListener("change", () => { O.metrics[sw.dataset.metric] = sw.checked; render(); }));
[["o-colors", "colors"]].forEach(([id, key]) => document.getElementById(id).addEventListener("change", e => { O[key] = e.target.checked; render(); }));
[["o-transp", "transparency", v => v + "%"], ["o-scale", "scale", v => v + "%"], ["o-radius", "radius", v => v + " px"]].forEach(([id, key, label]) => {
  const input = document.getElementById(id), out = document.getElementById(id + "-v");
  input.addEventListener("input", () => { O[key] = Number(input.value); out.textContent = label(input.value); render(); });
});

const WINDOWS = { game: document.getElementById("win-game"), render: document.getElementById("win-render"), download: document.getElementById("win-download") };
const TB_COLORS = { game: "#E84A8A", render: "#7E57C2", download: "#3B9CF4" };
let progress = 0;
const FILES = [[T.files[0], 48, 0.18], [T.files[1], 3.2, 0.55], [T.files[2], 1.4, 0.05]];
function setScenario(v) {
  scenario = v;
  Object.entries(WINDOWS).forEach(([k, el]) => el.classList.toggle("show", k === v));
  const running = document.getElementById("tb-running");
  running.classList.toggle("running", v in WINDOWS);
  document.getElementById("tb-running-dot").style.background = TB_COLORS[v] || "#7E57C2";
  progress = 0; speedTest.stage = null; speedTest.left = 0;
  if (v === "game") startGame(); else { stopGame(); S.fps = null; }
  render();
}
document.querySelectorAll("#scenes button").forEach(btn => btn.addEventListener("click", () => {
  document.querySelectorAll("#scenes button").forEach(b => b.setAttribute("aria-pressed", String(b === btn)));
  setScenario(btn.dataset.v);
}));
function updateWindows() {
  if (scenario === "render") {
    progress = progress >= 100 ? 0 : progress + 1.25;
    document.getElementById("ed-prog").style.width = progress + "%";
    document.getElementById("ed-head").style.left = `calc(14px + (100% - 28px) * ${progress / 100})`;
    document.getElementById("ed-status").textContent = `${T.exporting} · ${Math.floor(progress)}%`;
    document.getElementById("ed-left").textContent = T.minutesLeft(Math.max(1, Math.ceil((100 - progress) / 1.25 / 60)));
    const prev = document.getElementById("ed-preview");
    prev.style.setProperty("--hue", String((progress * 7) % 360));
    prev.textContent = `${T.frame} ${Math.floor(progress * 72)}`;
  }
  if (scenario === "download") {
    FILES.forEach((f, i) => { f[2] = f[2] >= 1 ? 0 : Math.min(1, f[2] + [0.01, 0.04, 0.07][i]); });
    document.getElementById("dl-list").innerHTML = FILES.map(([name, gb, done], i) => `<li class="dl-item"><span class="dl-icon">${name.split(".").pop().toUpperCase()}</span><span>${name}</span><span class="prog"><i style="width:${(done * 100).toFixed(1)}%"></i></span><span class="dl-meta">${fmt(gb * done, 1)} ${T.of} ${fmt(gb, 1)} GB · ${fmt([28, 16, 9][i] + Math.random() * 4, 1)} MB/s</span></li>`).join("");
  }
}

const canvas = document.getElementById("game"), ctx = canvas.getContext("2d");
let gameRaf = null, frames = 0, fpsStart = 0, roadOffset = 0, lastT = 0;
function drawGame(t) {
  const W = canvas.width, H = canvas.height, horizon = H * 0.48;
  const dt = Math.min(0.05, (t - lastT) / 1000 || 0); lastT = t;
  const sky = ctx.createLinearGradient(0, 0, 0, horizon);
  sky.addColorStop(0, "#120B2E"); sky.addColorStop(1, "#E04A8A");
  ctx.fillStyle = sky; ctx.fillRect(0, 0, W, horizon);
  ctx.save(); ctx.beginPath(); ctx.arc(W / 2, horizon, 74, Math.PI, 0); ctx.closePath(); ctx.clip();
  const sun = ctx.createLinearGradient(0, horizon - 74, 0, horizon); sun.addColorStop(0, "#FFD86B"); sun.addColorStop(1, "#FF6A88");
  ctx.fillStyle = sun; ctx.fillRect(W / 2 - 74, horizon - 74, 148, 74);
  ctx.fillStyle = "#E04A8A"; for (let i = 0; i < 5; i++) ctx.fillRect(W / 2 - 74, horizon - 30 + i * 6, 148, 2 + i * 0.6);
  ctx.restore();
  ctx.fillStyle = "#2A1550"; ctx.beginPath(); ctx.moveTo(0, horizon);
  [[0.08, 0.62], [0.18, 0.78], [0.3, 0.55], [0.42, 0.8], [0.58, 0.6], [0.7, 0.82], [0.84, 0.58], [1, 0.75]].forEach(([x, k]) => ctx.lineTo(x * W, horizon - (1 - k) * 120));
  ctx.lineTo(W, horizon); ctx.fill();
  ctx.fillStyle = "#170A2B"; ctx.fillRect(0, horizon, W, H - horizon);
  roadOffset = (roadOffset + dt * 2.2) % 1;
  ctx.strokeStyle = "rgba(224, 74, 138, 0.55)"; ctx.lineWidth = 1;
  for (let i = 0; i < 14; i++) { const z = (i + roadOffset) / 14, y = horizon + (H - horizon) * z * z; ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(W, y); ctx.stroke(); }
  for (let i = -8; i <= 8; i++) { ctx.beginPath(); ctx.moveTo(W / 2 + i * 14, horizon); ctx.lineTo(W / 2 + i * 120, H); ctx.stroke(); }
  ctx.fillStyle = "#24133F"; ctx.beginPath(); ctx.moveTo(W / 2 - 18, horizon); ctx.lineTo(W / 2 + 18, horizon); ctx.lineTo(W / 2 + 230, H); ctx.lineTo(W / 2 - 230, H); ctx.fill();
  ctx.fillStyle = "#FFD86B";
  for (let i = 0; i < 10; i++) { const z0 = (i + roadOffset) / 10, z1 = z0 + 0.04, y0 = horizon + (H - horizon) * z0 * z0, y1 = horizon + (H - horizon) * z1 * z1; const w0 = 1 + 5 * z0; ctx.fillRect(W / 2 - w0 / 2, y0, w0, y1 - y0); }
  const carX = W / 2 + Math.sin(t / 900) * 70, carY = H - 70;
  ctx.fillStyle = "#0E0E14"; ctx.fillRect(carX - 52, carY + 34, 104, 8);
  ctx.fillStyle = "#4DB3EE"; ctx.beginPath(); ctx.moveTo(carX - 50, carY + 34); ctx.lineTo(carX - 44, carY + 8); ctx.lineTo(carX - 28, carY - 4); ctx.lineTo(carX + 28, carY - 4); ctx.lineTo(carX + 44, carY + 8); ctx.lineTo(carX + 50, carY + 34); ctx.fill();
  ctx.fillStyle = "#1B2B4A"; ctx.fillRect(carX - 24, carY, 48, 10);
  ctx.fillStyle = "#FF4D6D"; ctx.fillRect(carX - 44, carY + 18, 18, 6); ctx.fillRect(carX + 26, carY + 18, 18, 6);
  ctx.fillStyle = "#FFFFFF"; ctx.font = "600 15px Segoe UI, system-ui, sans-serif";
  ctx.fillText(T.lap, 16, 26);
  ctx.textAlign = "right"; ctx.fillText(`${Math.round(208 + Math.sin(t / 700) * 14)} km/h`, W - 16, H - 16); ctx.textAlign = "left";
}
function gameLoop(t) {
  drawGame(t);
  frames++;
  if (t - fpsStart >= 500) { S.fps = Math.round(frames * 1000 / (t - fpsStart)); frames = 0; fpsStart = t; }
  gameRaf = requestAnimationFrame(gameLoop);
}
function startGame() {
  if (reduceMotion) { drawGame(0); S.fps = "—"; return; }
  if (gameRaf) return;
  frames = 0; fpsStart = performance.now(); lastT = fpsStart; S.fps = "—";
  gameRaf = requestAnimationFrame(gameLoop);
}
function stopGame() { if (gameRaf) cancelAnimationFrame(gameRaf); gameRaf = null; }

function clock() {
  const now = new Date();
  document.getElementById("clock").innerHTML = `${now.toLocaleTimeString(T.locale, { hour: "2-digit", minute: "2-digit" })}<br>${now.toLocaleDateString(T.locale)}`;
}
let demoVisible = true;
new IntersectionObserver(([e]) => {
  demoVisible = e.isIntersecting;
  if (!demoVisible) stopGame(); else if (scenario === "game") startGame();
}).observe(desk);
clock(); render(); setInterval(clock, 30000);
if (!reduceMotion) setInterval(() => { if (!demoVisible || dragging) return; tick(); updateWindows(); render(); }, 1000);

const formatDate = iso => new Date(iso).toLocaleDateString(T.locale, { day: "numeric", month: "long", year: "numeric" });
const mb = bytes => `${fmt(bytes / 1048576, 1)} MB`;
async function loadReleases() {
  const box = document.getElementById("releases");
  try {
    const res = await fetch(`https://api.github.com/repos/${REPO}/releases?per_page=6`, { headers: { Accept: "application/vnd.github.html+json" } });
    if (!res.ok) throw new Error(res.status);
    const list = (await res.json()).filter(r => !r.draft && !r.prerelease);
    if (!list.length) { box.innerHTML = `<p class="releases-empty">${T.noReleases}</p>`; return; }
    box.innerHTML = list.map(r => `<article class="release"><header><h3>${r.name || r.tag_name}</h3><time datetime="${r.published_at}">${formatDate(r.published_at)}</time></header><div class="release-notes">${r.body_html || `<p>${T.noNotes}</p>`}</div></article>`).join("");
    // Links inside the release notes come from GitHub and point to other pages.
    box.querySelectorAll("a[href]").forEach(openInNewTab);
    const latest = list[0], version = latest.tag_name.replace(/^v/, "");
    document.getElementById("hero-version").textContent = T.version(version);
    document.getElementById("req-version").textContent = T.currentVersion(version);
    document.getElementById("footer-version").textContent = T.version(version);
    releaseFiles = latest.assets;
    showPackages();
  } catch {
    box.innerHTML = `<p class="releases-empty">${T.releasesFailed(`https://github.com/${REPO}/releases`)}</p>`;
  }
}
// Files of the latest release and the chosen architecture; the buttons follow both.
let releaseFiles = [], arch = "x64";
function showPackages() {
  // Each button looks for its own package by the end of the file name, so other files in the release don't matter.
  const showPackage = (suffix, linkId, sizeId) => {
    const file = releaseFiles.find(a => a.name.toLowerCase().endsWith(suffix));
    // Without that package in the latest release, the button opens the releases page.
    const link = document.getElementById(linkId);
    link.href = file ? file.browser_download_url : `https://github.com/${REPO}/releases`;
    if (file) { link.removeAttribute("target"); link.removeAttribute("rel"); } else { openInNewTab(link); }
    document.getElementById(sizeId).textContent = file ? `${file.name} · ${mb(file.size)}` : "";
  };
  showPackage(`-portable-win-${arch}.zip`, "dl-portable", "size-portable");
  showPackage(`-framework-dependent-win-${arch}.zip`, "dl-compact", "size-compact");
  document.getElementById("runtime-link").href = `https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/${RUNTIME_VERSION}/windowsdesktop-runtime-${RUNTIME_VERSION}-win-${arch}.exe`;
  document.getElementById("runtime-arch").textContent = arch === "x86" ? " (x86)" : "";
}
document.querySelectorAll("#arch button").forEach(btn => btn.addEventListener("click", () => {
  document.querySelectorAll("#arch button").forEach(b => b.setAttribute("aria-pressed", String(b === btn)));
  arch = btn.dataset.arch;
  showPackages();
}));
loadReleases();

const form = document.getElementById("contact-form"), formStatus = document.getElementById("form-status"), send = document.getElementById("send");
form.addEventListener("submit", async e => {
  e.preventDefault();
  formStatus.className = "form-status full"; formStatus.textContent = "";
  if (!form.checkValidity()) { formStatus.classList.add("err"); formStatus.textContent = T.formInvalid; form.reportValidity(); return; }
  const data = Object.fromEntries(new FormData(form));
  const payload = { access_key: WEB3FORMS_KEY, subject: `[cobihw] ${data.topic}: mensagem de ${data.name}`, from_name: T.fromName, name: data.name, email: data.email, message: data.message, botcheck: data.botcheck ? true : "" };
  send.disabled = true; send.textContent = T.sending;
  try {
    const res = await fetch("https://api.web3forms.com/submit", { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json" }, body: JSON.stringify(payload) });
    const json = await res.json();
    if (!json.success) throw new Error(json.message);
    form.reset(); formStatus.classList.add("ok"); formStatus.textContent = T.sent;
  } catch {
    formStatus.classList.add("err"); formStatus.textContent = T.sendFailed;
  } finally {
    send.disabled = false; send.textContent = T.send;
  }
});
