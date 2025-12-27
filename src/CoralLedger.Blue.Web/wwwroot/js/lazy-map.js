/**
 * Lazy Map Observer - Uses IntersectionObserver to defer map loading
 * until the element is visible in the viewport.
 */
window.lazyMapObserver = (function() {
    const observers = new WeakMap();

    /**
     * Start observing an element for visibility
     * @param {HTMLElement} element - The container element to observe
     * @param {DotNetObjectReference} dotNetRef - Reference to the Blazor component
     * @param {number} rootMargin - Pixels to add to the root margin (preload before visible)
     */
    function observe(element, dotNetRef, rootMargin = 100) {
        if (!element || observers.has(element)) {
            return;
        }

        // Check if element is already visible on initial load
        const rect = element.getBoundingClientRect();
        const isVisible = rect.top < window.innerHeight + rootMargin && rect.bottom > -rootMargin;

        if (isVisible) {
            // Already visible, notify immediately
            dotNetRef.invokeMethodAsync('SetVisible');
            return;
        }

        const options = {
            root: null, // viewport
            rootMargin: `${rootMargin}px`,
            threshold: 0
        };

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    // Element is now visible, notify Blazor
                    dotNetRef.invokeMethodAsync('SetVisible');
                    // Stop observing after first intersection
                    observer.unobserve(element);
                    observers.delete(element);
                }
            });
        }, options);

        observer.observe(element);
        observers.set(element, { observer, dotNetRef });
    }

    /**
     * Stop observing an element
     * @param {HTMLElement} element - The container element to stop observing
     */
    function unobserve(element) {
        if (!element) return;

        const data = observers.get(element);
        if (data) {
            data.observer.unobserve(element);
            data.observer.disconnect();
            observers.delete(element);
        }
    }

    return {
        observe,
        unobserve
    };
})();
