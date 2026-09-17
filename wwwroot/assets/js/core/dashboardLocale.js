const supported = new Set(['ru','en','fr','el','ar']);
let lang = 'ru';
try { const saved = localStorage.getItem('site_lang'); if (supported.has(saved)) lang = saved; } catch {}
document.documentElement.lang = lang;
document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
