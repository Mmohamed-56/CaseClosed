/**
 * Shared abuse guards for Case Closed API proxy.
 * Secrets live only in Netlify env — never in this repo.
 */

const DEFAULT_ALLOWED_ORIGINS = [
  "https://ubcaseclosed.netlify.app",
  "http://localhost",
  "http://127.0.0.1",
  "null", // Unity standalone / file / WebView often sends Origin: null
];

/** In-memory sliding window (best-effort per function instance). */
const buckets = new Map();

export function env(name, fallback = "") {
  const v = process.env[name];
  return v == null || v === "" ? fallback : v;
}

export function allowedOrigins() {
  const raw = env("CORS_ALLOWLIST", "");
  const list = raw
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
  return list.length > 0 ? list : DEFAULT_ALLOWED_ORIGINS;
}

export function corsHeaders(origin) {
  const allow = allowedOrigins();
  const ok =
    !origin ||
    allow.includes("*") ||
    allow.includes(origin) ||
    (origin === "null" && allow.includes("null"));
  const headers = {
    "Access-Control-Allow-Methods": "POST, OPTIONS, GET",
    "Access-Control-Allow-Headers":
      "Content-Type, X-Case-Closed-Token, Authorization",
    "Access-Control-Max-Age": "86400",
    Vary: "Origin",
  };
  if (ok && origin) {
    headers["Access-Control-Allow-Origin"] = origin === "null" ? "*" : origin;
  } else if (allow.includes("*")) {
    headers["Access-Control-Allow-Origin"] = "*";
  } else {
    // Unity players often omit Origin — still allow response without ACAO for non-browser clients.
    headers["Access-Control-Allow-Origin"] = allow[0] || "*";
  }
  return headers;
}

export function json(statusCode, body, extraHeaders = {}) {
  return {
    statusCode,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store",
      ...extraHeaders,
    },
    body: JSON.stringify(body),
  };
}

export function preflight(event) {
  const origin = event.headers?.origin || event.headers?.Origin || "";
  return {
    statusCode: 204,
    headers: corsHeaders(origin),
    body: "",
  };
}

/**
 * Shared app token check.
 * Header: X-Case-Closed-Token: <token>
 * Or Authorization: Bearer <token>
 * If CASE_CLOSED_APP_TOKEN is unset, auth is skipped (dev only — Magdy must set it in prod).
 */
export function authorize(event) {
  const expected = env("CASE_CLOSED_APP_TOKEN", "");
  if (!expected) {
    return { ok: true, warn: "CASE_CLOSED_APP_TOKEN unset — auth skipped" };
  }

  const headers = event.headers || {};
  const fromHeader =
    headers["x-case-closed-token"] ||
    headers["X-Case-Closed-Token"] ||
    "";
  let fromAuth = headers.authorization || headers.Authorization || "";
  if (fromAuth.toLowerCase().startsWith("bearer ")) {
    fromAuth = fromAuth.slice(7).trim();
  } else {
    fromAuth = "";
  }

  const got = (fromHeader || fromAuth || "").trim();
  if (!got || got !== expected) {
    return { ok: false, statusCode: 401, error: "Unauthorized" };
  }
  return { ok: true };
}

export function clientIp(event) {
  const h = event.headers || {};
  const xf = h["x-forwarded-for"] || h["X-Forwarded-For"] || "";
  if (xf) return xf.split(",")[0].trim();
  return h["client-ip"] || h["x-nf-client-connection-ip"] || "unknown";
}

/**
 * Simple rate limit: max N requests per windowMs per IP+route.
 */
export function rateLimit(event, { max = 30, windowMs = 60_000, route = "api" } = {}) {
  const ip = clientIp(event);
  const key = `${route}:${ip}`;
  const now = Date.now();
  let bucket = buckets.get(key);
  if (!bucket || now - bucket.start >= windowMs) {
    bucket = { start: now, count: 0 };
    buckets.set(key, bucket);
  }
  bucket.count += 1;
  if (bucket.count > max) {
    return {
      ok: false,
      statusCode: 429,
      error: "Rate limit exceeded. Try again shortly.",
      retryAfter: Math.ceil((windowMs - (now - bucket.start)) / 1000),
    };
  }
  return { ok: true, remaining: max - bucket.count };
}

export function bodyTooLarge(event, maxBytes) {
  const len = Number(
    event.headers?.["content-length"] || event.headers?.["Content-Length"] || 0
  );
  if (len > maxBytes) return true;
  if (typeof event.body === "string" && event.body.length > maxBytes) return true;
  return false;
}

export function parseJsonBody(event) {
  if (!event.body) return {};
  let raw = event.body;
  if (event.isBase64Encoded) {
    raw = Buffer.from(raw, "base64").toString("utf8");
  }
  return JSON.parse(raw);
}

export function withGuards(event, { maxBytes, route, maxRpm }) {
  const origin = event.headers?.origin || event.headers?.Origin || "";
  const headers = corsHeaders(origin);

  if (event.httpMethod === "OPTIONS") {
    return { early: preflight(event) };
  }

  if (event.httpMethod !== "POST") {
    return {
      early: json(405, { error: "Method not allowed" }, headers),
    };
  }

  if (bodyTooLarge(event, maxBytes)) {
    return {
      early: json(413, { error: "Request body too large" }, headers),
    };
  }

  const auth = authorize(event);
  if (!auth.ok) {
    return {
      early: json(auth.statusCode, { error: auth.error }, headers),
    };
  }

  const rl = rateLimit(event, { max: maxRpm, windowMs: 60_000, route });
  if (!rl.ok) {
    return {
      early: json(
        rl.statusCode,
        { error: rl.error },
        { ...headers, "Retry-After": String(rl.retryAfter || 60) }
      ),
    };
  }

  return { headers, auth };
}
