import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: '../../../tests/Pwa',
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: 'http://127.0.0.1:4174',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'VITE_DEMO_MODE=true VITE_API_BASE_URL=http://127.0.0.1:5011 npm run dev -- --host 127.0.0.1 --port 4174',
    url: 'http://127.0.0.1:4174',
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'phone', use: { ...devices['iPhone 13'] } },
  ],
})
