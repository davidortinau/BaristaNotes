import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: process.env.services__api__http__0 ?? process.env.API_HTTP,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
