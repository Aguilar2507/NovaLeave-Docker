// Login validation and UX enhancements
// Client-side validation, auto-focus, disable-on-submit (CU-012 FP-03/FP-06/FP-07)

(function () {
    'use strict';

    // Wait for DOM to be ready
    document.addEventListener('DOMContentLoaded', function () {
        const form = document.getElementById('loginForm');
        const submitBtn = document.getElementById('submitBtn');
        const submitText = document.getElementById('submitText');
        const submitSpinner = document.getElementById('submitSpinner');
        const emailInput = document.querySelector('input[name="Email"]');
        const passwordInput = document.querySelector('input[name="Password"]');

        // Auto-focus on email field (CU-012 FP-03)
        if (emailInput) {
            emailInput.focus();
        }

        // Form submission handler
        if (form && submitBtn) {
            form.addEventListener('submit', function (e) {
                // Clear previous errors
                clearValidationErrors();

                // Client-side validation
                let isValid = true;

                // Validate email
                if (!emailInput || !emailInput.value.trim()) {
                    showValidationError(emailInput, 'El correo electrónico es obligatorio.');
                    isValid = false;
                } else if (!isValidEmail(emailInput.value.trim())) {
                    showValidationError(emailInput, 'Formato de correo electrónico inválido.');
                    isValid = false;
                }

                // Validate password
                if (!passwordInput || !passwordInput.value) {
                    showValidationError(passwordInput, 'La contraseña es obligatoria.');
                    isValid = false;
                }

                // If validation fails, prevent submit
                if (!isValid) {
                    e.preventDefault();
                    return false;
                }

                // Disable submit button to prevent double-submit (CU-012 FP-06)
                submitBtn.disabled = true;
                submitText.classList.add('d-none');
                submitSpinner.classList.remove('d-none');

                // Re-enable after 5 seconds as fallback (in case server error prevents page reload)
                setTimeout(function () {
                    submitBtn.disabled = false;
                    submitText.classList.remove('d-none');
                    submitSpinner.classList.add('d-none');
                }, 5000);
            });
        }

        // Email validation helper
        function isValidEmail(email) {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            return emailRegex.test(email);
        }

        // Show validation error for a field
        function showValidationError(input, message) {
            if (!input) return;

            const span = input.parentElement.querySelector('.text-danger');
            if (span) {
                span.textContent = message;
                span.style.display = 'block';
            }

            input.classList.add('is-invalid');
        }

        // Clear all validation errors
        function clearValidationErrors() {
            const errorSpans = form.querySelectorAll('.text-danger');
            errorSpans.forEach(function (span) {
                span.textContent = '';
                span.style.display = 'none';
            });

            const inputFields = form.querySelectorAll('.is-invalid');
            inputFields.forEach(function (input) {
                input.classList.remove('is-invalid');
            });
        }

        // Clear validation error on input
        if (emailInput) {
            emailInput.addEventListener('input', function () {
                const span = emailInput.parentElement.querySelector('.text-danger');
                if (span) {
                    span.textContent = '';
                    span.style.display = 'none';
                }
                emailInput.classList.remove('is-invalid');
            });
        }

        if (passwordInput) {
            passwordInput.addEventListener('input', function () {
                const span = passwordInput.parentElement.querySelector('.text-danger');
                if (span) {
                    span.textContent = '';
                    span.style.display = 'none';
                }
                passwordInput.classList.remove('is-invalid');
            });
        }
    });
})();
