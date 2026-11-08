/**
 * University Public Safety Records app — loads CaseWebsitePayload from #case=<base64url>
 * or local fallback JSON. No truth / culprit / scoring data is interpreted here.
 * JS globals keep the UBPD* prefix for Unity API stability (fictional code, not a real agency).
 */
(function (global) {
  "use strict";

  var SAMPLE_URL = "data/sample-case.json";
  var STORAGE_KEY = "ubpd.case";
  var TYPE_LABELS = {
    access: "Access logs",
    purchase: "Purchases",
    phone: "Phone activity",
    social: "Social posts",
    context: "Case context",
    other: "Other records"
  };

  function showToast(text) {
    var el = document.getElementById("toast");
    if (!el) {
      el = document.createElement("div");
      el.id = "toast";
      el.className = "toast";
      document.body.appendChild(el);
    }
    el.textContent = text;
    el.classList.add("show");
    clearTimeout(showToast._t);
    showToast._t = setTimeout(function () {
      el.classList.remove("show");
    }, 2800);
  }

  function setStatus(html) {
    var el = document.getElementById("conn-status");
    if (el) el.innerHTML = html;
  }

  function updateConnectionStatus() {
    var bridge = global.UBPDBridge;
    if (!bridge) {
      setStatus('<span class="warn">BRIDGE N/A</span>');
      return;
    }
    if (bridge.isConnected()) {
      setStatus('<span class="ok">UNITY LINK: ACTIVE</span>');
    } else {
      setStatus('<span class="warn">UNITY LINK: OFFLINE</span> <span>(hash fallback)</span>');
    }
  }

  /* --- base64url --- */
  function base64UrlToBytes(b64url) {
    var s = String(b64url).replace(/-/g, "+").replace(/_/g, "/");
    var pad = s.length % 4;
    if (pad) s += new Array(5 - pad).join("=");
    var bin = global.atob(s);
    var bytes = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return bytes;
  }

  function bytesToUtf8(bytes) {
    if (typeof TextDecoder !== "undefined") {
      return new TextDecoder("utf-8").decode(bytes);
    }
    var out = "";
    for (var i = 0; i < bytes.length; i++) out += String.fromCharCode(bytes[i]);
    try {
      return decodeURIComponent(escape(out));
    } catch (e) {
      return out;
    }
  }

  function decodeCaseHashFragment(hash) {
    var raw = (hash || "").replace(/^#/, "");
    if (!raw) return null;
    var parts = raw.split("&");
    for (var i = 0; i < parts.length; i++) {
      var p = parts[i];
      var eq = p.indexOf("=");
      if (eq < 0) continue;
      var key = p.slice(0, eq);
      var val = p.slice(eq + 1);
      if (key === "case" && val) {
        try {
          var json = bytesToUtf8(base64UrlToBytes(decodeURIComponent(val)));
          return JSON.parse(json);
        } catch (e) {
          console.warn("UPSR: failed to decode #case= fragment", e);
          return null;
        }
      }
    }
    return null;
  }

  function utf8ToBase64Url(str) {
    var bytes;
    if (typeof TextEncoder !== "undefined") {
      bytes = new TextEncoder().encode(str);
    } else {
      var uni = unescape(encodeURIComponent(str));
      bytes = new Uint8Array(uni.length);
      for (var i = 0; i < uni.length; i++) bytes[i] = uni.charCodeAt(i);
    }
    var bin = "";
    for (var j = 0; j < bytes.length; j++) bin += String.fromCharCode(bytes[j]);
    return global
      .btoa(bin)
      .replace(/\+/g, "-")
      .replace(/\//g, "_")
      .replace(/=+$/, "");
  }

  function encodeCaseToHash(caseData) {
    return "#case=" + utf8ToBase64Url(JSON.stringify(caseData));
  }

  function validatePayload(data) {
    if (!data || typeof data !== "object") return false;
    if (!data.caseId || !data.title) return false;
    if (!Array.isArray(data.suspects)) return false;
    return true;
  }

  function persist(data) {
    global.__UBPD_CASE__ = data;
    try {
      global.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(data));
    } catch (e) { /* ignore */ }
  }

  function readPersisted() {
    try {
      var raw = global.sessionStorage.getItem(STORAGE_KEY);
      if (!raw) return null;
      var data = JSON.parse(raw);
      return validatePayload(data) ? data : null;
    } catch (e) {
      return null;
    }
  }

  function fetchSample() {
    return fetch(SAMPLE_URL, { cache: "no-store" }).then(function (res) {
      if (!res.ok) throw new Error("sample HTTP " + res.status);
      return res.json();
    });
  }

  /**
   * Resolve case data: #case= wins, then in-memory/session, then sample JSON.
   */
  function loadCaseData() {
    var fromHash = decodeCaseHashFragment(global.location.hash);
    if (fromHash && validatePayload(fromHash)) {
      persist(fromHash);
      return Promise.resolve({ data: fromHash, source: "hash" });
    }
    if (global.__UBPD_CASE__ && validatePayload(global.__UBPD_CASE__)) {
      return Promise.resolve({ data: global.__UBPD_CASE__, source: "memory" });
    }
    var stored = readPersisted();
    if (stored) {
      global.__UBPD_CASE__ = stored;
      return Promise.resolve({ data: stored, source: "session" });
    }
    return fetchSample().then(function (data) {
      if (!validatePayload(data)) throw new Error("invalid sample payload");
      persist(data);
      return { data: data, source: "sample" };
    });
  }

  function qs(name) {
    var q = new URLSearchParams(global.location.search);
    return q.get(name);
  }

  function peopleLists(caseData) {
    var suspects = caseData && caseData.suspects ? caseData.suspects : [];
    var affiliates = caseData && caseData.affiliates ? caseData.affiliates : [];
    return suspects.concat(affiliates);
  }

  function findSuspect(caseData, suspectId) {
    var list = peopleLists(caseData);
    for (var i = 0; i < list.length; i++) {
      if (list[i].id === suspectId) return list[i];
    }
    return null;
  }

  function findRecord(caseData, recordId) {
    var list = peopleLists(caseData);
    for (var i = 0; i < list.length; i++) {
      var s = list[i];
      var recs = s.records || [];
      for (var j = 0; j < recs.length; j++) {
        if (recs[j].id === recordId) {
          return { record: recs[j], suspect: s };
        }
      }
    }
    return null;
  }

  /**
   * Soft category for UI tabs only — derived from record id / title keywords.
   * Does NOT come from Unity payload (payload has no category field).
   */
  function inferCategory(record) {
    var blob = ((record && record.id) || "") + " " + ((record && record.title) || "");
    blob = blob.toLowerCase();
    if (/\b(access|keycard|turnstile|door|badge|reader)\b/.test(blob)) return "access";
    if (/\b(purchase|vend|card|hardware|bought|terminal)\b/.test(blob)) return "purchase";
    if (/\b(phone|sms|text|call|dispatch|tower)\b/.test(blob)) return "phone";
    if (/\b(social|campusconnect|post|story|@)\b/.test(blob)) return "social";
    if (/\b(context|sign-in|log|roster|attendance|sheet|close-out|route)\b/.test(blob)) {
      return "context";
    }
    return "other";
  }

  function categoryLabel(key) {
    return TYPE_LABELS[key] || TYPE_LABELS.other;
  }

  function initials(name) {
    if (!name) return "?";
    var parts = String(name).trim().split(/\s+/);
    var a = parts[0] ? parts[0].charAt(0) : "";
    var b = parts.length > 1 ? parts[parts.length - 1].charAt(0) : "";
    return (a + b).toUpperCase() || "?";
  }

  function avatarColor(id) {
    var palette = ["#3a5a7c", "#5c4a6e", "#4a6b4a", "#7a4a3a", "#2f5d6e", "#6b4a2f"];
    var h = 0;
    var s = String(id || "");
    for (var i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
    return palette[h % palette.length];
  }

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function formatClock() {
    var d = new Date();
    function z(n) {
      return n < 10 ? "0" + n : String(n);
    }
    return (
      d.getFullYear() +
      "-" +
      z(d.getMonth() + 1) +
      "-" +
      z(d.getDate()) +
      " " +
      z(d.getHours()) +
      ":" +
      z(d.getMinutes()) +
      ":" +
      z(d.getSeconds())
    );
  }

  function paintChrome(caseData, source) {
    var caseEl = document.getElementById("active-case");
    if (caseEl && caseData) {
      caseEl.textContent = caseData.caseId + " — " + caseData.title;
    }
    var srcEl = document.getElementById("data-source");
    if (srcEl) {
      srcEl.textContent = "DATA: " + (source || "?").toUpperCase();
    }
    var clock = document.getElementById("clock");
    if (clock) {
      clock.textContent = formatClock();
      setInterval(function () {
        clock.textContent = formatClock();
      }, 1000);
    }
    updateConnectionStatus();
  }

  /* fake mainframe delay — skipped when ?demo=1 or demoMode on payload (optional) */
  function maybeQueryDelay(caseData) {
    var params = new URLSearchParams(global.location.search);
    if (params.get("demo") === "1") return Promise.resolve();
    if (caseData && caseData.demoMode === true) return Promise.resolve();
    if (params.get("fast") === "1") return Promise.resolve();
    // Only show on search submit flows that opt in via data-slow-query
    return Promise.resolve();
  }

  function runLoader(ms) {
    ms = ms || 1200;
    var loader = document.getElementById("loader");
    var fill = document.getElementById("loader-fill");
    var msg = document.getElementById("loader-msg");
    if (!loader) return Promise.resolve();
    loader.classList.add("show");
    var start = Date.now();
    return new Promise(function (resolve) {
      function tick() {
        var t = Math.min(1, (Date.now() - start) / ms);
        if (fill) fill.style.width = Math.round(t * 100) + "%";
        if (msg) {
          var lines = [
            "Querying campus records mainframe...",
            "Resolving badge index...",
            "Fetching public records slice...",
            "Almost there (probably)..."
          ];
          msg.textContent = lines[Math.min(lines.length - 1, Math.floor(t * lines.length))];
        }
        if (t < 1) {
          requestAnimationFrame(tick);
        } else {
          loader.classList.remove("show");
          resolve();
        }
      }
      requestAnimationFrame(tick);
    });
  }

  function sendEvidence(recordId) {
    var bridge = global.UBPDBridge;
    if (!bridge || typeof bridge.sendEvidenceRecordId !== "function") {
      showToast("Evidence hook unavailable — record id: " + recordId);
      return;
    }
    var result = bridge.sendEvidenceRecordId(recordId);
    if (result.ok) {
      if (result.offline) {
        showToast("Added to evidence board (offline signal: #" + recordId + ")");
      } else {
        showToast("Added to evidence board.");
      }
    } else {
      showToast("Could not signal evidence id.");
    }
    updateConnectionStatus();
  }

  function searchCase(caseData, query) {
    var q = String(query || "").trim().toLowerCase();
    if (!q) return { caseMatch: false, suspects: [] };
    var caseMatch =
      String(caseData.caseId || "").toLowerCase().indexOf(q) >= 0 ||
      String(caseData.title || "").toLowerCase().indexOf(q) >= 0;
    var suspects = peopleLists(caseData).filter(function (s) {
      return (
        String(s.name || "").toLowerCase().indexOf(q) >= 0 ||
        String(s.id || "").toLowerCase().indexOf(q) >= 0 ||
        String(s.role || "").toLowerCase().indexOf(q) >= 0 ||
        String(s.statement || "").toLowerCase().indexOf(q) >= 0
      );
    });
    return { caseMatch: caseMatch, suspects: suspects, query: query };
  }

  global.UBPDApp = {
    loadCaseData: loadCaseData,
    decodeCaseHashFragment: decodeCaseHashFragment,
    encodeCaseToHash: encodeCaseToHash,
    validatePayload: validatePayload,
    findSuspect: findSuspect,
    findRecord: findRecord,
    inferCategory: inferCategory,
    categoryLabel: categoryLabel,
    initials: initials,
    avatarColor: avatarColor,
    escapeHtml: escapeHtml,
    paintChrome: paintChrome,
    qs: qs,
    showToast: showToast,
    sendEvidence: sendEvidence,
    searchCase: searchCase,
    runLoader: runLoader,
    maybeQueryDelay: maybeQueryDelay,
    updateConnectionStatus: updateConnectionStatus,
    TYPE_LABELS: TYPE_LABELS
  };

  global.addEventListener("ubpd:case", function () {
    updateConnectionStatus();
  });
})(typeof window !== "undefined" ? window : this);
