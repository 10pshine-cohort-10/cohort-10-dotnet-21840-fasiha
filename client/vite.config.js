import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const proxy = {
  '/api': {
    target: process.env.TASK_API_URL || 'http://localhost:5147',
    changeOrigin: true,
  },
};

export default defineConfig({
  plugins: [react()],
  server: { proxy },
  preview: { proxy },
});
