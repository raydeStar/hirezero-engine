import {
  api,
  el,
  btn,
  card,
  specs,
  stepsList,
  limitSpecs,
  format,
  seconds,
  safeImage,
  imageLink,
} from "./view.js";

function metric(label, value) {
  const node = el("div", "metric");
  node.append(el("span", "metric-label", label), el("strong", "", value));
  return node;
}
function host(site) {
  try {
    return new URL(site).hostname;
  } catch {
    return site || "Approved site";
  }
}
function lead(run, number, photo) {
  const hero = el("article", "case-lead");
  const words = el("div");
  words.append(
    el(
      "p",
      "kicker",
      "Record " + String(number).padStart(2, "0") + "  /  " + run.title,
    ),
    el("h2", "", run.goal),
    el("div", "site", host(run.site)),
    el("div", "source", run.source),
  );
  hero.append(words);
  if (photo) {
    const preview = el("figure", "preview"),
      frame = el("a");
    frame.href = photo;
    frame.target = "_blank";
    frame.rel = "noopener noreferrer";
    const chrome = el("div", "browser-chrome");
    chrome.append(el("i"), el("i"), el("i"), el("span", "", host(run.site)));
    const img = el("img");
    img.src = photo;
    img.alt = "Open full-size screenshot from the approved browser capture";
    frame.append(chrome, img);
    preview.append(
      frame,
      el(
        "figcaption",
        "",
        run.captureCredit ||
          "Captured by the isolated browser · open full image",
      ),
    );
    hero.append(preview);
  }
  return hero;
}
function overview(run) {
  const grid = el("div", "grid");
  grid.append(
    card(
      "Answer from the capture",
      el("p", "bodycopy", run.answer || "No answer recorded."),
    ),
    card(
      "Execution record",
      specs([
        ["Origin", run.site],
        ["Provider", run.provider],
        ["Execution", run.state],
        ["Teardown", run.teardown],
      ]),
    ),
  );
  return grid;
}
function galleryCard(target, run, number) {
  const photo = safeImage(run.screenshotUrl, "/api/gallery/");
  const counts = el("div", "metrics");
  counts.append(
    metric("Browser time", seconds(run.wallMilliseconds)),
    metric("Model tokens", format(run.tokens)),
    metric("Allowed requests", format(run.allowedRequests)),
    metric("Sandbox release", run.teardown),
  );
  const tabs = el("div", "tabbar"),
    detail = el("div");
  tabs.setAttribute("aria-label", "Run evidence");
  let selected;
  const sections = [
    ["Overview", () => overview(run)],
    [
      "Approval",
      () =>
        card(
          "Approved itinerary",
          ...(run.steps?.length
            ? [stepsList(run.steps)]
            : [el("p", "hint", "Steps were not included in this snapshot.")]),
          ...(run.limits ? [limitSpecs(run.limits)] : []),
        ),
    ],
    [
      "Capture",
      () =>
        card(
          "Captured evidence",
          el("p", "bodycopy", run.capturedText || "No text excerpt selected."),
          ...(run.captureCredit ? [el("p", "hint", run.captureCredit)] : []),
          ...(photo
            ? [
                imageLink(
                  photo,
                  "Full browser screenshot for " + host(run.site),
                ),
              ]
            : []),
        ),
    ],
    [
      "Network",
      () =>
        card(
          "Network decisions",
          specs([
            ["Allowed by broker", run.allowedRequests],
            ["Refused by broker", run.refusedRequests],
            [
              "Off-site origins observed",
              run.blockedOrigins?.length
                ? run.blockedOrigins.join(", ")
                : "None recorded",
            ],
          ]),
          el(
            "p",
            "hint",
            "Chromium checks the approved origin before forwarding a request to the host broker. A refusal inside Chromium can happen before the broker sees it.",
          ),
        ),
    ],
  ];
  for (const [name, render] of sections) {
    const button = btn(name, "tab", () => {
      if (selected) selected.setAttribute("aria-pressed", "false");
      selected = button;
      button.setAttribute("aria-pressed", "true");
      detail.replaceChildren(render());
    });
    button.setAttribute("aria-pressed", "false");
    tabs.append(button);
    if (!selected) button.click();
  }
  target.replaceChildren(lead(run, number, photo), counts, tabs, detail);
}
export async function showGallery() {
  const cases = document.getElementById("cases");
  cases.replaceChildren(el("p", "hint", "Opening selected run records…"));
  const runs = await api("/api/gallery");
  if (!runs.length) {
    cases.replaceChildren(
      card(
        "No selected runs yet",
        el(
          "p",
          "subtle",
          "An operator can add a reviewed summary and screenshot to this gallery.",
        ),
      ),
    );
    return;
  }
  const content = el("div");
  cases.replaceChildren();
  if (runs.length > 1) {
    const picker = el("div", "case-choose");
    const label = el("label", "", "Selected run"),
      select = el("select");
    select.id = "case-select";
    label.htmlFor = select.id;
    for (const [i, run] of runs.entries()) {
      const opt = el("option", "", run.title);
      opt.value = String(i);
      select.append(opt);
    }
    select.addEventListener("change", () =>
      galleryCard(
        content,
        runs[select.selectedIndex],
        select.selectedIndex + 1,
      ),
    );
    picker.append(label, select);
    cases.append(picker);
  }
  cases.append(content);
  galleryCard(content, runs[0], 1);
}
