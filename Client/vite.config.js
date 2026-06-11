import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      // SW only built by `yarn build`; never during `yarn dev` (avoids local cache confusion).
      devOptions: { enabled: false },
      manifest: {
        name: 'R3 記帳 — 分帳不再吵架',
        short_name: 'R3',
        lang: 'zh-Hant',
        description: 'R3 — 出遊聚餐分帳神器。LINE 記帳、AI 拆帳、最少轉帳結算。',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        background_color: '#ffffff',
        theme_color: '#60A5FA',
        icons: [
          { src: '/pwa-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/pwa-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/pwa-512-maskable.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,png,svg,ico,webmanifest}'],
        // SPA offline navigation, but never hijack backend routes.
        navigateFallback: 'index.html',
        navigateFallbackDenylist: [/^\/api/, /^\/webhook/],
        runtimeCaching: [
          {
            // GET /api/* (excluding auth/ai/health) → network-first, readable offline.
            urlPattern: ({ url, sameOrigin }) =>
              sameOrigin &&
              url.pathname.startsWith('/api/') &&
              !url.pathname.startsWith('/api/auth') &&
              !url.pathname.startsWith('/api/ai') &&
              url.pathname !== '/api/health',
            handler: 'NetworkFirst',
            method: 'GET',
            options: {
              cacheName: 'api-cache',
              networkTimeoutSeconds: 5,
              expiration: { maxEntries: 50, maxAgeSeconds: 86400 },
              cacheableResponse: { statuses: [200] },
            },
          },
        ],
      },
    }),
  ],
  server: {
    port: 5173,
    strictPort: true,
    allowedHosts: ['.ngrok-free.dev', '.ngrok-free.app', '.ngrok.io'],
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
});
