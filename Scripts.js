window.updateImage = function (base64Image) {
    const imgElement = document.getElementById("dynamicImage");
    if (imgElement) {
        imgElement.src = base64Image;
    }
};

window.updateProgressBar = (percentage) => {
    let progressBar = document.getElementById("healthProgressBar");
    progressBar.style.width = percentage + "%";
    progressBar.innerText = percentage.toFixed(2) + "%"; // отображаем значение с двумя десятичными знаками
}