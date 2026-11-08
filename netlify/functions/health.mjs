/**
 * GET /api/health — liveness + which secrets are present (never values).
 */

import { corsHeaders, env, json } from "./_shared.mjs";

export async function handler(event) {
  const origin = event.headers?.origin || event.headers?.Origin || "";
  const headers = corsHeaders(origin);

  if (event.httpMethod === "OPTIONS") {
    return { statusCode: 204, headers, body: "" };
  }

  return json(
    200,
    {
      ok: true,
      service: "case-closed-api-proxy",
      geminiKey: Boolean(env("GEMINI_API_KEY")),
      elevenLabsKey: Boolean(env("ELEVENLABS_API_KEY")),
      appToken: Boolean(env("CASE_CLOSED_APP_TOKEN")),
    },
    headers
  );
}
