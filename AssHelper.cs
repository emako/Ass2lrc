using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ass2lrc
{
    public static class StringConvert
    {
        public static int ToInt32(this string slef, int def)
        {
            if (int.TryParse(slef, out int i32))
            {
                return i32;
            }
            return def;
        }

        public static double ToDouble(this string slef, double def)
        {
            if (double.TryParse(slef, out double d))
            {
                return d;
            }
            return def;
        }

        public static string Reverse(this string self) => new(self.ToCharArray().Reverse().ToArray());

        public static string Right(this string self, int count)
        {
            if (!string.IsNullOrEmpty(self))
            {
                StringBuilder sb = new();

                int countReal = self.Length > count ? count : self.Length;
                for (int i = self.Length; i > self.Length - countReal; i--)
                {
                    sb.Append(self[i - 1]);
                }

                return sb.ToString().Reverse();
            }
            else
            {
                return self;
            }
        }
    }

    public static class StreamConvert
    {
        public static void Write(this FileStream stream, byte[] buffer) => stream.Write(buffer, 0, buffer.Length);
    }

    public class AssHelper
    {
        public static string StripAssTag(string text)
        {
            text = Regex.Replace(text, "(\\{)[^}]*(\\})", "");
            text = text.Replace("\\N", "\r\n");
            text = text.Replace("\\n", "\r\n");
            text = text.Replace("\\h", " ");

            return text;
        }

        public static long AssTimeToLrcTime(string timecode)
        {
            long startTimeMsec = 0;
            //MatchCollection match = Regex.Matches(timecode, "(\\d+)?:(\\d+)?:(\\d+)?(\\.\\d+)?(\\S+)?");
            MatchCollection match = Regex.Matches(timecode, "\\d+");

            if (match.Count < 0)
            {
                return startTimeMsec;
            }
            else
            {
                int hour = match[0].Value.ToInt32(0);
                int min = match[1].Value.ToInt32(0);
                int sec = match[2].Value.ToInt32(0);
                double msec = $"0.{match[3].Value}".ToDouble(0d);

                startTimeMsec = (long)((hour * 60 * 60 + min* 60 + sec + msec) * 1000d);
            }
            return startTimeMsec;
        }

        public static string GetFileText(string path)
        {
            using StreamReader stream = File.OpenText(path);
            return stream.ReadToEnd();
        }

        public static string ToTimecode(long a_msecTime, bool a_fullFormat)
        {
            int secTime = (int)(a_msecTime / 1000d);
            int hourPart = (int)Math.Floor((decimal)secTime / (60 * 60));
            int minPart = (int)(Math.Floor((decimal)secTime % (60 * 60)) / 60);
            int secPart = (int)Math.Floor((decimal)secTime % 60);
            int msecPart = a_msecTime.ToString().Right(3).ToInt32(0);
            string timecode;

            if(a_fullFormat)
            {
                timecode = $"{hourPart:0}:{minPart:00}:{secPart:00}.{msecPart / 10d:00}";
            }
            else
            {
                if(hourPart >= 1)
                {
                    minPart = 60;
                    secPart = 0;
                    msecPart = 0;
                }
                timecode = $"{minPart:00}:{secPart:00}.{msecPart / 10d:00}";
            }

            return timecode;
        }

        public static bool AssToLyric(string a_srcPath, string a_tarPath)
        {
            List<KeyValuePair<long, string>> assParsedList = AssParse(GetFileText(a_srcPath));

            FileStream stream = File.OpenWrite(a_tarPath);

            string lrcHeader = $"[ti:None]{Environment.NewLine}"
                             + $"[ar:None]{Environment.NewLine}"
                             + $"[al:None]{Environment.NewLine}"
                             + $"[by:TCAX Creator]{Environment.NewLine}";

            try
            {
                stream.Write(Encoding.UTF8.GetPreamble());
                stream.Write(Encoding.UTF8.GetBytes(lrcHeader));

                foreach (KeyValuePair<long, string> assParsedLine in assParsedList)
                {
                    long startTimeMsec = assParsedLine.Key;
                    string text = assParsedLine.Value;

                    text = $"[{ToTimecode(startTimeMsec, false)}]{text}{Environment.NewLine}";
                    stream.Write(Encoding.UTF8.GetBytes(text));
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                return false;
            }

            return true;
        }

        public static List<KeyValuePair<long, string>> AssParse(string a_str)
        {
            List<KeyValuePair<long, string>> assParsedList = new();
            string assStr = a_str;
            string[] assLines = assStr.Replace("\r", string.Empty).Split('\n');

            if (assLines.Length < 0)
            {
                return assParsedList;
            }

            for (int i = default; i < assLines.Length; i++)
            {
                string assLine = assLines[i];
                string[] events = SplitAssEvents(assLine);

                if (events.Length >= (int)AssEvents.MaxEvent)
                {
                    long startTimeMsec = AssTimeToLrcTime(events[(int)AssEvents.Start]);
                    long endTimeMsec = AssTimeToLrcTime(events[(int)AssEvents.End]);
                    string text = events[(int)AssEvents.Text];

                    if (startTimeMsec < 0)
                    {
                        continue;
                    }

                    assParsedList.Add(new KeyValuePair<long, string>(startTimeMsec, text));

                    /* No making times continued. */
                    if (i < assLines.Length && endTimeMsec >= 0)
                    {
                        string assLineNext = assLines[i + 1];
                        string[] events2 = SplitAssEvents(assLineNext);

                        if (events2.Length >= (int)AssEvents.MaxEvent)
                        {
                            long startTimeMsecNext = AssTimeToLrcTime(events2[(int)AssEvents.Start]);

                            if (endTimeMsec != startTimeMsecNext && startTimeMsec >= 0)
                            {
                                assParsedList.Add(new KeyValuePair<long, string>(endTimeMsec, string.Empty));
                            }
                        }
                    }
                }
            }
            return assParsedList;
        }

        public static string[] SplitAssEvents(string a_assLine)
        {
            string[] eventsFirstSplited = StripAssTag(a_assLine).Split(',');
            List<string> events = new();

            if (!a_assLine.StartsWith("Dialogue") && !a_assLine.StartsWith("Comment"))
            {
                return events.ToArray();
            }

            if (eventsFirstSplited.Length < (int)AssEvents.MaxEvent)
            {
                return events.ToArray();
            }

            else
            {
                string text = string.Empty;

                if (eventsFirstSplited.Length == (int)AssEvents.MaxEvent)
                {
                    text = eventsFirstSplited[(int)AssEvents.Text];
                }
                else if (eventsFirstSplited.Length > (int)AssEvents.MaxEvent)
                {
                    List<string> texts = new();

                    for (int i = (int)AssEvents.Text; i < eventsFirstSplited.Length; i++)
                    {
                        texts.Add(eventsFirstSplited[i]);
                    }
                    text = string.Join(",", texts.ToArray());
                }

                for (int i = (int)AssEvents.Layer; i < (int)AssEvents.Text; i++)
                {
                    events.Add(eventsFirstSplited[i]);
                }
                events.Add(text);
            }

            return events.ToArray();
        }
    }

    public enum AssEvents
    {
        Layer = 0,
        Start,
        End,
        Style,
        Name,
        MarginL,
        MarginR,
        MarginV,
        Effect,
        Text,
        MaxEvent,
    };

}
