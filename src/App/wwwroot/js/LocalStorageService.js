/**
 * @param {readonly string[]} keys
 * @returns {Readonly<Record<string, string | null>>}
 */
export function loadItems(keys) {
    const result = {};
    keys.forEach(key => {
        result[key] = localStorage.getItem(key);
    });
    return result;
}

/**
 * @param {Readonly<Record<string, string | null>>} items
 * @returns {void}
 */
export function saveItems(items) {
    Object.entries(items).forEach(([key, value]) => {
        if (value === null) {
            localStorage.removeItem(key);
        } else {
            localStorage.setItem(key, value);
        }
    });
}
