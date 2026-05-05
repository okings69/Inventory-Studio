(() => {
  if (window.bootstrap) {
    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(element => {
      bootstrap.Tooltip.getOrCreateInstance(element);
    });
  }

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
        deleteFirst: 'Supprimer le premier inventaire sélectionné ?',
        unsavedChanges: 'Modifications non enregistrées',
        saving: 'Enregistrement...',
        saved: 'Tous les changements sont enregistrés',
        conflict: 'Conflit',
        previewAppearsAsYouType: "L'aperçu apparaît pendant la saisie.",
        noImageUrl: "Aucune URL d'image",
        imageCouldNotBeLoaded: "L'image n'a pas pu être chargée"
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
        deleteFirst: 'Delete the first selected inventory?',
        unsavedChanges: 'Unsaved changes',
        saving: 'Saving...',
        saved: 'All changes saved',
        conflict: 'Conflict',
        previewAppearsAsYouType: 'Preview appears as you type.',
        noImageUrl: 'No image URL',
        imageCouldNotBeLoaded: 'Image could not be loaded'
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
    const singular = scope?.dataset.selectionSingular || text.selected;
    const plural = scope?.dataset.selectionPlural || text.selectedPlural;
    const emptyHint = scope?.dataset.selectionEmptyHint || text.selectInventory;
    const pluralHint = scope?.dataset.selectionPluralHint;
    const toolbarMode = scope?.dataset.toolbarMode || 'actions';

    toolbar.classList.toggle('is-visible', count > 0);
    toolbar.classList.toggle('is-inspector', toolbarMode === 'inspect');
    toolbarCount.textContent = `${count} ${count === 1 ? singular : plural}`;
    toolbarHint.textContent = count === 0
      ? emptyHint
      : count === 1
        ? title
        : (pluralHint ? pluralHint.replace('{0}', count) : text.selectedInventories(count));

    if (selectionLabel) {
      selectionLabel.textContent = count ? `${count} ${count === 1 ? singular : plural}` : text.selectRows;
    }

    editButton.hidden = toolbarMode === 'inspect';
    deleteButton.hidden = toolbarMode === 'inspect';
    editButton.disabled = toolbarMode === 'inspect' || count !== 1;
    deleteButton.disabled = toolbarMode === 'inspect' || count === 0 || !scope;
    document.dispatchEvent(new CustomEvent('table-selection-changed', {
      detail: { scope, rows, count }
    }));
  };

  const setRowSelected = (row, selected) => {
    const checkbox = row.querySelector('input[data-row-select][type="checkbox"], input[type="checkbox"]:not([disabled])');
    row.classList.toggle('is-selected', selected);
    if (checkbox) checkbox.checked = selected;
    if (selected) selectedRows.add(row);
    else selectedRows.delete(row);
  };

  document.querySelectorAll('[data-selection-scope] tbody input[data-row-select][type="checkbox"], [data-selection-scope] tbody input[type="checkbox"]:not([disabled])').forEach(checkbox => {
    checkbox.addEventListener('change', () => {
      const row = checkbox.closest('tr');
      if (!row) return;
      const scope = row.closest('[data-selection-scope]');
      if (checkbox.checked && scope?.dataset.selectionMode === 'single') {
        getSelectedRows().forEach(otherRow => {
          if (otherRow !== row) setRowSelected(otherRow, false);
        });
        document.querySelectorAll('[data-selection-scope] .js-check-all').forEach(input => {
          input.checked = false;
        });
      }
      setRowSelected(row, checkbox.checked);
      syncToolbar();
    });
  });

  document.querySelectorAll('[data-selection-scope] .js-check-all').forEach(checkAll => {
    checkAll.addEventListener('change', () => {
      const scope = checkAll.closest('[data-selection-scope]');
      if (scope?.dataset.selectionMode === 'single') {
        checkAll.checked = false;
        return;
      }
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

  const settingsForm = document.getElementById('settingsForm');
  if (settingsForm) {
    let dirty = false;
    const status = document.getElementById('autosaveStatus');
    settingsForm.addEventListener('input', () => {
      dirty = true;
      status.textContent = text.unsavedChanges;
    });
    setInterval(async () => {
      if (!dirty) return;
      dirty = false;
      status.textContent = text.saving;
      const response = await fetch(settingsForm.action, {
        method: 'POST',
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
        body: new FormData(settingsForm)
      });
      const result = await response.json();
      status.textContent = result.ok ? text.saved : text.conflict;
    }, 8000);
  }

  const initInventoryChat = () => {
    if (window.__inventoryChatInitialized || !window.signalR) return;

    const chatPane = document.getElementById('chat');
    const chatForm = document.getElementById('chatForm');
    const chatMessages = document.getElementById('chatMessages');
    if (!chatPane || !chatForm || !chatMessages) return;

    window.__inventoryChatInitialized = true;

    const inventoryId = Number(chatPane.dataset.inventoryId);
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/inventory-discussion').build();
    let activeOnlineUsers = new Set();
    const setOnlineUsers = onlineUserIds => {
      const online = new Set(onlineUserIds || []);
      activeOnlineUsers = online;
      chatMessages.querySelectorAll('[data-chat-author-id]').forEach(avatar => {
        avatar.classList.toggle('is-online', online.has(avatar.dataset.chatAuthorId));
      });
    };

    const appendMessage = message => {
      const avatar = (message.author || 'U').trim().charAt(0).toUpperCase() || 'U';
      chatMessages.insertAdjacentHTML('beforeend', `
        <article class="chat-message">
          <div class="chat-avatar ${activeOnlineUsers.has(message.authorId || '') ? 'is-online' : ''}" data-chat-author-id="${message.authorId || ''}">${avatar}</div>
          <div class="chat-bubble">
            <div class="chat-meta">${message.author} <span>${message.createdAt}</span></div>
            <div class="markdown">${message.html}</div>
          </div>
        </article>`);
      chatMessages.scrollTop = chatMessages.scrollHeight;
    };

    connection.on('ReceiveMessage', appendMessage);
    connection.on('PresenceChanged', payload => {
      if (Number(payload.inventoryId) !== inventoryId) return;
      setOnlineUsers(payload.onlineUserIds);
    });

    connection.start()
      .then(() => connection.invoke('JoinInventoryGroup', inventoryId))
      .catch(() => {
        window.__inventoryChatInitialized = false;
      });

    chatForm.addEventListener('submit', async event => {
      event.preventDefault();
      const textarea = chatForm.querySelector('textarea');
      const textValue = textarea?.value?.trim();
      if (!textValue) return;

      try {
        await connection.invoke('SendMessage', inventoryId, textValue);
        textarea.value = '';
      } catch {
        window.__inventoryChatInitialized = false;
        chatForm.submit();
      }
    });
  };

  if (document.readyState === 'complete') {
    initInventoryChat();
  } else {
    window.addEventListener('load', initInventoryChat, { once: true });
  }

  const createForm = document.querySelector('.form-card form');
  if (createForm) {
    createForm.addEventListener('submit', event => {
      if (!createForm.checkValidity()) {
        event.preventDefault();
        event.stopPropagation();
      }
      createForm.classList.add('was-validated');
    });
  }

  const markdownInput = document.getElementById('DescriptionMarkdown');
  const markdownPreview = document.getElementById('markdownPreview');
  if (markdownInput && markdownPreview) {
    const renderMarkdownPreview = () => {
      const raw = markdownInput.value.trim();
      if (!raw) {
        markdownPreview.textContent = text.previewAppearsAsYouType;
        return;
      }
      const escaped = raw
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;');
      const html = escaped
        .split(/\n{2,}/)
        .map(block => {
          const inline = block
            .replace(/^### (.*)$/gm, '<h3>$1</h3>')
            .replace(/^## (.*)$/gm, '<h2>$1</h2>')
            .replace(/^# (.*)$/gm, '<h1>$1</h1>')
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            .replace(/\*(.*?)\*/g, '<em>$1</em>')
            .replace(/\n/g, '<br>');
          return /^<h[1-3]>/.test(inline) ? inline : `<p>${inline}</p>`;
        })
        .join('');
      markdownPreview.innerHTML = html;
    };
    markdownInput.addEventListener('input', renderMarkdownPreview);
    renderMarkdownPreview();
  }

  const imageUrlInput = document.getElementById('ImageUrl');
  const imagePreview = document.getElementById('imagePreview');
  if (imageUrlInput && imagePreview) {
    const updateImagePreview = () => {
      const url = imageUrlInput.value.trim();
      if (!url) {
        imagePreview.textContent = text.noImageUrl;
        return;
      }
      imagePreview.innerHTML = '';
      const img = document.createElement('img');
      img.alt = 'Inventory preview';
      img.src = url;
      img.onerror = () => {
        imagePreview.textContent = text.imageCouldNotBeLoaded;
      };
      imagePreview.appendChild(img);
    };
    imageUrlInput.addEventListener('input', updateImagePreview);
    updateImagePreview();
  }

  const tagsInput = document.getElementById('Tags');
  const tagPreview = document.getElementById('tagPreview');
  if (tagsInput && tagPreview) {
    const updateTagPreview = () => {
      tagPreview.innerHTML = '';
      tagsInput.value
        .split(',')
        .map(tag => tag.trim())
        .filter(Boolean)
        .slice(0, 12)
        .forEach(tag => {
          const chip = document.createElement('span');
          chip.className = 'tag-chip';
          chip.textContent = tag;
          tagPreview.appendChild(chip);
        });
    };
    tagsInput.addEventListener('input', updateTagPreview);
    updateTagPreview();
  }

  syncToolbar();
})();
