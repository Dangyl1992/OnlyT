﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OnlyT.Utils;
using Serilog;

namespace OnlyT.MeetingSongsFile
{

    /// <summary>
    /// Finds meeting songs numbers and talk timers for use in "Automatic" mode.
    /// This code adapted from SoundBox.
    /// </summary>
    internal class MeetingSongsFinder
    {
        private static readonly string SoundBoxFileName = "mtg_songs_1.xml";
        private static readonly string SoundBoxUrl = $"http://cv8.org.uk/soundbox/{SoundBoxFileName}";
        private readonly string _localMtgSongsFile;
        private readonly int _tooOldDays = 20;


        public MeetingSongsFinder()
        {
            // e.g. C:\Users\Antony\AppData\Roaming\SoundBox\mtg_songs_1.xml
            _localMtgSongsFile = Path.Combine(FileUtils.GetAppDataFolder(), SoundBoxFileName);
        }

        /// <summary>
        /// Gets the song numbers and meeting timers for today's date. Assumes
        /// that the "midweek" meeting occurs midweek and the "weekend" meeting is 
        /// at the weekend!
        /// </summary>
        /// <returns></returns>
        public MeetingSongsAndTimers GetSongNumbersAndTimersForToday()
        {
            DateTime now = DateTime.Now;
            XDocument x = GetMtgSongsXDoc(now);
            return GetSongNumbersAndTimersFromXDoc(now, x);
        }


        private bool LocalFileToOld()
        {
            bool tooOld = true;

            if (File.Exists(_localMtgSongsFile))
            {
                DateTime created = File.GetLastWriteTime(_localMtgSongsFile);
                TimeSpan diff = (DateTime.Now - created);
                if (diff.TotalDays <= _tooOldDays)
                {
                    tooOld = false;
                }
            }

            return tooOld;
        }

        private void AddSongs(MeetingSongsAndTimers result, XElement elem)
        {
            const int MaxSongs = 3;

            for (int n = 1; n <= MaxSongs; ++n)
            {
                string key = $"nsong{n}";

                var value = elem.Attribute(key);
                if (value == null)
                {
                    break;
                }

                if (Int32.TryParse(value.Value, out var songNum))
                {
                    result.AddSong(songNum);
                }
            }
        }

        private void AddTimers(MeetingSongsAndTimers result, XElement elem)
        {
            string[] timerKeys =
            {
                MeetingSongsAndTimers.LivingTimer1Key, 
                MeetingSongsAndTimers.LivingTimer2Key,
                MeetingSongsAndTimers.MinistryTimer1Key,
                MeetingSongsAndTimers.MinistryTimer2Key,
                MeetingSongsAndTimers.MinistryTimer3Key
            };

            foreach (var key in timerKeys)
            {
                var value = elem.Attribute(key);
                if (value == null)
                {
                    break;
                }

                string timeStr = value.Value;

                bool useBell = false;
                if (timeStr.Contains("B"))
                {
                    useBell = true;
                    timeStr = timeStr.Replace("B", "");
                }

                if (Int32.TryParse(timeStr, out var timerMins))
                {
                    result.AddTimer(key, timerMins, useBell);
                }
            }
        }

        private MeetingSongsAndTimers GetSongNumbersAndTimersFromXDoc(DateTime theDate, XDocument x)
        {
            MeetingSongsAndTimers result = new MeetingSongsAndTimers();

            if (x.Root != null)
            {
                var dow = theDate.DayOfWeek;
                bool isSaturdayOrSunday = (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday);

                var mtgs = x.Root.Elements("meeting");

                bool doneTimers = false;
                bool doneSongs = false;

                foreach (XElement elem in mtgs)
                {
                    string mtgName = elem.Attribute("name").Value;
                    DateTime dt = DateTime.ParseExact(elem.Attribute("date").Value, "yyyyMMdd", CultureInfo.InvariantCulture);

                    if (theDate.Date == dt.Date && mtgName.Equals("exact"))
                    {
                        // found a special "exact" meeting, e.g. memorial...

                        AddSongs(result, elem);
                        break;
                    }

                    if (theDate.Date >= dt.Date && theDate.Date < dt.Date.AddDays(7))
                    {
                        if (!doneSongs)
                        {
                            if (isSaturdayOrSunday && mtgName.Equals("weekend"))
                            {
                                result.AddNullSong();
                                AddSongs(result, elem);
                                doneSongs = true;
                            }

                            if (!isSaturdayOrSunday && mtgName.Equals("midweek"))
                            {
                                AddSongs(result, elem);
                                doneSongs = true;
                            }
                        }

                        if (!doneTimers && mtgName.Equals("midweek"))
                        {
                            AddTimers(result, elem);
                            doneTimers = true;
                        }
                    }

                    if (doneSongs && doneTimers)
                    {
                        break;
                    }
                }
            }

            return result;
        }


        private XDocument GetMtgSongsXDoc(DateTime dt)
        {
            XDocument x = null;

            bool needRefresh = LocalFileToOld();

            if (!needRefresh)
            {
                try
                {
                    x = XDocument.Load(_localMtgSongsFile);
                    MeetingSongsAndTimers songsAndTimers = GetSongNumbersAndTimersFromXDoc(dt, x);
                    if (songsAndTimers.SongCount == 0)
                    {
                        needRefresh = true;
                    }
                }
                catch (Exception ex)
                {
                    needRefresh = true;
                    Log.Logger.Error(ex, "Getting meeting songs xml");
                }
            }

            if (needRefresh)
            {
                x = WebUtils.XDocLoadWithUserAgent(SoundBoxUrl);
                try
                {
                    x.Save(_localMtgSongsFile);
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Loading songs xml");
                }
            }

            CheckValidity(x);

            return x;
        }

        [Conditional("DEBUG")]
        private void CheckValidity(XDocument xDoc)
        {
            if (xDoc.Root != null)
            {
                // ReSharper disable once PossibleNullReferenceException
                CheckMtgsValidity(xDoc.Root.Elements("meeting").
                   Where(x => x.Attribute("name").Value.Equals("weekend", StringComparison.OrdinalIgnoreCase)));

                // ReSharper disable once PossibleNullReferenceException
                CheckMtgsValidity(xDoc.Root.Elements("meeting").
                   Where(x => x.Attribute("name").Value.Equals("midweek", StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                throw new Exception("Could not find root element");
            }
        }

        [Conditional("DEBUG")]
        private void CheckMtgsValidity(IEnumerable<XElement> mtgs)
        {
            DateTime currentDate = DateTime.MinValue;
            foreach (XElement elem in mtgs)
            {
                // ReSharper disable once PossibleNullReferenceException
                DateTime dt = DateTime.ParseExact(elem.Attribute("date").Value, "yyyyMMdd", CultureInfo.InvariantCulture);

                if (dt.DayOfWeek != DayOfWeek.Monday)
                {
                    throw new Exception("Not a Monday!");
                }

                if (currentDate != DateTime.MinValue && dt.Date != currentDate.AddDays(7).Date)
                {
                    throw new Exception("Incorrect date sequence!");
                }

                currentDate = dt;
            }
        }

    }
}
