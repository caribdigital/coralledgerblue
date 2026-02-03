/**
 * Form Utilities - Handles unsaved changes warnings and form state
 */

window.formUtils = {
    /**
     * Sets up an unsaved changes warning that prevents navigation
     * @param {boolean} hasUnsavedChanges - Whether the form has unsaved changes
     */
    setUnsavedChangesWarning: function (hasUnsavedChanges) {
        if (hasUnsavedChanges) {
            // Set up beforeunload event to warn user
            window.onbeforeunload = function (e) {
                e.preventDefault();
                // Most modern browsers ignore custom message and show their own
                const message = 'You have unsaved changes. Are you sure you want to leave?';
                e.returnValue = message;
                return message;
            };
        } else {
            // Remove the warning
            window.onbeforeunload = null;
        }
    },

    /**
     * Clears the unsaved changes warning
     */
    clearUnsavedChangesWarning: function () {
        window.onbeforeunload = null;
    }
};
