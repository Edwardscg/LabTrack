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

const reservationForm = document.querySelector('#reservation-form');
if (reservationForm) {
    const start = reservationForm.querySelector('#HoraInicio');
    const end = reservationForm.querySelector('#HoraFin');
    const validateTime = () => end.setCustomValidity(start.value && end.value && end.value <= start.value
        ? 'La hora de fin debe ser posterior a la hora de inicio.' : '');
    start.addEventListener('input', validateTime);
    end.addEventListener('input', validateTime);
}
