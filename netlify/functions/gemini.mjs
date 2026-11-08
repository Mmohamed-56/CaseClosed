/**
 * POST /api/gemini
 * LEGACY / UNUSED by default gameplay (ScenarioDialogueClient owns ask turns).
 * Kept so Magdy can drop GEMINI_API_KEY later without a hard deploy break.
 * Proxies Unity interrogation prompts to Google Gemini when opt-in useGeminiApi is on.
 * Server injects GEMINI_API_KEY — never returned to the client.
 *
 * Request JSON (matches GeminiApiClient proxy path):
 * {
 *   "systemPrompt": string,
 *   "userPrompt": string,
 *   "model"?: string,            // default gemini-2.0-flash
 *   "maxOutputTokens"?: number,  // default 90, clamp 40–160
 *   "temperature"?: number       // default 0.6, clamp 0–1.5
 * }
 *
 * Response JSON: { "text": "<model text>" }  (Unity extracts this)
 * Or on failure: { "error": "..." }
 */

import {
  withGuards,
  parseJsonBody,
  json,
  env,
} from "./_shared.mjs";

const MAX_BODY = 24 * 1024; // 24 KB — short interrogation prompts only
const MAX_RPM = 40;

function clamp(n, lo, hi, fallback) {
  const x = Number(n);
  if (!Number.isFinite(x)) return fallback;
  return Math.min(hi, Math.max(lo, x));
}

function extractModelText(responseJson) {
  try {
    const data = typeof responseJson === "string" ? JSON.parse(responseJson) : responseJson;
    const parts = data?.candidates?.[0]?.content?.parts;
    if (!Array.isArray(parts)) return "";
    return parts
      .map((p) => (typeof p?.text === "string" ? p.text : ""))
      .join("")
      .trim();
  } catch {
    return "";
  }
}

export async function handler(event) {
  const gated = withGuards(event, {
    maxBytes: MAX_BODY,
    route: "gemini",
    maxRpm: MAX_RPM,
  });
  if (gated.early) return gated.early;
  const { headers } = gated;

  const apiKey = env("GEMINI_API_KEY");
  if (!apiKey) {
    return json(503, { error: "GEMINI_API_KEY not configured on host" }, headers);
  }

  let body;
  try {
    body = parseJsonBody(event);
  } catch {
    return json(400, { error: "Invalid JSON body" }, headers);
  }

  const systemPrompt = String(body.systemPrompt ?? "").slice(0, 12_000);
  const userPrompt = String(body.userPrompt ?? "").slice(0, 4_000);
  if (!systemPrompt || !userPrompt) {
    return json(
      400,
      { error: "systemPrompt and userPrompt are required" },
      headers
    );
  }

  const modelRaw = String(body.model || env("GEMINI_MODEL", "gemini-2.0-flash")).trim();
  // Allow only gemini-* model ids (block URL injection)
  if (!/^gemini-[a-z0-9._-]+$/i.test(modelRaw)) {
    return json(400, { error: "Invalid model id" }, headers);
  }

  const maxOutputTokens = clamp(body.maxOutputTokens, 40, 160, 90);
  const temperature = clamp(body.temperature, 0, 1.5, 0.6);

  const url =
    "https://generativelanguage.googleapis.com/v1beta/models/" +
    encodeURIComponent(modelRaw) +
    ":generateContent?key=" +
    encodeURIComponent(apiKey);

  const payload = {
    system_instruction: { parts: [{ text: systemPrompt }] },
    contents: [{ role: "user", parts: [{ text: userPrompt }] }],
    generationConfig: {
      temperature,
      maxOutputTokens,
      responseMimeType: "application/json",
    },
  };

  let upstream;
  try {
    upstream = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
  } catch (err) {
    return json(
      502,
      { error: "Gemini upstream unreachable", detail: String(err?.message || err) },
      headers
    );
  }

  const raw = await upstream.text();
  if (!upstream.ok) {
    return json(
      upstream.status >= 400 && upstream.status < 600 ? upstream.status : 502,
      {
        error: "Gemini upstream error",
        status: upstream.status,
        detail: raw.slice(0, 500),
      },
      headers
    );
  }

  const text = extractModelText(raw);
  if (!text) {
    return json(502, { error: "Gemini returned empty payload" }, headers);
  }

  return json(200, { text }, headers);
}
