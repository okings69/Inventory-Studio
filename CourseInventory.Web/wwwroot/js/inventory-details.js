(() => {
  document.querySelectorAll('[data-bs-toggle="tab"]').forEach(tab => {
    tab.addEventListener('shown.bs.tab', event => {
      history.replaceState(null, '', event.target.dataset.bsTarget || location.pathname);
    });
  });

  const initialTab = location.hash && document.querySelector(`[data-bs-target="${location.hash}"]`);
  if (initialTab && window.bootstrap) bootstrap.Tab.getOrCreateInstance(initialTab).show();

  const table = document.querySelector('[data-details-items-table]');
  const toolbar = document.querySelector('[data-items-toolbar]');
  const selectAll = document.querySelector('[data-items-select-all]');
  const selectedCount = document.querySelector('[data-items-selected-count]');
  const hiddenInputs = document.querySelector('[data-items-hidden-inputs]');
  const editButton = document.querySelector('[data-items-edit]');
  const clearButton = document.querySelector('[data-items-clear]');

  if (table) {
    const rows = Array.from(table.querySelectorAll('tbody tr[data-id]'));
    const selectedRows = () => rows.filter(row => row.querySelector('[data-item-select]')?.checked);

    const sync = () => {
      const selected = selectedRows();
      rows.forEach(row => row.classList.toggle('is-selected', row.querySelector('[data-item-select]')?.checked));
      if (toolbar) toolbar.hidden = selected.length === 0;
      if (selectedCount) selectedCount.textContent = `${selected.length} selected`;
      if (editButton) editButton.disabled = selected.length !== 1;

      if (hiddenInputs) {
        hiddenInputs.innerHTML = '';
        selected.forEach(row => {
          const input = document.createElement('input');
          input.type = 'hidden';
          input.name = 'ids';
          input.value = row.dataset.id;
          hiddenInputs.appendChild(input);
        });
      }

      if (selectAll) {
        selectAll.checked = selected.length > 0 && selected.length === rows.length;
        selectAll.indeterminate = selected.length > 0 && selected.length < rows.length;
      }
    };

    table.addEventListener('click', event => {
      if (event.target.closest('[data-no-row-click], input, button, a, label')) return;
      const row = event.target.closest('tr[data-href]');
      if (row) window.location.href = row.dataset.href;
    });

    table.addEventListener('keydown', event => {
      if (event.target.closest('input, button, a, label')) return;
      if (event.key !== 'Enter' && event.key !== ' ') return;
      const row = event.target.closest('tr[data-href]');
      if (!row) return;
      event.preventDefault();
      window.location.href = row.dataset.href;
    });

    table.querySelectorAll('[data-item-select]').forEach(input => input.addEventListener('change', sync));
    selectAll?.addEventListener('change', () => {
      rows.forEach(row => {
        row.querySelector('[data-item-select]').checked = selectAll.checked;
      });
      sync();
    });
    clearButton?.addEventListener('click', () => {
      table.querySelectorAll('[data-item-select]').forEach(input => { input.checked = false; });
      sync();
    });
    editButton?.addEventListener('click', () => {
      const row = selectedRows()[0];
      if (row?.dataset.href) window.location.href = row.dataset.href;
    });

    const likeToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    table.querySelectorAll('[data-item-like]').forEach(button => {
      button.addEventListener('click', async event => {
        event.preventDefault();
        event.stopPropagation();

        if (!likeToken || button.disabled) return;

        button.disabled = true;

        try {
          const payload = new URLSearchParams();
          payload.set('__RequestVerificationToken', likeToken);
          payload.set('itemId', button.dataset.itemId || '');
          payload.set('inventoryId', button.dataset.inventoryId || '');

          const response = await fetch('/Items/ToggleLike', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
              'X-Requested-With': 'XMLHttpRequest'
            },
            body: payload.toString()
          });

          if (!response.ok) {
            return;
          }

          const result = await response.json();
          if (!result.ok) {
            return;
          }

          const count = button.querySelector('[data-item-like-count]');
          if (count) count.textContent = result.count;
          button.classList.toggle('is-liked', !!result.liked);
          button.setAttribute('aria-pressed', (!!result.liked).toString());
        } finally {
          button.disabled = false;
        }
      });
    });

    sync();
  }

  const accessSearch = document.querySelector('[data-access-search]');
  const accessUserId = document.querySelector('[data-access-user-id]');
  const accessSuggestions = document.querySelector('[data-access-suggestions]');

  if (accessSearch && accessSuggestions && accessUserId) {
    let requestId = 0;
    accessSearch.addEventListener('input', async () => {
      const term = accessSearch.value.trim();
      accessUserId.value = '';
      if (term.length < 2) {
        accessSuggestions.hidden = true;
        return;
      }

      const current = ++requestId;
      const response = await fetch(`/Access/Users?term=${encodeURIComponent(term)}`);
      const users = await response.json();
      if (current !== requestId) return;

      accessSuggestions.innerHTML = '';
      users.forEach(user => {
        const button = document.createElement('button');
        button.type = 'button';
        button.innerHTML = `<strong>${user.userName}</strong><span>${user.email || ''}</span>`;
        button.addEventListener('click', () => {
          accessSearch.value = `${user.userName} (${user.email || 'no email'})`;
          accessUserId.value = user.id;
          accessSuggestions.hidden = true;
        });
        accessSuggestions.appendChild(button);
      });
      accessSuggestions.hidden = users.length === 0;
    });
  }
})();
