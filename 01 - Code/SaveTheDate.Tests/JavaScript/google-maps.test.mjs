import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

// Exercise the provider boundary without credentials, billing or live requests.
const source = await readFile(new URL('../../SaveTheDate/wwwroot/js/google-maps.js', import.meta.url), 'utf8');
const { mount } = await import('data:text/javascript;base64,' + Buffer.from(source).toString('base64'));
const widgets = [];
const observers = [];
class Widget extends EventTarget {
  constructor() { super(); widgets.push(this); }
  setAttribute() {}
}
globalThis.google = { maps: { importLibrary: async () => ({ PlaceAutocompleteElement: Widget }) } };
globalThis.document = { body: {} };
globalThis.MutationObserver = class {
  constructor(callback) { this.callback = callback; observers.push(this); }
  observe() {}
  disconnect() { this.disconnected = true; }
};
const tick = () => new Promise(resolve => setImmediate(resolve));
function select(widget, place) {
  const event = new Event('gmp-select');
  event.placePrediction = { toPlace: () => place };
  widget.dispatchEvent(event);
}
function setup() {
  const calls = [];
  return { calls, host: { isConnected: true, replaceChildren(widget) { this.widget = widget; } },
    receiver: { async invokeMethodAsync(...args) { calls.push(args); } } };
}

test('selects a place, reports denied requests and keeps multiple modules independent', async () => {
  const first = setup(), second = setup();
  await mount(first.host, '', first.receiver);
  await mount(second.host, '', second.receiver);
  select(first.host.widget, { id: 'ChIJ_first', formattedAddress: 'Córdoba', async fetchFields({fields}) { assert.deepEqual(fields, ['id', 'formattedAddress']); } });
  await tick();
  assert.deepEqual(first.calls, [['SelectPlace', 'ChIJ_first', 'Córdoba']]);
  assert.deepEqual(second.calls, []);
  second.host.widget.dispatchEvent(new Event('gmp-error'));
  await tick();
  assert.deepEqual(second.calls, [['SearchFailed']]);
});

test('a slower old selection cannot replace a newer selection', async () => {
  const state = setup();
  await mount(state.host, '', state.receiver);
  let complete;
  select(state.host.widget, { id: 'old', fetchFields: () => new Promise(resolve => { complete = resolve; }) });
  select(state.host.widget, { id: 'new', formattedAddress: 'Nuevo lugar', async fetchFields() {} });
  await tick(); complete(); await tick();
  assert.deepEqual(state.calls, [['SelectPlace', 'new', 'Nuevo lugar']]);
});

test('unmount cancels pending notifications and releases listeners and observer', async () => {
  const state = setup();
  await mount(state.host, '', state.receiver);
  let complete;
  select(state.host.widget, { id: 'removed', fetchFields: () => new Promise(resolve => { complete = resolve; }) });
  state.host.isConnected = false;
  const observer = observers.at(-1); observer.callback();
  complete(); await tick();
  state.host.widget.dispatchEvent(new Event('gmp-error'));
  await tick();
  assert.deepEqual(state.calls, []);
  assert.equal(observer.disconnected, true);
});

test('a module removed before the library loads is not mounted', async () => {
  const state = setup(); state.host.isConnected = false;
  await mount(state.host, '', state.receiver);
  assert.equal(state.host.widget, undefined);
});
