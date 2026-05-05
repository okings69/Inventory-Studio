(() => {
  const table = document.querySelector('[data-inventory-table]');
  if (!table) return;

  const tbody = table.querySelector('tbody');
  const rows = Array.from(tbody.querySelectorAll('tr'));
  const selectAll = table.querySelector('[data-select-all]');
  const toolbar = document.querySelector('[data-inventory-toolbar]');
  const selectionLabel = document.querySelector('[data-inventory-selection-label]');
  const selectedCount = toolbar?.querySelector('[data-selected-count]');
  const selectedInputs = toolbar?.querySelector('[data-selected-inputs]');
  const editButton = toolbar?.querySelector('[data-action="edit"]');
  const deleteButton = toolbar?.querySelector('[data-action="delete"]');
  const deleteLoader = toolbar?.querySelector('[data-delete-loader]');
  const clearButton = toolbar?.querySelector('[data-action="clear"]');
  const textFilter = document.getElementById('inventoryTextFilter');
  const accessFilter = document.getElementById('inventoryAccessFilter');
  const emptyState = document.querySelector('[data-empty-state]');
  const visualGrid = document.querySelector('[data-inventory-visual-grid]');
  const visualCards = Array.from(document.querySelectorAll('[data-visual-card]'));
  const visualEmptyState = document.querySelector('[data-visual-empty-state]');
  const prevButton = document.querySelector('[data-page-prev]');
  const nextButton = document.querySelector('[data-page-next]');
  const pageStatus = document.querySelector('[data-page-status]');
  const pagination = document.querySelector('.table-pagination');
  const viewButtons = Array.from(document.querySelectorAll('[data-view-mode]'));
  const modalElement = document.querySelector('[data-confirm-delete-modal]');
  const modalMessage = document.querySelector('[data-delete-message]');
  const confirmDeleteButton = document.querySelector('[data-confirm-delete]');
  const confirmDeleteLoader = document.querySelector('[data-confirm-delete-loader]');
  const toastElement = document.querySelector('[data-inventory-toast]');
  const pageSize = Number(table.dataset.pageSize || 10);
  const deleteModal = modalElement && window.bootstrap ? new bootstrap.Modal(modalElement) : null;
  const selectedTemplate = toolbar?.dataset.selectedTemplate || selectionLabel?.dataset.selectedTemplate || '{0} selected';
  const selectedDefault = selectionLabel?.dataset.selectedDefault || 'Select rows';
  const deleteTemplate = toolbar?.dataset.deleteTemplate || 'Delete {0} selected {1}?';
  const inventorySingular = toolbar?.dataset.inventorySingular || 'inventory';
  const inventoryPlural = toolbar?.dataset.inventoryPlural || 'inventories';

  let currentPage = 1;
  let sort = { key: 'updated', direction: 'desc' };
  let confirmedDelete = false;
  let currentView = localStorage.getItem('inventoryViewMode') === 'visual' ? 'visual' : 'table';

  const getSelectedRows = () => rows.filter(row => row.querySelector('[data-row-select]')?.checked);

  const rowMatchesFilters = row => {
    const term = (textFilter?.value || '').trim().toLowerCase();
    const access = (accessFilter?.value || '').trim().toLowerCase();
    const haystack = [
      row.dataset.title,
      row.dataset.category,
      row.dataset.owner
    ].join(' ').toLowerCase();
    return (!term || haystack.includes(term)) && (!access || row.dataset.access === access);
  };

  const sortedVisibleRows = () => rows
    .filter(rowMatchesFilters)
    .sort((a, b) => {
      const dir = sort.direction === 'asc' ? 1 : -1;
      if (sort.key === 'updated') {
        return (Number(a.dataset.updated) - Number(b.dataset.updated)) * dir;
      }
      return (a.dataset[sort.key] || '').localeCompare(b.dataset[sort.key] || '') * dir;
    });

  const syncSelection = () => {
    const selected = getSelectedRows();
    rows.forEach(row => row.classList.toggle('is-selected', row.querySelector('[data-row-select]')?.checked));

    if (toolbar) toolbar.hidden = selected.length === 0;
    if (selectionLabel) selectionLabel.textContent = selected.length ? selectedTemplate.replace('{0}', selected.length) : selectedDefault;
    if (selectedCount) selectedCount.textContent = selectedTemplate.replace('{0}', selected.length);
    if (editButton) editButton.disabled = selected.length !== 1;
    if (deleteButton) deleteButton.disabled = selected.length === 0;

    if (selectedInputs) {
      selectedInputs.innerHTML = '';
      selected.forEach(row => {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = 'ids';
        input.value = row.dataset.id;
        selectedInputs.appendChild(input);
      });
    }

    const visibleRows = rows.filter(row => !row.hidden);
    const checkedVisible = visibleRows.filter(row => row.querySelector('[data-row-select]')?.checked);
    if (selectAll) {
      selectAll.checked = visibleRows.length > 0 && checkedVisible.length === visibleRows.length;
      selectAll.indeterminate = checkedVisible.length > 0 && checkedVisible.length < visibleRows.length;
    }
  };

  const render = () => {
    const visible = sortedVisibleRows();
    const totalPages = Math.max(1, Math.ceil(visible.length / pageSize));
    currentPage = Math.min(currentPage, totalPages);
    const start = (currentPage - 1) * pageSize;
    const pageRows = new Set(visible.slice(start, start + pageSize));

    rows.forEach(row => {
      row.hidden = !pageRows.has(row);
      tbody.appendChild(row);
    });

    if (emptyState) emptyState.hidden = visible.length > 0;
    table.hidden = visible.length === 0;
    if (pagination) pagination.hidden = visible.length === 0;
    if (prevButton) prevButton.disabled = currentPage <= 1;
    if (nextButton) nextButton.disabled = currentPage >= totalPages;
    if (pageStatus) pageStatus.textContent = `${currentPage} / ${totalPages}`;
    syncSelection();
  };

  const renderVisual = () => {
    const term = (textFilter?.value || '').trim().toLowerCase();
    const access = (accessFilter?.value || '').trim().toLowerCase();
    let visibleCount = 0;

    visualCards.forEach(card => {
      const haystack = [
        card.dataset.title,
        card.dataset.category,
        card.dataset.owner
      ].join(' ').toLowerCase();

      const matches = (!term || haystack.includes(term)) && (!access || card.dataset.access === access);
      card.hidden = !matches;
      if (matches) visibleCount += 1;
    });

    if (visualEmptyState) visualEmptyState.hidden = visibleCount > 0;
  };

  const syncView = () => {
    const isVisual = currentView === 'visual';
    const tableWrapper = table.closest('.table-responsive');
    if (tableWrapper) tableWrapper.hidden = isVisual;
    if (visualGrid) visualGrid.hidden = !isVisual;
    if (pagination) pagination.hidden = isVisual || table.hidden;
    if (toolbar) toolbar.hidden = isVisual || getSelectedRows().length === 0;

    viewButtons.forEach(button => {
      const active = button.dataset.viewMode === currentView;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-pressed', active.toString());
    });
  };

  table.addEventListener('click', event => {
    if (event.target.closest('input, button, a, label')) return;
    const row = event.target.closest('tr[data-href]');
    if (row) window.location.href = row.dataset.href;
  });

  visualCards.forEach(card => {
    card.addEventListener('click', () => {
      if (card.dataset.href) window.location.href = card.dataset.href;
    });
  });

  table.querySelectorAll('[data-row-select]').forEach(checkbox => {
    checkbox.addEventListener('change', syncSelection);
  });

  selectAll?.addEventListener('change', () => {
    rows.filter(row => !row.hidden).forEach(row => {
      row.querySelector('[data-row-select]').checked = selectAll.checked;
    });
    syncSelection();
  });

  clearButton?.addEventListener('click', () => {
    table.querySelectorAll('[data-row-select]').forEach(input => { input.checked = false; });
    if (selectAll) {
      selectAll.checked = false;
      selectAll.indeterminate = false;
    }
    syncSelection();
  });

  editButton?.addEventListener('click', () => {
    const row = getSelectedRows()[0];
    if (row?.dataset.editHref) window.location.href = row.dataset.editHref;
  });

  toolbar?.addEventListener('submit', event => {
    const selected = getSelectedRows();
    if (selected.length === 0) {
      event.preventDefault();
      return;
    }

    if (!confirmedDelete) {
      event.preventDefault();
      if (modalMessage) {
        const noun = selected.length === 1 ? inventorySingular : inventoryPlural;
        modalMessage.textContent = deleteTemplate.replace('{0}', selected.length).replace('{1}', noun);
      }
      deleteModal?.show();
      return;
    }

    setLoading(true);
  });

  confirmDeleteButton?.addEventListener('click', () => {
    confirmedDelete = true;
    setLoading(true);
    toolbar?.requestSubmit();
  });

  table.querySelectorAll('[data-sort]').forEach(button => {
    button.addEventListener('click', () => {
      const key = button.dataset.sort;
      sort = {
        key,
        direction: sort.key === key && sort.direction === 'asc' ? 'desc' : 'asc'
      };
      currentPage = 1;
      table.querySelectorAll('[data-sort]').forEach(item => item.dataset.direction = '');
      button.dataset.direction = sort.direction;
      table.querySelectorAll('[data-sort]').forEach(item => {
        item.classList.toggle('is-active', item === button);
        item.setAttribute('aria-sort', item === button ? sort.direction : 'none');
      });
      render();
    });
  });

  [textFilter, accessFilter].forEach(input => {
    input?.addEventListener('input', () => {
      currentPage = 1;
      render();
      renderVisual();
      syncView();
    });
    input?.addEventListener('change', () => {
      currentPage = 1;
      render();
      renderVisual();
      syncView();
    });
  });

  viewButtons.forEach(button => {
    button.addEventListener('click', () => {
      currentView = button.dataset.viewMode || 'table';
      localStorage.setItem('inventoryViewMode', currentView);
      syncView();
    });
  });

  prevButton?.addEventListener('click', () => {
    currentPage -= 1;
    render();
  });

  nextButton?.addEventListener('click', () => {
    currentPage += 1;
    render();
  });

  const setLoading = loading => {
    if (deleteButton) deleteButton.disabled = loading;
    if (confirmDeleteButton) confirmDeleteButton.disabled = loading;
    deleteLoader?.classList.toggle('d-none', !loading);
    confirmDeleteLoader?.classList.toggle('d-none', !loading);
    toolbar?.classList.toggle('is-loading', loading);
  };

  if (toastElement && window.bootstrap) {
    new bootstrap.Toast(toastElement, { delay: 3500 }).show();
  }

  const initialSortButton = table.querySelector(`[data-sort="${sort.key}"]`);
  if (initialSortButton) {
    initialSortButton.dataset.direction = sort.direction;
    initialSortButton.classList.add('is-active');
    initialSortButton.setAttribute('aria-sort', sort.direction);
  }

  render();
  renderVisual();
  syncView();
})();
