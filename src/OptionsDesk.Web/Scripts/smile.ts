// Implied vol smile on the chain page, with spot marked. Following desk convention it plots the
// out-of-the-money side at each strike (puts below spot, calls above), where quotes are most liquid.
// Data comes from the JSON block the view renders next to the chart.

interface SmilePoint {
  strike: number;
  call: number;
  put: number;
}

interface SmileData {
  spot: number;
  points: SmilePoint[];
}

const SVG = "http://www.w3.org/2000/svg";
const MARGIN = { top: 10, right: 12, bottom: 24, left: 44 };

const chart = document.querySelector<SVGSVGElement>("#smile-chart");
const source = document.querySelector<HTMLScriptElement>("#smile-data");
if (chart && source?.textContent) {
  const data = JSON.parse(source.textContent) as SmileData;
  const draw = () => render(chart, data);
  draw();
  new ResizeObserver(draw).observe(chart);
}

function el<K extends keyof SVGElementTagNameMap>(name: K, attrs: Record<string, string | number>, parent: SVGElement) {
  const node = document.createElementNS(SVG, name);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, String(value));
  parent.append(node);
  return node;
}

/** Round, evenly spaced tick values covering [min, max]. */
export function niceTicks(min: number, max: number, target = 5): number[] {
  const raw = (max - min) / target;
  const magnitude = 10 ** Math.floor(Math.log10(raw));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((s) => s >= raw) ?? raw;
  const ticks: number[] = [];
  for (let t = Math.ceil(min / step) * step; t <= max + step * 1e-9; t += step) ticks.push(Number(t.toFixed(10)));
  return ticks;
}

function render(svg: SVGSVGElement, data: SmileData): void {
  const width = svg.clientWidth;
  const height = svg.clientHeight;
  if (!width || data.points.length < 2) return;
  svg.replaceChildren();
  svg.setAttribute("viewBox", `0 0 ${width} ${height}`);

  const strikes = data.points.map((p) => p.strike);
  const otm = data.points.map((p) => ({ strike: p.strike, vol: p.strike < data.spot ? p.put : p.call }));
  const vols = otm.map((p) => p.vol);
  const [kMin, kMax] = [Math.min(...strikes), Math.max(...strikes)];
  const pad = (Math.max(...vols) - Math.min(...vols)) * 0.1 || 0.01;
  const [vMin, vMax] = [Math.min(...vols) - pad, Math.max(...vols) + pad];

  const x = (k: number) => MARGIN.left + ((k - kMin) / (kMax - kMin)) * (width - MARGIN.left - MARGIN.right);
  const y = (v: number) => height - MARGIN.bottom - ((v - vMin) / (vMax - vMin)) * (height - MARGIN.top - MARGIN.bottom);

  const bottom = height - MARGIN.bottom;
  el("line", { class: "axis", x1: MARGIN.left, x2: width - MARGIN.right, y1: bottom, y2: bottom }, svg);
  el("line", { class: "axis", x1: MARGIN.left, x2: MARGIN.left, y1: MARGIN.top, y2: bottom }, svg);
  for (const v of niceTicks(vMin, vMax, 4)) {
    el("text", { x: MARGIN.left - 6, y: y(v) + 4, "text-anchor": "end" }, svg).textContent = `${(v * 100).toFixed(0)}%`;
  }
  for (const k of niceTicks(kMin, kMax, 6)) {
    el("text", { x: x(k), y: bottom + 16, "text-anchor": "middle" }, svg).textContent = String(k);
  }

  if (data.spot >= kMin && data.spot <= kMax) {
    el("line", { class: "spot", x1: x(data.spot), x2: x(data.spot), y1: MARGIN.top, y2: bottom }, svg);
  }

  const pts = otm.map((p) => `${x(p.strike).toFixed(1)},${y(p.vol).toFixed(1)}`).join(" ");
  el("polyline", { class: "line otm", points: pts }, svg);
  for (const p of otm) {
    const side = p.strike < data.spot ? "put" : "call";
    const dot = el("circle", { class: side, cx: x(p.strike), cy: y(p.vol), r: 3 }, svg);
    el("title", {}, dot).textContent = `${p.strike} ${side}: ${(p.vol * 100).toFixed(1)}%`;
  }
}
