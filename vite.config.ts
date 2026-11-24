import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const target = process.env.KAGE95_API_TARGET || 'http://127.0.0.1:3001'

export default defineConfig(({ mode }) => ({
  plugins: [react()],
  base: mode === 'pages' ? '/kage95-proje/' : '/',
  server: {
    proxy: {
      '/api': target,
      '/uploads': target,
    },
  },
}))
