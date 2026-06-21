/**
 * app.js — Premium Frontend Controller v2.0
 * Manages Socket.IO events, state synchronization, customizable settings,
 * compact process table rendering, column sorting, and modals.
 */

document.addEventListener('DOMContentLoaded', () => {
    // Native WebSocket client wrapper imitating Socket.io
    let isConnected = false;
    const socketCallbacks = {};
    let ws = null;
    
    function initWebSocket() {
        const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        const wsUrl = `${protocol}//${window.location.host}/ws`;
        ws = new WebSocket(wsUrl);
        
        ws.onopen = () => {
            isConnected = true;
            console.log('WebSocket connected successfully');
            if (socketCallbacks['connect']) {
                socketCallbacks['connect']();
            }
        };
        
        ws.onclose = () => {
            isConnected = false;
            console.warn('WebSocket disconnected. Reconnecting...');
            if (socketCallbacks['disconnect']) {
                socketCallbacks['disconnect']();
            }
            setTimeout(initWebSocket, 2000);
        };
        
        ws.onmessage = (event) => {
            try {
                const message = JSON.parse(event.data);
                const eventName = message.event;
                const payload = message.payload;
                if (socketCallbacks[eventName]) {
                    socketCallbacks[eventName](payload);
                }
            } catch (e) {
                console.warn('Error processing WS message:', e);
            }
        };
    }
    
    initWebSocket();
    
    const socket = {
        on: (eventName, callback) => {
            socketCallbacks[eventName] = callback;
        },
        emit: (eventName, payload) => {
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ event: eventName, payload: payload }));
            }
        },
        get connected() {
            return isConnected;
        }
    };

    // DOM Elements
    const systemRamCircle = document.getElementById('system-ram-circle');
    const systemRamPercent = document.getElementById('system-ram-percent');
    const systemRamUsed = document.getElementById('system-ram-used');
    const systemRamAvail = document.getElementById('system-ram-avail');
    const systemRamStandby = document.getElementById('system-ram-standby');
    const adminBadge = document.getElementById('admin-badge');
    const connectionDot = document.getElementById('connection-dot');
    const connectionText = document.getElementById('connection-text');
    const serverUptime = document.getElementById('server-uptime');
    const processTableBody = document.getElementById('process-table-body');
    const noProcessElement = document.getElementById('no-process-element');

    const robloxCount = document.getElementById('roblox-count');
    const robloxTotalRam = document.getElementById('roblox-total-ram');
    const toggleAutoOptimize = document.getElementById('toggle-auto-optimize');
    const countdownLabel = document.getElementById('countdown-label');
    const rangeInterval = document.getElementById('range-interval');
    const intervalLabel = document.getElementById('interval-label');
    const autoFlushTierSelect = document.getElementById('auto-flush-tier-select');

    const statTotalSaved = document.getElementById('stat-total-saved');
    const statOptCount = document.getElementById('stat-opt-count');
    const statSuspendCount = document.getElementById('stat-suspend-count');

    const searchInput = document.getElementById('search-input');
    const selectAllCheckbox = document.getElementById('select-all-checkbox');
    const btnBulkOptimize = document.getElementById('btn-bulk-optimize');
    const btnBulkSuspend = document.getElementById('btn-bulk-suspend');

    const confirmModal = document.getElementById('confirm-modal');
    const btnConfirmCancel = document.getElementById('btn-confirm-cancel');
    const btnConfirmOk = document.getElementById('btn-confirm-ok');

    const killAllModal = document.getElementById('kill-all-modal');
    const btnKillAllCancel = document.getElementById('btn-kill-all-cancel');
    const btnKillAllConfirm = document.getElementById('btn-kill-all-confirm');
    const btnKillAll = document.getElementById('btn-kill-all');

    // Global limit and Pagefile DOM Elements
    const toggleGlobalLimit = document.getElementById('toggle-global-limit');
    const rangeGlobalLimit = document.getElementById('range-global-limit');
    const globalLimitVal = document.getElementById('global-limit-val');
    const selectVramSize = document.getElementById('select-vram-size');
    const btnSetVram = document.getElementById('btn-set-vram');
    const currentVramText = document.getElementById('current-vram-text');

    // Update elements
    const inputUpdateUrl = document.getElementById('input-update-url');
    const btnSaveUpdateUrl = document.getElementById('btn-save-update-url');
    const btnCheckUpdate = document.getElementById('btn-check-update');
    const updateStatusArea = document.getElementById('update-status-area');
    const updateStatusText = document.getElementById('update-status-text');
    const btnApplyUpdate = document.getElementById('btn-apply-update');
    const btnExportManifest = document.getElementById('btn-export-manifest');

    // RAM Limit Modal DOM Elements
    const limitModal = document.getElementById('limit-modal');
    const limitModalLabel = document.getElementById('limit-modal-label');
    const rangeLimitVal = document.getElementById('range-limit-val');
    const limitValText = document.getElementById('limit-val-text');
    const btnLimitRemove = document.getElementById('btn-limit-remove');
    const btnLimitCancel = document.getElementById('btn-limit-cancel');
    const btnLimitSave = document.getElementById('btn-limit-save');

    // State Management variables
    let allProcesses = [];
    let selectedPids = new Set();
    let currentConfirmAction = null;
    let totalOptCount = 0;
    let sortColumn = 'label';
    let sortDirection = 'asc'; // 'asc' or 'desc'
    let sessionUptimeSeconds = 0;
    let currentLimitPid = null;

    // session uptime counter
    setInterval(() => {
        sessionUptimeSeconds++;
        const hours = Math.floor(sessionUptimeSeconds / 3600).toString().padStart(2, '0');
        const mins = Math.floor((sessionUptimeSeconds % 3600) / 60).toString().padStart(2, '0');
        const secs = (sessionUptimeSeconds % 60).toString().padStart(2, '0');
        serverUptime.textContent = `${hours}:${mins}:${secs}`;
    }, 1000);

    // Auto Flush Countdown ticking state
    let autoFlushRemainingSeconds = -1;
    let countdownTimerId = null;

    function updateCountdownUI() {
        if (!toggleAutoOptimize.checked || autoFlushRemainingSeconds < 0) {
            countdownLabel.textContent = '';
            return;
        }

        const mins = Math.floor(autoFlushRemainingSeconds / 60);
        const secs = autoFlushRemainingSeconds % 60;
        countdownLabel.textContent = `(Còn ${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')})`;
    }

    function startLocalCountdown() {
        if (countdownTimerId) clearInterval(countdownTimerId);
        countdownTimerId = setInterval(() => {
            if (toggleAutoOptimize.checked && autoFlushRemainingSeconds > 0) {
                autoFlushRemainingSeconds--;
                updateCountdownUI();
            } else if (autoFlushRemainingSeconds === 0) {
                countdownLabel.textContent = '(Đang dọn...)';
            } else {
                countdownLabel.textContent = '';
            }
        }, 1000);
    }

    // Initial setup
    fetchSettings();
    fetchHistory();
    fetchProcessesData();
    fetchPagefileData();
    startLocalCountdown();

    // Polling fallback if disconnected
    setInterval(async () => {
        if (!socket || !socket.connected) {
            updateConnectionUI(false);
            await fetchProcessesData();
        } else {
            updateConnectionUI(true);
        }
    }, 2000);

    async function fetchProcessesData() {
        try {
            const procRes = await fetch('/api/processes');
            if (procRes.ok) {
                allProcesses = await procRes.json();
                renderProcessTable();
                updateSummaryStats();
            }
            const sysRes = await fetch('/api/system');
            if (sysRes.ok) {
                const sysData = await sysRes.json();
                updateSystemStats(sysData);
            }
            // If socket is not connected, also fetch history to update statistics
            if (!socket || !socket.connected) {
                await fetchHistory();
            }
        } catch (err) {
            console.warn('API fetch failed:', err);
        }
    }

    async function fetchPagefileData() {
        try {
            const res = await fetch('/api/system/pagefile');
            if (res.ok) {
                const data = await res.json();
                if (data.auto) {
                    currentVramText.textContent = 'Auto Managed';
                    selectVramSize.value = 'auto';
                } else {
                    currentVramText.textContent = `${data.size_gb} GB`;
                    selectVramSize.value = data.size_gb.toString();
                }
            }
        } catch (err) {
            console.error('Failed to fetch pagefile data:', err);
        }
    }

    function updateConnectionUI(online) {
        if (online) {
            connectionDot.className = 'connection-dot status-online';
            connectionText.className = 'status-text-online';
            connectionText.textContent = 'Đang hoạt động';
        } else {
            connectionDot.className = 'connection-dot status-offline';
            connectionText.className = 'status-text-offline';
            connectionText.textContent = 'Mất kết nối';
        }
    }

    // ──────────────────────────────────────────────
    // Socket.IO Events
    // ──────────────────────────────────────────────
    if (socket) {
        socket.on('connect', () => {
            updateConnectionUI(true);
            showToast('Kết nối thành công tới Optimizer backend', 'success');
            fetchProcessesData();
        });

        socket.on('disconnect', () => {
            updateConnectionUI(false);
            showToast('Mất kết nối với backend', 'danger');
        });

        socket.on('system_update', (data) => {
            updateSystemStats(data);
        });

        socket.on('process_update', (data) => {
            allProcesses = data;
            renderProcessTable();
            updateSummaryStats();
        });

        socket.on('auto_optimize_tick', (data) => {
            if (data.reason === 'no_roblox_processes') {
                showToast('Auto Flush: Không tìm thấy tiến trình Roblox nào đang chạy', 'warning');
            } else {
                if (data.processes > 0) {
                    showToast(`Auto Flush: Đã tự động dọn dẹp và tiết kiệm thêm ${data.saved_mb} MB`, 'success');
                    totalOptCount += data.processes;
                    statOptCount.textContent = totalOptCount;
                }
                
                if (data.skipped > 0 && data.results) {
                    const reasons = new Set();
                    data.results.forEach(r => {
                        if (r.skipped && r.reason) {
                            if (r.reason === 'active_foreground') {
                                reasons.add('game đang chơi');
                            } else if (r.reason === 'startup_protection') {
                                reasons.add('game mới chạy < 60s');
                            } else if (r.reason === 'cooldown') {
                                reasons.add('đang cooldown');
                            } else if (r.reason === 'high_page_faults') {
                                reasons.add('hiệu năng bận');
                            } else if (r.reason === 'heavy_io') {
                                reasons.add('đọc ghi đĩa nặng');
                            } else if (r.reason === 'already_min_footprint') {
                                reasons.add('RAM tối thiểu');
                            }
                        }
                    });
                    if (reasons.size > 0) {
                        const reasonStr = Array.from(reasons).join(', ');
                        showToast(`Auto Flush: Bỏ qua ${data.skipped} tiến trình để bảo vệ hiệu năng (${reasonStr})`, 'warning');
                    }
                }
            }
            fetchHistory(); 
        });

        socket.on('optimize_result', (data) => {
            if (data.success) {
                if (data.skipped) {
                    showToast(`Bỏ qua PID ${data.pid}: Cần duy trì hiệu năng (${data.reason === 'cooldown' ? 'đang cooldown' : 'page fault cao'})`, 'warning');
                } else {
                    if (data.downgrade_reasons && data.downgrade_reasons.length > 0) {
                        let reasonText = '';
                        if (data.downgrade_reasons.includes('active_foreground')) {
                            reasonText = 'game đang chơi';
                        } else if (data.downgrade_reasons.includes('startup_protection')) {
                            reasonText = 'game mới chạy';
                        } else if (data.downgrade_reasons.includes('no_pagefile')) {
                            reasonText = 'thiếu Pagefile';
                        } else {
                            reasonText = data.downgrade_reasons.join(', ');
                        }
                        showToast(`PID ${data.pid}: Tự động hạ cấp sang Safe Flush do ${reasonText} để tránh lag. Tiết kiệm ${data.saved_mb} MB`, 'warning');
                    } else {
                        showToast(`Tối ưu hoàn tất PID ${data.pid}: Tiết kiệm ${data.saved_mb} MB`, 'success');
                    }
                }
            } else {
                showToast(`Lỗi tối ưu PID ${data.pid}: ${data.error}`, 'danger');
            }
        });

        socket.on('ghost_killed', (data) => {
            showToast(`Đã tự động diệt tab Roblox Ghost (PID: ${data.pid}, Nhãn: ${data.label}) chạy ngầm đơ nền`, 'warning');
            fetchProcessesData();
        });

        socket.on('emergency_flush_triggered', (data) => {
            showToast(`⚠️ CẢNH BÁO: RAM hệ thống vượt quá ${data.percent}%. Đang kích hoạt giải phóng bộ nhớ khẩn cấp (Emergency Flush) cho các tab ẩn!`, 'danger');
        });
    }

    // ──────────────────────────────────────────────
    // API Request helpers
    // ──────────────────────────────────────────────
    async function apiPost(endpoint, body = {}) {
        try {
            const res = await fetch(endpoint, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
            if (res.ok) return await res.json();
            throw new Error(`API error ${res.status}`);
        } catch (err) {
            console.error(err);
            showToast('Không thể kết nối đến API backend', 'danger');
        }
    }

    async function apiDelete(endpoint) {
        try {
            const res = await fetch(endpoint, { method: 'DELETE' });
            if (res.ok) return await res.json();
            throw new Error(`API error ${res.status}`);
        } catch (err) {
            console.error(err);
            showToast('Không thể kết nối đến API backend', 'danger');
        }
    }

    // ──────────────────────────────────────────────
    // Settings API handlers
    // ──────────────────────────────────────────────
    async function fetchSettings() {
        try {
            const res = await fetch('/api/settings');
            const data = await res.json();
            
            toggleAutoOptimize.checked = data.auto_optimize || false;
            const intervalMin = data.auto_interval_minutes || 10;
            rangeInterval.value = intervalMin;
            intervalLabel.textContent = `Mỗi ${intervalMin} phút`;
            
            if (data.auto_flush_tier) {
                autoFlushTierSelect.value = data.auto_flush_tier;
            }

            if (toggleGlobalLimit && data.global_limit_enabled !== undefined) {
                toggleGlobalLimit.checked = data.global_limit_enabled || false;
            }
            if (rangeGlobalLimit && data.global_limit_mb !== undefined) {
                rangeGlobalLimit.value = data.global_limit_mb;
                globalLimitVal.textContent = `${data.global_limit_mb} MB`;
            }
            if (inputUpdateUrl && data.update_server_url !== undefined) {
                inputUpdateUrl.value = data.update_server_url || '';
            }
        } catch (err) {
            console.error(err);
        }
    }

    async function fetchHistory() {
        try {
            const res = await fetch('/api/history');
            const data = await res.json();
            
            const savedGb = (data.total_saved_mb / 1024).toFixed(2);
            statTotalSaved.textContent = `${savedGb} GB`;
            
            if (data.history) {
                totalOptCount = data.history.reduce((acc, curr) => acc + (curr.processes || 0), 0);
                statOptCount.textContent = totalOptCount;
            }
        } catch (err) {
            console.error(err);
        }
    }

    function updateSystemStats(data) {
        const percent = data.percent;
        systemRamPercent.textContent = `${percent}%`;
        systemRamCircle.setAttribute('stroke-dasharray', `${percent}, 100`);

        // Color based on RAM pressure
        if (percent > 90) {
            systemRamCircle.style.stroke = 'var(--color-danger)';
        } else if (percent > 75) {
            systemRamCircle.style.stroke = 'var(--color-warning)';
        } else {
            systemRamCircle.style.stroke = 'var(--accent-purple)';
        }

        systemRamUsed.textContent = `${data.used_gb.toFixed(1)} GB / ${data.total_gb.toFixed(1)} GB`;
        systemRamAvail.textContent = `${data.available_gb.toFixed(1)} GB`;
        
        const standbyGb = data.standby_mb ? (data.standby_mb / 1024).toFixed(1) : (data.available_gb * 0.35).toFixed(1);
        systemRamStandby.textContent = `${standbyGb} GB`;

        if (data.is_admin) {
            adminBadge.className = 'admin-badge';
            adminBadge.innerHTML = '<span class="badge-icon">🛡️</span> Quyền Administrator';
        } else {
            adminBadge.className = 'admin-badge offline-badge';
            adminBadge.innerHTML = '<span class="badge-icon">⚠️</span> Quyền User (Thiếu Admin)';
        }

        // Sync countdown timer from backend
        if (data.auto_flush_remaining_seconds !== undefined) {
            autoFlushRemainingSeconds = data.auto_flush_remaining_seconds;
            updateCountdownUI();
        }
    }

    // ──────────────────────────────────────────────
    // Global actions click handlers
    // ──────────────────────────────────────────────
    document.getElementById('btn-optimize-all').addEventListener('click', async () => {
        showToast('Đang tiến hành tối ưu hóa tất cả tabs (Safe)...', 'warning');
        const data = await apiPost('/api/optimize/all', { tier: 'safe' });
        if (data && data.count > 0) {
            showToast(`Tối ưu hoàn tất! Tiết kiệm thêm ${data.total_saved_mb} MB`, 'success');
            fetchHistory();
            fetchProcessesData();
        } else {
            showToast('Không có tab Roblox nào cần tối ưu hoặc đang cooldown', 'warning');
        }
    });

    document.getElementById('btn-clear-standby').addEventListener('click', async () => {
        showToast('Đang giải phóng Standby Memory...', 'warning');
        const res = await apiPost('/api/system/clear-standby');
        if (res && res.success) {
            if (res.skipped) {
                showToast('Bỏ qua: Vừa dọn dẹp gần đây, vui lòng đợi 60 giây.', 'warning');
            } else {
                showToast('Đã giải phóng Standby Memory cache thành công!', 'success');
            }
        } else {
            showToast(`Lỗi: ${res ? res.error : 'Không có quyền Admin'}`, 'danger');
        }
    });

    document.getElementById('btn-flush-modified').addEventListener('click', async () => {
        showToast('Đang dọn Modified page list...', 'warning');
        const res = await apiPost('/api/system/flush-modified');
        if (res && res.success) {
            if (res.skipped) {
                showToast('Bỏ qua: Vừa flush gần đây, vui lòng đợi 60 giây.', 'warning');
            } else {
                showToast('Đã flush thành công dirty memory list!', 'success');
            }
        } else {
            showToast(`Lỗi: ${res ? res.error : 'Không có quyền Admin'}`, 'danger');
        }
    });

    // Auto Flush toggles
    toggleAutoOptimize.addEventListener('change', async () => {
        const enabled = toggleAutoOptimize.checked;
        await apiPost('/api/settings', { auto_optimize: enabled });
        showToast(`Tự động dọn RAM (Auto Flush): ${enabled ? 'Đã kích hoạt' : 'Đã dừng'}`, 'success');
        if (!enabled) {
            autoFlushRemainingSeconds = -1;
            updateCountdownUI();
        }
    });

    rangeInterval.addEventListener('input', () => {
        intervalLabel.textContent = `Mỗi ${rangeInterval.value} phút`;
    });

    rangeInterval.addEventListener('change', async () => {
        const val = parseInt(rangeInterval.value);
        await apiPost('/api/settings', { auto_interval_minutes: val });
        showToast(`Tần suất dọn RAM đổi thành: ${val} phút`, 'success');
    });

    autoFlushTierSelect.addEventListener('change', async () => {
        const tier = autoFlushTierSelect.value;
        await apiPost('/api/settings', { auto_flush_tier: tier });
        showToast(`Chế độ Auto Flush đổi thành: ${tier.toUpperCase()}`, 'success');
    });

    // Global limit controls listeners
    if (toggleGlobalLimit) {
        toggleGlobalLimit.addEventListener('change', async () => {
            const enabled = toggleGlobalLimit.checked;
            await apiPost('/api/settings', { global_limit_enabled: enabled });
            showToast(`Tự động giới hạn RAM (Global Limit): ${enabled ? 'Đã kích hoạt' : 'Đã tắt'}`, 'success');
        });
    }

    if (rangeGlobalLimit) {
        rangeGlobalLimit.addEventListener('input', () => {
            globalLimitVal.textContent = `${rangeGlobalLimit.value} MB`;
        });

        rangeGlobalLimit.addEventListener('change', async () => {
            const val = parseInt(rangeGlobalLimit.value);
            await apiPost('/api/settings', { global_limit_mb: val });
            showToast(`Giới hạn RAM mặc định đổi thành: ${val} MB`, 'success');
        });
    }

    // Virtual RAM setup button listener
    if (btnSetVram) {
        btnSetVram.addEventListener('click', async () => {
            const val = selectVramSize.value;
            showToast('Đang tiến hành thiết lập RAM ảo (Pagefile) hệ thống...', 'warning');
            
            let size_gb = 0;
            if (val !== 'auto') {
                size_gb = parseInt(val);
            }
            
            const res = await apiPost('/api/system/pagefile', { size_gb: size_gb });
            if (res && res.success) {
                showToast('Đã cấu hình RAM ảo thành công! Vui lòng KHỞI ĐỘNG LẠI máy tính để áp dụng.', 'success');
                fetchPagefileData();
            } else {
                showToast(`Lỗi cấu hình RAM ảo: ${res ? res.error : 'Không có phản hồi'}`, 'danger');
            }
        });
    }

    // Save update server URL
    if (btnSaveUpdateUrl) {
        btnSaveUpdateUrl.addEventListener('click', async () => {
            const urlVal = inputUpdateUrl.value.trim();
            await apiPost('/api/settings', { update_server_url: urlVal });
            showToast('Đã lưu cấu hình máy chủ cập nhật!', 'success');
        });
    }

    // Check for updates
    if (btnCheckUpdate) {
        btnCheckUpdate.addEventListener('click', async () => {
            showToast('Đang quét bản cập nhật mới...', 'warning');
            updateStatusArea.style.display = 'none';
            btnApplyUpdate.style.display = 'none';

            try {
                const res = await apiPost('/api/update/check');
                if (res && res.success) {
                    updateStatusArea.style.display = 'block';
                    if (res.update_available) {
                        const fileCount = res.files.length;
                        updateStatusText.textContent = `Có bản cập nhật mới (${fileCount} tệp tin khác biệt).`;
                        updateStatusText.style.color = 'var(--accent-cyan)';
                        btnApplyUpdate.style.display = 'block';
                    } else {
                        updateStatusText.textContent = 'Ứng dụng đang ở phiên bản mới nhất!';
                        updateStatusText.style.color = 'var(--color-success)';
                    }
                } else {
                    showToast(res ? res.error : 'Không thể check update', 'danger');
                }
            } catch (err) {
                showToast('Lỗi kết nối kiểm tra cập nhật', 'danger');
            }
        });
    }

    // Apply updates
    if (btnApplyUpdate) {
        btnApplyUpdate.addEventListener('click', async () => {
            showToast('Bắt đầu tải bản cập nhật...', 'warning');
            btnApplyUpdate.disabled = true;
            updateStatusText.textContent = 'Đang tải bản cập nhật...';

            try {
                const res = await apiPost('/api/update/apply');
                if (res && res.success) {
                    if (res.restart_required) {
                        updateStatusText.textContent = 'Tải xong! Đang tự động restart...';
                        showToast('Đang tiến hành tự động khởi động lại ứng dụng!', 'success');
                        setTimeout(async () => {
                            await apiPost('/api/update/restart');
                        }, 1000);
                    } else {
                        updateStatusText.textContent = 'Cập nhật thành công các tệp tin phụ!';
                        showToast('Đã cập nhật các tệp tin HTML/JS/DLL thành công!', 'success');
                        btnApplyUpdate.disabled = false;
                        btnApplyUpdate.style.display = 'none';
                        fetchProcessesData();
                    }
                } else {
                    showToast(res ? res.error : 'Cập nhật thất bại', 'danger');
                    btnApplyUpdate.disabled = false;
                    updateStatusText.textContent = 'Cập nhật thất bại.';
                }
            } catch (err) {
                showToast('Lỗi trong quá trình cập nhật', 'danger');
                btnApplyUpdate.disabled = false;
                updateStatusText.textContent = 'Lỗi cập nhật.';
            }
        });
    }

    // Export update manifest
    if (btnExportManifest) {
        btnExportManifest.addEventListener('click', async () => {
            showToast('Đang tạo file update_manifest.json...', 'warning');
            try {
                const res = await apiPost('/api/update/export-manifest');
                if (res && res.success) {
                    showToast('Đã tạo file update_manifest.json thành công!', 'success');
                } else {
                    showToast(res ? res.error : 'Tạo manifest thất bại', 'danger');
                }
            } catch (err) {
                showToast('Lỗi khi kết nối tạo manifest', 'danger');
            }
        });
    }

    // Limit modal preset buttons click listeners
    document.querySelectorAll('.limit-presets .btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const val = btn.dataset.preset;
            rangeLimitVal.value = val;
            limitValText.textContent = `${val} MB`;
        });
    });

    if (rangeLimitVal) {
        rangeLimitVal.addEventListener('input', () => {
            limitValText.textContent = `${rangeLimitVal.value} MB`;
        });
    }

    if (btnLimitCancel) {
        btnLimitCancel.addEventListener('click', () => {
            limitModal.classList.remove('show');
            currentLimitPid = null;
        });
    }

    if (btnLimitRemove) {
        btnLimitRemove.addEventListener('click', async () => {
            if (currentLimitPid) {
                showToast(`Đang gỡ giới hạn RAM cho PID ${currentLimitPid}...`, 'warning');
                const res = await apiPost(`/api/limit/remove/${currentLimitPid}`);
                if (res && res.success) {
                    showToast(`Đã gỡ giới hạn RAM thành công cho PID ${currentLimitPid}`, 'success');
                    limitModal.classList.remove('show');
                    currentLimitPid = null;
                    fetchProcessesData();
                } else {
                    showToast(`Lỗi gỡ giới hạn RAM: ${res ? res.error : 'Có lỗi xảy ra'}`, 'danger');
                }
            }
        });
    }

    if (btnLimitSave) {
        btnLimitSave.addEventListener('click', async () => {
            if (currentLimitPid) {
                const val = parseInt(rangeLimitVal.value);
                showToast(`Đang cấu hình giới hạn Working Set RAM ${val}MB cho PID ${currentLimitPid}...`, 'warning');
                const res = await apiPost(`/api/limit/${currentLimitPid}`, { limit_mb: val });
                if (res && res.success) {
                    showToast(`Đã đặt giới hạn Working Set RAM ${val}MB cho PID ${currentLimitPid}`, 'success');
                    limitModal.classList.remove('show');
                    currentLimitPid = null;
                    fetchProcessesData();
                } else {
                    showToast(`Lỗi cấu hình RAM Limit: ${res ? res.error : 'Có lỗi xảy ra'}`, 'danger');
                }
            }
        });
    }

    // Kill All Processes Modal triggering
    btnKillAll.addEventListener('click', () => {
        killAllModal.classList.add('show');
    });

    btnKillAllCancel.addEventListener('click', () => {
        killAllModal.classList.remove('show');
    });

    btnKillAllConfirm.addEventListener('click', async () => {
        killAllModal.classList.remove('show');
        showToast('Đang tắt toàn bộ tiến trình Roblox...', 'danger');
        const res = await apiPost('/api/kill/all');
        if (res) {
            showToast(`Đã đóng thành công ${res.count} Roblox processes.`, 'success');
            selectedPids.clear();
            selectAllCheckbox.checked = false;
            updateBulkButtonStates();
            fetchProcessesData();
        }
    });



    // ──────────────────────────────────────────────
    // Bulk operation buttons and Selection logic
    // ──────────────────────────────────────────────
    searchInput.addEventListener('input', renderProcessTable);

    selectAllCheckbox.addEventListener('change', () => {
        const checked = selectAllCheckbox.checked;
        const visibleRows = document.querySelectorAll('.process-table tbody tr');
        
        visibleRows.forEach(row => {
            const pid = parseInt(row.dataset.pid);
            const cb = row.querySelector('.row-select-checkbox');
            if (cb) {
                cb.checked = checked;
                if (checked) {
                    selectedPids.add(pid);
                    row.classList.add('selected');
                } else {
                    selectedPids.delete(pid);
                    row.classList.remove('selected');
                }
            }
        });
        updateBulkButtonStates();
    });

    function updateBulkButtonStates() {
        const size = selectedPids.size;
        const disable = size === 0;
        
        btnBulkOptimize.disabled = disable;
        btnBulkSuspend.disabled = disable;

        btnBulkOptimize.innerHTML = `⚡ Optimize (${size})`;
        btnBulkSuspend.innerHTML = `❄️ Suspend (${size})`;
    }

    btnBulkOptimize.addEventListener('click', async () => {
        showToast(`Đang gửi yêu cầu tối ưu safe cho ${selectedPids.size} tabs...`, 'warning');
        for (const pid of selectedPids) {
            await apiPost(`/api/optimize/${pid}`, { tier: 'safe' });
        }
        selectedPids.clear();
        selectAllCheckbox.checked = false;
        updateBulkButtonStates();
        fetchProcessesData();
    });



    btnBulkSuspend.addEventListener('click', async () => {
        const size = selectedPids.size;
        if (confirm(`Bạn có chắc chắn muốn đóng băng (suspend) ${size} tab Roblox đã chọn?`)) {
            showToast(`Đang đóng băng ${size} tabs...`, 'warning');
            for (const pid of selectedPids) {
                await apiPost(`/api/suspend/${pid}`);
            }
            selectedPids.clear();
            selectAllCheckbox.checked = false;
            updateBulkButtonStates();
            fetchProcessesData();
        }
    });

    // ──────────────────────────────────────────────
    // Sorting Click Handlers
    // ──────────────────────────────────────────────
    document.querySelectorAll('.process-table th.sortable').forEach(th => {
        th.addEventListener('click', () => {
            const col = th.dataset.sort;
            if (sortColumn === col) {
                sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
            } else {
                sortColumn = col;
                sortDirection = 'asc';
            }
            
            // Update sort icon indicators on table headers
            document.querySelectorAll('.process-table th.sortable').forEach(header => {
                const icon = header.querySelector('.sort-icon');
                if (header.dataset.sort === sortColumn) {
                    icon.textContent = sortDirection === 'asc' ? ' ▲' : ' ▼';
                } else {
                    icon.textContent = '';
                }
            });
            
            renderProcessTable();
        });
    });

    // ──────────────────────────────────────────────
    // Custom Table Sorting & Rendering (Diff-Based Layout)
    // ──────────────────────────────────────────────
    function renderProcessTable() {
        const query = searchInput.value.toLowerCase();
        
        // Filter processes
        let filtered = allProcesses.filter(p => {
            return p.pid.toString().includes(query) || p.label.toLowerCase().includes(query);
        });

        // Sort processes
        filtered.sort((a, b) => {
            let valA, valB;
            
            if (sortColumn === 'label') {
                // Natural sort label: extract number
                const numA = parseInt(a.label.match(/\d+/) || 0);
                const numB = parseInt(b.label.match(/\d+/) || 0);
                valA = numA;
                valB = numB;
            } else if (sortColumn === 'pid') {
                valA = a.pid;
                valB = b.pid;
            } else if (sortColumn === 'ram') {
                valA = a.ram_mb;
                valB = b.ram_mb;
            } else if (sortColumn === 'cpu') {
                valA = a.cpu_percent;
                valB = b.cpu_percent;
            } else if (sortColumn === 'threads') {
                valA = a.threads || 0;
                valB = b.threads || 0;
            } else if (sortColumn === 'status') {
                valA = a.status;
                valB = b.status;
            } else if (sortColumn === 'limit') {
                valA = a.ram_limit_mb || 999999;
                valB = b.ram_limit_mb || 999999;
            } else {
                valA = a.label;
                valB = b.label;
            }

            if (valA < valB) return sortDirection === 'asc' ? -1 : 1;
            if (valA > valB) return sortDirection === 'asc' ? 1 : -1;
            return 0;
        });

        if (filtered.length === 0) {
            processTableBody.innerHTML = '';
            noProcessElement.style.display = 'flex';
            return;
        } else {
            noProcessElement.style.display = 'none';
        }

        // Map tracked nodes in tbody to perform diff-based DOM sync
        const currentRows = {};
        processTableBody.querySelectorAll('tr').forEach(row => {
            currentRows[row.dataset.pid] = row;
        });

        // Build list order
        const listFragment = document.createDocumentFragment();

        filtered.forEach(p => {
            let row = currentRows[p.pid];
            const isSelected = selectedPids.has(p.pid);
            
            if (!row) {
                row = document.createElement('tr');
                row.dataset.pid = p.pid;
                
                row.innerHTML = `
                    <td class="col-select"><input type="checkbox" class="row-select-checkbox"></td>
                    <td class="col-label">${p.label}</td>
                    <td class="col-pid">${p.pid}</td>
                    <td class="col-ram">
                        <span class="ram-text">${p.ram_mb} MB</span>
                    </td>
                    <td class="col-cpu">${p.cpu_percent}%</td>
                    <td class="col-threads">${p.threads || 0}</td>
                    <td class="col-status">
                        <span class="status-indicator"></span>
                    </td>
                    <td class="col-limit">
                        <span class="limit-badge-container"></span>
                    </td>
                    <td class="col-actions">
                        <div class="row-actions">
                            <button class="btn-icon-only btn-opt" title="Dọn RAM nhẹ (Safe)">⚡</button>
                            <button class="btn-icon-only btn-limit" title="Giới hạn RAM cứng">🔒</button>
                            <button class="btn-icon-only btn-freeze" title="Đóng băng tab">❄️</button>
                            <button class="btn-icon-only btn-kill" title="Đóng tab này">☠️</button>
                        </div>
                    </td>
                `;

                // Add Event listeners to the interactive row elements
                row.querySelector('.row-select-checkbox').addEventListener('change', (e) => {
                    const checked = e.target.checked;
                    if (checked) {
                        selectedPids.add(p.pid);
                        row.classList.add('selected');
                    } else {
                        selectedPids.delete(p.pid);
                        row.classList.remove('selected');
                    }
                    updateBulkButtonStates();
                });

                row.querySelector('.btn-opt').addEventListener('click', () => {
                    apiPost(`/api/optimize/${p.pid}`, { tier: 'safe' });
                });

                row.querySelector('.btn-limit').addEventListener('click', () => {
                    currentLimitPid = p.pid;
                    limitModalLabel.textContent = p.label;
                    const curLimit = p.ram_limit_mb || 600;
                    rangeLimitVal.value = curLimit;
                    limitValText.textContent = `${curLimit} MB`;
                    limitModal.classList.add('show');
                });



                row.querySelector('.btn-freeze').addEventListener('click', () => {
                    if (p.status === 'suspended') {
                        apiPost(`/api/resume/${p.pid}`);
                    } else {
                        if (confirm(`Bạn có chắc chắn muốn đóng băng (freeze) ${p.label}? Game sẽ bị ngưng hoạt động cho đến khi được phục hồi (resume).`)) {
                            apiPost(`/api/suspend/${p.pid}`);
                        }
                    }
                });

                row.querySelector('.btn-kill').addEventListener('click', () => {
                    if (confirm(`Xác nhận đóng tab Roblox ${p.label} (PID: ${p.pid})?`)) {
                        apiPost(`/api/kill/${p.pid}`);
                    }
                });
            }

            // Sync dynamic values inside row
            row.className = isSelected ? 'selected' : '';
            row.querySelector('.row-select-checkbox').checked = isSelected;
            row.querySelector('.col-label').textContent = p.label;
            row.querySelector('.col-pid').textContent = p.pid;
            row.querySelector('.ram-text').textContent = `${p.ram_mb} MB`;
            row.querySelector('.col-cpu').textContent = `${p.cpu_percent}%`;
            row.querySelector('.col-threads').textContent = p.threads || 0;
            
            const limitContainer = row.querySelector('.limit-badge-container');
            if (limitContainer) {
                if (p.ram_limit_mb) {
                    limitContainer.innerHTML = `<span class="limit-badge">${p.ram_limit_mb} MB</span>`;
                } else {
                    limitContainer.innerHTML = `<span class="no-limit-text">Không</span>`;
                }
            }
            
            // Translate status
            let statusLabel = 'Chạy';
            let statusClass = 'running';
            if (p.status === 'suspended') {
                statusLabel = 'Đóng băng';
                statusClass = 'suspended';
            } else if (p.status === 'active') {
                statusLabel = 'Active';
                statusClass = 'active';
            }
            const statusIndicator = row.querySelector('.status-indicator');
            if (statusIndicator) {
                statusIndicator.className = `status-indicator ${statusClass}`;
                statusIndicator.textContent = statusLabel;
            }

            listFragment.appendChild(row);
        });

        // Clear existing nodes in DOM and insert sorted/diffed fragments
        processTableBody.innerHTML = '';
        processTableBody.appendChild(listFragment);
    }

    function updateSummaryStats() {
        robloxCount.textContent = allProcesses.length;
        
        const totalMb = allProcesses.reduce((acc, p) => acc + p.ram_mb, 0);
        if (totalMb > 1024) {
            robloxTotalRam.textContent = `${(totalMb / 1024).toFixed(1)} GB`;
        } else {
            robloxTotalRam.textContent = `${totalMb.toFixed(0)} MB`;
        }

        const suspendCount = allProcesses.filter(p => p.status === 'suspended').length;
        statSuspendCount.textContent = suspendCount;
    }



    // Confirm dialog controls
    btnConfirmCancel.addEventListener('click', () => {
        confirmModal.classList.remove('show');
        currentConfirmAction = null;
    });

    btnConfirmOk.addEventListener('click', async () => {
        if (currentConfirmAction) {
            await currentConfirmAction();
        }
        confirmModal.classList.remove('show');
        currentConfirmAction = null;
        fetchProcessesData();
    });

    // Toast Notifications trigger function
    function showToast(message, type = 'success') {
        const container = document.getElementById('toast-container');
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        
        let emoji = '🔔';
        if (type === 'success') emoji = '✅';
        if (type === 'warning') emoji = '⚠️';
        if (type === 'danger') emoji = '❌';

        toast.innerHTML = `<span>${emoji}</span> <div>${message}</div>`;
        container.appendChild(toast);

        // Slide out and remove toast after 4s
        setTimeout(() => {
            toast.style.animation = 'slide-in 0.25s reverse cubic-bezier(0.4, 0, 0.2, 1)';
            setTimeout(() => {
                toast.remove();
            }, 250);
        }, 4000);
    }
});
