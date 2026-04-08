  const cameraIdEl = document.getElementById("cameraId");
const btnLoad = document.getElementById("btnLoad");
const btnSave = document.getElementById("btnSave");
const btnModeObject = document.getElementById("btnModeObject");
const btnModeLabel = document.getElementById("btnModeLabel");
const checkRotationEl = document.getElementById("checkRotation");
const checkLabelEl = document.getElementById("checkLabel");
const saveStatusEl = document.getElementById("saveStatus");
const pageTitleEl = document.getElementById("pageTitle");

const bgImage = document.getElementById("bgImage");
const canvas = document.getElementById("roiCanvas");
const canvasWrap = document.getElementById("canvasWrap");
const roiText = document.getElementById("roiText");
const stateText = document.getElementById("stateText");

if (!(cameraIdEl instanceof HTMLInputElement) ||
    !(btnLoad instanceof HTMLButtonElement) ||
    !(btnSave instanceof HTMLButtonElement) ||
    !(btnModeObject instanceof HTMLButtonElement) ||
    !(btnModeLabel instanceof HTMLButtonElement) ||
    !(checkRotationEl instanceof HTMLInputElement) ||
    !(checkLabelEl instanceof HTMLInputElement) ||
    !(saveStatusEl instanceof HTMLElement) ||
    !(pageTitleEl instanceof HTMLElement) ||
    !(bgImage instanceof HTMLImageElement) ||
    !(canvas instanceof HTMLCanvasElement) ||
    !(canvasWrap instanceof HTMLElement) ||
    !(roiText instanceof HTMLElement) ||
    !(stateText instanceof HTMLElement)) {
    throw new Error("ROI 페이지 필수 요소를 찾을 수 없습니다.");
}

const ctx = canvas.getContext("2d");
if (!ctx) {
    throw new Error("Canvas 2D context를 생성할 수 없습니다.");
}

function redirectToLogin() {
    location.href = "/login.html";
}

function ensureLoggedIn() {
    const accessToken = localStorage.getItem("accessToken");
    if (!accessToken) {
        alert("로그인이 필요합니다.");
        redirectToLogin();
        return false;
    }
    return true;
}

function roiAuthHeaders(extra = {}) {
    const accessToken = localStorage.getItem("accessToken");
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

if (!ensureLoggedIn()) {
    throw new Error("로그인되지 않은 상태입니다.");
}

const queryCameraId = Number(new URLSearchParams(window.location.search).get("cameraId"));
if (Number.isInteger(queryCameraId) && queryCameraId > 0) {
    cameraIdEl.value = String(queryCameraId);
}

function updatePageTitle(cameraName) {
    const currentCameraId = Number(cameraIdEl.value) || 1;
    const title = cameraName
        ? `${cameraName} ROI 디버그`
        : `Camera ${currentCameraId} ROI 디버그`;
    pageTitleEl.textContent = title;
    document.title = title;
}

updatePageTitle();

function setSaveStatus(text, css) {
    saveStatusEl.textContent = text;
    saveStatusEl.className = `save-status ${css}`;
}

function getSelectedRect() {
    return mode === "object" ? objectRoi : labelRoi;
}

function setSelectedRect(rect) {
    if (mode === "object") {
        objectRoi = rect;
    } else {
        labelRoi = rect;
    }
}

function setMode(nextMode) {
    mode = nextMode;
    btnModeObject.classList.toggle("active-mode", mode === "object");
    btnModeLabel.classList.toggle("active-mode", mode === "label");
    drawCanvas();
}

btnModeObject.addEventListener("click", () => setMode("object"));
btnModeLabel.addEventListener("click", () => setMode("label"));

function clamp(v, min, max) {
    return Math.max(min, Math.min(max, v));
}

function clampRectToCanvas(rect) {
    const maxW = canvas.width;
    const maxH = canvas.height;

    rect.w = clamp(rect.w, MIN_SIZE, Math.max(MIN_SIZE, maxW));
    rect.h = clamp(rect.h, MIN_SIZE, Math.max(MIN_SIZE, maxH));

    rect.x = clamp(rect.x, 0, Math.max(0, maxW - rect.w));
    rect.y = clamp(rect.y, 0, Math.max(0, maxH - rect.h));

    return rect;
}

function getHandleRect(rect) {
    return {
        x: rect.x + rect.w - HANDLE_SIZE,
        y: rect.y + rect.h - HANDLE_SIZE,
        w: HANDLE_SIZE,
        h: HANDLE_SIZE
    };
}

function pointInRect(x, y, rect) {
    return x >= rect.x && x <= rect.x + rect.w &&
        y >= rect.y && y <= rect.y + rect.h;
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
        ctx.strokeStyle = "#ffffff";
        ctx.lineWidth = 1;
        ctx.strokeRect(h.x, h.y, h.w, h.h);
    }
}

function getDisplayScale() {
    if (!bgImage.naturalWidth || !bgImage.naturalHeight || !canvas.width || !canvas.height) {
        return { scaleX: 1, scaleY: 1 };
    }

    return {
        scaleX: canvas.width / bgImage.naturalWidth,
        scaleY: canvas.height / bgImage.naturalHeight
    };
}

function toDisplayRect(srcRect) {
    const { scaleX, scaleY } = getDisplayScale();

    return {
        x: Math.round(srcRect.x * scaleX),
        y: Math.round(srcRect.y * scaleY),
        w: Math.round(srcRect.w * scaleX),
        h: Math.round(srcRect.h * scaleY)
    };
}

function toOriginalRect(displayRect) {
    const { scaleX, scaleY } = getDisplayScale();

    return {
        x: Math.round(displayRect.x / scaleX),
        y: Math.round(displayRect.y / scaleY),
        w: Math.round(displayRect.w / scaleX),
        h: Math.round(displayRect.h / scaleY)
    };
}

function syncDisplayRectsFromOriginal() {
    objectRoi = clampRectToCanvas(toDisplayRect(objectRoiOriginal));
    labelRoi = clampRectToCanvas(toDisplayRect(labelRoiOriginal));
    drawCanvas();
}

function drawCanvas() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    drawSingleRect(
        objectRoi,
        "#2563eb",
        "rgba(37,99,235,0.16)",
        "ROTATION",
        mode === "object"
    );

    drawSingleRect(
        labelRoi,
        "#ef4444",
        "rgba(239,68,68,0.16)",
        "LABEL",
        mode === "label"
    );

    const objectOriginalNow = toOriginalRect(objectRoi);
    const labelOriginalNow = toOriginalRect(labelRoi);

    roiText.textContent =
        `[회전 ROI - 화면 좌표]
X = ${objectRoi.x}
Y = ${objectRoi.y}
W = ${objectRoi.w}
H = ${objectRoi.h}

[라벨 ROI - 화면 좌표]
X = ${labelRoi.x}
Y = ${labelRoi.y}
W = ${labelRoi.w}
H = ${labelRoi.h}

[회전 ROI - 원본 좌표]
X = ${objectOriginalNow.x}
Y = ${objectOriginalNow.y}
W = ${objectOriginalNow.w}
H = ${objectOriginalNow.h}

[라벨 ROI - 원본 좌표]
X = ${labelOriginalNow.x}
Y = ${labelOriginalNow.y}
W = ${labelOriginalNow.w}
H = ${labelOriginalNow.h}

[편집 상태]
roiDirty = ${roiDirty}`;
}

function fitCanvasToImage(keepCurrentRect = true) {
    const oldWidth = canvas.width || 1;
    const oldHeight = canvas.height || 1;

    const prevObject = { ...objectRoi };
    const prevLabel = { ...labelRoi };

    canvas.width = bgImage.clientWidth;
    canvas.height = bgImage.clientHeight;

    if (!keepCurrentRect || oldWidth <= 1 || oldHeight <= 1) {
        syncDisplayRectsFromOriginal();
        return;
    }

    const scaleX = canvas.width / oldWidth;
    const scaleY = canvas.height / oldHeight;

    objectRoi = clampRectToCanvas({
        x: Math.round(prevObject.x * scaleX),
        y: Math.round(prevObject.y * scaleY),
        w: Math.round(prevObject.w * scaleX),
        h: Math.round(prevObject.h * scaleY)
    });

    labelRoi = clampRectToCanvas({
        x: Math.round(prevLabel.x * scaleX),
        y: Math.round(prevLabel.y * scaleY),
        w: Math.round(prevLabel.w * scaleX),
        h: Math.round(prevLabel.h * scaleY)
    });

    drawCanvas();
}

function getMousePos(evt) {
    const rect = canvas.getBoundingClientRect();
    return {
        x: Math.round(evt.clientX - rect.left),
        y: Math.round(evt.clientY - rect.top)
    };
}

function applyCursor(x, y) {
    const rect = getSelectedRect();
    const handle = getHandleRect(rect);

    if (pointInRect(x, y, handle)) {
        canvas.style.cursor = "nwse-resize";
        return;
    }

    if (pointInRect(x, y, rect)) {
        canvas.style.cursor = "move";
        return;
    }

    canvas.style.cursor = "default";
}

canvas.addEventListener("mousedown", (e) => {
    const p = getMousePos(e);
    const rect = getSelectedRect();
    const handle = getHandleRect(rect);

    dragMouseStart = p;
    dragRectStart = { ...rect };

    if (pointInRect(p.x, p.y, handle)) {
        activeAction = "resize";
        return;
    }

    if (pointInRect(p.x, p.y, rect)) {
        activeAction = "move";
        return;
    }

    activeAction = null;
});

canvas.addEventListener("mousemove", (e) => {
    const p = getMousePos(e);

    if (!activeAction) {
        applyCursor(p.x, p.y);
        return;
    }

    let rect = { ...dragRectStart };

    if (activeAction === "move") {
        const dx = p.x - dragMouseStart.x;
        const dy = p.y - dragMouseStart.y;

        rect.x = dragRectStart.x + dx;
        rect.y = dragRectStart.y + dy;

        setSelectedRect(clampRectToCanvas(rect));
        roiDirty = true;
        setSaveStatus("ROI 수정됨 - 저장 필요", "saving");
        drawCanvas();
        return;
    }

    if (activeAction === "resize") {
        rect.w = Math.max(MIN_SIZE, dragRectStart.w + (p.x - dragMouseStart.x));
        rect.h = Math.max(MIN_SIZE, dragRectStart.h + (p.y - dragMouseStart.y));

        if (rect.x + rect.w > canvas.width) {
            rect.w = canvas.width - rect.x;
        }
        if (rect.y + rect.h > canvas.height) {
            rect.h = canvas.height - rect.y;
        }

        setSelectedRect(clampRectToCanvas(rect));
        roiDirty = true;
        setSaveStatus("ROI 수정됨 - 저장 필요", "saving");
        drawCanvas();
        return;
    }
});

window.addEventListener("mouseup", () => {
    activeAction = null;
    dragRectStart = null;
    dragMouseStart = null;
});

window.addEventListener("resize", () => {
    if (!bgImage.naturalWidth || !bgImage.naturalHeight) return;
    fitCanvasToImage(true);
});

async function refreshImageOnly() {
    const cameraId = Number(cameraIdEl.value);

    await new Promise((resolve) => {
        let done = false;

        const finish = () => {
            if (done) return;
            done = true;
            resolve();
        };

        bgImage.onload = () => {
            fitCanvasToImage(true);
            canvasWrap.classList.add("ready");
            finish();
        };

        bgImage.onerror = () => finish();

        bgImage.src = `/api/Camera/${cameraId}/image?t=${Date.now()}`;

        setTimeout(finish, 1500);
    });
}

async function loadConfig() {
    const cameraId = Number(cameraIdEl.value);

    try {
        const res = await fetch(`/api/Camera/${cameraId}/debug-config`, {
            headers: roiAuthHeaders()
        });

        if (await handleUnauthorized(res)) return;

        if (!res.ok) {
            setSaveStatus("설정 정보를 불러오지 못했습니다.", "err");
            return;
        }

        const data = await res.json();
        updatePageTitle(data.cameraName);

        checkRotationEl.checked = data.checkRotation;
        checkLabelEl.checked = data.checkLabel;

        objectRoiOriginal = {
            x: data.objectRoiX,
            y: data.objectRoiY,
            w: data.objectRoiW,
            h: data.objectRoiH
        };

        labelRoiOriginal = {
            x: data.labelRoiX,
            y: data.labelRoiY,
            w: data.labelRoiW,
            h: data.labelRoiH
        };

        await refreshImageOnly();
        syncDisplayRectsFromOriginal();
        roiDirty = false;
        drawCanvas();
    } catch (error) {
        console.error(error);
        setSaveStatus("설정 조회 중 오류가 발생했습니다.", "err");
    }
}

function boolText(v) {
    return v ? "TRUE" : "FALSE";
}

async function loadState() {
    const cameraId = Number(cameraIdEl.value);

    try {
        const res = await fetch(`/api/Camera/${cameraId}/debug-state`, {
            headers: roiAuthHeaders()
        });

        if (await handleUnauthorized(res)) return;

        if (!res.ok) {
            stateText.textContent = "상태 정보를 불러오지 못했습니다.";
            return;
        }

        const s = await res.json();

        stateText.textContent =
            `RotationActive           : ${boolText(s.rotationActive)}
LabelInZone              : ${boolText(s.labelInZone)}
CountedInCurrentRotation : ${boolText(s.countedInCurrentRotation)}

LastStarted              : ${boolText(s.lastStarted)}
LastEnded                : ${boolText(s.lastEnded)}
LastLabelEnter           : ${boolText(s.lastLabelEnter)}

LastDetectorFound        : ${boolText(s.lastDetectorFound)}
LastDetectorConfidence   : ${s.lastDetectorConfidence}

LabelDetectedStreak      : ${s.labelDetectedStreak}
LabelOffStreak           : ${s.labelOffStreak}

LastRotationChangeValue  : ${s.lastRotationChangeValue}
LastMotionRatio          : ${s.lastMotionRatio}
LastLabelChangeValue     : ${s.lastLabelChangeValue}

ProductionCount          : ${s.productionCount}

LastFrameAt              : ${s.lastFrameAt}
LastSuccessfulReadAt     : ${s.lastSuccessfulReadAt}
LastReconnectAt          : ${s.lastReconnectAt}
SessionStartedAt         : ${s.sessionStartedAt}
LastUpdatedAt            : ${s.lastUpdatedAt}
StreamJustReconnected    : ${boolText(s.streamJustReconnected)}

roiDirty                 : ${boolText(roiDirty)}
activeAction             : ${activeAction ?? "-"}`;
    } catch (error) {
        console.error(error);
        stateText.textContent = "상태 조회 중 오류가 발생했습니다.";
    }
}

function validateRoi(rect, name) {
    if (rect.w <= 0 || rect.h <= 0) {
        setSaveStatus(`${name} ROI의 폭/높이가 0입니다.`, "err");
        return false;
    }
    if (rect.w < MIN_SIZE || rect.h < MIN_SIZE) {
        setSaveStatus(`${name} ROI가 너무 작습니다.`, "err");
        return false;
    }
    return true;
}

async function saveRoi() {
    if (saveBusy) return;

    if (!validateRoi(objectRoi, "회전")) return;
    if (!validateRoi(labelRoi, "라벨")) return;

    saveBusy = true;
    btnSave.disabled = true;
    setSaveStatus("ROI 저장 중...", "saving");

    try {
        const cameraId = Number(cameraIdEl.value);

        objectRoiOriginal = toOriginalRect(objectRoi);
        labelRoiOriginal = toOriginalRect(labelRoi);

        const payload = {
            objectRoiX: objectRoiOriginal.x,
            objectRoiY: objectRoiOriginal.y,
            objectRoiW: objectRoiOriginal.w,
            objectRoiH: objectRoiOriginal.h,

            labelRoiX: labelRoiOriginal.x,
            labelRoiY: labelRoiOriginal.y,
            labelRoiW: labelRoiOriginal.w,
            labelRoiH: labelRoiOriginal.h,

            checkRotation: checkRotationEl.checked,
            checkLabel: checkLabelEl.checked
        };

        const res = await fetch(`/api/Camera/${cameraId}/roi`, {
            method: "POST",
            headers: roiAuthHeaders({
                "Content-Type": "application/json"
            }),
            body: JSON.stringify(payload)
        });

        if (await handleUnauthorized(res)) return;

        if (!res.ok) {
            setSaveStatus("ROI 저장 실패", "err");
            return;
        }

        roiDirty = false;
        setSaveStatus("ROI 저장 완료", "ok");

        await loadConfig();
        await loadState();
    } catch (e) {
        console.error(e);
        setSaveStatus("ROI 저장 중 오류 발생", "err");
    } finally {
        saveBusy = false;
        btnSave.disabled = false;
        drawCanvas();
    }
}

btnLoad.addEventListener("click", async () => {
    updatePageTitle();
    canvasWrap.classList.remove("ready");
    setSaveStatus("설정 불러오는 중...", "saving");
    await loadConfig();
    await loadState();
    setSaveStatus("불러오기 완료", "ok");
});

btnSave.addEventListener("click", saveRoi);

setInterval(async () => {
    if (refreshBusy || saveBusy || activeAction || roiDirty) {
        if (roiDirty && !saveBusy && !activeAction) {
            await loadState();
        }
        return;
    }

    refreshBusy = true;
    try {
        await refreshImageOnly();
        await loadState();
    } finally {
        refreshBusy = false;
    }
}, 1000);

(async function init() {
    if (window.updateGlobalHubState) {
        window.updateGlobalHubState("connected", "ROI Ready");
    }
    canvasWrap.classList.remove("ready");
    setSaveStatus("초기 로딩 중...", "saving");
    await loadConfig();
    await loadState();
    setSaveStatus("준비됨", "idle");
})();