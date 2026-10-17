namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// D-156: Vitality earned by walks on one local day, derived from the processed-record rows
    /// (<c>ProcessedRecordLedgerState.VitalityOnLocalDay</c>); nothing is persisted for it.
    /// <see cref="IsKnown"/> is false when the day cannot be determined (offset out of range) or may
    /// include retired rows; the shell then shows nothing.
    /// </summary>
    public sealed class DailyEarningsReadModel
    {
        public static readonly DailyEarningsReadModel Unknown = new DailyEarningsReadModel(null, 0L, false);

        public DailyEarningsReadModel(int? localDay, long vitality, bool isKnown)
        {
            LocalDay = localDay;
            Vitality = vitality;
            IsKnown = isKnown;
        }

        /// <summary>Local day number (<c>LocalDayCalendar.DayOf</c>), or null when unknown.</summary>
        public int? LocalDay { get; }

        /// <summary>Net applied Vitality from walks dated that day; 0 when unknown. Never negative.</summary>
        public long Vitality { get; }

        public bool IsKnown { get; }
    }
}
