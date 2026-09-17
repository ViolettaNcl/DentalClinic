import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const source = fs.readFileSync(path.join(root, 'wwwroot/assets/js/core/chatBot.js'), 'utf8');
const controllerSource = fs.readFileSync(path.join(root, 'Controllers/ChatController.cs'), 'utf8');

test('Denta page links stay visible after the next user message', () => {
  const start = source.lastIndexOf('_removeSuggestions() {');
  assert.notEqual(start, -1);
  const fragment = source.slice(start, start + 360);
  assert.match(fragment, /chat-suggestions/);
  assert.match(fragment, /chat-booking-start/);
  assert.doesNotMatch(fragment, /chat-links/);
});

test('Denta renders safe page links as dedicated clickable buttons without undefined hrefs', () => {
  assert.match(source, /a\.className = 'chat-link-btn'/);
  assert.match(source, /link\?\.url \?\? link\?\.Url/);
  assert.match(source, /link\?\.text \?\? link\?\.Text/);
  assert.match(source, /if \(!url\.startsWith\('\/pages\/'\) \|\| !text\) return/);
  assert.match(source, /a\.href = url/);
  assert.doesNotMatch(source, /a\.href = link\.url/);
});

test('Denta SSE serializes nested response DTOs with web camelCase', () => {
  assert.match(controllerSource, /JsonSerializerOptions\(JsonSerializerDefaults\.Web\)/);
  assert.doesNotMatch(controllerSource, /JsonSerializer\.Serialize\(payload\);/);
});
