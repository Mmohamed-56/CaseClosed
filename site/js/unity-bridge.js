/**
 * Desk host bridge (protocol code "ubpd" — do not rename; CollectEvidence depends on it).
 * Public API: UBPDBridge.sendEvidenceRecordId / receiveCaseData / isConnected
 * Channels: UBPD_onAddEvidence → vuplex → Unity.call → #clue=<recordId>
 */
(function (global) {
  "use strict";

  var PROTOCOL = "ubpd";
  var VERSION = 1;

  function buildMessage(recordId) {
    return {
      protocol: PROTOCOL,
      version: VERSION,
      action: "addToEvidence",
      recordId: String(recordId),
      timestamp: new Date().toISOString()
    };
  }

  function tryCallback(recordId, message) {
    if (typeof global.UBPD_onAddEvidence === "function") {
      global.UBPD_onAddEvidence(recordId, message);
      return "callback:UBPD_onAddEvidence";
    }
    return null;
  }

  function tryVuplex(message) {
    try {
      if (global.vuplex && typeof global.vuplex.postMessage === "function") {
        global.vuplex.postMessage(JSON.stringify(message));
        return "vuplex.postMessage";
      }
    } catch (e) { /* ignore */ }
    return null;
  }

  function tryUnityCall(message) {
    try {
      if (global.Unity && typeof global.Unity.call === "function") {
        global.Unity.call(JSON.stringify(message));
        return "Unity.call";
      }
    } catch (e) { /* ignore */ }
    return null;
  }

  function setClueHash(recordId) {
    var id = encodeURIComponent(String(recordId));
    var hash = (global.location.hash || "").replace(/^#/, "");
    var parts = hash ? hash.split("&") : [];
    var kept = [];
    for (var i = 0; i < parts.length; i++) {
      var p = parts[i];
      if (!p) continue;
      if (p.indexOf("clue=") === 0) continue;
      kept.push(p);
    }
    kept.push("clue=" + id);
    var next = "#" + kept.join("&");
    if (global.location.hash === next) {
      global.location.hash = "#clue=_ping";
    }
    global.location.hash = next;
    return "hash";
  }

  function isConnected() {
    return !!(
      typeof global.UBPD_onAddEvidence === "function" ||
      (global.vuplex && typeof global.vuplex.postMessage === "function") ||
      (global.Unity && typeof global.Unity.call === "function")
    );
  }

  /**
   * Signal Unity that the player selected this public record for the evidence board.
   * @param {string} recordId value of record.id from the case payload
   * @returns {{ ok: boolean, channel: string, recordId: string, offline: boolean }}
   */
  function sendEvidenceRecordId(recordId) {
    if (recordId === undefined || recordId === null || recordId === "") {
      return { ok: false, channel: "none", recordId: "", offline: !isConnected() };
    }
    var id = String(recordId);
    var message = buildMessage(id);
    var channel =
      tryCallback(id, message) ||
      tryVuplex(message) ||
      tryUnityCall(message) ||
      setClueHash(id);

    var offline = channel === "hash" && !isConnected();

    try {
      if (typeof global.dispatchEvent === "function") {
        global.dispatchEvent(
          new CustomEvent("ubpd:evidence", {
            detail: { recordId: id, channel: channel, message: message }
          })
        );
      }
    } catch (e) { /* ignore */ }

    return { ok: true, channel: channel, recordId: id, offline: offline, message: message };
  }

  /** Optional: Unity may push a CaseWebsitePayload object without a full reload. */
  function receiveCaseData(caseData) {
    if (!caseData || typeof caseData !== "object") return false;
    global.__UBPD_CASE__ = caseData;
    try {
      global.sessionStorage.setItem("ubpd.case", JSON.stringify(caseData));
    } catch (e) { /* ignore */ }
    try {
      if (typeof global.dispatchEvent === "function") {
        global.dispatchEvent(new CustomEvent("ubpd:case", { detail: caseData }));
      }
    } catch (e) { /* ignore */ }
    return true;
  }

  global.UBPDBridge = {
    version: VERSION,
    protocol: PROTOCOL,
    sendEvidenceRecordId: sendEvidenceRecordId,
    /** @deprecated alias — prefer sendEvidenceRecordId */
    addToEvidence: sendEvidenceRecordId,
    receiveCaseData: receiveCaseData,
    isConnected: isConnected
  };
})(typeof window !== "undefined" ? window : this);
