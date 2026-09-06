// Reviewed copy corrections for terms that are easy to translate too literally
// or that should avoid absolute clinical promises. Keeping this small and explicit
// lets the shared i18n engine apply the same safer wording to static and dynamic UI.
// Every key here must already exist in the corresponding JSON dictionary; CI tests
// enforce that rule so this cannot become a second independent translation catalogue.

export const TRANSLATION_QUALITY_OVERRIDES = Object.freeze({
    ru: Object.freeze({
        about_p2: 'Наша миссия — восстанавливать здоровье и красоту улыбки каждого пациента. Мы специализируемся на минимально инвазивных методах лечения, используем современное оборудование и проверенные протоколы. Наша команда опытных специалистов стремится сделать приём максимально комфортным и снизить возможный дискомфорт, независимо от сложности клинического случая.',
        doc1_name: 'д-р Раис Наджиб',
        doc2_name: 'д-р Лилит Рейнер',
        doc1_approach_text: 'с вниманием к комфорту пациента, без лишних процедур, с акцентом на естественную улыбку и понятный план лечения.'
    }),
    en: Object.freeze({
        nav_doctors: 'Dentists',
        doctors_h1: 'Our Dentists',
        doctors_title: 'Our Dentists - Dental Clinic',
        about_p2: "Our mission is to restore the health and beauty of every patient's smile. We specialize in minimally invasive treatment methods, using modern equipment and proven protocols. Our experienced team focuses on making each visit as comfortable as possible and minimizing discomfort, regardless of the complexity of the clinical case.",
        doc1_role: 'Founder · Director · First-category Dentist',
        doc2_role: 'Implant Surgeon · Aesthetic Dentist · Highest-category Dentist',
        doc1_approach_text: 'focused on patient comfort, avoiding unnecessary procedures, and creating a natural smile with a clear treatment plan.'
    }),
    fr: Object.freeze({
        nav_doctors: 'Dentistes',
        doctors_h1: 'Nos dentistes',
        doctors_title: 'Nos dentistes - Dental Clinic',
        svc_bridges: 'Ponts dentaires',
        about_p2: "Notre mission est de restaurer la santé et la beauté du sourire de chaque patient. Nous privilégions des méthodes de traitement mini-invasives, avec des équipements modernes et des protocoles éprouvés. Notre équipe veille à rendre chaque visite aussi confortable que possible et à réduire au minimum l'inconfort, quelle que soit la complexité du cas clinique.",
        doc1_approach_text: 'axée sur le confort du patient, sans procédures inutiles, avec un sourire naturel et un plan de traitement clair.',
        br_title: 'Ponts dentaires - Dental Clinic',
        br_hero_title: 'Ponts dentaires'
    }),
    el: Object.freeze({
        nav_doctors: 'Οδοντίατροι',
        doctors_h1: 'Οι Οδοντίατροί μας',
        doctors_title: 'Οι Οδοντίατροί μας - Dental Clinic',
        about_p2: 'Η αποστολή μας είναι να αποκαθιστούμε την υγεία και την ομορφιά του χαμόγελου κάθε ασθενούς. Ειδικευόμαστε σε ελάχιστα επεμβατικές μεθόδους θεραπείας, χρησιμοποιώντας σύγχρονο εξοπλισμό και δοκιμασμένα πρωτόκολλα. Η ομάδα μας δίνει έμφαση σε μια όσο το δυνατόν πιο άνετη επίσκεψη και στη μείωση της ενόχλησης, ανεξάρτητα από την πολυπλοκότητα της κλινικής περίπτωσης.',
        doc1_approach_text: 'με έμφαση στην άνεση του ασθενούς, χωρίς περιττές διαδικασίες, σε ένα φυσικό χαμόγελο και σε σαφές πλάνο θεραπείας.'
    }),
    ar: Object.freeze({
        nav_doctors: 'أطباء الأسنان',
        svc_implants: 'زراعة الأسنان',
        doctors_h1: 'أطباء الأسنان لدينا',
        doctors_title: 'أطباء الأسنان - Dental Clinic',
        about_p2: 'مهمتنا هي استعادة صحة وجمال ابتسامة كل مريض. نتخصص في طرق العلاج طفيفة التوغل باستخدام معدات حديثة وبروتوكولات مثبتة. يركز فريقنا على جعل الزيارة مريحة قدر الإمكان وتقليل الانزعاج، بغض النظر عن تعقيد الحالة السريرية.',
        about_adv_4: 'التهدئة للمرضى الذين يعانون من رهاب طب الأسنان',
        doc1_approach_text: 'مع التركيز على راحة المريض وتجنب الإجراءات غير الضرورية، للوصول إلى ابتسامة طبيعية وخطة علاج واضحة.',
        doc2_implants: 'زراعة الأسنان'
    })
});

export function applyTranslationQualityOverrides(code, dictionary) {
    if (!dictionary || typeof dictionary !== 'object') return dictionary;
    const overrides = TRANSLATION_QUALITY_OVERRIDES[code];
    return overrides ? { ...dictionary, ...overrides } : dictionary;
}
