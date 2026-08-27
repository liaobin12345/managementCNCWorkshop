import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      // 开发环境代理到后端 API，避免跨域
      '/api': {
        target: 'http://localhost:5219',
        changeOrigin: true
      },
      // 现场照片静态资源
      '/uploads': {
        target: 'http://localhost:5219',
        changeOrigin: true
      }
    }
  }
})
