const productSelect = document.getElementById("productSelect");
const searchBtn = document.getElementById("btnCheck");
const clearBtn = document.getElementById("btnClear");

const loadingState = document.getElementById("invLoading");
const messageEl = document.getElementById("invMessage");
const resultCard = document.getElementById("invResult");

const resName = document.getElementById("resName");
const resQty = document.getElementById("resQty");
const resTime = document.getElementById("resTime");

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

function showLoading(show) {
    if (!loadingState) return;
    loadingState.hidden = !show;
}

function showMessage(text) {
    if (!messageEl) return;
    messageEl.textContent = text;
}

function showResult(show) {
    if (!resultCard) return;
    resultCard.hidden = !show;
}

function formatNow() {
    return new Date().toLocaleString("ko-KR");
}

function setResultValues(name, qty, time) {
    if (resName) resName.textContent = name;
    if (resQty) resQty.textContent = qty;
    if (resTime) resTime.textContent = time;
}

async function loadProducts() {
    try {
        const res = await fetch("/api/inventory/products", {
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        if (!res.ok) throw new Error("제품 목록 조회 실패");

        const data = await res.json();
        if (!Array.isArray(data)) return;

        productSelect.innerHTML = `<option value="">선택하세요</option>`;

        data.forEach((name) => {
            const opt = document.createElement("option");
            opt.value = name;
            opt.textContent = name;
            productSelect.appendChild(opt);
        });
    } catch (err) {
        console.error(err);
        showMessage("제품 목록을 불러오지 못했습니다.");
    }
}

async function searchInventory() {
    const productName = productSelect.value;

    if (!productName) {
        showResult(false);
        showMessage("제품을 선택해 주세요.");
        return;
    }

    try {
        showLoading(true);
        showResult(false);
        showMessage("조회 중입니다...");

        const url = `/api/inventory/stock?productName=${encodeURIComponent(productName)}`;
        const res = await fetch(url, {
            headers: authHeaders()
        });

        if (await handleUnauthorized(res)) return;
        if (!res.ok) throw new Error("재고 조회 실패");

        const data = await res.json();

        const name = data.productName || productName;
        const qty = (typeof data.remainQuantity === "number")
            ? String(data.remainQuantity)
            : (data.remainQuantity ?? "-");
        const time = formatNow();

        setResultValues(name, qty, time);
        showMessage("조회가 완료되었습니다.");
        showResult(true);
    } catch (err) {
        console.error(err);
        showResult(false);
        showMessage("재고 조회 중 오류가 발생했습니다.");
    } finally {
        showLoading(false);
    }
}

function clearInventory() {
    if (productSelect) productSelect.value = "";
    setResultValues("-", "-", "-");
    showResult(false);
    showMessage("제품을 선택하고 조회하세요.");
}

document.addEventListener("DOMContentLoaded", async () => {
    const token = localStorage.getItem("accessToken");
    if (!token) {
        redirectToLogin();
        return;
    }

    if (searchBtn) searchBtn.addEventListener("click", searchInventory);
    if (clearBtn) clearBtn.addEventListener("click", clearInventory);

    clearInventory();
    await loadProducts();
});