namespace OptiLoad.Application.Algorithms
{
    /// <summary>
    /// כל הקבועים המשמשים את אלגוריתמי השיבוץ — מקום אחד מרכזי לשינוי ערכים.
    /// </summary>
    public static class AlgorithmConfig
    {
        // ─── B&B ────────────────────────────────────────────────────────────────
        /// <summary>מספר הצמתים המקסימלי לפני מעבר לחמדני</summary>
        public const int MaxGlobalNodes = 10_000_000;

        /// <summary>מגבלת זמן ברירת מחדל בשניות לריצת B&B</summary>
        public const double DefaultTimeLimitSeconds = 3600.0;

        /// <summary>בונוס מינימלי בשניות לתקציב ה-repack מעל מגבלת הזמן</summary>
        public const double RepackMinBonusSeconds = 2.0;

        /// <summary>שיעור הבונוס לתקציב ה-repack (10% מ-TimeLimitSeconds)</summary>
        public const double RepackBonusFactor = 0.1;

        // ─── מגבלות מילוי ────────────────────────────────────────────────────────
        /// <summary>יחס גובה מילוי מקסימלי ברירת מחדל (100%)</summary>
        public const double DefaultMaxFillHeightRatio = 1.0;

        /// <summary>מגבלת זמן ברירת מחדל ל-RunPackingJobInMemory</summary>
        public const double DefaultInMemoryTimeLimitSeconds = 300.0;

        // ─── Epsilon / דיוק ─────────────────────────────────────────────────────
        /// <summary>סף דיוק כללי להשוואות מיקום ומידות (floating-point)</summary>
        public const double Epsilon = 1e-9;

        /// <summary>סף דיוק לבדיקות שכבה (גסה יותר מ-Epsilon)</summary>
        public const double LayerEpsilon = 1e-6;

        // ─── SingleBinFiller ────────────────────────────────────────────────────
        /// <summary>מספר הצמתים המקסימלי ב-SingleBinFiller</summary>
        public const int SingleBinMaxNodes = 10_000_000;
    }
}
