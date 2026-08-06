/**
 * seller-shop.js
 * Client-side logic for the Seller Shop Profile page.
 * Handles tab switching, AJAX product loading, sorting, and pagination.
 */

document.addEventListener('DOMContentLoaded', function () {

    // === Tab Switching ===
    const tabs = document.querySelectorAll('.shop-nav-tab');
    const panels = document.querySelectorAll('.shop-tab-panel');

    tabs.forEach(tab => {
        tab.addEventListener('click', function () {
            const target = this.dataset.tab;

            // Update active tab
            tabs.forEach(t => t.classList.remove('active'));
            this.classList.add('active');

            // Show target panel
            panels.forEach(p => p.classList.remove('active'));
            const targetPanel = document.getElementById('panel-' + target);
            if (targetPanel) {
                targetPanel.classList.add('active');
            }

            // Auto-load product grid when "All Products" tab is activated
            if (target === 'all-products') {
                loadShopProducts();
            }
        });
    });

    // === AJAX Product Loading ===
    const sellerId = document.getElementById('shopSellerId')?.value;
    let currentCategory = '';
    let currentSort = 'popular';
    let currentPage = 1;

    /**
     * Fetch products via AJAX and inject into the grid container.
     */
    function loadShopProducts() {
        const container = document.getElementById('shopProductGridContainer');
        if (!container || !sellerId) return;

        // Dim the container to show loading state without layout shift/flicker
        container.style.opacity = '0.5';
        container.style.pointerEvents = 'none';

        const params = new URLSearchParams({
            sellerId: sellerId,
            categoryId: currentCategory || '',
            sort: currentSort,
            page: currentPage
        });

        fetch('/Home/ShopProducts?' + params.toString(), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
        .then(response => response.text())
        .then(html => {
            container.innerHTML = html;
            bindPaginationEvents();
        })
        .catch(() => {
            container.innerHTML = '<div style="text-align:center; padding:40px; color:#d9534f;">Failed to load products.</div>';
        })
        .finally(() => {
            // Restore container visibility and interactivity
            container.style.opacity = '1';
            container.style.pointerEvents = 'auto';
        });
    }

    // === Sidebar Category Clicks ===
    document.querySelectorAll('.shop-sidebar-item').forEach(item => {
        item.addEventListener('click', function () {
            // Update active state in sidebar
            document.querySelectorAll('.shop-sidebar-item').forEach(i => i.classList.remove('active'));
            this.classList.add('active');

            currentCategory = this.dataset.categoryId || '';
            currentPage = 1;
            loadShopProducts();
        });
    });

    // === Sort Button Clicks ===
    document.querySelectorAll('.shop-sort-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.shop-sort-btn').forEach(b => b.classList.remove('active'));
            this.classList.add('active');

            currentSort = this.dataset.sort;
            currentPage = 1;
            loadShopProducts();
        });
    });

    // === Price Dropdown ===
    const priceSelect = document.getElementById('priceSortSelect');
    if (priceSelect) {
        priceSelect.addEventListener('change', function () {
            if (this.value) {
                // Deactivate other sort buttons when price is selected
                document.querySelectorAll('.shop-sort-btn').forEach(b => b.classList.remove('active'));
                currentSort = this.value;
                currentPage = 1;
                loadShopProducts();
            }
        });
    }

    // === Pagination Event Binding (called after AJAX load) ===
    function bindPaginationEvents() {
        document.querySelectorAll('.shop-page-btn').forEach(btn => {
            btn.addEventListener('click', function () {
                const page = parseInt(this.dataset.page);
                if (!isNaN(page)) {
                    currentPage = page;
                    loadShopProducts();
                    // Scroll to sort bar for better UX
                    document.querySelector('.shop-sort-bar')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
        });
    }

    // === Search Scope Dropdown ===
    const scopeSelect = document.getElementById('searchScopeSelect');
    const searchForm = document.getElementById('shopSearchForm');
    if (scopeSelect && searchForm) {
        searchForm.addEventListener('submit', function (e) {
            const scope = scopeSelect.value;
            const query = document.getElementById('shopSearchInput')?.value || '';

            if (scope === 'platform') {
                // Redirect to platform-wide search
                e.preventDefault();
                window.location.href = '/Home/Index?searchQuery=' + encodeURIComponent(query);
            }
            // Otherwise, let the form submit normally (in-shop search handled by default action)
        });
    }
});
