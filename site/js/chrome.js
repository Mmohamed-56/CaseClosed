/**
 * Shared chrome helpers for multi-page static shell.
 */
(function (global) {
  "use strict";

  function pageName() {
    var path = (global.location.pathname || "").split("/").pop() || "index.html";
    return path.toLowerCase();
  }

  function navHtml(active) {
    var items = [
      { href: "index.html", id: "index.html", label: "Case desk" },
      { href: "search.html", id: "search.html", label: "Search" }
    ];
    var html = "";
    for (var i = 0; i < items.length; i++) {
      var it = items[i];
      var cls = it.id === active ? ' class="active"' : "";
      html += '<a href="' + it.href + '"' + cls + ">" + it.label + "</a>";
      if (i < items.length - 1) html += '<span class="sep">|</span>';
    }
    return html;
  }

  function ensureShell() {
    if (document.querySelector(".topbar")) return;

    var top =
      '<header class="topbar">' +
      '  <a class="brand" href="index.html">' +
      '    <div class="badge">UPSR</div>' +
      '    <div class="brand-text">' +
      '      <div class="brand-name">University Public Safety — Records</div>' +
      '      <div class="brand-sub">Fictional campus intranet · Night Shift Desk</div>' +
      "    </div>" +
      "  </a>" +
      '  <div class="topmeta">' +
      "    <div>SYSTEM: UPSR-RECORDS-V1</div>" +
      '    <div id="clock">--</div>' +
      "  </div>" +
      "</header>" +
      '<nav class="navstrip" id="navstrip"></nav>' +
      '<div class="statusline">' +
      '  <span id="active-case">NO CASE LOADED</span>' +
      "  <span>" +
      '    <span id="data-source">DATA: —</span> · ' +
      '    <span id="conn-status">UNITY LINK: —</span>' +
      "  </span>" +
      "</div>";

    var footer =
      '<footer class="footer">' +
      "  <span>Authorized training use only. Fictional public-safety records — not a real police agency.</span>" +
      "  <span>Case Closed · desk monitor build</span>" +
      "</footer>" +
      '<div id="toast" class="toast" role="status" aria-live="polite"></div>' +
      '<div id="loader" class="loader" aria-hidden="true">' +
      '  <div class="loader-box">' +
      "    <h2>UPSR Mainframe Query</h2>" +
      '    <div class="bar-track"><div id="loader-fill" class="bar-fill"></div></div>' +
      '    <div id="loader-msg" class="loader-msg">Querying campus records mainframe...</div>' +
      "  </div>" +
      "</div>";

    document.body.insertAdjacentHTML("afterbegin", top);
    document.body.insertAdjacentHTML("beforeend", footer);
    var nav = document.getElementById("navstrip");
    if (nav) nav.innerHTML = navHtml(pageName());
  }

  global.UBPDChrome = { ensureShell: ensureShell, pageName: pageName };
})(typeof window !== "undefined" ? window : this);
