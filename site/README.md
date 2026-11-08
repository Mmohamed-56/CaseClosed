# University Public Safety Records — static site for *Case Closed at UB*

Early-2000s campus public-safety intranet UI for the in-game desk monitor.

**Fiction notice:** The public site brands a made-up office — **University Public Safety Records (UPSR)** at fictional **Northbridge State University**. It is **not** University at Buffalo Police, UBPD, or any real law-enforcement agency. In-game / private UB flavor elsewhere is Magdy’s call; this hosted site stays clearly fictional.

**Static only** (HTML/CSS/JS). No backend. No secrets. No case-truth fields.

Drop this folder into a Unity browser / WebView package (or `StreamingAssets`) and point the monitor at `index.html`.

## Quick preview (no Unity)

```bash
cd /agent/ubpd-records
python3 -m http.server 8765 --bind 127.0.0.1
```

Open `http://127.0.0.1:8765/` — loads `data/sample-case.json` when no `#case=` hash is present.

Optional: click **Load sample case** on the desk page to also stuff the sample into `#case=<base64url>` (same path Unity uses).

---

## Unity → site: case payload

Unity injects public case data via the URL hash fragment produced by  
`CaseWebsitePayload` / `GetCurrentCaseHashFragment()`:

```text
…/index.html#case=<base64url>
```

### Decode steps (implemented in `js/app.js`)

1. Read `location.hash`, find the `case=` parameter (other params like `clue=` may coexist, joined with `&`).
2. Treat the value as **base64url** (`-`/`_` alphabet, padding optional).
3. Decode → UTF-8 JSON.
4. Validate minimally: `caseId`, `title`, `suspects[]`.
5. Persist in `sessionStorage` so multi-page navigation keeps the case without re-injecting.

### Exact public schema (schemaVersion 1)

```json
{
  "schemaVersion": 1,
  "caseId": "UB-0000000042",
  "title": "The Missing Mascot Head",
  "briefing": "...",
  "suspects": [
    {
      "id": "suspect_jamie",
      "name": "Jamie Rivera",
      "role": "Student volunteer",
      "statement": "...",
      "records": [
        {
          "id": "UB-0000000042_jamie_context",
          "title": "Event sign-in sheet",
          "description": "..."
        }
      ]
    }
  ]
}
```

| Field | Notes |
| --- | --- |
| `suspects[].id` | Stable suspect id — use as given |
| `records[].id` | Stable record id — use as given; this is what evidence signaling sends back |
| `records[].title` / `description` | Display only |

**Must not appear in this payload:** culprit id, clue tags / classifications, hidden explanations, scoring, answer/truth data. Unity owns those.

Sample file matching this schema: `data/sample-case.json` (fictional campus copy).

Optional live push without reload: `UBPDBridge.receiveCaseData(caseObject)`.

---

## Site → Unity: evidence signal

On the record detail page, **Add to evidence** calls the isolated bridge:

```js
UBPDBridge.sendEvidenceRecordId(record.id)
```

> **API naming:** `UBPDBridge`, `UBPD_onAddEvidence`, protocol `"ubpd"`, and related `UBPD*` JS globals are **kept for Unity compatibility**. Here **UBPD is a fictional protocol code**, not “University at Buffalo Police.” Visible site chrome uses **UPSR** (University Public Safety Records).

Unity should eventually route that string into:

```csharp
CaseWebsiteBridge.ReceiveEvidenceRecordId(recordId);
```

### Isolated hook (no hard dependency on a browser package)

`js/unity-bridge.js` tries, in order:

1. `window.UBPD_onAddEvidence(recordId, message)` — **preferred**; Magdy can assign this from the chosen WebView package.
2. `window.vuplex.postMessage(JSON.stringify(message))` — if Vuplex is present.
3. `window.Unity.call(JSON.stringify(message))` — generic stub some packs expose.
4. **Hash fallback:** set/update `location.hash` with `clue=<recordId>` (preserves existing `case=` when present). Unity can listen for URL/hash changes, then call `ReceiveEvidenceRecordId`.

Message shape when posting JSON:

```json
{
  "protocol": "ubpd",
  "version": 1,
  "action": "addToEvidence",
  "recordId": "UB-0000000042_jamie_access",
  "timestamp": "2026-11-07T21:00:00.000Z"
}
```

If nothing is wired, the site still works: toast confirms the click, hash fallback fires, no exceptions.

Suggested Unity wiring (package-agnostic):

```csharp
// From your WebView JS binding, once:
 // browser.ExecuteJS("window.UBPD_onAddEvidence = function(id, msg) { /* forward id */ };");
// Then forward `id` → CaseWebsiteBridge.ReceiveEvidenceRecordId(id);
```

---

## Pages

| File | Role |
| --- | --- |
| `index.html` | Case landing — title, briefing, suspect cards |
| `search.html` | Search by case id / title or suspect name / id / role |
| `suspect.html?id=<suspect.id>` | Dossier + record list (UI category tabs inferred from id/title text only — **not** from payload fields) |
| `record.html?id=<record.id>` | Record detail + **Add to evidence** |

Shared assets: `css/style.css`, `js/app.js`, `js/unity-bridge.js`, `js/chrome.js`.

---

## Folder layout (drop-in)

```text
ubpd-records/
  index.html
  search.html
  suspect.html
  record.html
  README.md
  css/style.css
  js/app.js
  js/unity-bridge.js
  js/chrome.js
  data/sample-case.json
```

GoDaddy zip: `/agent/dist/ubpd-records-godaddy.zip` — see Project docs `godaddy-deploy.md`.

---

## Open questions for Unity adapter

1. Which browser package wins (UnityWebBrowser vs Vuplex vs other)? Prefer implementing `window.UBPD_onAddEvidence` so the site does not care.
2. Does the package surface hash-change events reliably? Hash fallback is ready either way.
3. Should Unity clear `clue=` from the hash after consuming it, or leave it? Site can re-signal the same id (forces a brief `_ping` hash bump if unchanged).
4. Confirm `GetCurrentCaseHashFragment()` emits **base64url** (not standard base64). Site decoder expects url-safe alphabet.
