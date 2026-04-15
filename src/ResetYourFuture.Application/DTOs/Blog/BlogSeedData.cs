namespace ResetYourFuture.Shared.DTOs;

/// <summary>
/// Static seed content for blog articles.
/// Each article includes both English (En) and Greek (El) variants.
/// </summary>
public static class BlogSeedData
{
    public static IReadOnlyList<SaveBlogArticleRequest> SeedArticles { get; } =
    [
        new SaveBlogArticleRequest(
            TitleEn: "5 Strategies to Relaunch Your Career After a Break",
            TitleEl: "5 Στρατηγικές για να Επανεκκινήσεις την Καριέρα σου Μετά από Διάλειμμα",
            Slug: "5-strategies-relaunch-career-after-break",
            SummaryEn: "Returning to work after a gap can feel overwhelming, but with the right mindset and concrete steps you can rebuild momentum fast.",
            SummaryEl: "Η επιστροφή στην εργασία μετά από ένα κενό μπορεί να φαίνεται τρομακτική, αλλά με τη σωστή νοοτροπία και συγκεκριμένα βήματα μπορείς να ανακτήσεις τον ρυθμό σου γρήγορα.",
            ContentEn: """
                <h2>1. Audit Your Transferable Skills</h2>
                <p>Time away from work doesn't erase your expertise. Start by listing everything you did before and during the break — caregiving, freelance projects, volunteering — and map each to a market-ready skill.</p>

                <h2>2. Refresh Your Digital Presence</h2>
                <p>Update your LinkedIn headline to reflect where you're heading, not just where you've been. Recruiters search by skill keywords, so make them count.</p>

                <h2>3. Target Returnship Programmes</h2>
                <p>Many large companies run structured re-entry programmes with mentorship built in. These roles are designed for people exactly in your situation.</p>

                <h2>4. Build a "Micro-Portfolio" Fast</h2>
                <p>Even two or three recent projects — a redesigned process doc, a small consulting engagement, an online course certificate — signal that you stayed sharp.</p>

                <h2>5. Network With Intent</h2>
                <p>One warm introduction beats fifty cold applications. Reconnect with former colleagues on LinkedIn, attend industry meetups, and ask for 15-minute coffee chats — not job referrals.</p>
                """,
            ContentEl: """
                <h2>1. Καταγραφή Μεταβιβάσιμων Δεξιοτήτων</h2>
                <p>Ο χρόνος μακριά από την εργασία δεν σβήνει την εμπειρία σου. Ξεκίνα κάνοντας λίστα με ό,τι έκανες πριν και κατά τη διάρκεια του διαλείμματος — φροντίδα οικογένειας, freelance έργα, εθελοντισμός — και αντιστοίχισε κάθε δράση με μία δεξιότητα που αναγνωρίζει η αγορά.</p>

                <h2>2. Ανανέωση Ψηφιακής Παρουσίας</h2>
                <p>Ενημέρωσε τον τίτλο σου στο LinkedIn ώστε να αντικατοπτρίζει τον προορισμό σου, όχι μόνο το παρελθόν σου. Οι recruiters ψάχνουν με λέξεις-κλειδιά δεξιοτήτων — κάνε τες να μετράνε.</p>

                <h2>3. Στόχευσε Προγράμματα Επιστροφής</h2>
                <p>Πολλές μεγάλες εταιρείες έχουν δομημένα προγράμματα επανένταξης με mentoring. Αυτοί οι ρόλοι είναι σχεδιασμένοι ακριβώς για ανθρώπους στη δική σου θέση.</p>

                <h2>4. Φτιάξε Γρήγορα ένα "Micro-Portfolio"</h2>
                <p>Ακόμα και δύο-τρία πρόσφατα έργα αρκούν για να δείξεις ότι παρέμεινες ενεργός. Ένα ανανεωμένο επαγγελματικό κείμενο, μια μικρή συμβουλευτική δουλειά ή ένα πιστοποιητικό online μαθήματος μπορούν να κάνουν τη διαφορά.</p>

                <h2>5. Networking με Σκοπό</h2>
                <p>Μία ζεστή σύσταση αξίζει περισσότερο από πενήντα κρύες αιτήσεις. Επανασύνδεσε με πρώην συναδέλφους, πήγαινε σε επαγγελματικές εκδηλώσεις και ζήτα 15λεπτες συνομιλίες — όχι άμεσες προτάσεις εργασίας.</p>
                """,
            CoverImageUrl: null,
            AuthorName: "Reset Your Future Team",
            Tags: ["Career", "Job Search", "Mindset"],
            IsPublished: true
        ),

        new SaveBlogArticleRequest(
            TitleEn: "The Hidden Job Market: How to Find Roles Before They're Posted",
            TitleEl: "Η Κρυφή Αγορά Εργασίας: Πώς να Βρεις Θέσεις Πριν Δημοσιευτούν",
            Slug: "hidden-job-market-find-roles-before-posted",
            SummaryEn: "Up to 70% of jobs are never advertised publicly. Learn how to tap into the hidden job market and get in front of hiring managers before the competition even starts.",
            SummaryEl: "Έως και το 70% των θέσεων εργασίας δεν διαφημίζεται ποτέ δημόσια. Μάθε πώς να αξιοποιήσεις την κρυφή αγορά και να βρεθείς μπροστά στους υπεύθυνους πρόσληψης πριν ξεκινήσει καν ο ανταγωνισμός.",
            ContentEn: """
                <h2>Why Most Jobs Never Get Posted</h2>
                <p>Companies prefer referrals and internal moves because they reduce hiring risk and cost. By the time a role appears on a job board, dozens of candidates are already in the pipeline.</p>

                <h2>Identify Your Target Companies</h2>
                <p>Make a list of 20–30 companies you'd love to work for. Follow them on LinkedIn, set Google Alerts for their news, and study their org chart to find the hiring manager — not just HR.</p>

                <h2>The Value-First Outreach Method</h2>
                <p>Instead of "I'm looking for a job", lead with something useful: a short industry insight, a congratulation on a company win, or a thoughtful comment on their content. Build rapport first.</p>

                <h2>Leverage Alumni Networks</h2>
                <p>University and former-employer alumni groups are dramatically underused. A shared background is the strongest conversation opener you have.</p>

                <h2>Track Everything</h2>
                <p>Use a simple spreadsheet to track your outreach — company, contact, date, last touchpoint, next action. Consistency over time wins.</p>
                """,
            ContentEl: """
                <h2>Γιατί οι Περισσότερες Θέσεις Δεν Δημοσιεύονται</h2>
                <p>Οι εταιρείες προτιμούν συστάσεις και εσωτερικές μετακινήσεις γιατί μειώνουν τον κίνδυνο και το κόστος πρόσληψης. Μέχρι να εμφανιστεί μια θέση σε job board, δεκάδες υποψήφιοι βρίσκονται ήδη στη διαδικασία.</p>

                <h2>Εντόπισε τις Εταιρείες-Στόχους σου</h2>
                <p>Φτιάξε μια λίστα με 20–30 εταιρείες όπου θα ήθελες να δουλεύεις. Ακολούθησέ τες στο LinkedIn, βάλε Google Alerts για τα νέα τους και μελέτησε το οργανόγραμμά τους για να βρεις τον υπεύθυνο — όχι απλώς το τμήμα HR.</p>

                <h2>Η Μέθοδος "Αξία Πρώτα"</h2>
                <p>Αντί για "ψάχνω για δουλειά", ξεκίνα με κάτι χρήσιμο: μια παρατήρηση για τον κλάδο, ένα συγχαρητήριο για μια επιτυχία της εταιρείας ή ένα σχόλιο στο περιεχόμενό τους. Χτίσε σχέση πρώτα.</p>

                <h2>Αξιοποίησε τα Δίκτυα Αποφοίτων</h2>
                <p>Τα δίκτυα αποφοίτων πανεπιστημίων και πρώην εργοδοτών χρησιμοποιούνται λιγότερο απ' ό,τι θα έπρεπε. Η κοινή ιστορία είναι η πιο ισχυρή αφορμή για συνομιλία που διαθέτεις.</p>

                <h2>Κατέγραφε Τα Πάντα</h2>
                <p>Χρησιμοποίησε ένα απλό spreadsheet: εταιρεία, επαφή, ημερομηνία, τελευταία επικοινωνία, επόμενο βήμα. Η συνέπεια στον χρόνο κερδίζει.</p>
                """,
            CoverImageUrl: null,
            AuthorName: "Reset Your Future Team",
            Tags: ["Job Search", "Networking", "Career Strategy"],
            IsPublished: true
        ),

        new SaveBlogArticleRequest(
            TitleEn: "How to Build Interview Confidence",
            TitleEl: "Πώς να Χτίσεις Αυτοπεποίθηση στη Συνέντευξη Εργασίας",
            Slug: "how-to-build-interview-confidence",
            SummaryEn: "Interview confidence is not a personality trait — it's a skill you can develop. Here's how.",
            SummaryEl: "Η αυτοπεποίθηση στη συνέντευξη δεν είναι χαρακτηριστικό της προσωπικότητάς σου — είναι δεξιότητα που μπορείς να καλλιεργήσεις. Δες πώς.",
            ContentEn: """
                <h2>Why We Feel Anxious in Interviews</h2>
                <p>Anxiety comes from the sense of being judged. Once you realise that the interview is a mutual evaluation — you are assessing the employer equally — the tone shifts entirely.</p>

                <h2>The STAR Technique</h2>
                <p>Situation, Task, Action, Result. Every behavioural question ("Tell me about a time when...") is answered with this structure. Prepare 6–8 stories from your experience and reuse them.</p>

                <h2>Practice With Structure</h2>
                <p>Record yourself answering common questions. The first listen is uncomfortable — but it reveals tics you don't know you have.</p>

                <h2>The Power of the Pause</h2>
                <p>Don't rush to fill every silence. A 3-second pause before answering makes you look thoughtful, not weak.</p>
                """,
            ContentEl: """
                <h2>Γιατί Νιώθουμε Άγχος στις Συνεντεύξεις</h2>
                <p>Το άγχος προέρχεται από την αίσθηση ότι κρινόμαστε. Όταν αντιληφθείς ότι η συνέντευξη είναι αμοιβαία αξιολόγηση — εσύ αξιολογείς τον εργοδότη εξίσου — ο τόνος αλλάζει εντελώς.</p>

                <h2>Η Τεχνική STAR</h2>
                <p>Situation, Task, Action, Result. Κάθε συμπεριφορική ερώτηση ("Πες μου για μια φορά που...") αντιμετωπίζεται με αυτή τη δομή. Προετοίμασε 6–8 ιστορίες από την εμπειρία σου και ξαναχρησιμοποίησέ τες.</p>

                <h2>Εξάσκηση με Δομή</h2>
                <p>Ηχογράφησε τον εαυτό σου να απαντά σε κοινές ερωτήσεις. Η πρώτη ακρόαση είναι δύσκολη — αλλά αποκαλύπτει tic που δεν γνωρίζεις ότι έχεις.</p>

                <h2>Η Δύναμη της Παύσης</h2>
                <p>Μην βιάζεσαι να γεμίσεις κάθε σιωπή. Μια παύση 3 δευτερολέπτων πριν την απάντηση σε κάνει να φαίνεσαι σκεπτόμενος, όχι αδύναμος.</p>
                """,
            CoverImageUrl: null,
            AuthorName: "Reset Your Future Team",
            Tags: ["Interview", "Mindset", "Career"],
            IsPublished: true
        ),

        new SaveBlogArticleRequest(
            TitleEn: "From Employee to Freelancer: What Nobody Tells You",
            TitleEl: "Από Εργαζόμενος σε Freelancer: Αυτά που Κανείς Δεν σου Λέει",
            Slug: "employee-to-freelancer-what-nobody-tells-you",
            SummaryEn: "Making the leap to freelancing is exciting — and full of surprises. Here's an honest look at the financial, psychological, and practical realities of going solo.",
            SummaryEl: "Το άλμα στο freelancing είναι συναρπαστικό — και γεμάτο εκπλήξεις. Μια ειλικρινής ματιά στις οικονομικές, ψυχολογικές και πρακτικές πραγματικότητες του να δουλεύεις μόνος σου.",
            ContentEn: """
                <h2>The First 90 Days Are the Hardest</h2>
                <p>Pipeline takes time to build. Most successful freelancers say their first quarter was the most stressful — and also the most educational. Plan your runway accordingly: 3–6 months of living expenses in reserve is not a luxury, it's a requirement.</p>

                <h2>You Are Now in Sales</h2>
                <p>No one is going to sell your services for you. Block time every week — even when you're busy — for outreach, proposals, and follow-up. The freelancers who stop marketing when they're busy are the ones who face feast-and-famine cycles.</p>

                <h2>Pricing: Charge More Than You Think You Should</h2>
                <p>Your rate needs to cover taxes, insurance, unpaid admin time, gaps between projects, and professional development. Research market rates, then add 20%.</p>

                <h2>Protect Your Time Ruthlessly</h2>
                <p>Without a boss setting structure, you have to do it yourself. Fixed working hours, a dedicated workspace, and a hard stop time prevent the job from consuming your entire life.</p>
                """,
            ContentEl: """
                <h2>Οι Πρώτες 90 Μέρες Είναι οι Πιο Δύσκολες</h2>
                <p>Η δημιουργία πελατολογίου χρειάζεται χρόνο. Οι περισσότεροι επιτυχημένοι freelancers λένε ότι το πρώτο τρίμηνο ήταν το πιο αγχωτικό — και το πιο διδακτικό. Σχεδίασε ανάλογα: 3–6 μήνες εφεδρικών εξόδων δεν είναι πολυτέλεια, είναι απαραίτητα.</p>

                <h2>Είσαι Πλέον στις Πωλήσεις</h2>
                <p>Κανείς δεν θα πουλήσει τις υπηρεσίες σου στη θέση σου. Κράτα χρόνο κάθε εβδομάδα — ακόμα κι όταν είσαι απασχολημένος — για επικοινωνία, προτάσεις και follow-up. Οι freelancers που σταματούν το marketing όταν είναι busy είναι αυτοί που βιώνουν κύκλους υπερφόρτωσης και αδράνειας.</p>

                <h2>Τιμολόγηση: Χρέωσε Περισσότερο απ' Ό,τι Νομίζεις</h2>
                <p>Το ωρομίσθιό σου πρέπει να καλύπτει φόρους, ασφάλιση, μη χρεώσιμες ώρες διαχείρισης, κενά μεταξύ έργων και επαγγελματική ανάπτυξη. Έρευνα τιμές αγοράς, μετά πρόσθεσε 20%.</p>

                <h2>Προστάτεψε τον Χρόνο σου Ανελέητα</h2>
                <p>Χωρίς αφεντικό που ορίζει τη δομή, πρέπει να το κάνεις μόνος σου. Σταθερές ώρες εργασίας, αποκλειστικός χώρος και αυστηρή ώρα λήξης εμποδίζουν τη δουλειά να καταλάβει ολόκληρη τη ζωή σου.</p>
                """,
            CoverImageUrl: null,
            AuthorName: "Reset Your Future Team",
            Tags: ["Freelancing", "Career Change", "Entrepreneurship"],
            IsPublished: true
        )
    ];
}
