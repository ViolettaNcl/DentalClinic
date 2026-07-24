// /assets/js/core/tabManager.js

export class TabManager {
    constructor({
        navSelector,
        sectionSelector,
        defaultSection,
        storageKey = null,
        titleSelector = null,
        titlesMap = {}
    }) {
        this.navButtons = document.querySelectorAll(navSelector);
        this.sections = document.querySelectorAll(sectionSelector);
        this.pageTitle = titleSelector ? document.querySelector(titleSelector) : null;
        this.defaultSection = defaultSection;
        this.storageKey = storageKey;
        this.titlesMap = titlesMap;
    }

    init() {
        // Навешиваем обработчики на кнопки навигации
        this.navButtons.forEach(b =>
            b.addEventListener('click', () => this.show(b.dataset.section))
        );

        // Восстанавливаем последнюю активную секцию
        const saved = this.storageKey ? localStorage.getItem(this.storageKey) : null;
        const initial = saved || this.navButtons[0]?.dataset.section || this.defaultSection;
        this.show(initial);
    }

    show(target) {
        // Переключаем активный класс на кнопках навигации
        this.navButtons.forEach(b =>
            b.classList.toggle('active', b.dataset.section === target)
        );

        // Показываем нужную секцию, скрываем остальные
        this.sections.forEach(s =>
            s.classList.toggle('hidden', s.id !== `section-${target}`)
        );

        // Обновляем заголовок страницы, если задан
        if (this.pageTitle && this.titlesMap[target]) {
            this.pageTitle.textContent = this.titlesMap[target];
        }

        // Сохраняем выбор в localStorage, если задан ключ
        if (this.storageKey) {
            localStorage.setItem(this.storageKey, target);
        }
    }
}