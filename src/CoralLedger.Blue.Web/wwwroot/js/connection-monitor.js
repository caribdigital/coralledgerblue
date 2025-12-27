/**
 * Connection Monitor - Real-time online/offline detection
 * Provides Blazor interop for connection status changes
 */
window.connectionMonitor = (function() {
    let dotNetRef = null;
    let isRegistered = false;

    /**
     * Handle online event
     */
    function handleOnline() {
        console.log('[ConnectionMonitor] Connection restored');
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnOnlineStatusChanged', true)
                .catch(err => console.warn('[ConnectionMonitor] Failed to notify Blazor:', err));
        }
    }

    /**
     * Handle offline event
     */
    function handleOffline() {
        console.log('[ConnectionMonitor] Connection lost');
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnOnlineStatusChanged', false)
                .catch(err => console.warn('[ConnectionMonitor] Failed to notify Blazor:', err));
        }
    }

    /**
     * Register a Blazor component to receive connection updates
     * @param {DotNetObjectReference} ref - Reference to Blazor component
     */
    function register(ref) {
        dotNetRef = ref;

        if (!isRegistered) {
            window.addEventListener('online', handleOnline);
            window.addEventListener('offline', handleOffline);
            isRegistered = true;
            console.log('[ConnectionMonitor] Registered for connection events');
        }
    }

    /**
     * Unregister from connection events
     */
    function unregister() {
        dotNetRef = null;
        // Keep listeners active for other potential subscribers
    }

    /**
     * Get current connection status
     */
    function isOnline() {
        return navigator.onLine;
    }

    /**
     * Perform a connectivity check by fetching a small resource
     * More reliable than navigator.onLine for some scenarios
     */
    async function checkConnectivity() {
        try {
            const response = await fetch('/api/health', {
                method: 'HEAD',
                cache: 'no-store',
                signal: AbortSignal.timeout(5000)
            });
            return response.ok;
        } catch {
            return false;
        }
    }

    return {
        register,
        unregister,
        isOnline,
        checkConnectivity
    };
})();
