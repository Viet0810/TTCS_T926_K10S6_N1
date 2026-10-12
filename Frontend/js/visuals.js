window.AppVisual = (() => {
  const paths = {
    calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v4m10-4v4M3 11h18m-14 4h2m3 0h2m3 0h2"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    signature: '<path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9l-6-6Zm0 0v6h6m-12 8 3-4 2 4 3-2"/>',
    assignment: '<circle cx="8" cy="7" r="3"/><path d="M2 21v-3a6 6 0 0 1 12 0v3m1-10 3 3 4-5"/>',
    shield: '<path d="M12 3 4 6v6c0 5 8 9 8 9s8-4 8-9V6l-8-3Z"/><path d="m8 12 3 3 5-6"/>',
    users: '<circle cx="9" cy="7" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 4a3 3 0 0 1 0 6m2 5a5 5 0 0 1 3 5"/>',
    account: '<circle cx="8" cy="7" r="3"/><path d="M2 20v-2a6 6 0 0 1 10-4m4-3v2m0 6v2m-5-5h2m6 0h2m-8-3-1-1m7 7 1 1m-7-1-1 1m7-7 1-1"/><circle cx="16" cy="16" r="3"/>',
    mentor: '<path d="m2 8 10-5 10 5-10 5-10-5Zm4 3v6c4 3 8 3 12 0v-6m4-3v8"/>',
    profile: '<rect x="3" y="4" width="18" height="16" rx="3"/><circle cx="9" cy="10" r="2"/><path d="M5 17a4 4 0 0 1 8 0m3-8h2m-2 4h2"/>',
    file: '<path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9l-6-6Zm0 0v6h6M8 13h8m-8 4h6"/>',
    review: '<rect x="5" y="5" width="14" height="16" rx="2"/><rect x="9" y="3" width="6" height="4" rx="1"/><path d="m8 14 3 3 5-6"/>',
    search: '<circle cx="10" cy="10" r="6"/><path d="m15 15 6 6"/>',
    upload: '<path d="M6 18a5 5 0 0 1-1-10 7 7 0 0 1 13-1 5 5 0 0 1 0 10M12 21V11m-4 4 4-4 4 4"/>',
    dashboard: '<rect x="3" y="3" width="7" height="7" rx="2"/><rect x="14" y="3" width="7" height="7" rx="2"/><rect x="3" y="14" width="7" height="7" rx="2"/><rect x="14" y="14" width="7" height="7" rx="2"/>',
  };
  function icon(name) {
    const wrapper = document.createElement('span');
    wrapper.innerHTML = `<svg class="app-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name] || paths.file}</svg>`;
    return wrapper.firstElementChild;
  }
  function illustration(name = 'file') {
    const figure = document.createElement('span'); figure.className = 'empty-visual'; figure.setAttribute('aria-hidden', 'true');
    figure.append(icon(name)); return figure;
  }
  const roleIcons = { ADMIN: 'shield', HR: 'users', MENTOR: 'mentor', INTERN: 'profile' };
  const moduleIcon = href => /contract/.test(href) ? 'signature' : /assignment/.test(href) ? 'assignment' : /attendance/.test(href) ? 'clock' : /schedule|program/.test(href) ? 'calendar' : /permission/.test(href) ? 'shield' : /search/.test(href) ? 'search' : /review/.test(href) ? 'review' : /upload/.test(href) ? 'upload' : /user-/.test(href) ? 'account' : /mentor/.test(href) ? 'mentor' : /dashboard/.test(href) ? 'dashboard' : /profile|intern-manage/.test(href) ? 'profile' : 'file';
  document.querySelectorAll('[data-role]').forEach(button => button.prepend(icon(roleIcons[button.dataset.role])));
  document.querySelectorAll('.section-heading h2').forEach(heading => heading.prepend(icon(moduleIcon(location.pathname))));
  document.querySelector('.auth-brand-content')?.append(illustration('profile'));
  return { icon, illustration, roleIcons, moduleIcon };
})();
