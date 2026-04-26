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
const cameraStatusCache = new Map();

// --- Three.js State ---
let scene, camera, renderer, turntableGroup, product, roiBox, threeCamera, sightLine, countSprite;
let lastProductionCount = 0;
let isSimulating = false;

function initThreeScene() {
    const container = document.getElementById('threeView');
    if (!container) return;

    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0a0a0a);

    camera = new THREE.PerspectiveCamera(45, container.clientWidth / container.clientHeight, 0.1, 1000);
    camera.position.set(8, 6, 8);
    camera.lookAt(0, 1, 0);

    renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setSize(container.clientWidth, container.clientHeight);
    container.appendChild(renderer.domElement);

    // Lights
    scene.add(new THREE.AmbientLight(0xffffff, 0.5));
    const spotLight = new THREE.SpotLight(0xffffff, 1.5);
    spotLight.position.set(10, 15, 10);
    scene.add(spotLight);

    // 1. Turntable Group
    turntableGroup = new THREE.Group();
    scene.add(turntableGroup);

    const tableGeo = new THREE.CylinderGeometry(4, 4, 0.2, 32);
    const tableMat = new THREE.MeshPhongMaterial({ color: 0x222222 });
    const tableMesh = new THREE.Mesh(tableGeo, tableMat);
    tableMesh.position.y = -0.1;
    turntableGroup.add(tableMesh);

    // 2. Product Group
    const prodGroup = new THREE.Group();
    prodGroup.position.set(2.8, 0.8, 0);
    turntableGroup.add(prodGroup);

    const bodyGeo = new THREE.CylinderGeometry(0.6, 0.6, 1.6, 16);
    const bodyMat = new THREE.MeshPhongMaterial({ color: 0x94a3b8, transparent: true, opacity: 0.8 });
    product = new THREE.Mesh(bodyGeo, bodyMat);
    prodGroup.add(product);

    const capGeo = new THREE.CylinderGeometry(0.2, 0.2, 0.4, 12);
    const capMat = new THREE.MeshPhongMaterial({ color: 0x334155 });
    const capMesh = new THREE.Mesh(capGeo, capMat);
    capMesh.position.y = 1.0;
    prodGroup.add(capMesh);

    // 3. ROI Plane
    const roiGeo = new THREE.PlaneGeometry(0.8, 0.8);
    const roiMat = new THREE.MeshBasicMaterial({ 
        color: 0x00ff00, 
        transparent: true, 
        opacity: 0.4, 
        side: THREE.DoubleSide 
    });
    roiBox = new THREE.Mesh(roiGeo, roiMat);
    roiBox.position.set(0, 0, 0.61);
    prodGroup.add(roiBox);

    const edges = new THREE.EdgesGeometry(roiGeo);
    const line = new THREE.LineSegments(edges, new THREE.LineBasicMaterial({ color: 0x00ff00 }));
    roiBox.add(line);

    // 4. Count Sprite Label
    countSprite = createTextSprite("0");
    countSprite.position.set(0, 1.8, 0);
    prodGroup.add(countSprite);

    // 5. Static Camera
    const camGroup = new THREE.Group();
    camGroup.position.set(7, 2.5, 0);
    scene.add(camGroup);

    const camBodyGeo = new THREE.BoxGeometry(0.6, 0.6, 1.2);
    const camBodyMat = new THREE.MeshPhongMaterial({ color: 0x3b82f6 });
    threeCamera = new THREE.Mesh(camBodyGeo, camBodyMat);
    threeCamera.lookAt(0, 1.5, 0);
    camGroup.add(threeCamera);

    // 6. Sightline (Beam)
    const lineMat = new THREE.MeshBasicMaterial({ color: 0x3b82f6, transparent: true, opacity: 0.2 });
    const lineGeo = new THREE.CylinderGeometry(0.02, 0.05, 1);
    sightLine = new THREE.Mesh(lineGeo, lineMat);
    scene.add(sightLine);

    // Grid
    const grid = new THREE.GridHelper(20, 20, 0x333333, 0x222222);
    grid.position.y = -0.2;
    scene.add(grid);

    window.addEventListener('resize', onThreeResize);
    animateThree();
}

function createTextSprite(text) {
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    canvas.width = 128;
    canvas.height = 64;
    
    context.fillStyle = 'rgba(0,0,0,0.5)';
    context.fillRect(0, 0, 128, 64);
    context.strokeStyle = '#3b82f6';
    context.lineWidth = 4;
    context.strokeRect(0, 0, 128, 64);
    
    context.fillStyle = '#ffffff';
    context.font = 'bold 40px Arial';
    context.textAlign = 'center';
    context.fillText(text, 64, 46);
    
    const texture = new THREE.CanvasTexture(canvas);
    const spriteMaterial = new THREE.SpriteMaterial({ map: texture });
    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.scale.set(1.5, 0.75, 1);
    return sprite;
}

function updateCountSprite(text) {
    if (!countSprite) return;
    const canvas = countSprite.material.map.image;
    const context = canvas.getContext('2d');
    context.clearRect(0, 0, 128, 64);
    
    context.fillStyle = 'rgba(0,0,0,0.5)';
    context.fillRect(0, 0, 128, 64);
    context.strokeStyle = '#3b82f6';
    context.lineWidth = 4;
    context.strokeRect(0, 0, 128, 64);
    
    context.fillStyle = '#ffffff';
    context.font = 'bold 40px Arial';
    context.textAlign = 'center';
    context.fillText(text, 64, 46);
    
    countSprite.material.map.needsUpdate = true;
}

function onThreeResize() {
    const container = document.getElementById('threeView');
    if (!container || !camera || !renderer) return;
    camera.aspect = container.clientWidth / container.clientHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(container.clientWidth, container.clientHeight);
}

function animateThree() {
    requestAnimationFrame(animateThree);

    if (isSimulating) {
        turntableGroup.rotation.y -= 0.02;

        const roiWorldPos = new THREE.Vector3();
        roiBox.getWorldPosition(roiWorldPos);
        const camWorldPos = new THREE.Vector3();
        threeCamera.getWorldPosition(camWorldPos);

        // Update Sightline Beam
        const direction = new THREE.Vector3().subVectors(roiWorldPos, camWorldPos);
        const distance = direction.length();
        
        sightLine.scale.set(1, distance, 1);
        sightLine.position.copy(camWorldPos).add(direction.multiplyScalar(0.5));
        sightLine.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.clone().normalize());

        // Tracking Logic
        if (roiWorldPos.x > 2.5 && Math.abs(roiWorldPos.z) < 1.2) {
            roiBox.material.color.setHex(0xff0000);
            roiBox.material.opacity = 0.7;
            sightLine.material.opacity = 0.6;
        } else {
            roiBox.material.color.setHex(0x00ff00);
            roiBox.material.opacity = 0.2;
            sightLine.material.opacity = 0.1;
        }
    } else {
        sightLine.scale.set(0.001, 0.001, 0.001);
    }

    renderer.render(scene, camera);
}

function triggerDetectionAnimation() {
    if (!product || !roiBox) return;
    
    // Scale pulse
    const originalScale = 1.0;
    product.scale.set(1.3, 1.3, 1.3);
    
    // Blink ROI
    const originalOpacity = roiBox.material.opacity;
    roiBox.material.color.setHex(0xffffff);
    roiBox.material.opacity = 1.0;
    
    setTimeout(() => {
        product.scale.set(originalScale, originalScale, originalScale);
        roiBox.material.color.setHex(0xff0000);
        roiBox.material.opacity = originalOpacity;
    }, 150);
}

// --- Dashboard Logic ---

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

function clearButtonBusy(button) {
    if (!(button instanceof HTMLButtonElement)) return;

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
    if (Number(data.cameraId) !== cameraId) return;

    if (liveNameEl) liveNameEl.textContent = data.cameraName || "-";
    if (liveIdEl) liveIdEl.textContent = "ID: " + (data.cameraId || "-");

    if (liveBadgeEl) {
        const status = (data.status || "-").toString();
        liveBadgeEl.textContent = status;
        liveBadgeEl.className = "status-pill";
        if (status === "Running") {
            liveBadgeEl.classList.add("status-pill--running");
            isSimulating = true;
        } else if (status === "Stopped") {
            liveBadgeEl.classList.add("status-pill--stopped");
            isSimulating = false;
        } else {
            liveBadgeEl.classList.add("status-pill--neutral");
            isSimulating = (status === "Starting" || status === "Connecting");
        }
    }

    if (liveMsgEl) liveMsgEl.textContent = data.message || "-";

    // Update 3D Count Label
    updateCountSprite((data.productionCount ?? 0).toString());

    // 3D Animation trigger on count change
    if (data.productionCount > lastProductionCount) {
        triggerDetectionAnimation();
    }
    lastProductionCount = data.productionCount;

    updateControlButtons(data.status);
}

function updateControlButtons(status) {
    if (!btnStartEl || !btnStopEl) return;

    const s = (status || "").toString();

    // 시작 버튼 제어
    if (s === "Stopped" || s === "Error") {
        btnStartEl.disabled = false;
    } else {
        btnStartEl.disabled = true;
    }

    // 중지 버튼 제어
    if (s === "Running" || s === "Stale") {
        btnStopEl.disabled = false;
    } else {
        btnStopEl.disabled = true;
    }
}

function renderCameraStatus(data) {
    if (Number(data.cameraId) !== cameraId) return;
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
                <span class="camera-status__meta-label">Production</span>
                <span class="camera-status__meta-value">${data.productionCount ?? 0}</span>
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
    updateCameraOptionLabel(data);
}

function cacheCameraStatus(data) {
    const receivedCameraId = Number(data?.cameraId);
    if (!Number.isFinite(receivedCameraId)) return;

    cameraStatusCache.set(receivedCameraId, data);
}

function applyCachedCameraStatus() {
    const cached = cameraStatusCache.get(cameraId);
    if (!cached) return false;

    renderCameraStatus(cached);
    return true;
}

function updateCameraOptionLabel(data) {
    if (!(cameraSelectEl instanceof HTMLSelectElement)) return;

    const options = cameraSelectEl.options;
    for (let i = 0; i < options.length; i++) {
        if (Number(options[i].value) === Number(data.cameraId)) {
            const statusText = data.status || (data.enabled ? "사용중" : "비활성");
            options[i].textContent = `${data.cameraName} (${data.cameraId}) - ${statusText}`;
            break;
        }
    }
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

async function joinDashboardGroup() {
    if (!signalRConnection) return;
    if (signalRConnection.state !== "Connected") return;

    await signalRConnection.invoke("JoinDashboardGroup");
    pushEventLog("그룹 가입: camera-dashboard");
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
            const receivedCameraId = Number(payload?.cameraId);
            console.log("CameraStatusChanged received", payload);
            pushEventLog(`CameraStatusChanged received: camera-${Number.isFinite(receivedCameraId) ? receivedCameraId : "-"} ${payload?.status || "-"}`);

            cacheCameraStatus(payload);
            updateCameraOptionLabel(payload);

            if (receivedCameraId !== cameraId) return;
            renderCameraStatus(payload);
        });

        signalRConnection.onreconnecting(() => {
            setHubState("reconnecting");
            pushEventLog("SignalR 재연결 시도 중");
        });

        signalRConnection.onreconnected(async () => {
            await joinDashboardGroup();
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
        await joinDashboardGroup();
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
        cacheCameraStatus(data);
        renderCameraStatus(data);
    } catch (error) {
        console.error(error);
    }
}

async function startCamera() {
    if (!ensureLoggedIn()) return;
    setButtonBusy(btnStartEl, true, "시작 중...");

    try {
        const res = await fetch(`/api/Camera/${cameraId}/start`, {
            method: "POST",
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        const data = res.ok ? await res.json() : null;

        const status = (data?.status || "").toString();
        const succeeded = res.ok && status !== "Error";

        pushEventLog(succeeded
            ? `카메라 ${cameraId} 시작 요청 성공: ${status || "OK"}`
            : `카메라 ${cameraId} 시작 실패${data?.message ? ` - ${data.message}` : ""}`);

    } catch (error) {
        console.error(error);
        pushEventLog(`카메라 ${cameraId} 시작 오류 발생`);
    } finally {
        clearButtonBusy(btnStartEl);
        if (!applyCachedCameraStatus()) {
            await loadCameraStatus();
        }
    }
}

async function stopCamera() {
    if (!ensureLoggedIn()) return;
    setButtonBusy(btnStopEl, true, "중지 중...");

    try {
        const res = await fetch(`/api/Camera/${cameraId}/stop`, {
            method: "POST",
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        const data = res.ok ? await res.json() : null;

        const status = (data?.status || "").toString();
        const succeeded = res.ok && status !== "Error";

        pushEventLog(succeeded
            ? `카메라 ${cameraId} 중지 요청 성공: ${status || "OK"}`
            : `카메라 ${cameraId} 중지 실패${data?.message ? ` - ${data.message}` : ""}`);

    } catch (error) {
        console.error(error);
        pushEventLog(`카메라 ${cameraId} 중지 오류 발생`);
    } finally {
        clearButtonBusy(btnStopEl);
        if (!applyCachedCameraStatus()) {
            await loadCameraStatus();
        }
    }
}

document.addEventListener("DOMContentLoaded", async () => {
    if (!ensureLoggedIn()) return;

    if (cameraSelectEl) {
        cameraSelectEl.addEventListener("change", async () => {
            cameraId = Number(cameraSelectEl.value) || 1;

            // 즉시 UI 레이블 업데이트 (이전 카메라 정보 잔상 제거)
            const selectedOpt = cameraSelectEl.options[cameraSelectEl.selectedIndex];
            if (liveNameEl && selectedOpt) {
                // "이름 (ID) - 상태" 형식에서 이름만 추출
                liveNameEl.textContent = selectedOpt.text.split(" (")[0];
            }
            if (liveIdEl) liveIdEl.textContent = "ID: " + cameraId;
            if (liveBadgeEl) {
                liveBadgeEl.textContent = "Syncing...";
                liveBadgeEl.className = "status-pill status-pill--neutral";
            }

            // 하단 상세 상태 패널 초기화
            if (cameraStatusEl) {
                cameraStatusEl.innerHTML = `<div class="page-desc">카메라 정보를 동기화 중입니다...</div>`;
            }

            // 버튼 비지 상태 및 비활성화 초기화
            clearButtonBusy(btnStartEl);
            clearButtonBusy(btnStopEl);
            if (!applyCachedCameraStatus()) {
                updateControlButtons("Unknown");
            }

            await switchCameraGroup(cameraId);
            await loadCameraStatus();
        });
    }

    if (btnStartEl) btnStartEl.addEventListener("click", startCamera);
    if (btnStopEl) btnStopEl.addEventListener("click", stopCamera);
    if (btnQuickPrevEl) btnQuickPrevEl.addEventListener("click", () => selectCameraByIndex(cameraSelectEl.selectedIndex - 1));
    if (btnQuickNextEl) btnQuickNextEl.addEventListener("click", () => selectCameraByIndex(cameraSelectEl.selectedIndex + 1));

    setHubState("reconnecting");
    initThreeScene(); // Initialize 3D View
    await loadCameraOptions();
    await connectCameraStatusHub();
    await loadCameraStatus();
});
