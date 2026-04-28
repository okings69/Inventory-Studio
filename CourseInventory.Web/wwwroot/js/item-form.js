(() => {
  const form = document.querySelector('[data-item-form]');
  if (!form) return;

  const submit = form.querySelector('[data-item-submit]');
  const submitLabel = form.querySelector('[data-item-submit-label]');
  const submitLoader = form.querySelector('[data-item-submit-loader]');
  const toast = document.querySelector('[data-item-toast]');
  const defaultSubmitLabel = submitLabel?.textContent ?? 'Save';

  const setSubmitState = isSaving => {
    if (submit) {
      submit.disabled = isSaving;
    }

    if (submitLabel) {
      submitLabel.textContent = isSaving ? 'Saving...' : defaultSubmitLabel;
    }

    submitLoader?.classList.toggle('d-none', !isSaving);
  };

  const resetSubmitState = () => {
    setSubmitState(false);
  };

  form.querySelectorAll('input, select, textarea').forEach(input => {
    input.addEventListener('input', resetSubmitState);
    input.addEventListener('change', resetSubmitState);
  });

  form.addEventListener('submit', event => {
    if (!form.checkValidity()) {
      event.preventDefault();
      event.stopPropagation();
      resetSubmitState();
      form.classList.add('was-validated');
      return;
    }

    setSubmitState(true);
  });

  if (toast && window.bootstrap) {
    new bootstrap.Toast(toast, { delay: 4200 }).show();
  }
})();
