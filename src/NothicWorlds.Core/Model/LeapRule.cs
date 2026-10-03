namespace NothicWorlds.Core.Model;

/// <summary>
/// When a calendar has leap years, and what they add (VISION.md CAL-04; owner's choice: "every
/// N years, except…" in up to three tiers, adding days to a chosen month). Our own calendar is
/// every 4 years, except every 100, except every 400, adding 1 day to February. Years are
/// counted by their number in the calendar (year 0 and negative years too). Immutable.
/// </summary>
/// <param name="Every">A year whose number divides by this is a leap year (at least 1).</param>
/// <param name="Except">
/// ...unless its number also divides by this (a multiple of <paramref name="Every"/>), or null.
/// </param>
/// <param name="ExceptAgain">
/// ...unless its number also divides by this (a multiple of <paramref name="Except"/>), or null.
/// Only with <paramref name="Except"/>.
/// </param>
/// <param name="Month">The month that gets the extra days (0 is the first month).</param>
/// <param name="Days">How many days a leap year adds (at least 1).</param>
public sealed record LeapRule(int Every, int? Except, int? ExceptAgain, int Month, int Days)
{
    /// <summary>The most days a leap year can add.</summary>
    public const int MaxDays = 1000;

    /// <summary>The longest any tier can be, in years.</summary>
    public const int MaxYears = 1_000_000;

    // Drift slower than a day in this many years needs no further tier.
    private const double GoodEnoughYears = 2000;

    /// <summary>True if the year with this number is a leap year.</summary>
    public bool IsLeap(long year)
    {
        if (Modulo(year, Every) != 0)
        {
            return false;
        }

        if (Except is not int except || Modulo(year, except) != 0)
        {
            return true;
        }

        return ExceptAgain is int again && Modulo(year, again) == 0;
    }

    /// <summary>The average number of extra days a year, over a whole cycle.</summary>
    public double AverageExtraDays =>
        Days * (1.0 / Every - (Except is int except ? 1.0 / except : 0)
            + (ExceptAgain is int again ? 1.0 / again : 0));

    /// <summary>
    /// How many years the pattern takes to repeat (the longest tier), so whole cycles of years
    /// can be skipped at once when counting days.
    /// </summary>
    public int CycleYears => ExceptAgain ?? Except ?? Every;

    /// <summary>
    /// What's wrong with the rule for a calendar of <paramref name="months"/> months, or null if
    /// it's usable.
    /// </summary>
    public string? Problem(int months)
    {
        if (Every is < 1 or > MaxYears || Except is < 1 or > MaxYears
            || ExceptAgain is < 1 or > MaxYears)
        {
            return $"leap year spans must be 1 to {MaxYears:N0} years";
        }

        if (Except is int except && (except <= Every || except % Every != 0))
        {
            return "the leap year exception must be a larger multiple of the leap year span";
        }

        if (ExceptAgain is int again
            && (Except is not int outer || again <= outer || again % outer != 0))
        {
            return "the second exception must be a larger multiple of the first";
        }

        if (Month < 0 || Month >= months)
        {
            return "the leap day's month isn't in the calendar";
        }

        return Days is < 1 or > MaxDays ? $"a leap year adds 1 to {MaxDays:N0} days" : null;
    }

    /// <summary>
    /// The simplest rule (fewest tiers, adding <paramref name="days"/> to
    /// <paramref name="month"/>) that keeps a calendar close to a year that's
    /// <paramref name="extraDays"/> longer than its months add up to, or null if adding days
    /// can't (the year is shorter than the months, or longer by more than
    /// <paramref name="days"/>). A tier is added only while it keeps the calendar right much
    /// longer: one day off in at least 2,000 years is close enough.
    /// </summary>
    public static LeapRule? Suggest(double extraDays, int month, int days = 1)
    {
        double rate = extraDays / days;  // Leap years needed per year
        if (!double.IsFinite(rate) || rate <= 1e-6 || rate > 1)
        {
            return null;
        }

        LeapRule? best = null;
        foreach (LeapRule candidate in Candidates(rate, month, days))
        {
            double error = Math.Abs(candidate.AverageExtraDays - extraDays);
            if (best is null || error < Math.Abs(best.AverageExtraDays - extraDays) * 0.5)
            {
                best = candidate;
            }

            if (error < 1.0 / GoodEnoughYears)
            {
                break;
            }
        }

        return best;
    }

    /// <summary>
    /// How many years the calendar takes to drift a whole day from a year of
    /// <paramref name="extraDays"/> extra days, or null if it never does (a perfect match).
    /// </summary>
    public double? YearsPerDayOfDrift(double extraDays)
    {
        double error = Math.Abs(AverageExtraDays - extraDays);
        return error < 1e-12 ? null : 1 / error;
    }

    // Rules with one, then two, then three tiers, each the best of its kind for the rate.
    private static IEnumerable<LeapRule> Candidates(double rate, int month, int days)
    {
        int every = Math.Clamp((int)Math.Round(1 / rate), 1, MaxYears);
        yield return new LeapRule(every, null, null, month, days);

        // Too many leap years at `every`: drop one every `except` years to make up the rest.
        double surplus = 1.0 / every - rate;
        if (surplus <= 0)
        {
            yield break;
        }

        int except = RoundToMultiple(1 / surplus, every);
        if (except <= every || except > MaxYears)
        {
            yield break;
        }

        yield return new LeapRule(every, except, null, month, days);

        double shortfall = rate - (1.0 / every - 1.0 / except);
        if (shortfall <= 0)
        {
            yield break;
        }

        int again = RoundToMultiple(1 / shortfall, except);
        if (again > except && again <= MaxYears)
        {
            yield return new LeapRule(every, except, again, month, days);
        }
    }

    private static int RoundToMultiple(double value, int step)
    {
        double rounded = Math.Round(value / step) * step;
        return rounded > MaxYears ? MaxYears + 1 : (int)Math.Max(rounded, step);
    }

    private static long Modulo(long value, long divisor)
    {
        long remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}
