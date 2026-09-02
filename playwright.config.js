import {defineConfig,devices} from '@playwright/test';
export default defineConfig({
 testDir:'./tests/e2e',timeout:30000,retries:1,workers:1,
 use:{baseURL:process.env.BASE_URL||'https://dental-clinic-vn.vercel.app',trace:'retain-on-failure'},
 projects:[{name:'chromium',use:{...devices['Desktop Chrome']}},{name:'mobile',use:{...devices['Pixel 7']}}]
});
