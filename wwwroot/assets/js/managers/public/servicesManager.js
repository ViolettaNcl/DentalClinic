document.addEventListener("DOMContentLoaded", () => {
    const cards = document.querySelectorAll(".card");

    const serviceLinks = {
        cosmetic: "/pages/services/cosmetic-treatments.html",
        fillings: "/pages/services/fillings.html",
        crowns: "/pages/services/crowns.html",
        implants: "/pages/services/implants.html",
        "root-canal": "/pages/services/root-canal.html",
        bridges: "/pages/services/bridges.html",
        extractions: "/pages/services/extractions.html",
        dentures: "/pages/services/prosthetics.html"
    };

    cards.forEach(card => {
        card.style.cursor = "pointer";

        card.addEventListener("click", () => {
            const id = card.getAttribute("id");
            const url = serviceLinks[id];
            if (url) window.location.href = url;
        });
    });
});