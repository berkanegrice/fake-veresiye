import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The app calls the API with relative "/api/..." URLs. In standalone dev that is proxied
// to the ASP.NET Core server below; when the API hosts the built bundle, same-origin covers it.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: process.env.VITE_API_TARGET ?? "http://localhost:5080",
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "dist",
  },
});
