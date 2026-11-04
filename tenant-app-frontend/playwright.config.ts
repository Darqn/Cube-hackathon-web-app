import { defineConfig } from '@playwright/test';
export default defineConfig({ testDir: './tests/browser', workers: 1, use: { baseURL: 'http://127.0.0.1:8082', launchOptions: { executablePath: process.env.CHROMIUM_PATH || '/usr/bin/chromium', args: ['--no-sandbox'] }, screenshot: 'only-on-failure', trace: 'retain-on-failure' } });
