let aiAnalysisBusy = false;
let latestValidationResult = null;
let latestDebugState = null;

const cameraSelectEl = document.getElementById("cameraSelect");
const btnLoad = document.getElementById("btnLoad");
const btnSave = document.getElementById("btnSave");
const btnLiveRefresh = document.getElementById("btnLiveRefresh");
const btnModeObject = document.getElementById("btnModeObject");
const btnModeLabel = document.getElementById("btnModeLabel");
const checkRotationEl = document.getElementById("checkRotation");
const checkLabelEl = document.getElementById("checkLabel");
const saveStatusEl = document.getElementById("saveStatus");
const pageTitleEl = document.getElementById("pageTitle");

const btnValidateAi = document.getElementById("btnValidateAi");
const aiValidationText = document.getElementById("aiValidationText");
const btnAiAnalysis = document.getElementById("btn-ai-analysis");
const aiAnalysisText = document.getElementById("aiAnalysisText");

const bgImage = document.getElementById("bgImage");
const canvas = document.getElementById("roiCanvas");
const canvasWrap = document.getElementById("canvasWrap");
const statusOverlay = document.getElementById("statusOverlay");
const stateText = document.getElementById("stateText");

const ctx = canvas.getContext("2d");

// --- State Variables ---
let cameraId = 1;
let currentStatus = "Unknown";
let isLiveRefresh = false;
let pollingTimer = null;
let signalRConnection = null;
let joinedCameraGroupId = null;

let mode = "object";
let refreshBusy = false;
let saveBusy = false;
let activeAction = null;
let dragRectStart = null;
let dragMouseStart = null;
let roiDirty = false;

const HANDLE_SIZE = 12;
const MIN_SIZE = 20;

let objectRoiOriginal = { x: 50, y: 50, w: 200, h: 240 };
let labelRoiOriginal = { x: 120, y: 100, w: 90, h: 90 };

let objectRoi = { x: 50, y: 50, w: 200, h: 240 };
let labelRoi = { x: 120, y: 100, w: 90, h: 90 };

// --- Helpers ---
function showOverlay(message) {
    if (!statusOverlay) return;
    statusOverlay.textContent = message;
    statusOverlay.hidden = false;
    statusOverlay.style.display = "flex";
}

function hideOverlay() {
    if (!statusOverlay) return;
    statusOverlay.hidden = true;
    statusOverlay.style.display = "none";
}

function authHeaders(extra = {}) {
    const token = localStorage.getItem("accessToken");
    return { "Authorization": `Bearer ${token}`, ...extra };
}

async function handleUnauthorized(res) {
    if (res.status === 401) {
        alert("세션 만료. 다시 로그인하세요.");
        location.href = "/login.html";
        return true;
    }
    return false;
}

function setSaveStatus(text, css) {
    saveStatusEl.textContent = text;
    saveStatusEl.className = `save-status ${css}`;
}

function updatePageTitle(cameraName) {
    const title = cameraName ? `${cameraName} ROI 디버그` : `Camera ${cameraId} ROI 디버그`;
    pageTitleEl.textContent = title;
    document.title = title;
}

// --- ROI Logic ---
function getSelectedRect() { return mode === "object" ? objectRoi : labelRoi; }
function setSelectedRect(rect) { if (mode === "object") objectRoi = rect; else labelRoi = rect; }

function setMode(nextMode) {
    mode = nextMode;
    btnModeObject.classList.toggle("active-mode", mode === "object");
    btnModeLabel.classList.toggle("active-mode", mode === "label");
    drawCanvas();
}

function clamp(v, min, max) { return Math.max(min, Math.min(max, v)); }
function clampRectToCanvas(rect) {
    rect.w = clamp(rect.w, MIN_SIZE, canvas.width);
    rect.h = clamp(rect.h, MIN_SIZE, canvas.height);
    rect.x = clamp(rect.x, 0, canvas.width - rect.w);
    rect.y = clamp(rect.y, 0, canvas.height - rect.h);
    return rect;
}

function getHandleRect(rect) {
    return { x: rect.x + rect.w - HANDLE_SIZE, y: rect.y + rect.h - HANDLE_SIZE, w: HANDLE_SIZE, h: HANDLE_SIZE };
}

function pointInRect(x, y, rect) {
    return x >= rect.x && x <= rect.x + rect.w && y >= rect.y && y <= rect.y + rect.h;
}

function drawSingleRect(rect, strokeColor, fillColor, title, selected) {
    ctx.strokeStyle = strokeColor;
    ctx.lineWidth = selected ? 3 : 2;
    ctx.strokeRect(rect.x, rect.y, rect.w, rect.h);
    ctx.fillStyle = fillColor;
    ctx.fillRect(rect.x, rect.y, rect.w, rect.h);
    ctx.fillStyle = strokeColor;
    ctx.font = "bold 14px Arial";
    ctx.fillText(title, rect.x + 6, Math.max(18, rect.y - 8));
    if (selected) {
        const h = getHandleRect(rect);
        ctx.fillStyle = strokeColor;
        ctx.fillRect(h.x, h.y, h.w, h.h);
    }
}

function getDisplayScale() {
    if (!bgImage.naturalWidth || !canvas.width) return { scaleX: 1, scaleY: 1 };
    return { scaleX: canvas.width / bgImage.naturalWidth, scaleY: canvas.height / bgImage.naturalHeight };
}

function toDisplayRect(srcRect) {
    const { scaleX, scaleY } = getDisplayScale();
    return { x: Math.round(srcRect.x * scaleX), y: Math.round(srcRect.y * scaleY), w: Math.round(srcRect.w * scaleX), h: Math.round(srcRect.h * scaleY) };
}

function toOriginalRect(displayRect) {
    const { scaleX, scaleY } = getDisplayScale();
    return { x: Math.round(displayRect.x / scaleX), y: Math.round(displayRect.y / scaleY), w: Math.round(displayRect.w / scaleX), h: Math.round(displayRect.h / scaleY) };
}

function drawCanvas() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    drawSingleRect(objectRoi, "#2563eb", "rgba(37,99,235,0.16)", "ROTATION", mode === "object");
    drawSingleRect(labelRoi, "#ef4444", "rgba(239,68,68,0.16)", "LABEL", mode === "label");
}

function fitCanvasToImage() {
    canvas.width = bgImage.clientWidth;
    canvas.height = bgImage.clientHeight;
    objectRoi = clampRectToCanvas(toDisplayRect(objectRoiOriginal));
    labelRoi = clampRectToCanvas(toDisplayRect(labelRoiOriginal));
    drawCanvas();
}

// --- API Calls ---
async function loadCameraOptions() {
    try {
        const res = await fetch("/api/Camera/list", { headers: authHeaders() });
        if (await handleUnauthorized(res)) return;
        const cameras = await res.json();
        cameraSelectEl.innerHTML = "";
        cameras.forEach(cam => {
            const opt = document.createElement("option");
            opt.value = cam.cameraId;
            opt.textContent = `${cam.cameraName} (${cam.cameraId})`;
            cameraSelectEl.appendChild(opt);
        });

        const qId = new URLSearchParams(window.location.search).get("cameraId");
        if (qId) cameraSelectEl.value = qId;
        cameraId = Number(cameraSelectEl.value);
    } catch (e) { console.error(e); }
}

async function loadConfig() {
    try {
        const res = await fetch(`/api/Camera/${cameraId}/debug-config`, { headers: authHeaders() });
        if (await handleUnauthorized(res)) return;
        if (!res.ok) return;
        const data = await res.json();
        updatePageTitle(data.cameraName);
        checkRotationEl.checked = data.checkRotation;
        checkLabelEl.checked = data.checkLabel;
        objectRoiOriginal = { x: data.objectRoiX, y: data.objectRoiY, w: data.objectRoiW, h: data.objectRoiH };
        labelRoiOriginal = { x: data.labelRoiX, y: data.labelRoiY, w: data.labelRoiW, h: data.labelRoiH };
        fitCanvasToImage();
    } catch (e) { console.error(e); }
}

async function loadState() {
    try {
        const res = await fetch(`/api/Camera/${cameraId}/status`, { headers: authHeaders() });
        if (await handleUnauthorized(res)) return;
        if (!res.ok) return;
        const data = await res.json();
        currentStatus = data.status;
        updateStatusUI(data);
    } catch (e) { console.error(e); }
}

async function updateImage() {
    if (refreshBusy) return;
    if (currentStatus !== "Running") return;

    // 이미 이미지가 로드되어 보이고 있다면 로딩 중 안내를 표시하지 않음
    const isImageVisible = bgImage.complete && bgImage.naturalWidth > 0;
    
    if (!isImageVisible && !canvasWrap.classList.contains("ready")) {
        showOverlay("이미지를 로딩 중입니다...");
    }

    refreshBusy = true;
    return new Promise(resolve => {
        bgImage.onload = () => {
            fitCanvasToImage();
            canvasWrap.classList.add("ready");
            // 로드 성공 시 Running 상태라면 무조건 오버레이 숨김
            if (currentStatus === "Running") {
                hideOverlay();
            }
            refreshBusy = false;
            resolve();
        };
        bgImage.onerror = () => { 
            showOverlay("이미지 로드 실패");
            refreshBusy = false; 
            resolve(); 
        };
        bgImage.src = `/api/Camera/${cameraId}/image?t=${Date.now()}`;
    });
}

function updateStatusUI(data) {
    if (Number(data.cameraId) !== cameraId) return;

    const prevStatus = currentStatus;
    currentStatus = data.status;

    // 1. 배지 및 상태 텍스트 업데이트
    let badgeClass = "idle";
    let statusText = data.status;

    switch (data.status) {
        case "Running": badgeClass = "ok"; break;
        case "Starting":
        case "Connecting":
        case "Stopping":
            badgeClass = "saving"; break;
        case "Error":
        case "Stale":
            badgeClass = "err"; break;
        case "Stopped": badgeClass = "idle"; break;
    }

    setSaveStatus(statusText, badgeClass);

    stateText.textContent = `Camera: ${data.cameraName} (${data.cameraId})
Status: ${data.status}
Enabled: ${data.enabled}
Production: ${data.productionCount ?? 0}
Last Update: ${new Date(data.changedAt).toLocaleTimeString()}
Message: ${data.message}`;

    // 2. 버튼 활성화 제어
    const isCameraReady = data.status === "Running";
    if (btnValidateAi) btnValidateAi.disabled = !isCameraReady;
    if (btnAiAnalysis) btnAiAnalysis.disabled = !isCameraReady;
    if (btnLiveRefresh) btnLiveRefresh.disabled = !isCameraReady;

    // 3. 오버레이 및 폴링 제어
    if (isCameraReady) {
        // 이미지가 로드된 상태거나 그릴 준비가 되었다면 오버레이 즉시 숨김
        const isImageVisible = bgImage.complete && bgImage.naturalWidth > 0;
        if (isImageVisible || canvasWrap.classList.contains("ready")) {
            hideOverlay();
        } else {
            showOverlay("이미지를 로딩 중입니다...");
        }

        // 처음 Running이 되었거나 이미지가 아예 없는 경우 강제 로드
        if (prevStatus !== "Running" || !canvasWrap.classList.contains("ready")) {
            updateImage();
        }
        
        if (isLiveRefresh) startPolling();
    } else {
        let msg = "카메라가 실행 중이 아닙니다.";
        if (data.status === "Starting" || data.status === "Connecting") msg = "카메라 연결 중...";
        if (data.status === "Error") msg = `카메라 오류: ${data.message || "연결 실패"}`;
        
        showOverlay(msg);
        stopPolling();
        canvasWrap.classList.remove("ready");
    }
}

// --- Polling & SignalR ---
function startPolling() {
    if (pollingTimer) return;
    const loop = async () => {
        if (!isLiveRefresh || currentStatus !== "Running") {
            stopPolling();
            return;
        }
        await updateImage();
        pollingTimer = setTimeout(loop, 1000);
    };
    loop();
}

function stopPolling() {
    if (pollingTimer) {
        clearTimeout(pollingTimer);
        pollingTimer = null;
    }
}

async function switchCameraGroup(nextId) {
    if (!signalRConnection || signalRConnection.state !== "Connected") return;
    if (joinedCameraGroupId === nextId) return;
    if (joinedCameraGroupId !== null) await signalRConnection.invoke("LeaveCameraGroup", joinedCameraGroupId);
    await signalRConnection.invoke("JoinCameraGroup", nextId);
    joinedCameraGroupId = nextId;
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
}

async function connectHub() {
    signalRConnection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/camera", { accessTokenFactory: () => localStorage.getItem("accessToken") })
        .withAutomaticReconnect()
        .build();

    signalRConnection.on("CameraStatusChanged", data => {
        if (Number(data.cameraId) === cameraId) {
            currentStatus = data.status;
            updateStatusUI(data);
        }
    });

    signalRConnection.onreconnecting(() => {
        setHubState("reconnecting");
    });

    signalRConnection.onreconnected(async () => {
        setHubState("connected");
        await switchCameraGroup(cameraId);
        await loadState();
    });

    signalRConnection.onclose(() => {
        setHubState("disconnected");
    });

    try {
        await signalRConnection.start();
        setHubState("connected");
        await switchCameraGroup(cameraId);
    } catch (e) {
        console.error(e);
        setHubState("disconnected");
    }
}

// --- Event Listeners ---
cameraSelectEl.addEventListener("change", async () => {
    cameraId = Number(cameraSelectEl.value);
    roiDirty = false;
    canvasWrap.classList.remove("ready");
    setSaveStatus("카메라 동기화 중...", "saving");
    await switchCameraGroup(cameraId);
    await loadConfig();
    await loadState();
    await updateImage();
});

btnLiveRefresh.addEventListener("click", () => {
    isLiveRefresh = !isLiveRefresh;
    btnLiveRefresh.classList.toggle("active-mode", isLiveRefresh);
    if (isLiveRefresh) startPolling();
    else stopPolling();
});

btnSave.addEventListener("click", async () => {
    if (saveBusy) return;
    saveBusy = true;
    btnSave.disabled = true;
    setSaveStatus("ROI 저장 중...", "saving");

    try {
        const payload = {
            objectRoiX: toOriginalRect(objectRoi).x,
            objectRoiY: toOriginalRect(objectRoi).y,
            objectRoiW: toOriginalRect(objectRoi).w,
            objectRoiH: toOriginalRect(objectRoi).h,
            labelRoiX: toOriginalRect(labelRoi).x,
            labelRoiY: toOriginalRect(labelRoi).y,
            labelRoiW: toOriginalRect(labelRoi).w,
            labelRoiH: toOriginalRect(labelRoi).h,
            checkRotation: checkRotationEl.checked,
            checkLabel: checkLabelEl.checked
        };

        const res = await fetch(`/api/Camera/${cameraId}/roi`, {
            method: "POST",
            headers: authHeaders({ "Content-Type": "application/json" }),
            body: JSON.stringify(payload)
        });

        if (await handleUnauthorized(res)) return;
        if (res.ok) {
            roiDirty = false;
            setSaveStatus("저장 완료", "ok");
            await loadConfig();
        } else {
            setSaveStatus("저장 실패", "err");
        }
    } catch (e) {
        setSaveStatus("오류 발생", "err");
    } finally {
        saveBusy = false;
        btnSave.disabled = false;
    }
});

canvas.addEventListener("mousedown", e => {
    const rect = canvas.getBoundingClientRect();
    const p = { x: Math.round(e.clientX - rect.left), y: Math.round(e.clientY - rect.top) };
    const r = getSelectedRect();
    const h = getHandleRect(r);

    dragMouseStart = p;
    dragRectStart = { ...r };
    if (pointInRect(p.x, p.y, h)) activeAction = "resize";
    else if (pointInRect(p.x, p.y, r)) activeAction = "move";
});

window.addEventListener("mousemove", e => {
    if (!activeAction) return;
    const canvasRect = canvas.getBoundingClientRect();
    const p = { x: Math.round(e.clientX - canvasRect.left), y: Math.round(e.clientY - canvasRect.top) };
    let r = { ...dragRectStart };

    if (activeAction === "move") {
        r.x = dragRectStart.x + (p.x - dragMouseStart.x);
        r.y = dragRectStart.y + (p.y - dragMouseStart.y);
    } else {
        r.w = Math.max(MIN_SIZE, dragRectStart.w + (p.x - dragMouseStart.x));
        r.h = Math.max(MIN_SIZE, dragRectStart.h + (p.y - dragMouseStart.y));
    }
    setSelectedRect(clampRectToCanvas(r));
    roiDirty = true;
    setSaveStatus("수정됨", "saving");
    drawCanvas();
});

window.addEventListener("mouseup", () => activeAction = null);

// --- Initialization ---
async function init() {
    const script = document.createElement("script");
    script.src = "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js";
    script.onload = async () => {
        await loadCameraOptions();
        await connectHub();
        await loadConfig();
        await loadState();
        await updateImage();
    };
    document.head.appendChild(script);
}

init();
