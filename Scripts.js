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





function toggleStream(username, password) {
    var iframe = document.getElementById('vdoNinjaIframe');
    var icon = document.getElementById('streamIcon');

    if (iframe.src.includes("vdo.ninja")) {
        iframe.src = "";
        icon.setAttribute('fill', 'limegreen');
    } else {
        iframe.src = `https://vdo.ninja/alpha/?room=${username}&pw=${password}&push&screenshare=1&autostart&noaudio`;
        icon.setAttribute('fill', 'red');
    }
}
