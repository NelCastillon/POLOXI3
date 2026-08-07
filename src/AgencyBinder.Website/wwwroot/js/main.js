
// Mobile nav toggle
const mobileToggle = document.querySelector('.mobile-toggle');
const navMenu = document.querySelector('.nav-menu');
if (mobileToggle && navMenu) {
  mobileToggle.addEventListener('click', () => {
    const isOpen = navMenu.classList.toggle('open');
    mobileToggle.setAttribute('aria-expanded', isOpen);
    mobileToggle.innerHTML = isOpen
      ? `<svg width="22" height="22" viewBox="0 0 22 22" fill="none" xmlns="http://www.w3.org/2000/svg"><line x1="3" y1="3" x2="19" y2="19" stroke="#344054" stroke-width="2.5" stroke-linecap="round"/><line x1="19" y1="3" x2="3" y2="19" stroke="#344054" stroke-width="2.5" stroke-linecap="round"/></svg>`
      : `<svg width="22" height="22" viewBox="0 0 22 22" fill="none" xmlns="http://www.w3.org/2000/svg"><rect y="4" width="22" height="2.5" rx="1.25" fill="#344054"/><rect y="10" width="22" height="2.5" rx="1.25" fill="#344054"/><rect y="16" width="22" height="2.5" rx="1.25" fill="#344054"/></svg>`;
  });
  // Close nav when any link inside the menu is clicked
  navMenu.querySelectorAll('a').forEach(a => {
    a.addEventListener('click', () => {
      navMenu.classList.remove('open');
      mobileToggle.setAttribute('aria-expanded', 'false');
    });
  });
}

// Smooth scroll for anchor links
const nav = document.querySelector('.nav');
document.querySelectorAll('[data-scroll]').forEach(a => {
  a.addEventListener('click', e => {
    const target = document.querySelector(a.getAttribute('href'));
    if (target) {
      e.preventDefault();
      target.scrollIntoView({behavior:'smooth', block:'start'});
    }
  });
});

// Dynamic header shadow on scroll
let lastScrollY = 0;
window.addEventListener('scroll', () => {
  const currentScrollY = window.scrollY;
  if (currentScrollY > 40) {
    nav.style.boxShadow = '0 12px 40px rgba(0,0,0,.08)';
  } else {
    nav.style.boxShadow = '0 8px 32px rgba(0,0,0,.04)';
  }
  lastScrollY = currentScrollY;
}, { passive: true });

// Intersection Observer for fade-in animations
const observerOptions = {
  threshold: 0.1,
  rootMargin: '0px 0px -50px 0px'
};

const observer = new IntersectionObserver((entries) => {
  entries.forEach(entry => {
    if (entry.isIntersecting) {
      entry.target.style.opacity = '1';
      entry.target.style.transform = 'translateY(0)';
    }
  });
}, observerOptions);

// Observe cards and steps for animation
document.querySelectorAll('.card:not(.price-card), .step, .price-card').forEach(el => {
  if (!el.style.opacity) {
    el.style.opacity = '0';
    el.style.transform = 'translateY(20px)';
    el.style.transition = '.6s cubic-bezier(.34,.1,.64,.1)';
  }
  observer.observe(el);
});

// Counter animation for metrics
const animateCounters = (element) => {
  const strongElements = element.querySelectorAll('.metric strong');
  strongElements.forEach(el => {
    const text = el.textContent;
    const numberMatch = text.match(/[\d,]+/);

    if (!numberMatch) {
      return;
    }

    el.style.minWidth = `${el.offsetWidth}px`;
    el.style.fontVariantNumeric = 'tabular-nums';

    const finalValue = parseInt(numberMatch[0].replace(/,/g, ''), 10);
    const prefix = text.slice(0, numberMatch.index);
    const suffix = text.slice(numberMatch.index + numberMatch[0].length);
    const duration = 900;
    const startTime = performance.now();

    const tick = (now) => {
      const progress = Math.min((now - startTime) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      const currentValue = Math.round(finalValue * eased);
      el.textContent = `${prefix}${currentValue.toLocaleString()}${suffix}`;

      if (progress < 1) {
        requestAnimationFrame(tick);
      } else {
        el.textContent = text;
      }
    };

    requestAnimationFrame(tick);
  });
};

// Trigger counter animation on scroll
const metricsObserver = new IntersectionObserver((entries) => {
  entries.forEach(entry => {
    if (entry.isIntersecting) {
      animateCounters(entry.target);
      metricsObserver.unobserve(entry.target);
    }
  });
}, { threshold: 0.5 });

const metricGrid = document.querySelector('.metric-grid');
if (metricGrid) {
  metricsObserver.observe(metricGrid);
}

// Demo form handling
const demoForm = document.querySelector('#demoForm');
if (demoForm) {
  demoForm.addEventListener('submit', e => {
    e.preventDefault();
    const message = document.querySelector('#formMessage');
    if (message) {
      message.textContent = 'Enterprise consultation request captured. Connect this form to your Azure Function, CRM, or Service Bus workflow.';
      message.style.display = 'block';
    }
  });
}
