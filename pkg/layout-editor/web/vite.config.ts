import { fileURLToPath } from "node:url";
import type { Connect } from "vite";
import { defineConfig } from "vite";

/** Dev-server clean URL fallback (mirrors LayoutEditorHttpServer.TryServeStatic). */
function spaFallback(): { name: string; configureServer(server: { middlewares: Connect.Server }) } {
  return {
    name: "layout-editor-spa-fallback",
    configureServer(server) {
      server.middlewares.use((req, _res, next) => {
        const raw = req.url ?? "/";
        const path = raw.split("?")[0] ?? "/";
        if (path.startsWith("/api") || path.startsWith("/@") || path.startsWith("/src")) {
          next();
          return;
        }
        if (path.includes(".")) {
          next();
          return;
        }
        if (path === "/recipes" || path === "/recipes/") {
          req.url = "/recipes.html" + raw.slice(path.length);
          next();
          return;
        }
        req.url = "/index.html" + raw.slice(path.length);
        next();
      });
    },
  };
}

export default defineConfig({
  base: "/",
  root: ".",
  publicDir: "public",
  plugins: [spaFallback()],
  define: {
    __APP_BUILD_TIME__: JSON.stringify(new Date().toISOString()),
  },
  server: {
    port: 5173,
    proxy: {
      "/api": {
        // Mac 开发机 → Windows 虚拟机内的 Unity 服务时，设置 VITE_DEV_PROXY_TARGET=http://<虚拟机IP>:8765
        target: process.env.VITE_DEV_PROXY_TARGET || "http://127.0.0.1:8765",
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    rollupOptions: {
      input: {
        main: fileURLToPath(new URL("./index.html", import.meta.url)),
        recipes: fileURLToPath(new URL("./recipes.html", import.meta.url)),
      },
    },
  },
});
