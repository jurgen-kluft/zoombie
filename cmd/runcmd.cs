using System.ComponentModel;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Spectre.Console;
using Spectre.Console.Cli;

namespace Zoombie
{
    [Description("Run Zoombie")]
    public sealed class RunCommand : Command<RunCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--today")]
            [Description("Run zoombie '[grey]but exit when no more tasks for today[/]'.")]
            [DefaultValue(false)]
            public bool Today { get; set; }

            // zoombie run --days Wednesday+Thursday
            [CommandOption("-d|--days")]
            [Description("Run zoombie but only start meetings on certain '[grey]'days'[/]'.")]
            [DefaultValue("Mo+Tu+We+Th+Fr+Sa+Su")]
            public string Days { get; set; }

            [CommandOption("-f|--file <FILENAME>")]
            [Description("Run zoombie but load the meetings from '[grey]'FILENAME'[/]'.")]
            [DefaultValue("zoom-meetings.json")]
            public string Filename { get; set; }
        }

        private const string ZoomUrl = "zoommtg://{MeetingURL}/join?confno={MeetingID}&pwd={MeetingPassword}";

        private static string GetFinalZoomUrl(string baseUrl, string meetingId, string meetingPassword)
        {
            string url = ZoomUrl.Replace("{MeetingURL}", baseUrl);
            url = url.Replace("{MeetingID}", meetingId);
            url = url.Replace("{MeetingPassword}", meetingPassword);
            return url;
        }

        public class MeetingsPerDay
        {
            public List<Meeting> Monday { get; set; } = new();
            public List<Meeting> Tuesday{ get; set; }= new();
            public List<Meeting> Wednesday{ get; set; }= new();
            public List<Meeting> Thursday{ get; set; }= new();
            public List<Meeting> Friday{ get; set; }= new();
            public List<Meeting> Saturday{ get; set; }= new();
            public List<Meeting> Sunday{ get; set; }= new();

            public List<Meeting> GetMeetingsForDay(DayOfWeek d)
            {
                switch (d)
                {
                    case DayOfWeek.Monday: return Monday;
                    case DayOfWeek.Tuesday: return Tuesday;
                    case DayOfWeek.Wednesday: return Wednesday;
                    case DayOfWeek.Thursday: return Thursday;
                    case DayOfWeek.Friday: return Friday;
                    case DayOfWeek.Saturday: return Saturday;
                    case DayOfWeek.Sunday: return Sunday;
                }
                return new();
            }

            private static int CompareTime(TimeSpan x, TimeSpan y)
            {
                if (x.Hours < y.Hours)
                {
                    return -1;
                }
                if (x.Hours > y.Hours)
                {
                    return 1;
                }
                if (x.Minutes < y.Minutes)
                {
                    return -1;
                }
                if (x.Minutes > y.Minutes)
                {
                    return 1;
                }
                return 0;
            }

            public void SortMeetingsByTimeFor(DayOfWeek d)
            {
                var list = GetMeetingsForDay(d);
                list.Sort((x,y) => CompareTime(x.MeetingTime, y.MeetingTime));
            }
        }

        private static HashSet<DayOfWeek> SplitDays(string d)
        {
            Dictionary<string, DayOfWeek> basicStringToDayOfWeek = new(StringComparer.OrdinalIgnoreCase) { {"monday", DayOfWeek.Monday}, {"tuesday",DayOfWeek.Tuesday},{"wednesday",DayOfWeek.Wednesday},{"thursday",DayOfWeek.Thursday},{"friday",DayOfWeek.Friday},{"saturday",DayOfWeek.Saturday},{"sunday", DayOfWeek.Sunday}};
            Dictionary<string, DayOfWeek> stringToDayOfWeek = new(StringComparer.OrdinalIgnoreCase);
            stringToDayOfWeek.Add("today", DateTime.Now.DayOfWeek);

            foreach (var day in basicStringToDayOfWeek)
            {
                stringToDayOfWeek.Add(day.Key, day.Value);
                for (int i=day.Key.Length-1; i>=1; --i)
                {
                    var shortName = day.Key.Substring(0, i);
                    if (!stringToDayOfWeek.ContainsKey(shortName))
                    {
                        stringToDayOfWeek.Add(shortName, day.Value);
                    }
                }
            }

            string[] days = d.ToLower().Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.TrimEntries);
            HashSet<DayOfWeek> onlyOnDays = new();
            foreach (var day in days)
            {
                if (stringToDayOfWeek.TryGetValue(day, out DayOfWeek dow))
                {
                    onlyOnDays.Add(dow);
                }
            }
            return onlyOnDays;
        }

        private static MeetingsPerDay DetermineMeetingsPerDay(List<Meeting> meetings, string days)
        {
            MeetingsPerDay meetingsPerDay = new();
            var OnlyOnDays = SplitDays(days);

            // For each list of meetings per day, sort it by time
            foreach (DayOfWeek d in Enum.GetValues(typeof(DayOfWeek)))
            {
                if (!OnlyOnDays.Contains(d))
                    continue;

                var list = meetingsPerDay.GetMeetingsForDay(d);
                foreach (var m in meetings)
                {
                    if (m.Occurence.IsSameDay(d))
                    {
                        list.Add(m);
                    }
                }
            }
            foreach (DayOfWeek d in Enum.GetValues(typeof(DayOfWeek)))
            {
                meetingsPerDay.SortMeetingsByTimeFor(d);
            }
            return meetingsPerDay;
        }

        private static void UpdateListToNow(List<Meeting> todayMeetings, DateTime now)
        {
            TimeSpan currentTime = new(now.TimeOfDay.Hours, now.TimeOfDay.Minutes, 0);

            // See if we need to clip off any meetings that have already occured
            while (todayMeetings.Count > 0 && (currentTime > todayMeetings[0].MeetingTime))
            {
                todayMeetings.RemoveAt(0);
            }
        }

        private static Meeting NextMeetingToOccur(List<Meeting> todayMeetings)
        {
            if (todayMeetings.Count == 0)
                return null;
            return todayMeetings[0];
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.Filename))
                return -1;
            var text = File.ReadAllText(settings.Filename);
            if (text == "")
                return -1;

            JsonSerializerOptions options = new();
            options.PropertyNameCaseInsensitive = true;

            List<Meeting> meetings;
            try
            {
                meetings = JsonSerializer.Deserialize<List<Meeting>>(text, options);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.ToString());
                return -1;
            }

            if (meetings == null)
                return -1;

            var meetingsOverviewTable = new Table();
            meetingsOverviewTable.AddColumn("Time");
            meetingsOverviewTable.AddColumn("Meeting Info");
            meetingsOverviewTable.AddColumn("Meeting ID");
            foreach (var m in meetings)
            {
                meetingsOverviewTable.AddRow($"[green]{m.MeetingTime}[/]", $"{m.MeetingInfo}", $"[red]{m.MeetingID}[/]");
            }
            AnsiConsole.Write(meetingsOverviewTable);

            DayOfWeek today = DateTime.Now.DayOfWeek;
            MeetingsPerDay meetingsPerDay = DetermineMeetingsPerDay(meetings, settings.Days);
            while (true)
            {
                // Make sure we copy the list of meetings since we are going to drain it
                List<Meeting> todayMeetings = new();
                todayMeetings.AddRange(meetingsPerDay.GetMeetingsForDay(today));
                UpdateListToNow(todayMeetings, DateTime.Now);

                if (todayMeetings.Count > 0)
                {
                    // Print coming meetings for today
                    var meetingsForTodayTable = new Table();
                    meetingsForTodayTable.Caption = new($"Meetings for today ({DateTime.Now.ToLongDateString()})");
                    meetingsForTodayTable.AddColumn("Time");
                    meetingsForTodayTable.AddColumn("Meeting Info");
                    meetingsForTodayTable.AddColumn("Meeting ID");
                    foreach (var m in meetings)
                    {
                        meetingsForTodayTable.AddRow($"[green]{m.MeetingTime}[/]", $"{m.MeetingInfo}", $"[red]{m.MeetingID}[/]");
                    }

                    AnsiConsole.Write(meetingsForTodayTable);
                }
                else
                {
                    AnsiConsole.Write($"No more scheduled meetings for today ({DateTime.Now.ToLongDateString()})");
                }

                // Keep running in this loop until the day actually changes
                while (today == DateTime.Now.DayOfWeek)
                {
                    // Get the meeting that will happen next
                    var mt = NextMeetingToOccur(todayMeetings);
                    if (mt == null)
                    {
                        // If we only needed to handle 'today' then here is the end since there are no more future meetings for today
                        if (settings.Today)
                            return 0;

                        // Sleep as much as we can until the next day starts
                        var todayDateTime = DateTime.Now;
                        var todayJustBeforeMidnight = new DateTime(todayDateTime.Year, todayDateTime.Month, todayDateTime.Day, 23, 59, 59);
                        var timeToTomorrow = todayJustBeforeMidnight.TimeOfDay.Subtract(todayDateTime.TimeOfDay);
                        switch (timeToTomorrow.TotalMinutes)
                        {
                            case > 30:
                                Thread.Sleep(30*60*1000);
                                break;
                            case > 5:
                                Thread.Sleep(5*60*1000);
                                break;
                            default:
                                Thread.Sleep(20_000);
                                break;
                        }
                        continue;
                    }

                    // What is the time span from now to mt.MeetingTime?
                    var timeToNextMeeting = mt.MeetingTime.Subtract(DateTime.Now.TimeOfDay);

                    if (timeToNextMeeting.TotalMinutes > 2)
                    {
                        timeToNextMeeting = timeToNextMeeting.Subtract(new TimeSpan(0, 2, 0));
                        Thread.Sleep((int)timeToNextMeeting.TotalMilliseconds);
                    }

                    while (true)
                    {
                        var now = DateTime.Now;
                        if ((now.TimeOfDay.Hours == mt.MeetingTime.Hours && now.TimeOfDay.Minutes == mt.MeetingTime.Minutes))
                        {
                            var content = new Markup($"Time: {mt.MeetingTime}\nInfo: {mt.MeetingInfo}\nID: {mt.MeetingID}\nURL: {mt.BaseURL}");
                            var panel = new Panel(content) { Header = new("Meeting!"), Border = BoxBorder.Ascii, Padding = new Padding(2, 2, 2, 2) };
                            AnsiConsole.Write(panel);

                            var url = GetFinalZoomUrl(mt.BaseURL, mt.MeetingID.ToString(), mt.MeetingPassword);
                            try
                            {
                                Process.Start(url);
                            }
                            catch
                            {
                                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                {
                                    url = url.Replace("&", "^&");
                                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                                }
                                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                {
                                    Process.Start("xdg-open", url);
                                }
                                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                {
                                    Process.Start("open", url);
                                }
                                else
                                {
                                    throw;
                                }
                            }

                            // We have started the Zoom meeting so now we are going to pause for 5 minutes
                            Thread.Sleep(5 * 60 * 1000);
                            break;
                        }
                        // Before we check again do sleep for 20 seconds
                        Thread.Sleep(20_000);
                    }

                    // Current meeting should have passed and will be removed from the list
                    UpdateListToNow(todayMeetings, DateTime.Now);
                }

                // If the day has changed and settings said that we should only handle today => exit
                if (settings.Today)
                    break;

                // Update 'today'
                today = DateTime.Now.DayOfWeek;
            }
            return 0;
        }

        public static void OpenZoomMeeting(string link)
        {
            string zoomDirectory = Environment.ExpandEnvironmentVariables(@"%APPDATA%\Zoom\bin");
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = $@"{zoomDirectory}\Zoom.exe",
                Arguments = $"--url={link}",
                WorkingDirectory = zoomDirectory
            };
            Process.Start(startInfo);
        }
    }
}
