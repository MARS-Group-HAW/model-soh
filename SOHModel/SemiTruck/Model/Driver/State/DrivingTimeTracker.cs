using System;
using SOHModel.SemiTruck.Model.Driver.Utils;

namespace SOHModel.SemiTruck.Model.Driver.State
{
    /// <summary>
    /// Tracks driving time against EU Regulation 561/2006: a 45 min break after 4.5 h of driving (which may be
    /// taken as 15 min followed by 30 min, never the other way around) and a daily rest after 9 h of driving.
    /// Any stationary period (rest area, charging stop) is credited via <see cref="RegisterStop"/>.
    /// Not modelled: the two 10 h driving days per week, split daily rest, weekly limits.
    /// </summary>
    public class DrivingTimeTracker
    {
        public TimeSpan DrivingSinceBreak { get; private set; }
        public TimeSpan DrivingSinceDailyRest { get; private set; }

        /// <summary>True once a first break part of at least 15 min has been taken; the next part must be 30 min.</summary>
        public bool FirstBreakPartTaken { get; private set; }

        public void AddDriving(TimeSpan duration)
        {
            DrivingSinceBreak += duration;
            DrivingSinceDailyRest += duration;
        }

        /// <summary>Credits a stationary period of the given length towards break and daily rest.</summary>
        public void RegisterStop(TimeSpan duration)
        {
            if (duration >= SemiTruckDriverConstants.DailyRestDuration)
            {
                DrivingSinceDailyRest = TimeSpan.Zero;
                ResetBreak();
            }
            else if (duration >= SemiTruckDriverConstants.BreakDuration)
                ResetBreak();
            else if (FirstBreakPartTaken && duration >= SemiTruckDriverConstants.BreakSecondPart)
                ResetBreak();
            else if (!FirstBreakPartTaken && duration >= SemiTruckDriverConstants.BreakFirstPart)
                FirstBreakPartTaken = true;
        }

        /// <summary>Length of the next break that completes the 45 min requirement.</summary>
        public TimeSpan RemainingBreak => FirstBreakPartTaken
            ? SemiTruckDriverConstants.BreakSecondPart
            : SemiTruckDriverConstants.BreakDuration;

        public TimeSpan LegalDrivingLeft => TimeSpan.FromTicks(Math.Min(
            (SemiTruckDriverConstants.MaxDrivingBeforeBreak - DrivingSinceBreak).Ticks,
            (SemiTruckDriverConstants.MaxDailyDriving - DrivingSinceDailyRest).Ticks));

        /// <summary>
        /// The stop that is due, if the legal driving time left is at most <paramref name="planningMargin"/>.
        /// A daily rest also satisfies the break requirement and takes precedence.
        /// </summary>
        public (TimeSpan Duration, bool IsDailyRest)? DueStop(TimeSpan planningMargin)
        {
            if (SemiTruckDriverConstants.MaxDailyDriving - DrivingSinceDailyRest <= planningMargin)
                return (SemiTruckDriverConstants.DailyRestDuration, true);
            if (SemiTruckDriverConstants.MaxDrivingBeforeBreak - DrivingSinceBreak <= planningMargin)
                return (RemainingBreak, false);
            return null;
        }

        private void ResetBreak()
        {
            DrivingSinceBreak = TimeSpan.Zero;
            FirstBreakPartTaken = false;
        }
    }
}
