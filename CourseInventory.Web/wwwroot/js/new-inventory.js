(() => {
  const form = document.querySelector('[data-new-inventory-form]');
  if (!form) return;

  const titleInput = form.querySelector('#Title');
  const categoryInput = form.querySelector('#Category');
  const imageInput = form.querySelector('#ImageUrl');
  const imagePreview = document.getElementById('imagePreview');
  const markdownInput = document.getElementById('DescriptionMarkdown');
  const markdownPreview = document.getElementById('markdownPreview');
  const hiddenTags = form.querySelector('[data-tags-hidden]');
  const tagInput = form.querySelector('[data-tag-input]');
  const tagBadges = form.querySelector('[data-tag-badges]');
  const tagSuggestions = form.querySelector('[data-tag-suggestions]');
  const submitButton = form.querySelector('[data-submit-button]');
  const submitLabel = form.querySelector('.submit-label');
  const submitSpinner = form.querySelector('.submit-spinner');
  const tags = new Set((hiddenTags?.value || '').split(',').map(t => t.trim()).filter(Boolean));

  const escapeHtml = value => value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');

  const renderMarkdown = () => {
    const raw = markdownInput.value.trim();
    if (!raw) {
      markdownPreview.textContent = markdownPreview.dataset.emptyText || 'Preview appears as you type.';
      return;
    }

    const html = escapeHtml(raw)
      .replace(/^### (.*)$/gm, '<h3>$1</h3>')
      .replace(/^## (.*)$/gm, '<h2>$1</h2>')
      .replace(/^# (.*)$/gm, '<h1>$1</h1>')
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/`(.*?)`/g, '<code>$1</code>')
      .split(/\n{2,}/)
      .map(block => /^<h[1-3]>/.test(block) ? block : `<p>${block.replace(/\n/g, '<br>')}</p>`)
      .join('');

    markdownPreview.innerHTML = html;
  };

  const updateImagePreview = () => {
    const url = imageInput.value.trim();
    imageInput.setCustomValidity('');
    if (!url) {
      imagePreview.textContent = imagePreview.dataset.emptyText || 'No image URL';
      validateForm();
      return;
    }

    try {
      new URL(url);
    } catch {
      imageInput.setCustomValidity('Invalid image URL');
      imagePreview.textContent = imagePreview.dataset.invalidText || 'Invalid image URL';
      validateForm();
      return;
    }

    imagePreview.innerHTML = '';
    const img = document.createElement('img');
    img.alt = 'Inventory preview';
    img.src = url;
    img.onload = validateForm;
    img.onerror = () => {
      imageInput.setCustomValidity('Invalid image URL');
      imagePreview.textContent = imagePreview.dataset.invalidText || 'Invalid image URL';
      validateForm();
    };
    imagePreview.appendChild(img);
  };

  const syncTags = () => {
    hiddenTags.value = Array.from(tags).join(', ');
    tagBadges.innerHTML = '';
    tags.forEach(tag => {
      const badge = document.createElement('button');
      badge.type = 'button';
      badge.className = 'tag-token';
      badge.innerHTML = `${escapeHtml(tag)} <span aria-hidden="true">&times;</span>`;
      badge.addEventListener('click', () => {
        tags.delete(tag);
        syncTags();
      });
      tagBadges.appendChild(badge);
    });
  };

  const addTag = value => {
    const tag = value.trim().replaceAll(',', '');
    if (!tag || tags.size >= 12) return;
    tags.add(tag);
    tagInput.value = '';
    tagSuggestions.hidden = true;
    syncTags();
  };

  const loadSuggestions = async () => {
    const term = tagInput.value.trim();
    if (!term) {
      tagSuggestions.hidden = true;
      return;
    }

    const response = await fetch(`/Tags/Suggest?term=${encodeURIComponent(term)}`);
    const suggestions = await response.json();
    tagSuggestions.innerHTML = '';
    suggestions.filter(item => !tags.has(item)).forEach(item => {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = item;
      button.addEventListener('click', () => addTag(item));
      tagSuggestions.appendChild(button);
    });
    tagSuggestions.hidden = tagSuggestions.childElementCount === 0;
  };

  function validateForm() {
    const valid = titleInput.value.trim().length > 0 &&
      categoryInput.value.trim().length > 0 &&
      imageInput.validity.valid;
    submitButton.disabled = !valid;
    return valid;
  }

  tagInput.addEventListener('keydown', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      addTag(tagInput.value);
    }
    if (event.key === 'Backspace' && !tagInput.value && tags.size > 0) {
      tags.delete(Array.from(tags).pop());
      syncTags();
    }
  });

  tagInput.addEventListener('input', loadSuggestions);
  markdownInput.addEventListener('input', renderMarkdown);
  imageInput.addEventListener('input', updateImagePreview);
  titleInput.addEventListener('input', validateForm);
  categoryInput.addEventListener('change', validateForm);

  form.addEventListener('submit', event => {
    updateImagePreview();
    if (!form.checkValidity() || !validateForm()) {
      event.preventDefault();
      event.stopPropagation();
      form.classList.add('was-validated');
      return;
    }

    submitButton.disabled = true;
    submitLabel.textContent = 'Creating...';
    submitSpinner.classList.remove('d-none');
  });

  syncTags();
  renderMarkdown();
  updateImagePreview();
  validateForm();
})();
