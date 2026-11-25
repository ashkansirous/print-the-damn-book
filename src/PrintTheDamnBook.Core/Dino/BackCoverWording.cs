using System.Globalization;

namespace PrintTheDamnBook.Core.Dino;

/// <summary>The words one language uses on a back cover: the same labels, month names, digits and "and" as Dino Reader's strings files.</summary>
public sealed record BackCoverWords(
    string StoryBy,
    string OriginalStoryBy,
    string Traditional,
    string RetoldBy,
    string Illustration,
    string Date,
    string Months,
    string Digits,
    string And,
    string AndWord,
    string List);

/// <summary>The labels of the credit roles on the KDP credits page, without a name.</summary>
public sealed record CreditLabels(string StoryBy, string OriginalStoryBy, string RetoldBy, string Illustrations);

/// <summary>One group of the credits page: its label (null for "A traditional tale") and the name or words under it.</summary>
public sealed record CreditRole(string? Label, string Name);

/// <summary>
/// The credit lines of a back cover in a language (Slice 23), the same lines in the same order as Dino Reader's <c>BackCoverText.Lines</c>:
/// story by (or original story by, or a traditional tale), retold by, illustration, month and year. A language without wording uses English.
/// Names are printed as the book spells them in that language when it has a spelling, else as written.
/// </summary>
public static class BackCoverWording
{
    public const string English = "en";

    private const char MonthSeparator = '|';
    private const string AsciiDigits = "0123456789";
    private const int MonthCount = 12;
    private const char LineBreak = '\n';

    private static readonly BackCoverWords EnglishWords = new(
        "Story by {0}", "Original story by {0}", "A traditional tale", "Retold for young readers by {0}", "Illustration: {0}", "{0} {1}",
        "January|February|March|April|May|June|July|August|September|October|November|December", "0123456789", "{0} and {1}", "and", "{0}, {1}");

    private static readonly Dictionary<string, BackCoverWords> Table = new(StringComparer.Ordinal)
    {
        [English] = EnglishWords,
        ["ar"] = new("قصة: {0}", "القصة الأصلية: {0}", "حكاية تقليدية", "أعاد روايتها للقرّاء الصغار: {0}", "الرسوم: {0}", "{0} {1}",
            "يناير|فبراير|مارس|أبريل|مايو|يونيو|يوليو|أغسطس|سبتمبر|أكتوبر|نوفمبر|ديسمبر", "٠١٢٣٤٥٦٧٨٩", "{0} و{1}", "و", "{0}، {1}"),
        ["da"] = new("Historie af {0}", "Oprindelig historie af {0}", "Et traditionelt eventyr", "Genfortalt for små læsere af {0}", "Illustration: {0}", "{0} {1}",
            "januar|februar|marts|april|maj|juni|juli|august|september|oktober|november|december", "0123456789", "{0} og {1}", "og", "{0}, {1}"),
        ["de"] = new("Geschichte von {0}", "Originalgeschichte von {0}", "Ein traditionelles Märchen", "Für junge Leser nacherzählt von {0}", "Illustration: {0}", "{0} {1}",
            "Januar|Februar|März|April|Mai|Juni|Juli|August|September|Oktober|November|Dezember", "0123456789", "{0} und {1}", "und", "{0}, {1}"),
        ["es"] = new("Historia de {0}", "Historia original de {0}", "Un cuento tradicional", "Adaptado para jóvenes lectores por {0}", "Ilustración: {0}", "{0} de {1}",
            "enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre", "0123456789", "{0} y {1}", "y", "{0}, {1}"),
        ["fa"] = new("داستان از {0}", "داستان اصلی از {0}", "یک قصهٔ قدیمی", "بازنویسی برای کودکان توسط {0}", "تصویرگری: {0}", "{0} {1}",
            "ژانویه|فوریه|مارس|آوریل|مه|ژوئن|ژوئیه|اوت|سپتامبر|اکتبر|نوامبر|دسامبر", "۰۱۲۳۴۵۶۷۸۹", "{0} و {1}", "و", "{0}، {1}"),
        ["fi"] = new("Tarina: {0}", "Alkuperäinen tarina: {0}", "Perinteinen kertomus", "Kerrottu uudelleen nuorille lukijoille: {0}", "Kuvitus: {0}", "{0} {1}",
            "tammikuu|helmikuu|maaliskuu|huhtikuu|toukokuu|kesäkuu|heinäkuu|elokuu|syyskuu|lokakuu|marraskuu|joulukuu", "0123456789", "{0} ja {1}", "ja", "{0}, {1}"),
        ["fr"] = new("Histoire de {0}", "Histoire originale de {0}", "Un conte traditionnel", "Raconté pour les jeunes lecteurs par {0}", "Illustration : {0}", "{0} {1}",
            "janvier|février|mars|avril|mai|juin|juillet|août|septembre|octobre|novembre|décembre", "0123456789", "{0} et {1}", "et", "{0}, {1}"),
        ["ja"] = new("おはなし：{0}", "原作：{0}", "むかしからのおはなし", "こどもむけに書きなおし：{0}", "え：{0}", "{1}年{0}",
            "1月|2月|3月|4月|5月|6月|7月|8月|9月|10月|11月|12月", "0123456789", "{0}と{1}", "と", "{0}、{1}"),
        ["nb"] = new("Historie av {0}", "Opprinnelig historie av {0}", "Et tradisjonelt eventyr", "Gjenfortalt for unge lesere av {0}", "Illustrasjon: {0}", "{0} {1}",
            "januar|februar|mars|april|mai|juni|juli|august|september|oktober|november|desember", "0123456789", "{0} og {1}", "og", "{0}, {1}"),
        ["nl"] = new("Verhaal van {0}", "Oorspronkelijk verhaal van {0}", "Een traditioneel verhaal", "Naverteld voor jonge lezers door {0}", "Illustratie: {0}", "{0} {1}",
            "januari|februari|maart|april|mei|juni|juli|augustus|september|oktober|november|december", "0123456789", "{0} en {1}", "en", "{0}, {1}"),
        ["pt"] = new("História de {0}", "História original de {0}", "Um conto tradicional", "Recontado para jovens leitores por {0}", "Ilustração: {0}", "{0} de {1}",
            "janeiro|fevereiro|março|abril|maio|junho|julho|agosto|setembro|outubro|novembro|dezembro", "0123456789", "{0} e {1}", "e", "{0}, {1}"),
        ["ru"] = new("Автор истории: {0}", "Оригинальная история: {0}", "Народная сказка", "Пересказ для юных читателей: {0}", "Иллюстрации: {0}", "{0} {1}",
            "Январь|Февраль|Март|Апрель|Май|Июнь|Июль|Август|Сентябрь|Октябрь|Ноябрь|Декабрь", "0123456789", "{0} и {1}", "и", "{0}, {1}"),
        ["sv"] = new("Berättelse av {0}", "Originalberättelse av {0}", "En traditionell saga", "Återberättad för unga läsare av {0}", "Illustration: {0}", "{0} {1}",
            "januari|februari|mars|april|maj|juni|juli|augusti|september|oktober|november|december", "0123456789", "{0} och {1}", "och", "{0}, {1}"),
        ["zh"] = new("故事：{0}", "原作：{0}", "传统故事", "为小读者改写：{0}", "插图：{0}", "{1}年{0}",
            "1月|2月|3月|4月|5月|6月|7月|8月|9月|10月|11月|12月", "0123456789", "{0}和{1}", "和", "{0}、{1}"),
    };

    /// <summary>The role labels of the KDP credits page (Slice 46, decision 11), labels only: the names and the date come from the same credit wording as the lines above.</summary>
    private static readonly Dictionary<string, CreditLabels> LabelTable = new(StringComparer.Ordinal)
    {
        [English] = new("Story by", "Original story by", "Retold for young readers by", "Illustrations by"),
    };

    /// <summary>The credit role labels of <paramref name="language"/>; English when the language has none yet (only English has them, Slice 46 decision 10).</summary>
    public static CreditLabels LabelsFor(string? language)
    {
        return LabelTable.TryGetValue(DinoBook.Primary(language), out var labels) ? labels : LabelTable[English];
    }

    /// <summary>True when <paramref name="language"/> has its own credit labels.</summary>
    public static bool HasLabels(string? language) => LabelTable.ContainsKey(DinoBook.Primary(language));

    /// <summary>
    /// The credits as label and name pairs (the KDP credits page): story by (or original story by, or a traditional tale with no label), retold by, illustrations by.
    /// Names are spelled as in <see cref="Lines"/>; the date line is <see cref="DateText"/>.
    /// </summary>
    public static IReadOnlyList<CreditRole> Roles(DinoCredits credits, string? language)
    {
        var words = WordsFor(language);
        var code = DinoBook.Primary(language);
        var labels = LabelsFor(language);
        var roles = new List<CreditRole>();
        if (credits.IsTraditional)
        {
            roles.Add(new CreditRole(null, words.Traditional));
        }
        else
        {
            roles.Add(new CreditRole(credits.Retold ? labels.OriginalStoryBy : labels.StoryBy, JoinNames(credits, words, code)));
        }

        if (credits.Retold)
        {
            roles.Add(new CreditRole(labels.RetoldBy, credits.NameIn(DinoCredits.Maker, code)));
        }

        roles.Add(new CreditRole(labels.Illustrations, credits.NameIn(credits.Illustration, code)));
        return roles;
    }

    /// <summary>The month and year line of the credits, e.g. "October 2026".</summary>
    public static string DateText(DinoCredits credits, string? language) => DateLine(credits, WordsFor(language));

    /// <summary>The language codes that have their own wording (English included).</summary>
    public static IReadOnlyCollection<string> Languages => Table.Keys;

    /// <summary>The words of <paramref name="language"/> (a code such as <c>fa</c> or <c>fa-IR</c>); English when the language has none or its entry is broken.</summary>
    public static BackCoverWords WordsFor(string? language)
    {
        var code = DinoBook.Primary(language);
        code = code is "no" or "nn" ? "nb" : code;
        return Table.TryGetValue(code, out var words) && IsUsable(words) ? words : EnglishWords;
    }

    /// <summary>The credit lines in <paramref name="language"/>, one string per line.</summary>
    public static IReadOnlyList<string> Lines(DinoCredits credits, string? language)
    {
        var words = WordsFor(language);
        var code = DinoBook.Primary(language);
        var lines = new List<string>();
        if (credits.IsTraditional)
        {
            lines.Add(words.Traditional);
        }
        else
        {
            lines.Add(string.Format(CultureInfo.InvariantCulture, credits.Retold ? words.OriginalStoryBy : words.StoryBy, JoinNames(credits, words, code)));
        }

        if (credits.Retold)
        {
            lines.Add(string.Format(CultureInfo.InvariantCulture, words.RetoldBy, credits.NameIn(DinoCredits.Maker, code)));
        }

        lines.Add(string.Format(CultureInfo.InvariantCulture, words.Illustration, credits.NameIn(credits.Illustration, code)));
        lines.Add(DateLine(credits, words));
        return lines;
    }

    /// <summary>The credit lines in <paramref name="language"/> as the page's words: one line each, no empty lines.</summary>
    public static string Text(DinoCredits credits, string? language) => string.Join(LineBreak, Lines(credits, language));

    /// <summary>True when the entry has 12 month names, 10 digits and every format has the placeholders it needs; a broken one is never used.</summary>
    public static bool IsUsable(BackCoverWords words)
    {
        return words.Months.Split(MonthSeparator).Length == MonthCount
            && words.Digits.Length == AsciiDigits.Length
            && new[] { words.StoryBy, words.OriginalStoryBy, words.RetoldBy, words.Illustration }.All(f => f.Contains("{0}", StringComparison.Ordinal))
            && words.Date.Contains("{0}", StringComparison.Ordinal) && words.Date.Contains("{1}", StringComparison.Ordinal)
            && words.And.Contains("{0}", StringComparison.Ordinal) && words.And.Contains("{1}", StringComparison.Ordinal)
            && words.List.Contains("{0}", StringComparison.Ordinal) && words.List.Contains("{1}", StringComparison.Ordinal);
    }

    /// <summary>Several names joined with the language's "and"; when a name already holds that word (French "Jacob et Wilhelm Grimm") the list separator is used so it never reads as three people.</summary>
    private static string JoinNames(DinoCredits credits, BackCoverWords words, string code)
    {
        var spelled = credits.Names().Select(name => credits.NameIn(name, code)).ToList();
        if (spelled.Count == 0)
        {
            return credits.StoryBy;
        }

        var holdsAnd = spelled.Any(part => part.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(words.AndWord.Trim(), StringComparer.OrdinalIgnoreCase));
        var join = holdsAnd ? words.List : words.And;
        return spelled.Skip(1).Aggregate(spelled[0], (joined, next) => string.Format(CultureInfo.InvariantCulture, join, joined, next));
    }

    /// <summary>The month and year, e.g. "November 2026"; the month name and the digits are the language's own.</summary>
    private static string DateLine(DinoCredits credits, BackCoverWords words)
    {
        var month = words.Months.Split(MonthSeparator)[credits.Month - 1];
        var year = new string(credits.Year.ToString(CultureInfo.InvariantCulture).Select(c => words.Digits[AsciiDigits.IndexOf(c)]).ToArray());
        return string.Format(CultureInfo.InvariantCulture, words.Date, month, year);
    }
}
