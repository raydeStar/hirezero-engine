import {
  api,
  btn,
  card,
  el,
  format,
  imageLink,
  limitSpecs,
  message,
  safeImage,
  seconds,
  specs,
  stepsList,
} from "./view.js";
import { showGallery } from "./gallery.js";

const $ = (id) => document.getElementById(id);
let session = null,
  busy = false,
  ownerJob = null;
function field(caption, tag, attrs) {
  const label = el("label", "field", caption),
    input = el(tag);
  Object.assign(input, attrs);
  label.append(input);
  return [label, input];
}
function submit(text) {
  const b = el("button", "primary", text);
  b.type = "submit";
  return b;
}
function setBusy(value) {
  busy = value;
  for (const b of document.querySelectorAll(
    "#owner button, #owner input, #owner textarea",
  ))
    b.disabled = value;
  $("owner-pane").setAttribute("aria-busy", String(value));
}
async function act(operation) {
  if (busy) return;
  setBusy(true);
  message();
  try {
    await operation();
  } catch (error) {
    message(
      error.message + " Check the job record before starting another request.",
    );
    try {
      session = await api("/api/session");
      if (!session.authenticated) login();
    } catch {
      /* Keep the visible outcome if the worker itself is offline. */
    }
  } finally {
    setBusy(false);
  }
}
function login() {
  ownerJob = null;
  $("logout").hidden = true;
  const form = el("form", "card login"),
    [label, input] = field("Owner passphrase", "input", {
      type: "password",
      required: true,
      autoComplete: "current-password",
      name: "passphrase",
    });
  form.append(
    el("div", "kicker", "Private access"),
    el("h2", "", "Sign in to the owner desk"),
    el(
      "p",
      "subtle",
      "The owner can draft and approve a run. Judges can read selected evidence without signing in.",
    ),
    label,
    submit("Sign in"),
  );
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    const passphrase = input.value;
    input.value = "";
    void act(async () => {
      session = await api("/api/login", { passphrase }, session.csrf);
      await dashboard();
    });
  });
  $("owner-pane").replaceChildren(form);
}
function draftForm() {
  const rehearsal = session.mode === "rehearsal",
    form = el("form", "card");
  const [g, goal] = field("Research question", "textarea", {
    required: true,
    maxLength: 600,
    name: "goal",
    rows: 2,
    value: rehearsal
      ? "What does the fictional paper catalog sell?"
      : "Which license does this site recommend for a simple, permissive license?",
  });
  const [s, site] = field("Single approved origin", "input", {
    type: "url",
    name: "site",
    required: true,
    maxLength: 240,
    value: rehearsal
      ? "https://paper.example.invalid"
      : "https://choosealicense.com",
  });
  const actions = el("div", "row");
  const template = submit("Use homepage template");
  template.value = "homepage";
  template.className = "quiet";
  actions.append(submit("Draft itinerary"), template);
  form.append(
    el("div", "kicker", "01  /  Prepare"),
    el("h2", "", "Draft a browser task"),
    g,
    s,
    el(
      "p",
      "hint",
      rehearsal
        ? "This rehearsal uses a fictional page and simulated model responses."
        : "Drafting sends one model request. Browsing waits until you approve the packet.",
    ),
    actions,
    el(
      "p",
      "hint",
      "The homepage template prepares a single read of the page body without calling a model to plan it.",
    ),
  );
  form.addEventListener("submit", (e) => {
    e.preventDefault();
    const body = { goal: goal.value.trim(), site: site.value.trim() };
    const endpoint =
      e.submitter?.value === "homepage" ? "/api/homepage-packet" : "/api/draft";
    void act(async () => {
      const state = el("p", "busy", "Preparing the itinerary…");
      form.append(state);
      try {
        const draft = await api(endpoint, body, session.csrf);
        ownerJob = draft.jobId;
        review(draft);
      } finally {
        state.remove();
      }
    });
  });
  return form;
}
async function jobList() {
  const box = card("Your runs");
  const jobs = await api("/api/jobs");
  if (!jobs.length)
    box.append(
      el("p", "hint", "Drafts and execution records will appear here."),
    );
  for (const j of jobs) {
    const b = btn(
      "",
      "job",
      () =>
        void act(async () => {
          const job = await api("/api/jobs/" + encodeURIComponent(j.jobId));
          ownerJob = j.jobId;
          if (job.canApprove) review(job);
          else result(job);
        }),
    );
    b.append(
      el("strong", "", j.goal),
      el("small", "", j.state + (j.simulated ? " · simulation" : "")),
    );
    box.append(b);
  }
  return box;
}
async function dashboard() {
  $("logout").hidden = false;
  const grid = el("div", "grid");
  grid.append(draftForm(), await jobList());
  $("owner-pane").replaceChildren(grid);
}
function backButton() {
  return btn("All runs", "back", () => void act(dashboard));
}
function review(draft) {
  const req = draft.request,
    main = el("article", "card"),
    aside = el("aside", "card");
  main.append(
    el("div", "kicker", "02  /  Owner review"),
    el("h2", "", "Approve this exact itinerary"),
    el("p", "", draft.goal),
    stepsList(req.steps),
    el("h4", "", "Permitted origins"),
    el("p", "mono", req.origins.join(", ")),
    el("div", "small-label", "SHA-256 packet digest"),
    el("code", "digest", draft.digest),
  );
  const controls = el("div", "row");
  const approve = btn(
    draft.simulated ? "Approve simulated run" : "Approve and run once",
    "primary",
    () =>
      void act(async () => {
        const state = el(
          "p",
          "busy",
          "Executing the approved packet. The result and release record will appear here.",
        );
        main.append(state);
        try {
          const run = await api(
            "/api/approve",
            { jobId: draft.jobId, digest: draft.digest },
            session.csrf,
          );
          result(run);
        } finally {
          state.remove();
        }
      }),
  );
  controls.append(approve, backButton());
  main.append(controls);
  aside.append(
    el("div", "kicker", "Sandbox contract"),
    el("h3", "", "Execution limits"),
    limitSpecs(req.limits),
    specs([
      ["Draft source", draft.source],
      ["Screenshots", req.screenshots ? "Enabled" : "Disabled"],
    ]),
  );
  if (draft.note) aside.append(el("p", "hint", draft.note));
  const packet = el("details");
  packet.append(
    el("summary", "", "Inspect complete packet"),
    el("pre", "", JSON.stringify(req, null, 2)),
  );
  aside.append(packet);
  const grid = el("div", "grid");
  grid.append(main, aside);
  $("owner-pane").replaceChildren(grid);
}
function result(job) {
  ownerJob = job.jobId;
  const top = el("div", "card");
  top.append(
    el(
      "div",
      "kicker",
      (job.simulated ? "Simulated job" : "Private job") + "  /  " + job.state,
    ),
    el("h2", "", job.goal),
    el("code", "digest", job.digest),
    specs([
      ["Allowed origin", job.site],
      ["State", job.state],
      ["Sandbox release", job.teardown?.status],
      [
        "Confirmed",
        job.teardown?.confirmed == null
          ? null
          : job.teardown.confirmed
            ? "Yes"
            : "No",
      ],
      ["Model tokens", format(job.tokens)],
      ["Browser time", seconds(job.wallMilliseconds)],
    ]),
  );
  const readiness = [
    "draft",
    "approved",
    "running",
    "uncertain",
    "interrupted",
  ].includes(job.state);
  if (readiness)
    top.append(
      el(
        "p",
        "hint",
        "Check the saved job state. Refreshing this record only reads evidence.",
      ),
    );
  const actions = el("div", "row");
  actions.append(
    backButton(),
    btn(
      "Refresh record",
      "quiet",
      () =>
        void act(async () => {
          const current = await api(
            "/api/jobs/" + encodeURIComponent(ownerJob),
          );
          if (current.canApprove) review(current);
          else result(current);
        }),
    ),
  );
  top.append(actions);
  const parts = [top];
  if (job.answer || job.answerNote)
    parts.push(
      card(
        "Answer from the capture",
        el("p", "bodycopy", job.answer || job.answerNote),
      ),
    );
  for (const c of job.captures ?? []) {
    const path = safeImage(
      c.screenshotUrl,
      "/api/jobs/" + encodeURIComponent(job.jobId) + "/captures/",
    );
    const source = card(
      c.title || "Browser capture",
      el("div", "mono", c.url),
      el("p", "bodycopy", c.text),
    );
    if (path)
      source.append(imageLink(path, "Approved capture, step " + c.step));
    parts.push(source);
  }
  if (job.events || job.blockedOrigins) {
    const network = card(
      "Network decisions",
      el(
        "p",
        "",
        job.blockedOrigins?.length
          ? "Blocked off-site origins: " + job.blockedOrigins.join(", ")
          : "No blocked off-site origins recorded.",
      ),
    );
    for (const e of job.events ?? [])
      network.append(
        el(
          "div",
          "mono",
          [e.outcome, e.kind, e.address, e.reason].filter(Boolean).join(" · "),
        ),
      );
    parts.push(network);
  }
  $("owner-pane").replaceChildren(...parts);
}
async function route() {
  const owner =
    location.hash === "#owner" && session && session.mode !== "gallery";
  $("gallery").hidden = owner;
  $("owner").hidden = !owner;
  $("show-gallery").classList.toggle("on", !owner);
  $("show-owner").classList.toggle("on", owner);
  message();
  if (!owner) {
    try {
      await showGallery();
    } catch {
      $("cases").replaceChildren(
        card(
          "Gallery unavailable",
          el("p", "subtle", "The selected evidence store could not be read."),
        ),
      );
    }
    return;
  }
  $("mode-note").replaceChildren(
    el(
      "div",
      "banner",
      session.mode === "rehearsal"
        ? "Offline rehearsal · Model calls, browser execution and usage in this workspace are simulated."
        : "Live worker · Drafting uses inference. An approved packet can start one qualified browser.",
    ),
  );
  session = await api("/api/session");
  if (session.authenticated) await dashboard();
  else login();
}
$("logout").addEventListener(
  "click",
  () =>
    void act(async () => {
      session = await api("/api/logout", {}, session.csrf);
      login();
    }),
);
$("show-gallery").addEventListener("click", () => (location.hash = "gallery"));
$("show-owner").addEventListener("click", () => (location.hash = "owner"));
window.addEventListener(
  "hashchange",
  () => void route().catch((e) => message(e.message)),
);
try {
  session = await api("/api/session");
  $("show-owner").hidden = session.mode === "gallery";
  await route();
} catch {
  message(
    "The evidence desk could not reach its server. Reload when the host is ready.",
  );
}
