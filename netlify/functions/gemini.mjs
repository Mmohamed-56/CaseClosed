/**
 * POST /api/gemini
 * Proxies Unity interrogation prompts to Google Gemini.
 * Server injects GEMINI_API_KEY — never returned to the client.
 *
 * Request JSON (matches GeminiApiClient proxy path):
 * {
 *   "systemPrompt": string,
 *   "userPrompt": string,
 *   "model"?: string,            // default gemini-3.5-flash-lite
 *   "maxOutputTokens"?: number,  // default 60, clamp 40–120
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
/** Fastest live flash-lite for short suspect barks (2.5-* currently 503/unavailable). */
const DEFAULT_MODEL = "gemini-3.5-flash-lite";

/** Retired / overloaded model ids → snappy flash-lite default. */
const RETIRED_MODEL_MAP = {
  "gemini-2.0-flash": DEFAULT_MODEL,
  "gemini-2.0-flash-001": DEFAULT_MODEL,
  "gemini-2.0-flash-lite": DEFAULT_MODEL,
  "gemini-2.0-flash-lite-001": DEFAULT_MODEL,
  "gemini-1.5-flash": DEFAULT_MODEL,
  "gemini-1.5-flash-latest": DEFAULT_MODEL,
  "gemini-1.5-pro": DEFAULT_MODEL,
  // 3.8 is too slow / overloaded for interrogation (30s Netlify timeouts).
  "gemini-3.8-flash": DEFAULT_MODEL,
  "gemini-flash-latest": DEFAULT_MODEL,
};

function clamp(n, lo, hi, fallback) {
  const x = Number(n);
  if (!Number.isFinite(x)) return fallback;
  return Math.min(hi, Math.max(lo, x));
}

function resolveModel(requested) {
  const raw = String(requested || env("GEMINI_MODEL", DEFAULT_MODEL)).trim();
  const mapped = RETIRED_MODEL_MAP[raw.toLowerCase()] || raw;
  return mapped;
}

function isGemini25Family(modelId) {
  return /^gemini-2\.5-/i.test(modelId);
}

function extractModelText(responseJson) {
  try {
    const data = typeof responseJson === "string" ? JSON.parse(responseJson) : responseJson;
    const parts = data?.candidates?.[0]?.content?.parts;
    if (!Array.isArray(parts)) return "";
    return parts
      .map((p) => {
        if (p?.thought === true) return "";
        return typeof p?.text === "string" ? p.text : "";
      })
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

  const modelRaw = resolveModel(body.model);
  if (!/^gemini-[a-z0-9._-]+$/i.test(modelRaw)) {
    return json(400, { error: "Invalid model id" }, headers);
  }

  // Keep replies tiny — interrogation barks only.
  const maxOutputTokens = clamp(body.maxOutputTokens, 40, 120, 60);
  const temperature = clamp(body.temperature, 0, 1.5, 0.6);

  const url =
    "https://generativelanguage.googleapis.com/v1beta/models/" +
    encodeURIComponent(modelRaw) +
    ":generateContent?key=" +
    encodeURIComponent(apiKey);

  const generationConfig = {
    temperature,
    maxOutputTokens,
    responseMimeType: "application/json",
  };

  // 2.5 family: thinkingBudget 0 disables thinking (fast).
  // 3.x family: thinking cannot be off — use lowest level ("low"; "minimal" errors on 3.8).
  if (isGemini25Family(modelRaw)) {
    generationConfig.thinkingConfig = { thinkingBudget: 0 };
  } else {
    generationConfig.thinkingConfig = { thinkingLevel: "low" };
  }

  const payload = {
    system_instruction: { parts: [{ text: systemPrompt }] },
    contents: [{ role: "user", parts: [{ text: userPrompt }] }],
    generationConfig,
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
    let finishReason = "";
    try {
      finishReason = JSON.parse(raw)?.candidates?.[0]?.finishReason || "";
    } catch {
      /* ignore */
    }
    return json(
      502,
      {
        error: "Gemini returned empty payload",
        finishReason: finishReason || undefined,
        detail: raw.slice(0, 300),
      },
      headers
    );
  }

  return json(200, { text, model: modelRaw }, headers);
}
