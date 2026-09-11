import { expect, test } from '../../src/Web/RetailPulse.Portal/node_modules/@playwright/test'

test('desktop dashboard exposes the manager workflows', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Bardstown Road' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Needs attention' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'What to investigate' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Adjust inventory' })).toBeVisible()
  await expect(page.getByRole('checkbox', { name: 'Low-stock notifications' })).toBeVisible()
})

test('phone layout has no horizontal overflow', async ({ page }) => {
  await page.goto('/')

  const dimensions = await page.evaluate(() => ({
    viewport: window.innerWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }))
  expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.viewport)
})

test('offline report is clearly labeled and storage has no card data', async ({ page }) => {
  await page.route('**/api/**', (route) => route.abort('failed'))
  await page.goto('/')

  await expect(page.getByText('Simulated fallback')).toBeVisible()
  const storedValues = await page.evaluate(() => Object.values(localStorage).join(' ').toLowerCase())
  expect(storedValues).not.toContain('pan')
  expect(storedValues).not.toContain('cvv')
  expect(storedValues).not.toContain('card')
})
