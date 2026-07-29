import { defineConfig } from "vite";

export default defineConfig({
  base: "./",
  root: ".",
  publicDir: "public",
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://127.0.0.1:8765",
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
});
