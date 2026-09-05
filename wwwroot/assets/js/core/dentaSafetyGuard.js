// Final presentation-layer safety guard for Denta responses.
// The backend already constrains Gemini output; this additionally normalizes
// legacy/proactive copy that does not pass through the model safety prompt.

const REPLACEMENTS = [
    [/абсолютно безболезненно/gi, 'проводится с обезболиванием; ощущения индивидуальны'],
    [/без боли и страха/gi, 'с обезболиванием и поддержкой команды; ощущения индивидуальны'],
    [/completely painless/gi, 'performed with anesthesia; individual sensations can vary'],
    [/without pain or fear/gi, 'with anesthesia and supportive care; individual sensations can vary'],
    [/totalement indolore/gi, 'réalisé avec anesthésie ; les sensations peuvent varier selon la personne'],
    [/sans douleur ni crainte/gi, 'avec anesthésie et accompagnement ; les sensations peuvent varier'],
    [/εντελώς ανώδυνο/gi, 'γίνεται με αναισθησία· η εμπειρία διαφέρει από άτομο σε άτομο'],
    [/χωρίς πόνο και φόβο/gi, 'με αναισθησία και υποστήριξη· η εμπειρία διαφέρει από άτομο σε άτομο'],
    [/غير مؤلم تمامًا/gi, 'يُجرى مع التخدير، وقد تختلف الأحاسيس من شخص لآخر'],
    [/دون ألم أو خوف/gi, 'مع التخدير والدعم، وقد تختلف الأحاسيس من شخص لآخر'],
];

export function sanitizeDentaText(value) {
    let text = typeof value === 'string' ? value : '';
    for (const [pattern, replacement] of REPLACEMENTS) {
        text = text.replace(pattern, replacement);
    }
    return text;
}

export function installDentaSafetyGuard(ChatBotClass) {
    if (!ChatBotClass?.prototype || ChatBotClass.prototype.__dentaSafetyInstalled) return;

    const originalAddBotMessage = ChatBotClass.prototype._addBotMessage;
    if (typeof originalAddBotMessage !== 'function') return;

    ChatBotClass.prototype._addBotMessage = function (text, suggestions = [], links = []) {
        return originalAddBotMessage.call(this, sanitizeDentaText(text), suggestions, links);
    };

    Object.defineProperty(ChatBotClass.prototype, '__dentaSafetyInstalled', {
        value: true,
        configurable: false,
        enumerable: false,
        writable: false,
    });
}
