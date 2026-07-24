document.addEventListener('DOMContentLoaded', () => {
    const items = document.querySelectorAll('.faq-item');

    items.forEach(item => {
        item.querySelector('.faq-question').addEventListener('click', e => {
        e.preventDefault();

        items.forEach(i => i.classList.toggle('active', i === item && !item.classList.contains('active')));
        });
    });
    });
