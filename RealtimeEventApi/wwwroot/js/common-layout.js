(function() {
  const currentPath = location.pathname.toLowerCase();

  function getActiveKey() {
    if (currentPath.includes("camera-management")) return "camera";
    if (currentPath.includes("history")) return "history";
    if (currentPath.includes("inventory")) return "inventory";
    if (currentPath.includes("roi")) return "roi";
    return "home";
  }

  function getCurrentCameraId() {
    const select = document.getElementById("cameraSelect");
    if (select && select.value) return encodeURIComponent(select.value);

    const url = new URL(location.href);
    const q = url.searchParams.get("cameraId");
    return q ? encodeURIComponent(q) : "";
  }

  function buildHeader() {
    const active = getActiveKey();
    const cameraId = getCurrentCameraId();
    const roiHref = cameraId ? `/roi.html?cameraId=${cameraId}` : "/roi.html";

    return `
      <header class="app-header">
        <div class="app-header__bar">
          <div class="app-header__brand">
            <a href="/index.html" class="app-header__title">
              <img src="/logo/aims-logo.png" class="app-header__logo" alt="AIMS">
            </a>
            <span class="app-badge app-badge--env">System</span>
          </div>

          <div class="app-header__right">
            <div class="app-header__hub" aria-live="polite">
              <span class="app-header__hub-label">Live</span>
              <span id="globalHubBadge" class="app-badge app-badge--neutral">Syncing</span>
            </div>

            <div class="app-header__user">
              <span id="globalUserName">admin</span>
              <button id="globalLogoutBtn" class="app-btn app-btn--ghost" type="button">Logout</button>
            </div>
          </div>
        </div>

        <nav class="app-nav" aria-label="메인 메뉴">
          <a href="/index.html" class="app-nav__link ${active === "home" ? "is-active" : ""}">대시보드</a>
          <a href="/camera-management.html" class="app-nav__link ${active === "camera" ? "is-active" : ""}">카메라</a>
          <a href="/history.html" class="app-nav__link ${active === "history" ? "is-active" : ""}">이력분석</a>
          <a href="/inventory.html" class="app-nav__link ${active === "inventory" ? "is-active" : ""}">생산량</a>
          <a href="${roiHref}" class="app-nav__link ${active === "roi" ? "is-active" : ""}">AI 설정</a>
        </nav>
      </header>
    `;
  }

  function buildFooter() {
    return `
      <footer class="app-footer">
        <div class="app-footer__inner">
          <div>AIMS Factory Dashboard · Production</div>
          <div>Industrial Vision AI System</div>
        </div>
      </footer>
    `;
  }

  function injectLayout() {
    const headerRoot = document.getElementById("app-header");
    const footerRoot = document.getElementById("app-footer");

    if (headerRoot) headerRoot.innerHTML = buildHeader();
    if (footerRoot) footerRoot.innerHTML = buildFooter();

    const userName =
      localStorage.getItem("username") ||
      localStorage.getItem("userName") ||
      "admin";

    const userEl = document.getElementById("globalUserName");
    if (userEl) {
      userEl.textContent = userName;
    }

    const logoutBtn = document.getElementById("globalLogoutBtn");
    if (logoutBtn) {
      logoutBtn.addEventListener("click", () => {
          localStorage.removeItem("accessToken");
          localStorage.removeItem("username");
          localStorage.removeItem("userName");
          location.href = "/login.html";
      });
    }
  }

  window.updateGlobalHubState = function(state, text) {
    const badge = document.getElementById("globalHubBadge");
    if (!badge) return;

    badge.className = "app-badge";
    if (state === "connected") {
      badge.classList.add("app-badge--ok");
      badge.textContent = text || "Live";
    } else if (state === "reconnecting") {
      badge.classList.add("app-badge--warn");
      badge.textContent = text || "Wait";
    } else if (state === "disconnected") {
      badge.classList.add("app-badge--danger");
      badge.textContent = text || "Off";
    } else {
      badge.classList.add("app-badge--neutral");
      badge.textContent = text || "Sync";
    }
  };

  document.addEventListener("DOMContentLoaded", injectLayout);
})();