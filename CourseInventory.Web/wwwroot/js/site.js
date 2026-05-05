const lang = document.documentElement.lang?.toLowerCase() || 'en';
const uiText = lang.startsWith('fr')
  ? {
      selectRows: 'Sélectionner',
      selected: count => `${count} sélectionné(s)`,
      unsavedChanges: 'Modifications non enregistrées',
      saving: 'Enregistrement...',
      saved: 'Tous les changements sont enregistrés',
      conflict: 'Conflit',
      previewAppearsAsYouType: "L'aperçu apparaît pendant la saisie.",
      noImageUrl: "Aucune URL d'image",
      imageCouldNotBeLoaded: "L'image n'a pas pu être chargée"
    }
  : {
      selectRows: 'Select rows',
      selected: count => `${count} selected`,
      unsavedChanges: 'Unsaved changes',
      saving: 'Saving...',
      saved: 'All changes saved',
      conflict: 'Conflict',
      previewAppearsAsYouType: 'Preview appears as you type.',
      noImageUrl: 'No image URL',
      imageCouldNotBeLoaded: 'Image could not be loaded'
    };

document.addEventListener('click', (event) => {
  const checkbox = event.target.closest('input[type="checkbox"]');
  if (checkbox) return;
  const row = event.target.closest('tr[data-href]');
  if (row) window.location = row.dataset.href;
});

document.querySelectorAll('.js-check-all').forEach(checkAll => {
  checkAll.addEventListener('change', () => {
    const table = checkAll.closest('table');
    table.querySelectorAll('tbody input[type="checkbox"]').forEach(cb => cb.checked = checkAll.checked);
    updateSelectionBar(table);
  });
});

document.querySelectorAll('.js-table-select tbody input[type="checkbox"]').forEach(cb => {
  cb.addEventListener('change', () => updateSelectionBar(cb.closest('table')));
});

function updateSelectionBar(table) {
  const form = table.closest('form');
  const count = table.querySelectorAll('tbody input[type="checkbox"]:checked').length;
  form?.querySelector('.selection-bar')?.classList.toggle('d-none', count === 0);
  table.closest('.panel')?.querySelector('.selection-count')?.replaceChildren(document.createTextNode(count ? uiText.selected(count) : uiText.selectRows));
}

document.querySelectorAll('.js-filter').forEach(input => {
  input.addEventListener('input', () => {
    const table = input.closest('.panel')?.querySelector('table') || input.nextElementSibling?.querySelector('table');
    const term = input.value.toLowerCase();
    table?.querySelectorAll('tbody tr').forEach(row => {
      row.hidden = !row.innerText.toLowerCase().includes(term);
    });
  });
});

document.querySelectorAll('.js-sortable th').forEach((th, index) => {
  th.addEventListener('click', () => {
    const tbody = th.closest('table').querySelector('tbody');
    [...tbody.rows]
      .sort((a, b) => a.cells[index].innerText.localeCompare(b.cells[index].innerText))
      .forEach(row => tbody.appendChild(row));
  });
});

const settingsForm = document.getElementById('settingsForm');
if (settingsForm) {
  let dirty = false;
  const status = document.getElementById('autosaveStatus');
  settingsForm.addEventListener('input', () => {
    dirty = true;
    status.textContent = uiText.unsavedChanges;
  });
  setInterval(async () => {
    if (!dirty) return;
    dirty = false;
    status.textContent = uiText.saving;
    const response = await fetch(settingsForm.action, {
      method: 'POST',
      headers: { 'X-Requested-With': 'XMLHttpRequest' },
      body: new FormData(settingsForm)
    });
    const result = await response.json();
    status.textContent = result.ok ? uiText.saved : uiText.conflict;
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
    const text = textarea?.value?.trim();
    if (!text) return;

    try {
      await connection.invoke('SendMessage', inventoryId, text);
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
      markdownPreview.textContent = uiText.previewAppearsAsYouType;
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
      imagePreview.textContent = uiText.noImageUrl;
      return;
    }
    imagePreview.innerHTML = '';
    const img = document.createElement('img');
    img.alt = 'Inventory preview';
    img.src = url;
    img.onerror = () => {
      imagePreview.textContent = uiText.imageCouldNotBeLoaded;
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
