(() => {
  document.querySelectorAll('[data-bs-toggle="tab"]').forEach(tab => {
    tab.addEventListener('shown.bs.tab', event => {
      const target = event.target.dataset.bsTarget;
      if (!target) return;

      const nextUrl = target === '#items'
        ? `${location.pathname}${location.search}`
        : `${location.pathname}${location.search}${target}`;

      history.replaceState(null, '', nextUrl);
    });
  });

  const initialTab = location.hash && document.querySelector(`[data-bs-target="${location.hash}"]`);
  if (initialTab && window.bootstrap) {
    bootstrap.Tab.getOrCreateInstance(initialTab).show();
  } else if (window.bootstrap) {
    const itemsTab = document.querySelector('[data-bs-target="#items"]');
    if (itemsTab) bootstrap.Tab.getOrCreateInstance(itemsTab).show();
  }

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
    const selectedTemplate = selectedCount?.dataset.selectedTemplate || '{0} selected';

    const sync = () => {
      const selected = selectedRows();
      rows.forEach(row => row.classList.toggle('is-selected', row.querySelector('[data-item-select]')?.checked));
      if (toolbar) toolbar.hidden = selected.length === 0;
      if (selectedCount) selectedCount.textContent = selectedTemplate.replace('{0}', selected.length);
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
      if (row?.dataset.href) window.location.href = row.dataset.href;
    });

    table.addEventListener('keydown', event => {
      if (event.target.closest('input, button, a, label')) return;
      if (event.key !== 'Enter' && event.key !== ' ') return;
      const row = event.target.closest('tr[data-href]');
      if (!row?.dataset.href) return;
      event.preventDefault();
      window.location.href = row.dataset.href;
    });

    table.querySelectorAll('[data-item-select]').forEach(input => input.addEventListener('change', sync));
    selectAll?.addEventListener('change', () => {
      rows.forEach(row => {
        const checkbox = row.querySelector('[data-item-select]');
        if (checkbox) checkbox.checked = selectAll.checked;
      });
      sync();
    });
    clearButton?.addEventListener('click', () => {
      table.querySelectorAll('[data-item-select]').forEach(input => { input.checked = false; });
      sync();
    });
    editButton?.addEventListener('click', () => {
      const row = selectedRows()[0];
      if (row?.dataset.editHref) window.location.href = row.dataset.editHref;
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
  const accessInventoryId = document.querySelector('[data-access-inventory-id]')?.value;
  const settingsTagsHidden = document.querySelector('[data-settings-tags-hidden]');
  const settingsTagInput = document.querySelector('[data-settings-tag-input]');
  const settingsTagBadges = document.querySelector('[data-settings-tag-badges]');
  const settingsTagSuggestions = document.querySelector('[data-settings-tag-suggestions]');
  const customIdTypeSelect = document.querySelector('[data-customid-type-select]');
  const customIdTypeHelpButton = document.querySelector('[data-customid-type-help]');
  const customIdFormatHelpButton = document.querySelector('[data-customid-format-help]');
  const customIdBuilder = document.querySelector('[data-customid-builder]');
  const customIdList = document.querySelector('[data-customid-list]');
  const customIdPreview = document.querySelector('[data-customid-preview]');

  if (window.bootstrap && customIdTypeSelect && customIdTypeHelpButton && customIdFormatHelpButton) {
    const typePopover = bootstrap.Popover.getOrCreateInstance(customIdTypeHelpButton);
    const formatPopover = bootstrap.Popover.getOrCreateInstance(customIdFormatHelpButton);

    const syncCustomIdHelp = () => {
      const selectedOption = customIdTypeSelect.selectedOptions[0];
      if (!selectedOption) return;

      const typeHelp = selectedOption.dataset.typeHelp || '';
      const formatHelp = selectedOption.dataset.formatHelp || customIdFormatHelpButton.dataset.bsContent || '';

      customIdTypeHelpButton.setAttribute('data-bs-content', typeHelp);
      customIdFormatHelpButton.setAttribute('data-bs-content', formatHelp);

      typePopover.setContent({ '.popover-body': typeHelp });
      formatPopover.setContent({ '.popover-body': formatHelp });
    };

    customIdTypeSelect.addEventListener('change', syncCustomIdHelp);
    syncCustomIdHelp();
  }

  if (customIdBuilder && customIdList && customIdPreview) {
    const token = customIdBuilder.querySelector('input[name="__RequestVerificationToken"]')?.value
      || document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const addForm = customIdBuilder.querySelector('[data-customid-add-form]');
    const fixedInput = addForm?.querySelector('input[name="fixedValue"]');
    const formatInput = addForm?.querySelector('input[name="format"]');
    let draggedRow = null;

    const pad = (value, width) => String(value).padStart(width, '0');
    const sequenceValue = format => {
      const match = /^d(\d+)$/i.exec((format || 'D5').trim());
      return match ? pad(1, Number(match[1])) : '1';
    };
    const dateValue = format => {
      const now = new Date();
      const year = String(now.getFullYear());
      const month = pad(now.getMonth() + 1, 2);
      const day = pad(now.getDate(), 2);
      return (format || 'yyyyMMdd')
        .replaceAll('yyyy', year)
        .replaceAll('MM', month)
        .replaceAll('dd', day);
    };
    const sampleFor = row => {
      const type = row.dataset.type;
      const fixed = row.dataset.fixed || '';
      const format = row.dataset.format || '';

      if (type === 'FixedText') return fixed;
      if (type === 'Random20Bit') return 'A1B2C';
      if (type === 'Random32Bit') return 'A1B2C3D4';
      if (type === 'Random6Digits') return '123456';
      if (type === 'Random9Digits') return '123456789';
      if (type === 'Guid') return format === 'D' ? '3f2504e0-4f89-11d3-9a0c-0305e82c3301' : '3f2504e04f8911d39a0c0305e82c3301';
      if (type === 'DateTime') return dateValue(format);
      if (type === 'Sequence') return sequenceValue(format);
      return '';
    };
    const rows = () => Array.from(customIdList.querySelectorAll('[data-customid-row]'));
    const renderPreview = () => {
      const savedRows = rows();
      const draftType = customIdTypeSelect?.selectedOptions?.[0]?.textContent?.trim();
      const draftFixed = fixedInput?.value?.trim() || '';
      const draftFormat = formatInput?.value?.trim() || '';
      const parts = savedRows.map(sampleFor);

      if (draftType && (draftFixed || draftFormat)) {
        parts.push(sampleFor({ dataset: { type: draftType, fixed: draftFixed, format: draftFormat } }));
      }

      customIdPreview.textContent = parts.join('') || 'ITEM-00001';
    };
    const saveOrder = async () => {
      if (!token || !customIdBuilder.dataset.reorderUrl) return;

      const payload = new URLSearchParams();
      payload.set('__RequestVerificationToken', token);
      payload.set('inventoryId', customIdBuilder.dataset.inventoryId || '');
      rows().forEach(row => payload.append('ids', row.dataset.id || ''));

      const response = await fetch(customIdBuilder.dataset.reorderUrl, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
          'X-Requested-With': 'XMLHttpRequest'
        },
        body: payload.toString()
      });

      if (!response.ok) return;
      const result = await response.json();
      if (result.ok && result.preview) customIdPreview.textContent = result.preview;
    };
    const rowAfterPointer = y => rows()
      .filter(row => row !== draggedRow)
      .reduce((closest, row) => {
        const box = row.getBoundingClientRect();
        const offset = y - box.top - box.height / 2;
        return offset < 0 && offset > closest.offset ? { offset, row } : closest;
      }, { offset: Number.NEGATIVE_INFINITY, row: null }).row;

    rows().forEach(row => {
      row.addEventListener('dragstart', () => {
        draggedRow = row;
        row.classList.add('is-dragging');
      });
      row.addEventListener('dragend', async () => {
        row.classList.remove('is-dragging');
        draggedRow = null;
        renderPreview();
        await saveOrder();
      });
    });

    customIdList.addEventListener('dragover', event => {
      if (!draggedRow) return;
      event.preventDefault();
      const after = rowAfterPointer(event.clientY);
      if (after) customIdList.insertBefore(draggedRow, after);
      else customIdList.appendChild(draggedRow);
      renderPreview();
    });

    [customIdTypeSelect, fixedInput, formatInput].forEach(input => {
      input?.addEventListener('input', renderPreview);
      input?.addEventListener('change', renderPreview);
    });

    renderPreview();
  }

  if (accessSearch && accessSuggestions && accessUserId && accessInventoryId) {
    let requestId = 0;
    accessSearch.addEventListener('input', async () => {
      const term = accessSearch.value.trim();
      accessUserId.value = '';
      if (term.length < 2) {
        accessSuggestions.hidden = true;
        return;
      }

      const current = ++requestId;
      const response = await fetch(`/Access/Users?inventoryId=${encodeURIComponent(accessInventoryId)}&term=${encodeURIComponent(term)}`);
      if (!response.ok) {
        accessSuggestions.hidden = true;
        return;
      }
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

  if (settingsTagsHidden && settingsTagInput && settingsTagBadges && settingsTagSuggestions) {
    const tags = new Set((settingsTagsHidden.value || '').split(',').map(tag => tag.trim()).filter(Boolean));
    let requestId = 0;

    const escapeHtml = value => value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;');

    const syncTags = () => {
      settingsTagsHidden.value = Array.from(tags).join(', ');
      settingsTagBadges.innerHTML = '';
      tags.forEach(tag => {
        const badge = document.createElement('button');
        badge.type = 'button';
        badge.className = 'tag-token';
        badge.innerHTML = `${escapeHtml(tag)} <span aria-hidden="true">&times;</span>`;
        badge.addEventListener('click', () => {
          tags.delete(tag);
          syncTags();
        });
        settingsTagBadges.appendChild(badge);
      });
    };

    const addTag = value => {
      const tag = value.trim().replaceAll(',', '');
      if (!tag || tags.size >= 12) return;
      tags.add(tag);
      settingsTagInput.value = '';
      settingsTagSuggestions.hidden = true;
      syncTags();
    };

    settingsTagInput.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        addTag(settingsTagInput.value);
      }
      if (event.key === 'Backspace' && !settingsTagInput.value && tags.size > 0) {
        tags.delete(Array.from(tags).pop());
        syncTags();
      }
    });

    settingsTagInput.addEventListener('input', async () => {
      const term = settingsTagInput.value.trim();
      if (!term) {
        settingsTagSuggestions.hidden = true;
        return;
      }

      const current = ++requestId;
      const response = await fetch(`/Tags/Suggest?term=${encodeURIComponent(term)}`);
      if (!response.ok) {
        settingsTagSuggestions.hidden = true;
        return;
      }

      const suggestions = await response.json();
      if (current !== requestId) return;

      settingsTagSuggestions.innerHTML = '';
      suggestions.filter(item => !tags.has(item)).forEach(item => {
        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = item;
        button.addEventListener('click', () => addTag(item));
        settingsTagSuggestions.appendChild(button);
      });
      settingsTagSuggestions.hidden = settingsTagSuggestions.childElementCount === 0;
    });

    syncTags();
  }
})();
