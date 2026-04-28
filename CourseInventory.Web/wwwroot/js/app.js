(() => {
  document.querySelectorAll('[data-pref-field][data-pref-value]').forEach(button => {
    button.addEventListener('click', () => {
      const form = button.closest('form');
      const input = form?.querySelector(`input[name="${button.dataset.prefField}"]`);
      if (!form || !input) return;
      input.value = button.dataset.prefValue;
      form.submit();
    });
  });

  const lang = document.documentElement.lang?.toLowerCase() || 'en';
  const text = lang.startsWith('fr')
    ? {
        selected: 'sélectionné',
        selectedPlural: 'sélectionnés',
        selectRows: 'Sélectionner',
        selectInventory: 'Sélectionnez un inventaire',
        selectedInventories: count => `${count} inventaires sélectionnés`,
        edit: 'Modifier',
        delete: 'Supprimer',
        deleteOne: title => `Supprimer "${title}" ?`,
        deleteFirst: 'Supprimer le premier inventaire sélectionné ?'
      }
    : {
        selected: 'selected',
        selectedPlural: 'selected',
        selectRows: 'Select rows',
        selectInventory: 'Select an inventory',
        selectedInventories: count => `${count} inventories selected`,
        edit: 'Edit',
        delete: 'Delete',
        deleteOne: title => `Delete "${title}"?`,
        deleteFirst: 'Delete the first selected inventory?'
      };

  const selectedRows = new Set();

  const toolbar = document.createElement('div');
  toolbar.className = 'floating-table-toolbar';
  toolbar.setAttribute('role', 'toolbar');
  toolbar.setAttribute('aria-live', 'polite');
  toolbar.innerHTML = `
    <div class="floating-table-toolbar__meta">
      <span class="floating-table-toolbar__count">0 ${text.selectedPlural}</span>
      <span class="floating-table-toolbar__hint">${text.selectInventory}</span>
    </div>
    <button class="btn btn-outline-primary btn-sm" type="button" data-toolbar-action="edit">${text.edit}</button>
    <button class="btn btn-outline-danger btn-sm" type="button" data-toolbar-action="delete">${text.delete}</button>
  `;
  document.body.appendChild(toolbar);

  const toolbarCount = toolbar.querySelector('.floating-table-toolbar__count');
  const toolbarHint = toolbar.querySelector('.floating-table-toolbar__hint');
  const editButton = toolbar.querySelector('[data-toolbar-action="edit"]');
  const deleteButton = toolbar.querySelector('[data-toolbar-action="delete"]');

  const getSelectedRows = () => [...selectedRows].filter(row => row.isConnected);

  const syncToolbar = () => {
    const rows = getSelectedRows();
    selectedRows.clear();
    rows.forEach(row => selectedRows.add(row));

    const count = rows.length;
    const first = rows[0];
    const title = first?.dataset.rowTitle || 'Selected inventory';
    const scope = first?.closest('[data-selection-scope]');
    const panel = first?.closest('.panel');
    const selectionLabel = panel?.querySelector('[data-selection-label]');

    toolbar.classList.toggle('is-visible', count > 0);
    toolbarCount.textContent = `${count} ${count === 1 ? text.selected : text.selectedPlural}`;
    toolbarHint.textContent = count === 1 ? title : text.selectedInventories(count);

    if (selectionLabel) {
      selectionLabel.textContent = count ? `${count} ${count === 1 ? text.selected : text.selectedPlural}` : text.selectRows;
    }

    editButton.disabled = count !== 1;
    deleteButton.disabled = count === 0 || !scope;
  };

  const setRowSelected = (row, selected) => {
    const checkbox = row.querySelector('tbody input[type="checkbox"], input[type="checkbox"]');
    row.classList.toggle('is-selected', selected);
    if (checkbox) checkbox.checked = selected;
    if (selected) selectedRows.add(row);
    else selectedRows.delete(row);
  };

  document.querySelectorAll('[data-selection-scope] tbody input[type="checkbox"]').forEach(checkbox => {
    checkbox.addEventListener('change', () => {
      const row = checkbox.closest('tr');
      if (!row) return;
      setRowSelected(row, checkbox.checked);
      syncToolbar();
    });
  });

  document.querySelectorAll('[data-selection-scope] .js-check-all').forEach(checkAll => {
    checkAll.addEventListener('change', () => {
      const table = checkAll.closest('table');
      table?.querySelectorAll('tbody tr').forEach(row => setRowSelected(row, checkAll.checked));
      syncToolbar();
    });
  });

  editButton.addEventListener('click', () => {
    const row = getSelectedRows()[0];
    if (row?.dataset.editHref) {
      window.location.href = row.dataset.editHref;
    }
  });

  deleteButton.addEventListener('click', () => {
    const rows = getSelectedRows();
    const first = rows[0];
    const panel = first?.closest('.panel');
    const form = panel?.querySelector('.js-inventory-delete-form');
    const idInput = form?.querySelector('input[name="id"]');
    const deleteId = first?.dataset.deleteId;
    if (!form || !idInput || !deleteId) return;

    const label = rows.length === 1
      ? text.deleteOne(first.dataset.rowTitle || 'this inventory')
      : text.deleteFirst;

    if (!window.confirm(label)) return;
    idInput.value = deleteId;
    form.submit();
  });

  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') {
      getSelectedRows().forEach(row => setRowSelected(row, false));
      document.querySelectorAll('[data-selection-scope] .js-check-all').forEach(input => {
        input.checked = false;
      });
      syncToolbar();
    }
  });

  const animateCounters = () => {
    document.querySelectorAll('.js-count-up').forEach(counter => {
      const target = Number(counter.dataset.count || 0);
      const duration = 900;
      const start = performance.now();

      const tick = now => {
        const progress = Math.min((now - start) / duration, 1);
        const eased = 1 - Math.pow(1 - progress, 3);
        counter.textContent = Math.round(target * eased).toLocaleString();
        if (progress < 1) requestAnimationFrame(tick);
      };

      counter.textContent = '0';
      requestAnimationFrame(tick);
    });
  };

  if ('IntersectionObserver' in window) {
    const stats = document.querySelector('.stats-strip, .profile-stats');
    if (stats) {
      const observer = new IntersectionObserver(entries => {
        if (!entries.some(entry => entry.isIntersecting)) return;
        animateCounters();
        observer.disconnect();
      }, { threshold: .35 });
      observer.observe(stats);
    }
  } else {
    animateCounters();
  }

  syncToolbar();
})();
