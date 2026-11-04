export function el(tag, className, content) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (content !== undefined && content !== null)
    node.textContent = String(content);
  return node;
}
export function btn(text, cls, click) {
  const button = el("button", cls, text);
  button.type = "button";
  button.addEventListener("click", click);
  return button;
}
export function card(title, ...children) {
  const box = el("article", "card");
  box.append(el("h3", "", title), ...children);
  return box;
}
export function specs(entries) {
  const list = el("dl", "spec");
  for (const [key, value] of entries)
    list.append(el("dt", "", key), el("dd", "", value ?? "Unreported"));
  return list;
}
export function stepsList(steps = []) {
  const list = el("ol", "steps");
  for (const step of steps) {
    const row = el("li");
    row.append(el("strong", "", step.action), el("code", "", step.value));
    list.append(row);
  }
  return list;
}
export function limitSpecs(limits = {}) {
  return specs([
    ["Time", limits.seconds == null ? null : limits.seconds + " seconds"],
    ["Requests", limits.requests],
    [
      "Transfer",
      limits.bytes == null ? null : format(limits.bytes / 1048576) + " MiB",
    ],
  ]);
}
export function format(value) {
  return value == null ? "Unreported" : Number(value).toLocaleString("en-US");
}
export function seconds(ms) {
  return ms == null
    ? "Unreported"
    : (ms / 1000).toLocaleString("en-US", { maximumFractionDigits: 3 }) + " s";
}
export function safeImage(path, prefix) {
  if (typeof path !== "string" || !path.startsWith(prefix)) return null;
  const url = new URL(path, location.origin);
  return url.origin === location.origin && url.pathname.startsWith(prefix)
    ? url.href
    : null;
}
export function imageLink(path, alt) {
  const anchor = el("a");
  anchor.href = path;
  anchor.target = "_blank";
  anchor.rel = "noopener noreferrer";
  const img = el("img", "article-img");
  img.src = path;
  img.alt = alt;
  anchor.append(img);
  return anchor;
}
export function message(text = "") {
  const node = document.getElementById("notice");
  node.replaceChildren();
  if (text) node.append(el("div", "notice", text));
}
export async function api(path, body, csrf) {
  const options = {
    credentials: "same-origin",
    cache: "no-store",
    headers: { Accept: "application/json" },
  };
  if (body !== undefined) {
    options.method = "POST";
    options.headers["Content-Type"] = "application/json";
    options.headers["X-HireZero-CSRF"] = csrf || "";
    options.body = JSON.stringify(body);
  }
  const response = await fetch(path, options);
  const data = await response.json().catch(() => ({}));
  if (!response.ok)
    throw new Error(data.error || "Request failed (" + response.status + ").");
  return data;
}
