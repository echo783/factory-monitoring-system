const cameraTable = document.getElementById("cameraTable");
const cameraTableBody = cameraTable ? cameraTable.querySelector("tbody") : null;
const listEmpty = document.getElementById("listEmpty");

const detailModal = document.getElementById("cameraDetailModal");
const detailId = document.getElementById("detailId");
const detailName = document.getElementById("detailName");
const detailRtsp = document.getElementById("detailRtsp");
const detailProduct = document.getElementById("detailProduct");
const detailEnabled = document.getElementById("detailEnabled");
const btnCloseDetail = document.getElementById("btnCloseDetail");
const btnDeleteCamera = document.getElementById("btnDeleteCamera");

let selectedCameraId = null;

const newForm = document.getElementById("newForm");
const inName = document.getElementById("inName");
const inRtsp = document.getElementById("inRtsp");
const inProduct = document.getElementById("inProduct");
const inEnabled = document.getElementById("inEnabled");
const btnSave = document.getElementById("btnSave");
const btnCancel = document.getElementById("btnCancel");
const formMsg = document.getElementById("formMsg");

function authHeaders(extra = {}) {
    const token = localStorage.getItem("accessToken");
    return {
        "Authorization": "Bearer " + token,
        ...extra
    };
}

function redirectToLogin() {
    location.href = "/login.html";
}

async function handleUnauthorized(res) {
    if (res.status === 401) {
        alert("로그인이 필요합니다.");
        redirectToLogin();
        return true;
    }
    return false;
}

function setListEmpty(show, text = "등록된 카메라가 없습니다.") {
    if (!listEmpty) return;
    listEmpty.hidden = !show;
    listEmpty.textContent = text;
}

function setFormMessage(msg, isError = false) {
    if (!formMsg) return;
    formMsg.textContent = msg;
    formMsg.style.color = isError ? "#dc2626" : "#0f766e";
}

function clearFormMessage() {
    setFormMessage("");
}

function openDetailModal() {
    if (!detailModal) return;
    detailModal.hidden = false;
    document.body.style.overflow = "hidden";
}

function closeDetailModal() {
    if (!detailModal) return;
    detailModal.hidden = true;
    document.body.style.overflow = "";
}

function escapeHtml(value) {
    return String(value ?? "-")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function renderCameraRow(camera) {
    const tr = document.createElement("tr");

    const idTd = document.createElement("td");
    idTd.textContent = camera.cameraId ?? "-";

    const nameTd = document.createElement("td");
    nameTd.textContent = camera.cameraName ?? "-";

    const productTd = document.createElement("td");
    productTd.textContent = camera.productName ?? "-";

    const enabledTd = document.createElement("td");
    enabledTd.textContent = camera.enabled ? "true" : "false";

    const rtspTd = document.createElement("td");
    rtspTd.className = "rtsp-cell";
    const rtspValue = camera.rtspUrl && String(camera.rtspUrl).trim() ? camera.rtspUrl : "-";
    rtspTd.innerHTML = `<span class="rtsp-ellipsis" title="${escapeHtml(rtspValue)}">${escapeHtml(rtspValue)}</span>`;

    tr.appendChild(idTd);
    tr.appendChild(nameTd);
    tr.appendChild(productTd);
    tr.appendChild(enabledTd);
    tr.appendChild(rtspTd);
    tr.appendChild(actionsTd);

    tr.addEventListener("click", () => loadCameraDetail(camera.cameraId));
    return tr;
}

async function enrichCameraList(cameras) {
    const enriched = await Promise.all(cameras.map(async (camera) => {
        if (camera.rtspUrl && String(camera.rtspUrl).trim()) return camera;
        try {
            const detailRes = await fetch(`/api/camera/${camera.cameraId}`, { headers: authHeaders() });
            if (!detailRes.ok) return camera;
            const detail = await detailRes.json();
            return { ...camera, rtspUrl: detail.rtspUrl ?? camera.rtspUrl ?? "-" };
        } catch {
            return camera;
        }
    }));
    return enriched;
}

async function loadCameraList() {
    try {
        setListEmpty(false);
        if (cameraTableBody) cameraTableBody.innerHTML = "";

        const res = await fetch("/api/camera/list", {
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        if (!res.ok) throw new Error("카메라 목록 조회 실패");

        const data = await res.json();

        if (!Array.isArray(data) || data.length === 0) {
            setListEmpty(true, "등록된 카메라가 없습니다.");
            return;
        }

        const enrichedData = await enrichCameraList(data);
        enrichedData.forEach(camera => {
            cameraTableBody.appendChild(renderCameraRow(camera));
        });
    } catch (err) {
        console.error(err);
        setListEmpty(true, "카메라 목록을 불러오지 못했습니다.");
    }
}

function renderCameraDetail(camera) {
    selectedCameraId = camera.cameraId ?? null;
    detailId.textContent = camera.cameraId ?? "-";
    detailName.textContent = camera.cameraName ?? "-";
    detailRtsp.textContent = "";

    if (camera.rtspUrl) {
        const value = document.createElement("div");
        value.textContent = camera.rtspUrl;
        detailRtsp.appendChild(value);
    } else {
        detailRtsp.textContent = "-";
    }

    detailProduct.textContent = camera.productName ?? "-";
    detailEnabled.textContent = camera.enabled ? "true" : "false";
    openDetailModal();
}

async function loadCameraDetail(id) {
    try {
        const res = await fetch(`/api/camera/${id}`, {
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        if (!res.ok) throw new Error("카메라 상세 조회 실패");

        const data = await res.json();
        renderCameraDetail(data);
    } catch (err) {
        console.error(err);
        alert("카메라 상세 정보를 불러오지 못했습니다.");
    }
}


async function tryDeleteRequest(url, options) {
    const res = await fetch(url, {
        ...options,
        headers: authHeaders({
            "Content-Type": "application/json",
            ...(options?.headers || {})
        })
    });

    if (await handleUnauthorized(res)) return { unauthorized: true, ok: false, res };
    return { ok: res.ok, status: res.status, res };
}

async function deleteCamera(cameraId, cameraName = "") {
    if (cameraId == null) return;
    const targetName = cameraName || `ID ${cameraId}`;
    const confirmed = confirm(`카메라 ${targetName} 을(를) 삭제하시겠습니까?`);
    if (!confirmed) return;

    const candidates = [
        { url: `/api/camera/${cameraId}`, options: { method: "DELETE" } },
        { url: `/api/camera/delete/${cameraId}`, options: { method: "DELETE" } },
        { url: `/api/camera/delete/${cameraId}`, options: { method: "POST" } },
        { url: `/api/camera/${cameraId}/delete`, options: { method: "POST" } }
    ];

    try {
        let lastResponse = null;
        for (const candidate of candidates) {
            const result = await tryDeleteRequest(candidate.url, candidate.options);
            if (result.unauthorized) return;
            lastResponse = result;
            if (result.ok) {
                closeDetailModal();
                clearFormMessage();
                setFormMessage("카메라가 삭제되었습니다.");
                await loadCameraList();
                return;
            }
            if (![404, 405].includes(result.status)) break;
        }

        let message = "카메라 삭제에 실패했습니다.";
        if (lastResponse?.res) {
            try {
                const text = await lastResponse.res.text();
                if (text) message = text;
            } catch {}
        }
        alert(message);
    } catch (err) {
        console.error(err);
        alert("카메라 삭제 중 오류가 발생했습니다.");
    }
}

async function saveCamera(e) {
    e.preventDefault();
    clearFormMessage();

    const payload = {
        cameraName: inName.value.trim(),
        rtspUrl: inRtsp.value.trim(),
        productName: inProduct.value.trim(),
        enabled: inEnabled.value === "true"
    };

    if (!payload.cameraName) {
        setFormMessage("카메라 이름을 입력하세요.", true);
        return;
    }

    if (!payload.rtspUrl) {
        setFormMessage("RTSP URL을 입력하세요.", true);
        return;
    }

    if (!payload.productName) {
        setFormMessage("제품명을 입력하세요.", true);
        return;
    }

    try {
        btnSave.disabled = true;

        const res = await fetch("/api/camera/add", {
            method: "POST",
            headers: authHeaders({
                "Content-Type": "application/json"
            }),
            body: JSON.stringify(payload)
        });

        if (await handleUnauthorized(res)) return;

        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || "카메라 등록 실패");
        }

        const result = await res.json();
        setFormMessage("카메라가 등록되었습니다.");

        newForm.reset();
        inEnabled.value = "true";

        await loadCameraList();
    } catch (err) {
        console.error(err);
        setFormMessage(err.message || "카메라 등록 중 오류가 발생했습니다.", true);
    } finally {
        btnSave.disabled = false;
    }
}

document.addEventListener("DOMContentLoaded", async () => {
    const token = localStorage.getItem("accessToken");
    if (!token) {
        redirectToLogin();
        return;
    }

    if (window.updateGlobalHubState) {
        window.updateGlobalHubState("connected", "Ready");
    }

    if (btnCancel) {
        btnCancel.addEventListener("click", () => {
            newForm.reset();
            inEnabled.value = "true";
            clearFormMessage();
            inName.focus();
        });
    }

    if (newForm) {
        newForm.addEventListener("submit", saveCamera);
    }

    if (btnCloseDetail) {
        btnCloseDetail.addEventListener("click", closeDetailModal);
    }

    if (btnDeleteCamera) {
        btnDeleteCamera.addEventListener("click", async () => {
            const name = detailName?.textContent?.trim() || "";
            await deleteCamera(selectedCameraId, name);
        });
    }

    if (detailModal) {
        detailModal.addEventListener("click", (event) => {
            if (event.target === detailModal) {
                closeDetailModal();
            }
        });
    }

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && detailModal && !detailModal.hidden) {
            closeDetailModal();
        }
    });

    await loadCameraList();
});
