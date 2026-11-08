# University Public Safety Records — static site for *Case Closed*

Early-2000s campus public-safety intranet UI for the in-game desk monitor.

**Fiction notice (dev):** Public chrome brands **University Public Safety Records (UPSR)** at fictional **Northbridge State University** — not a real law-enforcement agency. Protocol / JS globals keep the `UBPD*` names for Unity API stability.

**Static only** (HTML/CSS/JS). No backend secrets in this folder. Case truth stays in Unity.

## Archive sample (`data/sample-case.json`)

- **~100 subjects** in `suspects[]`: 4 flagged POIs, 6 Chizuru dialogue-bank related persons (`suspect_morgan` … `suspect_quinn` + bank-aligned `jamie`/`sam`), plus campus directory cards (`person_ns_*`).
- **Stable collectable record ids** (do not rename — `CollectEvidence` / `#clue=`):

```
UB-0000000042_jamie_{context,access,phone,purchase,social}
UB-0000000042_sam_{context,access,phone,purchase,social}
UB-0000000042_priya_{context,access,phone,social}
UB-0000000042_devon_{context,access,phone,purchase,social}
```

- Rebuild archive JSON: `node site/scripts/build-sample-case.mjs`

## Unity → site: case payload

`…/index.html#case=<base64url>` — schemaVersion 1 (`caseId`, `title`, `briefing`, `suspects[]` with `records[]`). Optional site-only flags on archive subjects: `poi`, `related` (ignored by Unity payload builder).

## Site → Unity: evidence signal

```js
UBPDBridge.sendEvidenceRecordId(record.id)
```

Fallback: `#clue=<recordId>`. Preferred host hook: `window.UBPD_onAddEvidence(recordId, message)`.

## Pages

| File | Role |
| --- | --- |
| `index.html` | Case desk — briefing + POI cards |
| `directory.html` | Full subject index (~100) |
| `search.html` | Search case / subjects |
| `suspect.html?id=` | Subject file + records |
| `record.html?id=` | Record detail + attach-to-case |

Live: https://ubcaseclosed.netlify.app/
