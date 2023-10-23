using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Mars.Common.Core.Collections;
using Mars.Common.Core.Random;

namespace SOHMultimodalModel.Planning;

/// <summary>
///     This class provides the generation of sequences of <see cref="Trip" />s
///     based on MID2018 mobility survey of germany.
/// </summary>
/// <remarks>
///     The method <see cref="CreateDayPlanForAgent" /> constructs a
///     virtual day-plan for an agent with pre-selected <see cref="TripReason" />s.
/// </remarks>
public static class DayPlanGenerator
{
    private const double AvgWorkTimeFullWorkerInMinutes = 7.9 * 60;
    private const double AvgFreeTimeFullWorkerInMinutes = 2.4 * 60;
    private const double AvgErrandTimeFullWorkerInMinutes = 2.5 * 60;

    private const double AvgFreeTimeHalfWorkerInMinutes = 3.9 * 60;
    private const double AvgErrandTimeHalfWorkerInMinutes = 4 * 60;

    private static readonly FastRandom Random = RandomHelper.Random;

    /// <summary>
    ///     This function creates a virtual day-plan based on probabilities from the MID2018 survey
    ///     for the given <see cref="date" />. The function constructs a full worker, half-time worker or
    ///     employless daily-plan and considers optional <see cref="appointments" />.
    /// </summary>
    /// <param name="date">The day for which to create the plan.</param>
    /// <param name="isWorker">Indicates whether the agent is worker</param>
    /// <param name="isPartTimeWorker">
    ///     Indicates whether the agent is full or part-time worker (working mostly until the
    ///     afternoon)
    /// </param>
    /// <param name="appointments">Optional set of appointment with reason and point in time.</param>
    /// <returns>
    ///     Returns an iterator of <see cref="Trip" />s marking the point in time,
    ///     when the corresponding dayplan reason is active.
    /// </returns>
    public static IEnumerable<Trip> CreateDayPlanForAgent(DateTime date,
        bool isWorker, bool isPartTimeWorker,
        IDictionary<TripReason, DateTime> appointments = null)
    {
        IEnumerable<TripReason> dayActions;

        var isWorkingDay = date.DayOfWeek != DayOfWeek.Saturday || date.DayOfWeek != DayOfWeek.Sunday;

        if (isWorker)
        {
            if (isPartTimeWorker)
                dayActions = isWorkingDay
                    ? CreateDayPlanActionsForHalfWorkerWorkingDay()
                    : CreateDayPlanActionsForHalfWorkerWeekendDay(date.DayOfWeek);
            else
                dayActions = isWorkingDay
                    ? CreateDayPlanActionsForFullWorkerWorkingDay()
                    : CreateDayPlanActionsForFullWorkerWeekendDay(date.DayOfWeek);
        }

        else
        {
            dayActions = CreateDayPlanActionsForNoWorkerWorkingDay();
        }

        var actionsWithStartTimes =
            CalculateStartTimesForAction(dayActions, date, isWorker, isPartTimeWorker, appointments);

        return actionsWithStartTimes.Do(action =>
        {
            if (action.StartTime.Date < date) action.StartTime = date;
        });
    }


    //Worker
    private static IEnumerable<TripReason> CreateDayPlanActionsForFullWorkerWorkingDay()
    {
        yield return OneThirdEatFreeTimeErrands();
        yield return TripReason.Work;
        yield return TripReason.Eat;
        yield return TripReason.Work;

        var action = OneThirdEatFreeTimeErrands();
        yield return action;
        yield return DecideNextToDoDependingOnThePrevious(action);
        yield return TripReason.HomeTime;
    }

    // Half-worker
    private static IEnumerable<TripReason> CreateDayPlanActionsForHalfWorkerWorkingDay()
    {
        var morningShift = Random.Next(100) < 50;

        var action = OneThirdEatFreeTimeErrands();
        yield return action;

        action = morningShift ? TripReason.Work : DecideNextToDoDependingOnThePrevious(action);
        yield return action;

        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;

        action = morningShift ? DecideNextToDoDependingOnThePrevious(action) : TripReason.Work;
        yield return action;

        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;
        yield return DecideNextToDoDependingOnThePrevious(action);
        yield return TripReason.HomeTime;
    }

    private static IEnumerable<TripReason> CreateDayPlanActionsForFullWorkerWeekendDay(DayOfWeek day)
    {
        var random = Random.Next(100);
        var worksOnWeekend = day == DayOfWeek.Saturday ? random <= 50 : random <= 20;

        var action = OneThirdEatFreeTimeErrands();
        yield return action;

        action = worksOnWeekend ? TripReason.Work : DecideNextToDoDependingOnThePrevious(action);
        yield return action;

        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;

        action = worksOnWeekend ? TripReason.Work : DecideNextToDoDependingOnThePrevious(action);
        yield return action;

        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;
        yield return DecideNextToDoDependingOnThePrevious(action);
        yield return TripReason.HomeTime;
    }

    private static IEnumerable<TripReason> CreateDayPlanActionsForHalfWorkerWeekendDay(DayOfWeek day)
    {
        var rand = Random.Next(100);
        var morningShift = Random.Next(100) < 50;
        bool workOnWeekend;

        if (day == DayOfWeek.Saturday)
            workOnWeekend = rand <= 50;
        else
            workOnWeekend = rand <= 20;

        var action = OneThirdEatFreeTimeErrands();
        yield return action;

        action = workOnWeekend && morningShift
            ? TripReason.Work
            : DecideNextToDoDependingOnThePrevious(action);
        yield return action;

        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;

        if (workOnWeekend)
            action = morningShift ? DecideNextToDoDependingOnThePrevious(action) : TripReason.Work;
        else
            action = DecideNextToDoDependingOnThePrevious(action);

        yield return action;

        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;
        yield return DecideNextToDoDependingOnThePrevious(action);
        yield return TripReason.HomeTime;
    }

    //No-Worker
    private static IEnumerable<TripReason> CreateDayPlanActionsForNoWorkerWorkingDay()
    {
        var action = OneThirdEatFreeTimeErrands();
        yield return action;
        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;
        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;
        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;
        action = DecideNextToDoDependingOnThePrevious(action);
        yield return action;
        yield return DecideNextToDoDependingOnThePrevious(action);
        yield return TripReason.HomeTime;
    }

    //Helpers
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TripReason DecideNextToDoDependingOnThePrevious(TripReason dayPlanAction)
    {
        int random;
        switch (dayPlanAction)
        {
            case TripReason.Eat:
                random = Random.Next(100);
                return random <= 50 ? TripReason.FreeTime : TripReason.Errands;
            case TripReason.FreeTime:
                random = Random.Next(100);
                return random <= 50 ? TripReason.Eat : TripReason.Errands;
            case TripReason.Errands:
                random = Random.Next(100);
                return random <= 50 ? TripReason.FreeTime : TripReason.Eat;
            default:
                return OneThirdEatFreeTimeErrands();
        }
    }

    private static TripReason OneThirdEatFreeTimeErrands()
    {
        var random = Random.Next(100);

        if (random <= 33) return TripReason.Eat;
        if (random <= 66) return TripReason.FreeTime;

        return TripReason.Errands;
    }

    private static IEnumerable<Trip> CalculateStartTimesForAction(
        IEnumerable<TripReason> dayPlanActions, DateTime date,
        bool isWorker, bool isPartTimeWorker,
        IDictionary<TripReason, DateTime> appointments = null)
    {
        var actionWithStartTimes = new List<Trip>();

        if (isWorker)
        {
            if (isPartTimeWorker)
            {
                // Half-Time worker
                var actionList = dayPlanActions.ToList();
                var index = actionList.IndexOf(TripReason.Work);

                var errandTime = AvgErrandTimeHalfWorkerInMinutes;
                var freeTime = AvgFreeTimeHalfWorkerInMinutes;
                DateTime startTimeForWorkAction;

                if (index <= actionList.Count / 2) // Morning shift
                {
                    var randomIntForTime = Random.Next(100);

                    // Check for fix working appointment
                    if (appointments != null && appointments.TryGetValue(TripReason.Work, out var time))
                    {
                        startTimeForWorkAction = CreateDateTime(date, time.Hour, time.Minute);
                    }
                    else if (randomIntForTime is >= 0 and < 4
                            ) // Generate the morning shift by probability.
                    {
                        var randFullHour = Random.Next(0, 5);
                        var randMinutes = (int)(Random.NextDouble() * 60);
                        startTimeForWorkAction = CreateDateTime(date, randFullHour, randMinutes);
                    }
                    else if (randomIntForTime is >= 4 and < 71)
                    {
                        //5 - 8 Uhr
                        var randFullHour = Random.Next(5, 8);
                        var randMinutes = (int)(Random.NextDouble() * 60);
                        startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day,
                            randFullHour, randMinutes, 0);
                    }
                    else if (randomIntForTime is >= 71 and < 92)
                    {
                        //8 bis 10 Uhr
                        var randFullHour = Random.Next(8, 10);
                        var randMinutes = Random.NextDouble() * 60;
                        startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day,
                            randFullHour,
                            (int)randMinutes, 0);
                    }
                    else if (randomIntForTime is >= 92 and < 100)
                    {
                        //10 - 13 Uhr
                        var randFullHour = Random.Next(10, 13);
                        var randMinutes = Random.NextDouble() * 60;
                        startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day,
                            randFullHour,
                            (int)randMinutes, 0);
                    }
                    else
                    {
                        startTimeForWorkAction = DateTime.MinValue;
                    }
                }
                else // Evening shift
                {
                    var randomIntForTime = Random.Next(100);

                    // Generate the evening shift 
                    if (randomIntForTime is >= 0 and < 54)
                    {
                        //13 bis 16
                        var randFullHour = Random.Next(13, 16);
                        var randMinutes = Random.NextDouble() * 60;
                        startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day,
                            randFullHour,
                            (int)randMinutes, 0);
                    }
                    else if (randomIntForTime is >= 54 and < 69)
                    {
                        //16 bis 19
                        var randFullHour = Random.Next(16, 19);
                        var randMinutes = Random.NextDouble() * 60;
                        startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day,
                            randFullHour,
                            (int)randMinutes, 0);
                    }
                    else if (randomIntForTime is >= 69 and < 77)
                    {
                        //19 bis 22
                        var randFullHour = Random.Next(19, 22);
                        var randMinutes = Random.NextDouble() * 60;
                        startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day,
                            randFullHour,
                            (int)randMinutes, 0);
                    }
                    else if (randomIntForTime is >= 77 and < 100)
                    {
                        //22 bis 00
                        var randFullHour = Random.Next(22, 24);
                        var randMinutes = Random.NextDouble() * 60;
                        startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day,
                            randFullHour,
                            (int)randMinutes, 0);
                    }
                    else
                    {
                        startTimeForWorkAction = DateTime.MinValue;
                    }
                }

                var tempList = new List<Trip>();

                var startTimeForAction = startTimeForWorkAction;

                for (var i = 0; i < index; i++)
                {
                    double randMinutes;
                    switch (actionList.ElementAt(i))
                    {
                        case TripReason.Eat:
                            randMinutes = Random.NextDouble() * 60;
                            tempList.Add(new Trip(actionList.ElementAt(i),
                                startTimeForAction = startTimeForAction.AddMinutes(-randMinutes)));
                            break;
                        case TripReason.FreeTime:
                            randMinutes = Random.NextDouble() * freeTime;
                            tempList.Add(new Trip(actionList.ElementAt(i),
                                startTimeForAction = startTimeForAction.AddMinutes(-randMinutes)));
                            freeTime -= randMinutes;
                            break;
                        case TripReason.Errands:
                            randMinutes = Random.NextDouble() * errandTime;
                            tempList.Add(new Trip(actionList.ElementAt(i),
                                startTimeForAction = startTimeForAction.AddMinutes(-randMinutes)));
                            errandTime -= randMinutes;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                for (var i = tempList.Count - 1; i >= 0; i--) actionWithStartTimes.Add(tempList.ElementAt(i));

                actionWithStartTimes.Add(new Trip(actionList.ElementAt(index), startTimeForWorkAction));


                //work for 4 to 6 hours
                var afterWork = startTimeForWorkAction.AddMinutes(Random.Next(4 * 60, 6 * 60));
                actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index), afterWork));

                for (var i = index; i < actionList.Count; i++)
                    switch (actionList.ElementAt(i))
                    {
                        case TripReason.HomeTime:
                            break;
                        case TripReason.Eat:
                            actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index),
                                afterWork = afterWork.AddMinutes(Random.NextDouble() * 120)));
                            break;
                        case TripReason.FreeTime:
                            actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index),
                                afterWork = afterWork.AddMinutes(freeTime)));
                            break;
                        case TripReason.Errands:
                            actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index),
                                afterWork = afterWork.AddMinutes(errandTime)));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                return actionWithStartTimes;
            }
            else
            {
                //FullWorker
                var errandTime = AvgErrandTimeFullWorkerInMinutes;
                var freeTime = AvgFreeTimeFullWorkerInMinutes;

                var actionList = dayPlanActions.ToList();
                var firstWorkBlock = actionList.Find(type => type == TripReason.Work);
                var randomIntForTime = Random.Next(102);
                var startTimeForWorkAction = DateTime.MinValue;

                if (appointments != null && appointments.TryGetValue(TripReason.Work, out var time))
                {
                    //Fix time point for work
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute,
                        time.Second);
                }
                else if (randomIntForTime is >= 0 and < 3)
                {
                    //00 bis 05 Uhr
                    var randFullHour = Random.Next(0, 5);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }
                else if (randomIntForTime is >= 3 and < 63)
                {
                    //5 bis 8 Uhr
                    var randFullHour = Random.Next(5, 8);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }
                else if (randomIntForTime is >= 63 and < 82)
                {
                    //8 bis 10 Uhr
                    var randFullHour = Random.Next(8, 10);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }
                else if (randomIntForTime is >= 82 and < 89)
                {
                    //10 bis 13 Uhr
                    var randFullHour = Random.Next(10, 13);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }
                else if (randomIntForTime is >= 89 and < 96)
                {
                    //13 bis 16 Uhr
                    var randFullHour = Random.Next(13, 16);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }
                else if (randomIntForTime is >= 96 and < 98)
                {
                    //16 bis 19 Uhr
                    var randFullHour = Random.Next(16, 19);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }
                else if (randomIntForTime is >= 98 and < 99)
                {
                    //19 bis 22 Uhr
                    var randFullHour = Random.Next(19, 22);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }
                else if (randomIntForTime is >= 99 and < 102)
                {
                    //22 bis 24 Uhr
                    var randFullHour = Random.Next(22, 24);
                    var randMinutes = Random.NextDouble() * 60;
                    startTimeForWorkAction = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                        (int)randMinutes, 0);
                }

                var index = actionList.IndexOf(firstWorkBlock);

                var startTimeForAction = startTimeForWorkAction;

                var tempList = new List<Trip>();
                for (var i = 0; i < index; i++)
                {
                    double randMinutes;
                    switch (actionList.ElementAt(i))
                    {
                        case TripReason.Eat:
                            randMinutes = Random.NextDouble() * 60;
                            tempList.Add(new Trip(actionList.ElementAt(i),
                                startTimeForAction = startTimeForAction.AddMinutes(-randMinutes)));
                            break;
                        case TripReason.FreeTime:
                            randMinutes = Random.NextDouble() * freeTime;
                            tempList.Add(new Trip(actionList.ElementAt(i),
                                startTimeForAction = startTimeForAction.AddMinutes(-randMinutes)));
                            freeTime -= randMinutes;
                            break;
                        case TripReason.Errands:
                            randMinutes = Random.NextDouble() * errandTime;
                            tempList.Add(new Trip(actionList.ElementAt(i),
                                startTimeForAction = startTimeForAction.AddMinutes(-randMinutes)));
                            errandTime -= randMinutes;
                            break;
                        default:
                            randMinutes = Random.NextDouble() * 60;
                            tempList.Add(new Trip(actionList.ElementAt(i),
                                startTimeForAction = startTimeForAction.AddMinutes(-randMinutes)));
                            break;
                    }
                }

                for (var i = tempList.Count - 1; i >= 0; i--) actionWithStartTimes.Add(tempList.ElementAt(i));

                actionWithStartTimes.Add(new Trip(actionList.ElementAt(index), startTimeForWorkAction));

                //lunch is randomized in the middle of avgWorkTimeFullWorker
                var beginLunchAfterMinutes = Random.Next((int)(AvgWorkTimeFullWorkerInMinutes / 2) - 30,
                    (int)(AvgWorkTimeFullWorkerInMinutes / 2) + 30);

                var lunchStart = startTimeForWorkAction.AddMinutes(beginLunchAfterMinutes);
                actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index), lunchStart));


                //lunch takes 30 to 60 minutes (gesetzliche Mittagspause)
                var lunchTimeInMinutes = Random.Next(30, 60);
                var secondWorkBlockStart = lunchStart.AddMinutes(lunchTimeInMinutes);
                actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index), secondWorkBlockStart));

                //afterWorkAction
                var currentDateTime =
                    secondWorkBlockStart.AddMinutes(AvgWorkTimeFullWorkerInMinutes - beginLunchAfterMinutes);
                actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index), currentDateTime));


                for (var i = index; i < actionList.Count; i++)
                    switch (actionList.ElementAt(i))
                    {
                        case TripReason.HomeTime:
                            break;
                        case TripReason.Eat:
                            actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index),
                                currentDateTime = currentDateTime.AddMinutes(Random.NextDouble() * 60)));
                            break;
                        case TripReason.FreeTime:
                            actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index),
                                currentDateTime = currentDateTime.AddMinutes(freeTime)));
                            break;
                        case TripReason.Errands:
                            actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index),
                                currentDateTime = currentDateTime.AddMinutes(errandTime)));
                            break;
                        default:
                            actionWithStartTimes.Add(new Trip(actionList.ElementAt(++index),
                                currentDateTime = currentDateTime.AddMinutes(Random.NextDouble() * 60)));
                            break;
                    }

                return actionWithStartTimes;
            }
        }

        {
            var actionList = dayPlanActions.ToList();
            //dayplan No-Worker 15% Errands 55% Freetime 30% HomeTime (Sleep)
            var percentErrandTime =
                actionList.Count(type => type == TripReason.Errands) > 0 ? Random.Next(10, 20) : 0;
            var percentFreeTime = 70 - percentErrandTime;

            var errandTimeInMinutes = 24D * percentErrandTime / 100 * 60;
            var freeTimeInMinutes = 24D * percentFreeTime / 100 * 60;

            var homeTimeBlock = actionList.Find(type => type == TripReason.HomeTime);

            var startTimeHomeBlock = DateTime.MinValue;

            var randomIntForTime = Random.Next(104);

            if (randomIntForTime is >= 0 and < 5)
            {
                //00 bis 05 Uhr
                var randFullHour = Random.Next(0, 5);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }

            if (randomIntForTime is >= 5 and < 7)
            {
                //5 bis 8 Uhr
                var randFullHour = Random.Next(5, 8);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }

            if (randomIntForTime is >= 7 and < 12)
            {
                //8 bis 10 Uhr
                var randFullHour = Random.Next(8, 10);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }


            if (randomIntForTime is >= 12 and < 31)
            {
                //10 bis 13 Uhr
                var randFullHour = Random.Next(10, 13);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }

            if (randomIntForTime is >= 31 and < 54)
            {
                //13 bis 16 Uhr
                var randFullHour = Random.Next(13, 16);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }

            if (randomIntForTime is >= 54 and < 86)
            {
                //16 bis 19 Uhr
                var randFullHour = Random.Next(16, 19);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }

            if (randomIntForTime is >= 86 and < 99)
            {
                //19 bis 22 Uhr
                var randFullHour = Random.Next(19, 22);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }


            if (randomIntForTime is >= 99 and < 104)
            {
                //22 bis 24 Uhr
                var randFullHour = Random.Next(22, 24);
                var randMinutes = Random.NextDouble() * 60;
                startTimeHomeBlock = new DateTime(date.Year, date.Month, date.Day, randFullHour,
                    (int)randMinutes, 0);
            }


            var index = actionList.IndexOf(homeTimeBlock);

            var timeForStartAction = startTimeHomeBlock;

            var tempList = new List<Trip>();

            for (var i = --index; i >= 0; i--)
            {
                double randMinutes;
                switch (actionList.ElementAt(i))
                {
                    case TripReason.Eat:
                        randMinutes = Random.NextDouble() * 120;
                        tempList.Add(new Trip(actionList.ElementAt(i),
                            timeForStartAction = timeForStartAction.AddMinutes(-randMinutes)));
                        break;
                    case TripReason.FreeTime:
                        randMinutes = Random.NextDouble() * freeTimeInMinutes;
                        tempList.Add(new Trip(actionList.ElementAt(i),
                            timeForStartAction = timeForStartAction.AddMinutes(-randMinutes)));
                        freeTimeInMinutes -= randMinutes;
                        break;
                    case TripReason.Errands:
                        randMinutes = Random.NextDouble() * errandTimeInMinutes;
                        tempList.Add(new Trip(actionList.ElementAt(i),
                            timeForStartAction = timeForStartAction.AddMinutes(-randMinutes)));
                        errandTimeInMinutes -= randMinutes;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }


            for (var i = tempList.Count - 1; i >= 0; i--) actionWithStartTimes.Add(tempList.ElementAt(i));

            actionWithStartTimes.Add(new Trip(homeTimeBlock, startTimeHomeBlock));

            return actionWithStartTimes;
        }
    }

    private static DateTime CreateDateTime(DateTime day, int hour, int minute)
    {
        return new DateTime(day.Year, day.Month, day.Day, hour, minute, 0);
    }
}