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
            <a href="/index.html" class="app-header__title">Factory API Dashboard</a>
            <span class="app-badge app-badge--env">DEV</span>
          </div>

          <div class="app-header__right">
            <div class="app-header__hub" aria-live="polite">
              <span class="app-header__hub-label">SignalR</span>
              <span id="globalHubBadge" class="app-badge app-badge--neutral">Checking</span>
            </div>

            <div class="app-header__user">
              <span id="globalUserName">운영자: admin</span>
              <button id="globalCopyTokenBtn" class="app-btn app-btn--ghost" type="button">토큰 복사</button>
              <button id="globalLogoutBtn" class="app-btn app-btn--ghost" type="button">로그아웃</button>
            </div>
          </div>
        </div>

        <nav class="app-nav" aria-label="전역 메뉴">
          <a href="/index.html" class="app-nav__link ${active === "home" ? "is-active" : ""}">운영 메인</a>
          <a href="/camera-management.html" class="app-nav__link ${active === "camera" ? "is-active" : ""}">카메라 관리</a>
          <a href="/history.html" class="app-nav__link ${active === "history" ? "is-active" : ""}">이벤트 이력</a>
          <a href="/inventory.html" class="app-nav__link ${active === "inventory" ? "is-active" : ""}">재고 조회</a>
          <a href="${roiHref}" class="app-nav__link ${active === "roi" ? "is-active" : ""}">ROI 설정</a>
        </nav>
      </header>
    `;
  }

  function buildFooter() {
    return `
      <footer class="app-footer">
        <div class="app-footer__inner">
          <div>Factory API Dashboard · DEV</div>
          <div>ASP.NET Core + SignalR + OpenCV</div>
        </div>
      </footer>
    `;
  }

  function injectLayout() {
    const headerRoot = document.getElementById("app-header");
    const footerRoot = document.getElementById("app-footer");

    if (headerRoot) headerRoot.innerHTML = buildHeader();
    if (footerRoot) footerRoot.innerHTML = buildFooter();

    const token = localStorage.getItem("accessToken");
    const userName =
      localStorage.getItem("username") ||
      localStorage.getItem("userName") ||
      "admin";

    const userEl = document.getElementById("globalUserName");
    if (userEl) {
      userEl.textContent = `운영자: ${userName}`;
    }

    const copyTokenBtn = document.getElementById("globalCopyTokenBtn");
    if (copyTokenBtn) {
      copyTokenBtn.addEventListener("click", async () => {
        const currentToken = localStorage.getItem("accessToken") || "";
        if (!currentToken) {
          alert("복사할 토큰이 없습니다.");
          return;
        }

        try {
          await navigator.clipboard.writeText(currentToken);
          copyTokenBtn.textContent = "복사 완료";
          setTimeout(() => {
            copyTokenBtn.textContent = "토큰 복사";
          }, 1500);
        } catch (error) {
          console.error(error);
          alert("토큰 복사에 실패했습니다.");
        }
      });
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

    if (!token && !currentPath.includes("login")) {
      console.warn("accessToken not found");
    }
  }

  window.updateGlobalHubState = function(state, text) {
    const badge = document.getElementById("globalHubBadge");
    if (!badge) return;

    badge.className = "app-badge";
    if (state === "connected") {
      badge.classList.add("app-badge--ok");
      badge.textContent = text || "Connected";
    } else if (state === "reconnecting") {
      badge.classList.add("app-badge--warn");
      badge.textContent = text || "Reconnecting";
    } else if (state === "disconnected") {
      badge.classList.add("app-badge--danger");
      badge.textContent = text || "Disconnected";
    } else {
      badge.classList.add("app-badge--neutral");
      badge.textContent = text || "Unknown";
    }
  };

  document.addEventListener("DOMContentLoaded", injectLayout);
})();