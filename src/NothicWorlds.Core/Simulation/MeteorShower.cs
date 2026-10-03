namespace NothicWorlds.Core.Simulation;

/// <summary>
/// One meteor shower (VISION.md EVT-02): a planet passing through the dust a comet left along
/// its orbit. Times are in standard days on the world's clock.
/// </summary>
/// <param name="CometId">The comet whose dust makes the shower.</param>
/// <param name="StartDays">When the first meteors appear.</param>
/// <param name="PeakDays">When the planet passes closest to the comet's orbit.</param>
/// <param name="EndDays">When the last meteors appear.</param>
/// <param name="PeakPerHour">About how many meteors an hour fall at the peak.</param>
public sealed record MeteorShower(
    Guid CometId, double StartDays, double PeakDays, double EndDays, double PeakPerHour)
{
    /// <summary>
    /// About how many meteors an hour fall at <paramref name="timeDays"/>: rising steadily from
    /// the start to the peak, then falling to the end; 0 outside the shower.
    /// </summary>
    public double PerHourAt(double timeDays)
    {
        if (timeDays <= StartDays || timeDays >= EndDays)
        {
            return 0;
        }

        double share = timeDays <= PeakDays
            ? (timeDays - StartDays) / (PeakDays - StartDays)
            : (EndDays - timeDays) / (EndDays - PeakDays);
        return PeakPerHour * share;
    }

    /// <summary>Whether the shower is under way at <paramref name="timeDays"/>.</summary>
    public bool IsActiveAt(double timeDays) => timeDays > StartDays && timeDays < EndDays;
}
