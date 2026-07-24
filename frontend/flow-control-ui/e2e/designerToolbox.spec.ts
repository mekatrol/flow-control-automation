import { expect, test } from './fixtures/flowTest';

/**
 * Designer toolbox end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

test('searches the node palette and adds registry-backed nodes', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill('timing');
  await expect(page.getByRole('button', { name: 'Add Pulse node' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Add Calculator node' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Add Pulse node' }).click();

  const pulse = page.getByRole('button', { name: /New Pulse, Pulse node/ });
  await expect(pulse).toHaveAttribute('aria-pressed', 'true');
  await expect(pulse).toHaveAttribute('data-node-category', 'timing');
  await expect(pulse.locator('.node-body')).not.toHaveAttribute('fill');
  await expect(page.getByText('5 nodes', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Trigger, input, any/ })).toBeVisible();

  await search.fill('routing');
  await page.getByRole('button', { name: 'Add Split node' }).click();
  const split = page.getByRole('button', { name: /New Split, Split node/ });
  await expect(split).toBeVisible();
  await expect(split).toHaveAttribute('data-node-category', 'routing');
  await expect(split.locator('.node-body')).not.toHaveAttribute('fill');
  await expect(split.locator('rect.connector-port')).toHaveCount(3);
  await expect(page.getByText('6 nodes', { exact: true })).toBeVisible();

  await search.fill('override');
  await expect(page.getByRole('heading', { name: 'override', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add Override node' }).click();
  const override = page.getByRole('button', { name: /New Override, Override node/ });
  await expect(override).toHaveAttribute('data-node-category', 'override');
  await expect(override.locator('.node-body')).not.toHaveAttribute('fill');
});

test('keeps dark-theme function blocks at WCAG AA text contrast', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('theme-preference', 'dark'));
  await page.goto('/flows/climate-control');

  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill('and');
  await page.getByRole('button', { name: 'Add And node' }).click();

  const contrastByCategory = await page.locator('.flow-node').evaluateAll((nodes) => {
    const luminance = (color: string): number => {
      const channels = color.match(/\d+/g)!.slice(0, 3).map(Number);
      const linear = channels.map((channel) => {
        const value = channel / 255;
        return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * linear[0]! + 0.7152 * linear[1]! + 0.0722 * linear[2]!;
    };

    return Object.fromEntries(
      nodes.map((node) => {
        const background = getComputedStyle(node.querySelector('.node-body')!).fill;
        const foreground = getComputedStyle(node.querySelector('.node-label')!).fill;
        const lighter = Math.max(luminance(background), luminance(foreground));
        const darker = Math.min(luminance(background), luminance(foreground));
        return [node.getAttribute('data-node-category'), (lighter + 0.05) / (darker + 0.05)];
      })
    );
  });

  expect(Object.keys(contrastByCategory).sort()).toEqual([
    'logic',
    'maths',
    'override',
    'routing',
    'timing'
  ]);
  for (const ratio of Object.values(contrastByCategory)) {
    expect(ratio).toBeGreaterThanOrEqual(4.5);
  }
});

test('drags a legacy function block from the toolbox onto the canvas', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill('average');
  const average = page.getByRole('button', { name: 'Add Average node' });
  await expect(average).toHaveAttribute('draggable', 'true');
  const canvas = page.getByRole('group', { name: 'Climate control flow graph' });
  const canvasBox = await canvas.boundingBox();
  expect(canvasBox).not.toBeNull();
  // Native mouse drag synthesis is unavailable in touch-emulating projects and
  // can target a node painted above the SVG background. Dispatch the same HTML
  // drag payload to the canvas at an explicit empty graph coordinate instead.
  const transfer = await page.evaluateHandle(() => new DataTransfer());
  await average.dispatchEvent('dragstart', { dataTransfer: transfer });
  await canvas.dispatchEvent('drop', {
    clientX: canvasBox!.x + 760,
    clientY: canvasBox!.y + 470,
    dataTransfer: transfer
  });

  await expect(page.getByRole('button', { name: /New Average, Average node/ })).toBeVisible();
  await expect(page.getByText('5 nodes', { exact: true })).toBeVisible();
});
