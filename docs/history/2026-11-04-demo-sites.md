# Demo site shortlist · November 4, 2026

**Choose a License is the first choice.** Its 10,536-byte homepage answers the question below even if the browser refuses its Google Fonts stylesheet. The site's published robots template allows all paths for all agents. CommonMark is a 9,314-byte alternative with the same kind of font request. [Choose a License policy source](https://github.com/github/choosealicense.com/blob/b054bc8eb90dce1c4293d8617b4f54ce37bc8515/robots.txt)

| Page inspected | Off-site resources declared in HTML | Suggested question for a single read |
| --- | --- | --- |
| [Choose a License](https://choosealicense.com/) | `fonts.googleapis.com` stylesheet | Which license does the page suggest for someone who wants it simple and permissive? |
| [CommonMark](https://commonmark.org/) | `fonts.googleapis.com` stylesheet | Why did CommonMark's authors think Markdown needed a specification? |
| [Markdown Guide](https://www.markdownguide.org/getting-started/) | `hawk.forklabs.com` and `media.ethicalads.io` scripts, `assets.jotbird.com` logo | How does Markdown mark a heading and bold text? |
| [W3C CSS overview](https://www.w3.org/Style/CSS/Overview.en.html) | `jigsaw.w3.org` validator badge image (a separate origin from `www.w3.org`) | Which three examples of styling does the introduction give? |
| [curl](https://curl.se/) | None evident | How does the project describe curl in one line? |
| [Project Gutenberg](https://www.gutenberg.org/about/) | None evident | What mission does Project Gutenberg state? |
| [RFC 2606](https://www.rfc-editor.org/rfc/rfc2606.html) | None evident | What are the reserved names in this memo intended to prevent? |
| [Pico CSS](https://picocss.com/) | None evident | How does Pico style HTML elements by default? |

All named resource origins use HTTPS. Each page returned HTTP 200 and readable HTML to one GET with the broker's user agent. The screen inspected resource tags and inline references without following links or loading assets. Outbound hyperlinks and structured metadata do not imply a fetch. Later script activity and dependencies of linked stylesheets were outside this screen.

The one-GET limit left the deployed robots policies unverified. CommonMark's published source tree contains no robots file. During an approved run the broker checks the deployed policy before reading the page, including its handling of a missing policy (HTTP 404 or 410). [CommonMark site source](https://github.com/commonmark/commonmark-web/tree/b45162cbc65abe4eaa5137d497775f219efeb7a0) · [Broker policy checks](../../src/HireZero.Integrations.Web/PublicBrowserNetwork.cs)

**What to film.** Approve only the selected page's origin. For pages below the site root, check the first navigation URL on the draft. The browser worker checks the origin before sending an intercepted request to the broker and records refused URLs in the execution result. Look there for a font, script or image refusal. Use a host-broker event as evidence only if that event actually appears. The site choice and observed refusal await the owner's run. [Browser driver](../../runtime/browser/driver.mjs) · [Browser task and fallback itinerary](../../src/HireZero.Agent/BrowserTask.cs)
