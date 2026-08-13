import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";

export default defineConfig({
  base: "./",
  root: ".",
  publicDir: "public",
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
