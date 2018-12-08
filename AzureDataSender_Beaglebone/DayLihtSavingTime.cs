// Copyright RoSchmi 
// Version Date 2017/07/30
/*
            The central european DayLightSavingTime begins on the last Sunday in March at 2 a.m.  MEZ (CET), 
            the counting of hours is set forward 1 hour from  2 a.m. to 3 a.m.. 
            It ends on the last Sunday in October at 3 a.m. MESZ (CEST), the counting of hours 
            is set back 1 hour from 3 a.m to 2 a.m.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoSchmi.DayLihtSavingTime
{
    class DayLihtSavingTime
    {
            public static DateTime Begin(string pStart, string pEnd, int pOffset, DateTime pDate)
            {
                return GetDstDate(pStart, pDate);
            }
            public static DateTime End(string pStart, string pEnd, int pOffset, DateTime pDate)
            {
                return GetDstDate(pEnd, pDate);
            }

            // This method returns an offset of 0 when dayLightSavingTime is not active
            // and an offset of 60 when DayLightSavingTime is active
            // For the changes from Not DayLightSavingTime to DaylaightSaveingTime and vice versa.
            // If the parameter pSmooth is set to true
            // the function makes a smooth change as the offset goes from 0 to the offset (for europe: 60) (and vice versa) in the time
            // from 2 A.M. to 3 A:M at the day of the change

            public static int DayLightTimeOffset(string pStart, string pEnd, int pOffset, DateTime pDate, bool pSmooth)
            {
                DateTime Begin = GetDstDate(pStart, pDate);
                DateTime End = GetDstDate(pEnd, pDate);

                if (DateTime.Compare(pDate, Begin) < 0)
                {
                    return 0;
                }
                else
                {
                    if (DateTime.Compare(pDate, End) >= 0)
                    {
                        return 0;
                    }
                    if (pSmooth)
                    {
                        if ((DateTime.Compare(pDate, Begin.AddHours(1.0)) >= 0) && (DateTime.Compare(pDate, End.AddHours(-1.0)) < 0))
                        {
                            return pOffset;
                        }

                        if (DateTime.Compare(pDate, End.AddHours(-1.0)) < 0)
                        {
                            return pDate.Minute * (pOffset / 60);
                        }
                        else
                        {
                            return pOffset - pDate.Minute * (pOffset / 60);
                            //return 60 - pDate.Minute;
                        }
                    }
                    else
                    {
                        return pOffset;
                    }
                }
            }

            public static bool IsDST(string pStart, string pEnd, int pOffset, DateTime today)
            {
                DateTime dstStartDay = GetDstDate(pStart, today);
                DateTime dstEndDay = GetDstDate(pEnd, today);

                if (dstStartDay <= dstEndDay)
                {   // northern hem
                    if (today < dstStartDay)
                        return false; // before dsl
                    else if (today >= dstEndDay)
                        return false; // after dsl
                    else
                        return true; // during dsl 
                }
                else
                {   // southern hem
                    if (today < dstEndDay)
                        return true; // still dsl
                    else if (today >= dstStartDay)
                        return true; // started dsl
                    else
                        return false; // not in dsl
                }
            }


            private static string[] Months = new string[] { "???", "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            private static string[] Days = new string[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

            //  decode dstDateStrings into DateTimes:    "Mth Day>=n" or "Mth lastDay"
            //   e.g. "Mar Sun>=8", "Nov Sun>=1", "Oct Sun>=1", "Apr Sun>=1", "Oct lastSun", "Feb 26"
            //        to change the time, append " @dd":  "Mar Sun>=8 @2" (US start)  "Mar lastSun @1" (EU start)
            private static DateTime GetDstDate(string dstDateString, DateTime today)
            {
                // adjust for exact time of change if not 2
                int timeOfChange = 2; // hours
                int spaceAt = dstDateString.IndexOf(" @");
                if (spaceAt > 0)
                {
                    timeOfChange = Convert.ToInt32(dstDateString.Substring(spaceAt + 2));
                    dstDateString = dstDateString.Substring(0, spaceAt);
                }

                DateTime dstDate = DateTime.MinValue;
                int year = today.Year;
                int month = 0;
                double doubleDate = 0;
                for (int i = 0; i < Months.Length; i++)
                    if (Months[i] == dstDateString.Substring(0, 3))
                        month = i;

                if (dstDateString.Substring(4, 4) == "last")
                {
                    int day = 0;
                    for (int i = 0; i < Days.Length; i++)
                        if (Days[i] == dstDateString.Substring(8, 3))
                            day = i;
                    if (month == 12)   // improbable case
                    {
                        month = 0;
                        year++;
                    }
                    dstDate = new DateTime(year, month + 1, 1).AddDays(-1); // last day of month
                    int lastDay = (int)dstDate.DayOfWeek;
                    dstDate = dstDate.AddDays(lastDay >= day ? day - lastDay : day - lastDay - 7);
                }
                else if (dstDateString.Substring(7, 2) == ">=")
                {
                    int day = 0;
                    for (int i = 0; i < Days.Length; i++)
                        if (Days[i] == dstDateString.Substring(4, 3))
                            day = i;
                    int date = Convert.ToInt32(dstDateString.Substring(9));
                    dstDate = new DateTime(year, month, date);   // minimum day
                    int minDay = (int)dstDate.DayOfWeek;
                    dstDate = dstDate.AddDays((7 + day - minDay) % 7);
                }
                else if (double.TryParse(dstDateString.Substring(4), out doubleDate))
                {
                    dstDate = new DateTime(year, month, (int)doubleDate);
                }
                dstDate = dstDate.AddHours(timeOfChange);
                return dstDate;
            }
        }
    }



