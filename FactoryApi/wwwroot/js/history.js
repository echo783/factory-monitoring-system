(function() {
    const cameraSelect = document.getElementById("cameraSelect");
    const startAt = document.getElementById("startAt");
    const endAt = document.getElementById("endAt");
    const btnQuery = document.getElementById("btnQuery");
    const btnReset = document.getElementById("btnReset");
    const resultCount = document.getElementById("resultCount");
    const loading = document.getElementById("loading");
    const eventsTableBody = document.getElementById("eventsTableBody");

    const snapshotModal = document.getElementById("snapshotModal");
    const snapshotModalClose = document.getElementById("snapshotModalClose");
    const snapshotModalImage = document.getElementById("snapshotModalImage");

    document.addEventListener("DOMContentLoaded", init);

    async function init() {
        bindEvents();
        setTodayRange();
        await loadCameraOptions();
        await fetchHistory();
    }

    function bindEvents() {
        btnQuery?.addEventListener("click", fetchHistory);

        btnReset?.addEventListener("click", async () => {
            if (cameraSelect) cameraSelect.value = "";
            setTodayRange();
            await fetchHistory();
        });

        snapshotModalClose?.addEventListener("click", closeSnapshotModal);

        snapshotModal?.addEventListener("click", (e) => {
            if (e.target?.dataset?.close === "true") {
                closeSnapshotModal();
            }
        });

        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape") closeSnapshotModal();
        });
    }

    function setTodayRange() {
        const now = new Date();
        const yyyy = now.getFullYear();
        const mm = String(now.getMonth() + 1).padStart(2, "0");
        const dd = String(now.getDate()).padStart(2, "0");
        const today = `${yyyy}-${mm}-${dd}`;

        if (startAt) startAt.value = today;
        if (endAt) endAt.value = today;
    }

    async function loadCameraOptions() {
        if (!cameraSelect) return;

        try {
            const token = localStorage.getItem("accessToken") || localStorage.getItem("token");

            const response = await fetch("/api/camera/list", {
                method: "GET",
                cache: "no-store",
                headers: {
                    "Authorization": token ? `Bearer ${token}` : ""
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const json = await response.json();
            const cameras = Array.isArray(json) ? json : (json.data || json.items || []);

            cameraSelect.innerHTML = `<option value="">모두</option>`;

            cameras.forEach((camera) => {
                const id = camera.cameraId ?? camera.id ?? "";
                const name = camera.cameraName ?? camera.name ?? `카메라 ${id}`;

                const option = document.createElement("option");
                option.value = String(id);
                option.textContent = name;
                cameraSelect.appendChild(option);
            });
        } catch (error) {
            console.error("카메라 목록 로드 실패:", error);
            cameraSelect.innerHTML = `<option value="">모두</option>`;
        }
    }

    async function fetchHistory() {
        setLoading(true);

        try {
            const query = buildQueryString();
            const response = await fetch(`/api/history/events?${query}`, { cache: "no-store" });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const items = await response.json();
            renderRows(Array.isArray(items) ? items : []);
        } catch (error) {
            console.error("이력 조회 실패:", error);
            renderError("조회 중 오류가 발생했습니다.");
        } finally {
            setLoading(false);
        }
    }

    function buildQueryString() {
        const params = new URLSearchParams();

        const cameraId = cameraSelect?.value?.trim();
        const startDate = startAt?.value?.trim();
        const endDate = endAt?.value?.trim();

        if (cameraId) params.append("cameraId", cameraId);
        if (startDate) params.append("from", `${startDate}T00:00:00`);
        if (endDate) params.append("to", `${endDate}T23:59:59`);

        return params.toString();
    }

    function setLoading(isLoading) {
        if (loading) loading.hidden = !isLoading;
    }

    function renderRows(items) {
        resultCount.textContent = items.length ? `${items.length}건` : "-";

        if (!items.length) {
            eventsTableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="empty-row">조회된 데이터가 없습니다.</td>
                </tr>
            `;
            return;
        }

        eventsTableBody.innerHTML = items.map((item) => {
            const occurredAt = item.eventTime || item.createdAt || "-";
            const cameraName = item.cameraName || `카메라 ${item.cameraId ?? "-"}`;
            const eventType = item.productName || "-";
            const message = `생산수량: ${item.productionCount ?? 0}`;
            const snapshotUrl = item.imagePath || "";

            return `
                <tr>
                    <td>${escapeHtml(formatDateTime(occurredAt))}</td>
                    <td>${escapeHtml(String(cameraName))}</td>
                    <td>${escapeHtml(String(eventType))}</td>
                    <td>${escapeHtml(String(message))}</td>
                    <td>
                        ${snapshotUrl
                    ? `<button type="button" class="snapshot-open-btn" data-snapshot="${escapeHtml(snapshotUrl)}">보기</button>`
                    : "-"
                }
                    </td>
                </tr>
            `;
        }).join("");

        bindSnapshotButtons();
    }

    function renderError(message) {
        eventsTableBody.innerHTML = `
            <tr>
                <td colspan="5" class="empty-row">${escapeHtml(message)}</td>
            </tr>
        `;
        resultCount.textContent = "-";
    }

    function bindSnapshotButtons() {
        document.querySelectorAll(".snapshot-open-btn").forEach((button) => {
            button.addEventListener("click", () => {
                const url = button.getAttribute("data-snapshot");
                openSnapshotModal(url);
            });
        });
    }

    function openSnapshotModal(url) {
        if (!snapshotModal || !snapshotModalImage) return;
        snapshotModalImage.src = url || "";
        snapshotModal.hidden = false;
        document.body.style.overflow = "hidden";
    }

    function closeSnapshotModal() {
        if (!snapshotModal || !snapshotModalImage) return;
        snapshotModal.hidden = true;
        snapshotModalImage.src = "";
        document.body.style.overflow = "";
    }

    function formatDateTime(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value || "-");

        const yyyy = date.getFullYear();
        const mm = String(date.getMonth() + 1).padStart(2, "0");
        const dd = String(date.getDate()).padStart(2, "0");
        const hh = String(date.getHours()).padStart(2, "0");
        const mi = String(date.getMinutes()).padStart(2, "0");
        const ss = String(date.getSeconds()).padStart(2, "0");

        return `${yyyy}-${mm}-${dd} ${hh}:${mi}:${ss}`;
    }

    function escapeHtml(value) {
        return String(value ?? "")
   .replaceAll("&", "&amp;")
   .replaceAll("<", "&lt;")
   .replaceAll(">", "&gt;")
   .replaceAll('"', "&quot;")
   .replaceAll("'", "&#039;");
    }
})();