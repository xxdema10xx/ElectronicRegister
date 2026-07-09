const capitalize = (str = '') => {
    return str.length
        ? str.charAt(0).toUpperCase() + str.slice(1)
        : '';
};

function populateFields(ids, values) {
    ids.forEach((id, i) => {
        const element = document.getElementById(id);
        if (element) element.innerText = values[i];
    });
}