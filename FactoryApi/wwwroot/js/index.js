const accessToken = localStorage.getItem("accessToken");
let cameraId = 1;

const cameraSelectEl = document.getElementById("cameraSelect");
const eventLogEl = document.getElementById("eventLog");

const btnStartEl = document.getElementById("btnStart");
const btnStopEl = document.getElementById("btnStop");
const btnQuickPrevEl = document.getElementById("btnQuickPrev");
const btnQuickNextEl = document.getElementById("btnQuickNext");

const cameraStatusEl = document.getElementById("cameraStatus");

const liveBadgeEl = document.getElementById("liveBadge");
const liveNameEl = document.getElementById("liveName");
const liveIdEl = document.getElementById("liveId");
const liveMsgEl = document.getElementById("liveMsg");

let signalRConnection = null;
let joinedCameraGroupId = null;
const MAX_EVENT_LOG = 20;

function redirectToLogin() {
    location.href = "/login.html";
}

function ensureLoggedIn() {
    if (!accessToken) {
        alert("로그인이 필요합니다.");
        redirectToLogin();
        return false;
    }
    return true;
}

function authHeaders(extra = {}) {
    return {
        "Authorization": "Bearer " + accessToken,
        ...extra
    };
}

async function handleUnauthorized(res) {
    if (res.status === 401) {
        alert("세션 만료. 다시 로그인하세요.");
        redirectToLogin();
        return true;
    }
    return false;
}

function formatLocalDateTime(value) {
    if (!value) return "-";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "-";
    return date.toLocaleString();
}



function setButtonBusy(button, isBusy, busyText) {
    if (!(button instanceof HTMLButtonElement)) return;

    if (!button.dataset.originalText) {
        button.dataset.originalText = button.textContent || "";
    }

    if (isBusy) {
        button.disabled = true;
        button.classList.add("btn-loading");
        if (busyText) button.textContent = busyText;
        return;
    }

    button.disabled = false;
    button.classList.remove("btn-loading");
    button.textContent = button.dataset.originalText || button.textContent || "";
}

function pushEventLog(message) {
    if (!(eventLogEl instanceof HTMLElement)) return;

    const item = document.createElement("li");
    item.textContent = `[${new Date().toLocaleTimeString()}] ${message}`;
    eventLogEl.prepend(item);

    while (eventLogEl.childElementCount > MAX_EVENT_LOG) {
        eventLogEl.removeChild(eventLogEl.lastElementChild);
    }
}

function updateDashboard(data) {
    if (liveNameEl) liveNameEl.textContent = data.cameraName || "-";
    if (liveIdEl) liveIdEl.textContent = "ID: " + (data.cameraId || "-");

    if (liveBadgeEl) {
        const status = (data.status || "-").toString();
        liveBadgeEl.textContent = status;
        liveBadgeEl.className = "status-pill";
        if (status === "Running") {
            liveBadgeEl.classList.add("status-pill--running");
        } else if (status === "Stopped") {
            liveBadgeEl.classList.add("status-pill--stopped");
        } else {
            liveBadgeEl.classList.add("status-pill--neutral");
        }
    }

    if (liveMsgEl) liveMsgEl.textContent = data.message || "-";
}

function renderCameraStatus(data) {
    if (!cameraStatusEl) return;

    const status = data.status || "Unknown";
    const changedAt = formatLocalDateTime(data.changedAt);

    cameraStatusEl.innerHTML = `
        <div class="camera-status__header">
            <span class="camera-status__label">상태</span>
            <span class="status-badge ${status === "Running" ? "status--running" : status === "Stopped" ? "status--stopped" : status === "Error" ? "status--error" : "status--warn"}">${status}</span>
        </div>
        <div class="camera-status__meta">
            <div class="camera-status__meta-item">
                <span class="camera-status__meta-label">CameraId</span>
                <span class="camera-status__meta-value">${data.cameraId}</span>
            </div>
            <div class="camera-status__meta-item">
                <span class="camera-status__meta-label">CameraName</span>
                <span class="camera-status__meta-value">${data.cameraName}</span>
            </div>
            <div class="camera-status__meta-item">
                <span class="camera-status__meta-label">Enabled</span>
                <span class="camera-status__meta-value">${data.enabled}</span>
            </div>
            <div class="camera-status__meta-item">
                <span class="camera-status__meta-label">ChangedAt</span>
                <span class="camera-status__meta-value">${changedAt}</span>
            </div>
        </div>
        <div class="camera-status__message">
            <strong>Message:</strong> ${data.message}
        </div>
    `;

    updateDashboard(data);
}

function renderCameraOptions(cameras) {
    if (!(cameraSelectEl instanceof HTMLSelectElement)) return;

    cameraSelectEl.innerHTML = "";
    cameras.forEach((cam) => {
        const option = document.createElement("option");
        option.value = String(cam.cameraId);
        option.textContent = `${cam.cameraName} (${cam.cameraId}) - ${cam.enabled ? "사용중" : "비활성"}`;
        cameraSelectEl.appendChild(option);
    });

}

function selectCameraByIndex(nextIndex) {
    if (!(cameraSelectEl instanceof HTMLSelectElement)) return;
    const opts = cameraSelectEl.options;
    if (!opts || opts.length === 0) return;

    const idx = Math.max(0, Math.min(nextIndex, opts.length - 1));
    cameraSelectEl.selectedIndex = idx;
    cameraId = Number(cameraSelectEl.value) || cameraId;
    cameraSelectEl.dispatchEvent(new Event("change"));
}

async function ensureSignalRScriptLoaded() {
    if (window.signalR) return;

    await new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js";
        script.onload = () => resolve();
        script.onerror = () => reject(new Error("SignalR 스크립트 로드 실패"));
        document.head.appendChild(script);
    });
}

function setHubState(state) {
    if (typeof window.updateGlobalHubState === "function") {
        if (state === "connected") {
            window.updateGlobalHubState("connected", "Connected");
        } else if (state === "reconnecting") {
            window.updateGlobalHubState("reconnecting", "Reconnecting");
        } else {
            window.updateGlobalHubState("disconnected", "Disconnected");
        }
    }

    if (state === "reconnecting") {
        if (btnStartEl) btnStartEl.disabled = true;
        if (btnStopEl) btnStopEl.disabled = true;
        return;
    }

    if (state === "connected") {
        if (btnStartEl) btnStartEl.disabled = false;
        if (btnStopEl) btnStopEl.disabled = false;
        return;
    }

    if (btnStartEl) btnStartEl.disabled = true;
    if (btnStopEl) btnStopEl.disabled = true;
}

async function switchCameraGroup(nextCameraId) {
    if (!signalRConnection) return;
    if (signalRConnection.state !== "Connected") return;
    if (joinedCameraGroupId === nextCameraId) return;

    if (joinedCameraGroupId !== null) {
        pushEventLog(`그룹 이탈: camera-${joinedCameraGroupId}`);
        await signalRConnection.invoke("LeaveCameraGroup", joinedCameraGroupId);
    }

    pushEventLog(`그룹 가입: camera-${nextCameraId}`);
    await signalRConnection.invoke("JoinCameraGroup", nextCameraId);
    joinedCameraGroupId = nextCameraId;
}

async function connectCameraStatusHub() {
    if (!ensureLoggedIn()) return;

    try {
        await ensureSignalRScriptLoaded();

        if (signalRConnection) {
            await signalRConnection.stop();
            signalRConnection = null;
        }

        signalRConnection = new window.signalR.HubConnectionBuilder()
            .withUrl("/hubs/camera", {
                accessTokenFactory: () => localStorage.getItem("accessToken") || ""
            })
            .withAutomaticReconnect()
            .build();

        signalRConnection.on("CameraStatusChanged", (payload) => {
            loadCameraOptions();
            if (Number(payload?.cameraId) !== cameraId) return;
            renderCameraStatus(payload);
            pushEventLog(`카메라 ${payload.cameraId} 상태 변경: ${payload.status}`);
        });

        signalRConnection.onreconnecting(() => {
            setHubState("reconnecting");
            pushEventLog("SignalR 재연결 시도 중");
        });

        signalRConnection.onreconnected(async () => {
            await switchCameraGroup(cameraId);
            await loadCameraStatus();
            setHubState("connected");
            pushEventLog("SignalR 재연결 완료");
        });

        signalRConnection.onclose(() => {
            setHubState("disconnected");
            pushEventLog("SignalR 연결 종료");
        });

        await signalRConnection.start();
        setHubState("connected");
        pushEventLog("SignalR 연결 성공");
        await switchCameraGroup(cameraId);
    } catch (error) {
        console.error(error);
        setHubState("disconnected");
        pushEventLog("SignalR 연결 실패");
    }
}

async function loadCameraOptions() {
    if (!ensureLoggedIn()) return;
    if (!(cameraSelectEl instanceof HTMLSelectElement)) return;

    try {
        const selectedCameraId = cameraId;
        const res = await fetch("/api/Camera/list", {
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        if (!res.ok) return;

        const cameras = await res.json();
        if (!Array.isArray(cameras) || cameras.length === 0) return;

        renderCameraOptions(cameras);

        const hasSelected = cameras.some((cam) => Number(cam.cameraId) === selectedCameraId);
        cameraId = hasSelected ? selectedCameraId : (Number(cameras[0].cameraId) || 1);
        cameraSelectEl.value = String(cameraId);
    } catch (error) {
        console.error(error);
    }
}

async function loadCameraStatus(fromButton = false) {
    if (!ensureLoggedIn()) return;


    try {
        const res = await fetch(`/api/Camera/${cameraId}/status`, {
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        if (!res.ok) return;

        const data = await res.json();
        renderCameraStatus(data);
    } catch (error) {
        console.error(error);
    }
}

async function startCamera() {
    if (!ensureLoggedIn()) return;
    setButtonBusy(btnStartEl, true, "시작 중");

    try {
        const res = await fetch(`/api/Camera/${cameraId}/start`, {
            method: "POST",
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        pushEventLog(res.ok ? `카메라 ${cameraId} 시작 요청 성공` : `카메라 ${cameraId} 시작 실패`);
        await loadCameraOptions();
        await loadCameraStatus();
    } catch (error) {
        console.error(error);
        pushEventLog(`카메라 ${cameraId} 시작 오류`);
    } finally {
        setButtonBusy(btnStartEl, false);
    }
}

async function stopCamera() {
    if (!ensureLoggedIn()) return;
    setButtonBusy(btnStopEl, true, "중지 중");

    try {
        const res = await fetch(`/api/Camera/${cameraId}/stop`, {
            method: "POST",
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        pushEventLog(res.ok ? `카메라 ${cameraId} 중지 요청 성공` : `카메라 ${cameraId} 중지 실패`);
        await loadCameraOptions();
        await loadCameraStatus();
    } catch (error) {
        console.error(error);
        pushEventLog(`카메라 ${cameraId} 중지 오류`);
    } finally {
        setButtonBusy(btnStopEl, false);
    }
}

document.addEventListener("DOMContentLoaded", async () => {
    if (!ensureLoggedIn()) return;

    if (cameraSelectEl) {
        cameraSelectEl.addEventListener("change", async () => {
            cameraId = Number(cameraSelectEl.value) || 1;
            await switchCameraGroup(cameraId);
            await loadCameraStatus();
        });
    }

    if (btnStartEl) btnStartEl.addEventListener("click", startCamera);
    if (btnStopEl) btnStopEl.addEventListener("click", stopCamera);
    if (btnQuickPrevEl) btnQuickPrevEl.addEventListener("click", () => selectCameraByIndex(cameraSelectEl.selectedIndex - 1));
    if (btnQuickNextEl) btnQuickNextEl.addEventListener("click", () => selectCameraByIndex(cameraSelectEl.selectedIndex + 1));

    setHubState("reconnecting");
    await loadCameraOptions();
    await connectCameraStatusHub();
    await loadCameraStatus();
});