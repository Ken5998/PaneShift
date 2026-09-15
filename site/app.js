'use strict';
const workspace = document.querySelector('.workspace');
document.querySelectorAll('[data-layout]').forEach(button => {
  if (button.tagName !== 'BUTTON') return;
  button.addEventListener('click', () => {
    workspace.dataset.layout = button.dataset.layout;
    document.querySelectorAll('.layout-picker button').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
  });
});
const previews = {
  shortcuts: ['PaneShift Shortcuts settings with customizable window placement shortcuts', 'SHORTCUTS'],
  layout: ['PaneShift Layout settings with window gap controls and a live spacing preview', 'LAYOUT'],
  general: ['PaneShift General settings with start at login and runtime privilege status', 'GENERAL']
};
document.querySelectorAll('[data-settings]').forEach(button => button.addEventListener('click', () => {
  const name = button.dataset.settings;
  const image = document.getElementById('settings-image');
  image.src = `assets/settings-${name}.png`;
  image.alt = previews[name][0];
  document.getElementById('settings-caption').textContent = `THE REAL THING · PANESHIFT 0.1.0 / ${previews[name][1]}`;
  document.querySelectorAll('[data-settings]').forEach(item => item.setAttribute('aria-pressed', String(item === button)));
}));
