// Accessibility utilities for keyboard navigation and ARIA support

window.accessibility = {
    /**
     * Sets up keyboard navigation for radio button-style groups
     * @param {string} selector - CSS selector for the group container
     */
    setupRadioGroupKeyboard: function(selector) {
        const group = document.querySelector(selector);
        if (!group) return;

        const options = group.querySelectorAll('[role="radio"]');
        if (options.length === 0) return;

        // Set up roving tabindex - only selected or first option is tabbable
        const updateTabIndices = () => {
            const selectedOption = Array.from(options).find(opt => opt.getAttribute('aria-checked') === 'true');
            const tabbableOption = selectedOption || options[0];
            
            options.forEach(option => {
                option.setAttribute('tabindex', option === tabbableOption ? '0' : '-1');
            });
        };

        updateTabIndices();

        // Watch for aria-checked changes to update tabindex
        const observer = new MutationObserver(() => {
            updateTabIndices();
        });

        options.forEach(option => {
            observer.observe(option, {
                attributes: true,
                attributeFilter: ['aria-checked']
            });
        });

        options.forEach((option, index) => {
            option.addEventListener('keydown', (e) => {
                let targetIndex = -1;

                switch(e.key) {
                    case 'ArrowRight':
                    case 'ArrowDown':
                        e.preventDefault();
                        targetIndex = (index + 1) % options.length;
                        break;
                    case 'ArrowLeft':
                    case 'ArrowUp':
                        e.preventDefault();
                        targetIndex = (index - 1 + options.length) % options.length;
                        break;
                    case 'Home':
                        e.preventDefault();
                        targetIndex = 0;
                        break;
                    case 'End':
                        e.preventDefault();
                        targetIndex = options.length - 1;
                        break;
                    case ' ':
                    case 'Enter':
                        e.preventDefault();
                        option.click();
                        return;
                }

                if (targetIndex !== -1) {
                    const targetOption = options[targetIndex];
                    targetOption.focus();
                    // Auto-select on arrow key navigation per ARIA authoring practices
                    targetOption.click();
                }
            });
        });
    },

    /**
     * Focuses an element by ID or selector
     * @param {string} elementId - Element ID or CSS selector
     */
    focusElement: function(elementId) {
        const element = document.getElementById(elementId) || document.querySelector(elementId);
        if (element) {
            element.focus();
        }
    },

    /**
     * Initialize keyboard navigation for all relevant groups
     */
    initialize: function() {
        // Set up keyboard navigation for type selector
        this.setupRadioGroupKeyboard('.type-selector');
        
        // Set up keyboard navigation for severity selector
        this.setupRadioGroupKeyboard('.severity-selector');
    }
};

// Auto-initialize on DOM content loaded
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => window.accessibility.initialize());
} else {
    window.accessibility.initialize();
}
