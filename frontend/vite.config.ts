import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // The React app calls /api/...; in development Vite forwards that to the ASP.NET API (no CORS needed).
    proxy: {
      "/api": {
        // API_URL lets a second API instance be used, e.g. API_URL=http://localhost:5299 npm run dev
        target: process.env.API_URL ?? "http://localhost:5288",
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ""),
      },
    },
  },
});
