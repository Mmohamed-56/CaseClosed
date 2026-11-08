/**
 * POST /api/tts
 * Proxies Unity TTS to ElevenLabs Flash streaming endpoint.
 * Server injects ELEVENLABS_API_KEY — never returned to the client.
 *
 * Request JSON (matches ElevenLabsVoiceClient proxy path):
 * {
 *   "voiceId": string,
 *   "text": string,
 *   "modelId"?: string,                   // default eleven_flash_v2_5
 *   "outputFormat"?: string,              // default mp3_22050_32
 *   "optimizeStreamingLatency"?: number,  // 0–4, default 4
 *   "stability"?: number,
 *   "similarityBoost"?: number
 * }
 *
 * Response: audio/mpeg bytes (base64-encoded in Netlify body with isBase64Encoded)
 */

import {
  withGuards,
  parseJsonBody,
  json,
  env,
  corsHeaders,
} from "./_shared.mjs";

const MAX_BODY = 8 * 1024; // 8 KB
const MAX_TEXT = 220; // match Unity TrimToSpeakable hard cap
const MAX_RPM = 60;

function clamp(n, lo, hi, fallback) {
  const x = Number(n);
  if (!Number.isFinite(x)) return fallback;
  return Math.min(hi, Math.max(lo, x));
}

export async function handler(event) {
  const gated = withGuards(event, {
    maxBytes: MAX_BODY,
    route: "tts",
    maxRpm: MAX_RPM,
  });
  if (gated.early) return gated.early;
  const { headers } = gated;

  const apiKey = env("ELEVENLABS_API_KEY");
  if (!apiKey) {
    return json(
      503,
      { error: "ELEVENLABS_API_KEY not configured on host" },
      headers
    );
  }

  let body;
  try {
    body = parseJsonBody(event);
  } catch {
    return json(400, { error: "Invalid JSON body" }, headers);
  }

  const voiceId = String(body.voiceId || "").trim();
  let text = String(body.text || "").trim();
  if (!voiceId || !text) {
    return json(400, { error: "voiceId and text are required" }, headers);
  }
  // Voice ids are alphanumeric / dash / underscore
  if (!/^[a-zA-Z0-9_-]{1,64}$/.test(voiceId)) {
    return json(400, { error: "Invalid voiceId" }, headers);
  }
  if (text.length > MAX_TEXT) {
    text = text.slice(0, MAX_TEXT).replace(/\s+$/, "") + "…";
  }

  const modelId = String(body.modelId || "eleven_flash_v2_5").trim();
  if (!/^eleven_[a-z0-9_]+$/i.test(modelId)) {
    return json(400, { error: "Invalid modelId" }, headers);
  }

  const outputFormat = String(body.outputFormat || "mp3_22050_32").trim();
  if (!/^[a-z0-9_]+$/i.test(outputFormat)) {
    return json(400, { error: "Invalid outputFormat" }, headers);
  }

  const optimize = clamp(body.optimizeStreamingLatency, 0, 4, 4);
  const stability = clamp(body.stability, 0, 1, 0.35);
  const similarityBoost = clamp(body.similarityBoost, 0, 1, 0.75);

  const url =
    `https://api.elevenlabs.io/v1/text-to-speech/${encodeURIComponent(voiceId)}/stream` +
    `?output_format=${encodeURIComponent(outputFormat)}` +
    `&optimize_streaming_latency=${optimize}`;

  const payload = {
    text,
    model_id: modelId,
    voice_settings: {
      stability,
      similarity_boost: similarityBoost,
      use_speaker_boost: false,
    },
  };

  let upstream;
  try {
    upstream = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "audio/mpeg",
        "xi-api-key": apiKey,
      },
      body: JSON.stringify(payload),
    });
  } catch (err) {
    return json(
      502,
      { error: "ElevenLabs upstream unreachable", detail: String(err?.message || err) },
      headers
    );
  }

  if (!upstream.ok) {
    const detail = (await upstream.text()).slice(0, 500);
    return json(
      upstream.status >= 400 && upstream.status < 600 ? upstream.status : 502,
      { error: "ElevenLabs upstream error", status: upstream.status, detail },
      headers
    );
  }

  const buf = Buffer.from(await upstream.arrayBuffer());
  const origin = event.headers?.origin || event.headers?.Origin || "";
  return {
    statusCode: 200,
    isBase64Encoded: true,
    headers: {
      ...corsHeaders(origin),
      "Content-Type": "audio/mpeg",
      "Cache-Control": "no-store",
    },
    body: buf.toString("base64"),
  };
}
