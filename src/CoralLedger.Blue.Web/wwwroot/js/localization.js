/**
 * Localization Helper Functions
 * Provides safe methods for managing culture/language preferences
 */

window.localization = {
    /**
     * Gets the current culture from the cookie
     * @returns {string} The current culture code (e.g., 'en', 'es', 'ht')
     */
    getCurrentCulture: function () {
        const cookieValue = document.cookie
            .split('; ')
            .find(row => row.startsWith('.AspNetCore.Culture='));
        
        if (!cookieValue) {
            return 'en';
        }

        // Parse the cookie format: .AspNetCore.Culture=c=en|uic=en
        const culturePart = cookieValue.split('=')[1];
        const cultureMatch = culturePart.match(/c=([^|]+)/);
        
        return cultureMatch ? cultureMatch[1] : 'en';
    },

    /**
     * Sets the culture preference cookie
     * @param {string} culture - The culture code to set (e.g., 'en', 'es', 'ht')
     */
    setCulture: function (culture) {
        // Validate culture code to prevent injection
        const validCultures = ['en', 'es', 'ht'];
        if (!validCultures.includes(culture)) {
            console.error('Invalid culture code:', culture);
            return;
        }

        // Set cookie with proper format
        const cookieValue = `c=${culture}|uic=${culture}`;
        const maxAge = 31536000; // 1 year in seconds
        document.cookie = `.AspNetCore.Culture=${cookieValue}; path=/; max-age=${maxAge}; SameSite=Lax`;
    }
};
