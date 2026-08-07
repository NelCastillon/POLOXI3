// Pricing page functionality
document.addEventListener('DOMContentLoaded', function() {
  const toggleBtns = document.querySelectorAll('.toggle-btn');

  toggleBtns.forEach(btn => {
    btn.addEventListener('click', function() {
      const billingMode = this.getAttribute('data-billing');

      // Update active toggle state
      toggleBtns.forEach(b => b.classList.remove('active'));
      this.classList.add('active');

      // Update all price displays
      document.querySelectorAll('.price-amount').forEach(priceEl => {
        if (billingMode === 'monthly') {
          const monthlyPrice = priceEl.getAttribute('data-monthly');
          priceEl.textContent = '$' + monthlyPrice;
        } else if (billingMode === 'annual') {
          const annualPrice = priceEl.getAttribute('data-annual');
          priceEl.textContent = '$' + annualPrice;
        }
      });
    });
  });

  // Smooth scroll for CTA buttons
  document.querySelectorAll('a[href="#"]').forEach(link => {
    link.addEventListener('click', function(e) {
      e.preventDefault();
    });
  });

  // FAQ accordion functionality
  document.querySelectorAll('details').forEach(detail => {
    detail.addEventListener('toggle', function() {
      if (this.open) {
        // Close other details
        document.querySelectorAll('details').forEach(otherDetail => {
          if (otherDetail !== this && otherDetail.open) {
            otherDetail.open = false;
          }
        });
      }
    });
  });
});
