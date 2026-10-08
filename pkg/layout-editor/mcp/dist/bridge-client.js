const DEFAULT_BRIDGE_URL = "http://10.211.55.3:8765";
export class BridgeError extends Error {
    status;
    retryable;
    constructor(message, status = 0, retryable = true) {
        super(message);
        this.name = "BridgeError";
        this.status = status;
        this.retryable = retryable;
    }
}
export class BridgeClient {
    baseUrl;
    constructor(baseUrl = process.env.OC2_EDITOR_BRIDGE_URL || DEFAULT_BRIDGE_URL) {
        this.baseUrl = baseUrl.replace(/\/$/, "");
    }
    async json(path, init) {
        let response;
        try {
            response = await fetch(`${this.baseUrl}${path}`, init);
        }
        catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            throw new BridgeError(`无法连接 Unity Bridge: ${message}`);
        }
        const text = await response.text();
        if (text.trimStart().startsWith("<") || (response.headers.get("content-type") || "").includes("text/html")) {
            throw new BridgeError("Unity Bridge 返回了 HTML，可能正在重载或前端资源已过期");
        }
        let data;
        try {
            data = text ? JSON.parse(text) : {};
        }
        catch {
            throw new BridgeError(`Unity Bridge 返回了非法 JSON: ${text.slice(0, 160)}`, response.status, false);
        }
        if (!response.ok) {
            const message = typeof data === "object" && data !== null && "error" in data
                ? String(data.error || `HTTP ${response.status}`)
                : `HTTP ${response.status}`;
            throw new BridgeError(message, response.status, response.status >= 500 || response.status === 408);
        }
        return data;
    }
    get(path) {
        return this.json(path);
    }
    post(path, body) {
        return this.json(path, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
        });
    }
}
export { DEFAULT_BRIDGE_URL };
