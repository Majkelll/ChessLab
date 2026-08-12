import { test, expect, Page } from '@playwright/test';
import { randomUUID } from 'crypto';

async function login(page: Page, userId: string, name: string) {
  await page.goto(`/TestAuth/Login?userId=${userId}&name=${encodeURIComponent(name)}`);
  await expect(page).toHaveURL('/');
  // Wait until WASM has finished downloading/booting and actually wired up event handlers,
  // not just until the (already-visible) static HTML has painted.
  await page.waitForLoadState('networkidle');
  await expect(page.getByRole('button', { name: 'Utwórz pokój' })).toBeEnabled();
}

function collectErrors(page: Page): string[] {
  const errors: string[] = [];
  page.on('pageerror', (err) => errors.push(`pageerror: ${err.message}\n${err.stack ?? ''}`));
  page.on('console', (msg) => {
    if (msg.type() === 'error') errors.push(`console.error: ${msg.text()}`);
  });
  return errors;
}

test('logged in user can create a room', async ({ page }) => {
  const errors = collectErrors(page);
  await login(page, randomUUID(), 'Ala');

  await page.getByRole('button', { name: 'Utwórz pokój' }).click();
  await expect(page).toHaveURL(/\/room\/[A-Z0-9]{6}/, { timeout: 10000 });
  await expect(page.getByText('Pokój')).toBeVisible();

  expect(errors, `Console/page errors:\n${errors.join('\n')}`).toEqual([]);
});

test('room filled with bots can be started and reaches the board', async ({ page }) => {
  const errors = collectErrors(page);
  await login(page, randomUUID(), 'Ala');

  await page.getByRole('button', { name: 'Utwórz pokój' }).click();
  await expect(page).toHaveURL(/\/room\/[A-Z0-9]{6}/, { timeout: 10000 });
  const code = page.url().split('/').pop()!;

  await page.getByRole('button', { name: 'Dołącz' }).first().click();

  // With 3 seats still empty, starting must be blocked and explained — not just silently disabled.
  await expect(page.getByText('Uzupełnij wszystkie 4 miejsca')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Rozpocznij grę' })).toBeDisabled();

  // Fill the three remaining empty seats with Easy bots. Wait for each pick to round-trip
  // (seat count drops) before touching the next select, otherwise a fast click can land on
  // a seat whose "claim" broadcast hasn't arrived yet (a real race, not just test flakiness).
  await expect(page.locator('select')).toHaveCount(3);
  for (let remaining = 3; remaining > 0; remaining--) {
    await page.locator('select').first().selectOption('Easy');
    await expect(page.locator('select')).toHaveCount(remaining - 1);
  }

  await expect(page.getByText('Uzupełnij wszystkie 4 miejsca')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Rozpocznij grę' })).toBeEnabled();
  await page.getByRole('button', { name: 'Rozpocznij grę' }).click();
  await expect(page).toHaveURL(new RegExp(`/game/${code}$`), { timeout: 15000 });

  await expect(page.getByText('Zegar:')).toBeVisible({ timeout: 10000 });

  expect(errors, `Console/page errors:\n${errors.join('\n')}`).toEqual([]);
});

test('solo Brain vs bots: teammate bot moves and play returns to the human', async ({ page }) => {
  // Requires a real `stockfish` binary reachable on PATH (e.g. run against the Docker image).
  // Skipped by default so the fast local suite doesn't depend on it being installed.
  test.skip(!process.env.EXPECT_BOTS_TO_MOVE, 'set EXPECT_BOTS_TO_MOVE=1 when stockfish is available');

  const errors = collectErrors(page);
  await login(page, randomUUID(), 'Solo');

  await page.getByRole('button', { name: 'Utwórz pokój' }).click();
  await expect(page).toHaveURL(/\/room\/[A-Z0-9]{6}/, { timeout: 10000 });

  // Claim White-Brain for myself, leave the other three seats to bots.
  await page.getByRole('button', { name: 'Dołącz' }).first().click();
  await expect(page.locator('select')).toHaveCount(3);
  for (let remaining = 3; remaining > 0; remaining--) {
    await page.locator('select').first().selectOption('Easy');
    await expect(page.locator('select')).toHaveCount(remaining - 1);
  }

  await page.getByRole('button', { name: 'Rozpocznij grę' }).click();
  await expect(page).toHaveURL(/\/game\//, { timeout: 15000 });

  // I'm the Brain: announce Pawn (always legal on the opening position).
  await page.getByRole('button', { name: 'Pion' }).click();

  // My Hand-bot teammate should move, then the whole black side (bot Brain + bot Hand)
  // should play too, handing the turn back to me — i.e. at least 2 plies in the history
  // and the "Mózg wskazuje figurę" prompt showing again for White.
  await expect(page.locator('ol li')).toHaveCount(2, { timeout: 15000 });
  await expect(page.getByText('Ruch: biali — Mózg wskazuje figurę')).toBeVisible({ timeout: 5000 });

  expect(errors, `Console/page errors:\n${errors.join('\n')}`).toEqual([]);
});
