/* ============================================================================
   NovaLeave Shared JavaScript
   Version: 1.0.0
   Specification: spec_003-screen-construction-guide.md

   Interactive behaviors for mobile nav, alerts, modals, and accessibility
   ============================================================================ */

(function() {
  'use strict';

  // =========================================================================
  // Alert Auto-Dismiss and Manual Dismiss
  // spec_003 §1.2 - success alerts auto-dismiss after 5s, errors persistent
  // =========================================================================
  function initAlerts() {
    const alerts = document.querySelectorAll('.alert');

    alerts.forEach(alert => {
      // Manual dismiss button handler
      const dismissButton = alert.querySelector('.alert__dismiss');
      if (dismissButton) {
        dismissButton.addEventListener('click', () => {
          dismissAlert(alert);
        });
      }

      // Escape key handler
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && alert.offsetParent !== null) {
          dismissAlert(alert);
        }
      });

      // Auto-dismiss for success alerts (5 seconds)
      if (alert.classList.contains('alert--success')) {
        setTimeout(() => {
          dismissAlert(alert);
        }, 5000);
      }
    });
  }

  function dismissAlert(alert) {
    // Check for reduced motion preference
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (prefersReducedMotion) {
      // Instant removal if user prefers reduced motion
      alert.remove();
    } else {
      // Fade out transition (180ms per tokens.css --motion-duration-fast)
      alert.style.opacity = '0';
      alert.style.transition = 'opacity 180ms ease-out';

      setTimeout(() => {
        alert.remove();
      }, 180);
    }
  }

  // =========================================================================
  // Modal Focus Trap and Keyboard Handlers
  // spec_003 §1.3 - focus trap, Escape close, confirm button NOT pre-focused
  // Supports native <dialog> (showModal/close) and legacy overlay-based modals.
  // =========================================================================
  function initModals() {
    // Native <dialog> modals: triggered by [data-modal-target] pointing to a <dialog> id
    var dialogTriggers = document.querySelectorAll('[data-modal-target]');
    dialogTriggers.forEach(function(button) {
      var targetId = button.getAttribute('data-modal-target');
      var dialog = document.getElementById(targetId);
      if (!dialog) return;

      if (dialog.tagName === 'DIALOG') {
        // Native <dialog> path
        button.addEventListener('click', function() {
          dialog.showModal();
          // Focus cancel button first to prevent accidental confirmation
          var cancelBtn = dialog.querySelector('[data-action="cancel"]');
          if (cancelBtn) cancelBtn.focus();
        });

        var cancelBtn = dialog.querySelector('[data-action="cancel"]');
        if (cancelBtn) {
          cancelBtn.addEventListener('click', function() {
            dialog.close();
          });
        }
      } else {
        // Legacy overlay-based modal fallback
        var overlay = dialog.closest('.modal-overlay');
        if (!overlay) return;

        button.addEventListener('click', function() {
          openModal(dialog, overlay);
        });

        var cancelButton = dialog.querySelector('[data-action="cancel"]');
        if (cancelButton) {
          cancelButton.addEventListener('click', function() {
            closeModal(dialog, overlay);
          });
        }

        var confirmButton = dialog.querySelector('[data-action="confirm"]');
        if (confirmButton) {
          confirmButton.addEventListener('click', function() {
            var form = confirmButton.closest('form');
            if (form) return;
          });
        }

        document.addEventListener('keydown', function(e) {
          if (e.key === 'Escape' && overlay.getAttribute('aria-hidden') === 'false') {
            closeModal(dialog, overlay);
          }
        });

        overlay.addEventListener('click', function(e) {
          if (e.target === overlay) {
            closeModal(dialog, overlay);
          }
        });
      }
    });
  }

  function openModal(modal, overlay) {
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    overlay.setAttribute('aria-hidden', 'false');
    overlay.style.display = 'flex';

    if (!prefersReducedMotion) {
      // Fade in transition (280ms per tokens.css --motion-duration-normal)
      overlay.style.opacity = '0';
      setTimeout(() => {
        overlay.style.transition = 'opacity 280ms ease-out';
        overlay.style.opacity = '1';
      }, 10);
    }

    // Set focus to first focusable element EXCEPT confirm button
    // (prevent accidental Enter confirmation per spec_003 §1.3)
    const focusableElements = modal.querySelectorAll(
      'button[data-action="cancel"], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );

    if (focusableElements.length > 0) {
      focusableElements[0].focus();
    }

    // Store last focused element to restore later
    overlay._previouslyFocused = document.activeElement;

    // Enable focus trap
    enableFocusTrap(modal);
  }

  function closeModal(modal, overlay) {
    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    overlay.setAttribute('aria-hidden', 'true');

    if (prefersReducedMotion) {
      overlay.style.display = 'none';
    } else {
      overlay.style.opacity = '0';
      setTimeout(() => {
        overlay.style.display = 'none';
      }, 280);
    }

    // Restore focus to previously focused element
    if (overlay._previouslyFocused) {
      overlay._previouslyFocused.focus();
    }

    // Disable focus trap
    disableFocusTrap(modal);
  }

  function enableFocusTrap(modal) {
    const focusableElements = modal.querySelectorAll(
      'button, input, select, textarea, a[href], [tabindex]:not([tabindex="-1"])'
    );

    if (focusableElements.length === 0) return;

    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];

    function trapFocus(e) {
      if (e.key !== 'Tab') return;

      if (e.shiftKey) {
        // Shift + Tab
        if (document.activeElement === firstElement) {
          e.preventDefault();
          lastElement.focus();
        }
      } else {
        // Tab
        if (document.activeElement === lastElement) {
          e.preventDefault();
          firstElement.focus();
        }
      }
    }

    modal._focusTrapHandler = trapFocus;
    modal.addEventListener('keydown', trapFocus);
  }

  function disableFocusTrap(modal) {
    if (modal._focusTrapHandler) {
      modal.removeEventListener('keydown', modal._focusTrapHandler);
      modal._focusTrapHandler = null;
    }
  }

  // =========================================================================
  // Working Day Calculator
  // Counts weekdays (Mon-Fri) between two dates inclusive, matching backend IWorkingDayCalculator
  // =========================================================================
  function calculateWorkingDays(startDate, endDate) {
    if (!startDate || !endDate || startDate > endDate) return 0;
    var count = 0;
    var current = new Date(startDate.getTime());
    while (current <= endDate) {
      var day = current.getDay();
      if (day !== 0 && day !== 6) { // Excluye sábado (6) y domingo (0)
        count++;
      }
      current.setDate(current.getDate() + 1);
    }
    return count;
  }

  function initWorkingDayCalculation() {
    var startInput = document.querySelector('input[name="StartDate"]');
    var endInput = document.querySelector('input[name="EndDate"]');
    var display = document.getElementById('computed-days');

    if (!startInput || !endInput || !display) return;

    function update() {
      var startVal = startInput.value;
      var endVal = endInput.value;
      if (startVal && endVal) {
        var start = new Date(startVal + 'T00:00:00');
        var end = new Date(endVal + 'T00:00:00');
        var days = calculateWorkingDays(start, end);
        display.textContent = days > 0 ? days : '—';
      } else {
        display.textContent = '—';
      }
    }

    startInput.addEventListener('change', update);
    endInput.addEventListener('change', update);
  }

  // =========================================================================
  // Mobile Navigation Toggle
  // spec_003 - responsive sidebar navigation, collapsible on mobile
  // =========================================================================
  function initMobileNav() {
    const navToggle = document.querySelector('[data-nav-toggle]');
    const sidebar = document.querySelector('.sidebar');

    if (!navToggle || !sidebar) return;

    navToggle.addEventListener('click', () => {
      const isExpanded = sidebar.getAttribute('aria-expanded') === 'true';
      sidebar.setAttribute('aria-expanded', !isExpanded);
      sidebar.classList.toggle('sidebar--open');
    });

    // Close sidebar when clicking outside on mobile
    document.addEventListener('click', (e) => {
      if (window.innerWidth < 768 && 
          !sidebar.contains(e.target) && 
          !navToggle.contains(e.target) &&
          sidebar.classList.contains('sidebar--open')) {
        sidebar.classList.remove('sidebar--open');
        sidebar.setAttribute('aria-expanded', 'false');
      }
    });
  }

  // =========================================================================
  // Initialization on DOM Ready
  // =========================================================================
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  function init() {
    initAlerts();
    initModals();
    initMobileNav();
    initWorkingDayCalculation();
  }

})();
