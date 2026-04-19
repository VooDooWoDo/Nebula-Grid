window.nebulaStorage = {
    _lastActiveHookRegistered: false,

    get(key) {
        return window.localStorage.getItem(key);
    },

    set(key, value) {
        window.localStorage.setItem(key, value);
    },

    remove(key) {
        window.localStorage.removeItem(key);
    },

    registerLastActiveOnClose(apiBaseUrl, selectedPlayerStorageKey) {
        if (this._lastActiveHookRegistered) {
            return;
        }

        let lastSentAt = 0;

        const touchLastActive = () => {
            const now = Date.now();
            if (now - lastSentAt < 1000) {
                return;
            }

            const rawPlayerId = window.localStorage.getItem(selectedPlayerStorageKey);
            const playerId = Number.parseInt(rawPlayerId ?? "0", 10);
            if (!Number.isFinite(playerId) || playerId <= 0) {
                return;
            }

            const trimmedBase = (apiBaseUrl ?? "").replace(/\/+$/, "");
            const endpoint = `${trimmedBase}/api/game/player/${playerId}/touch-last-active`;

            try {
                if (typeof navigator.sendBeacon === "function") {
                    const payload = new Blob(["{}"], { type: "application/json" });
                    navigator.sendBeacon(endpoint, payload);
                } else {
                    fetch(endpoint, { method: "POST", keepalive: true, headers: { "Content-Type": "application/json" }, body: "{}" });
                }

                lastSentAt = now;
            } catch {
                // Best effort only: closing the tab may cancel network requests.
            }
        };

        window.addEventListener("pagehide", touchLastActive, { capture: true });
        window.addEventListener("beforeunload", touchLastActive, { capture: true });
        this._lastActiveHookRegistered = true;
    }
};