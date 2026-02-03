// Utility functions for CoralLedger Blue

/**
 * Triggers a file download in the browser
 * @param {string} filename - The name of the file to download
 * @param {string} base64Data - Base64 encoded file content
 * @param {string} contentType - MIME type of the file
 */
window.downloadFile = function (filename, base64Data, contentType) {
    // Convert base64 to blob
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
        byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });

    // Create download link and trigger download
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};
