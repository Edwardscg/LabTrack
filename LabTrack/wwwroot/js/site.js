document.querySelectorAll('form').forEach(form => {
    form.addEventListener('submit', () => {
        if (!form.checkValidity()) return;
        form.querySelectorAll('button[type="submit"]').forEach(button => {
            button.disabled = true;
            button.setAttribute('aria-busy', 'true');
        });
    });
});

const identifier = document.querySelector('#Identificador');
identifier?.addEventListener('input', () => {
    const value = identifier.value.trim();
    const resemblesStudentCode = /^c\d/i.test(value);
    identifier.setCustomValidity(resemblesStudentCode && !/^C00[0-9]{6}$/.test(value)
        ? 'El código debe tener el formato C00 seguido de seis dígitos.' : '');
});
