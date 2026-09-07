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
        doc1_approach_text: 'с вниманием к комфорту пациента, без лишних процедур, с акцентом на естественную улыбку и понятный план лечения.',
        patient_confirmed_call_required: 'Подтверждённую запись можно изменить только через администратора клиники. Актуальные контакты доступны на странице «Контакты».',
        chat_booking_success: '✅ Заявка успешно отправлена!\n\nАдминистратор клиники свяжется с вами для подтверждения записи.',
        chat_booking_error: '⚠️ Не удалось отправить заявку. Попробуйте ещё раз позже или откройте страницу контактов клиники.'
    }),
    en: Object.freeze({
        nav_doctors: 'Dentists',
        doctors_h1: 'Our Dentists',
        doctors_title: 'Our Dentists - Dental Clinic',
        about_p2: "Our mission is to restore the health and beauty of every patient's smile. We specialize in minimally invasive treatment methods, using modern equipment and proven protocols. Our experienced team focuses on making each visit as comfortable as possible and minimizing discomfort, regardless of the complexity of the clinical case.",
        doc1_role: 'Founder · Director · First-category Dentist',
        doc2_role: 'Implant Surgeon · Aesthetic Dentist · Highest-category Dentist',
        doc1_approach_text: 'focused on patient comfort, avoiding unnecessary procedures, and creating a natural smile with a clear treatment plan.',
        patient_confirmed_call_required: 'A confirmed appointment can only be changed through clinic staff. Current contact details are available on the Contacts page.',
        chat_booking_success: '✅ Your request has been sent!\n\nClinic staff will contact you to confirm the appointment.',
        chat_booking_error: "⚠️ Couldn't send the request. Please try again later or open the clinic Contacts page."
    }),
    fr: Object.freeze({
        nav_doctors: 'Dentistes',
        doctors_h1: 'Nos dentistes',
        doctors_title: 'Nos dentistes - Dental Clinic',
        svc_bridges: 'Ponts dentaires',
        about_p2: "Notre mission est de restaurer la santé et la beauté du sourire de chaque patient. Nous privilégions des méthodes de traitement mini-invasives, avec des équipements modernes et des protocoles éprouvés. Notre équipe veille à rendre chaque visite aussi confortable que possible et à réduire au minimum l'inconfort, quelle que soit la complexité du cas clinique.",
        doc1_approach_text: 'axée sur le confort du patient, sans procédures inutiles, avec un sourire naturel et un plan de traitement clair.',
        br_title: 'Ponts dentaires - Dental Clinic',
        br_hero_title: 'Ponts dentaires',
        br_intro_title: "Qu'est-ce qu'un pont dentaire ?",
        br_intro_text: "Un pont dentaire est une prothèse fixe destinée à remplacer une ou plusieurs dents manquantes. Il s'appuie sur les dents piliers voisines et restaure la fonction et l'esthétique.",
        br_types_title: 'Types de ponts dentaires',
        br_type1_desc: 'Pont dentaire classique reposant sur deux dents piliers. Fiable et largement utilisé.',
        br_p2_name: 'Pont en céramique',
        imp_gal2_title: 'Pont sur implants',
        patient_confirmed_call_required: "Un rendez-vous confirmé ne peut être modifié que par l'équipe de la clinique. Les coordonnées à jour sont disponibles sur la page Contacts.",
        chat_booking_success: "✅ Votre demande a bien été envoyée !\n\nL'équipe de la clinique vous contactera pour confirmer le rendez-vous.",
        chat_booking_error: "⚠️ Impossible d'envoyer la demande. Réessayez plus tard ou ouvrez la page Contacts de la clinique."
    }),
    el: Object.freeze({
        nav_doctors: 'Οδοντίατροι',
        doctors_h1: 'Οι Οδοντίατροί μας',
        doctors_title: 'Οι Οδοντίατροί μας - Dental Clinic',
        about_p2: 'Η αποστολή μας είναι να αποκαθιστούμε την υγεία και την ομορφιά του χαμόγελου κάθε ασθενούς. Ειδικευόμαστε σε ελάχιστα επεμβατικές μεθόδους θεραπείας, χρησιμοποιώντας σύγχρονο εξοπλισμό και δοκιμασμένα πρωτόκολλα. Η ομάδα μας δίνει έμφαση σε μια όσο το δυνατόν πιο άνετη επίσκεψη και στη μείωση της ενόχλησης, ανεξάρτητα από την πολυπλοκότητα της κλινικής περίπτωσης.',
        doc1_approach_text: 'με έμφαση στην άνεση του ασθενούς, χωρίς περιττές διαδικασίες, σε ένα φυσικό χαμόγελο και σε σαφές πλάνο θεραπείας.',
        patient_confirmed_call_required: 'Ένα επιβεβαιωμένο ραντεβού μπορεί να αλλάξει μόνο μέσω του προσωπικού της κλινικής. Τα ενημερωμένα στοιχεία επικοινωνίας βρίσκονται στη σελίδα Επικοινωνία.',
        chat_booking_success: '✅ Το αίτημά σας στάλθηκε με επιτυχία!\n\nΤο προσωπικό της κλινικής θα επικοινωνήσει μαζί σας για να επιβεβαιώσει το ραντεβού.',
        chat_booking_error: '⚠️ Δεν ήταν δυνατή η αποστολή του αιτήματος. Δοκιμάστε ξανά αργότερα ή ανοίξτε τη σελίδα Επικοινωνία της κλινικής.'
    }),
    ar: Object.freeze({
        nav_doctors: 'أطباء الأسنان',
        svc_implants: 'زراعة الأسنان',
        doctors_h1: 'أطباء الأسنان لدينا',
        doctors_title: 'أطباء الأسنان - Dental Clinic',
        about_p2: 'مهمتنا هي استعادة صحة وجمال ابتسامة كل مريض. نتخصص في طرق العلاج طفيفة التوغل باستخدام معدات حديثة وبروتوكولات مثبتة. يركز فريقنا على جعل الزيارة مريحة قدر الإمكان وتقليل الانزعاج، بغض النظر عن تعقيد الحالة السريرية.',
        about_adv_4: 'التهدئة للمرضى الذين يعانون من رهاب طب الأسنان',
        doc1_approach_text: 'مع التركيز على راحة المريض وتجنب الإجراءات غير الضرورية، للوصول إلى ابتسامة طبيعية وخطة علاج واضحة.',
        doc2_implants: 'زراعة الأسنان',
        patient_confirmed_call_required: 'لا يمكن تغيير الموعد المؤكد إلا من خلال فريق العيادة. تتوفر بيانات التواصل المحدثة في صفحة الاتصال.',
        chat_booking_success: '✅ تم إرسال طلبك بنجاح!\n\nسيتواصل معك فريق العيادة لتأكيد الموعد.',
        chat_booking_error: '⚠️ تعذر إرسال الطلب. حاول مرة أخرى لاحقًا أو افتح صفحة الاتصال الخاصة بالعيادة.'
    })
});

export function applyTranslationQualityOverrides(code, dictionary) {
    if (!dictionary || typeof dictionary !== 'object') return dictionary;
    const overrides = TRANSLATION_QUALITY_OVERRIDES[code];
    return overrides ? { ...dictionary, ...overrides } : dictionary;
}
