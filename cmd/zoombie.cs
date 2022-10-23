using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

// Using https://spectreconsole.net/

namespace Zoombie
{
    public class Meeting
    {
        public Occurence Occurence { get; set; }
        public TimeSpan MeetingTime { get; set; }
        public string MeetingInfo { get; set; }
        public string BaseURL { get; set; }
        public ulong MeetingID { get; set; }
        public string MeetingPassword { get; set; }

        public bool IsSameDay(DayOfWeek dow)
        {
            return Occurence.IsSameDay(dow);
        }

        public static Meeting CreateDefault(string info, TimeSpan mt)
        {
            var m = new Meeting();
            m.Occurence = Occurence.CreateDefault();
            m.MeetingTime = mt;
            m.MeetingInfo = info;
            m.BaseURL = "zoom.us";
            m.MeetingID = 97670214975;
            m.MeetingPassword = "UForeUESWNuLzdvYDR2T0QVlFeUdmQa9";
            return m;
        }
    }

    // TODO Is it not possible to just use a DayOfWeek[] ?
    public class Occurence
    {
        public bool Monday{ get; set; }
        public bool Tuesday{ get; set; }
        public bool Wednesday{ get; set; }
        public bool Thursday{ get; set; }
        public bool Friday{ get; set; }
        public bool Saturday{ get; set; }
        public bool Sunday{ get; set; }

        public bool IsSameDay(DayOfWeek dow)
        {
            var isSameDay = dow switch
            {
                DayOfWeek.Monday => Monday == true,
                DayOfWeek.Tuesday => Tuesday == true,
                DayOfWeek.Wednesday => Wednesday == true,
                DayOfWeek.Thursday => Thursday == true,
                DayOfWeek.Friday => Friday == true,
                DayOfWeek.Saturday => Saturday == true,
                DayOfWeek.Sunday => Sunday == true,
                _ => false
            };
            return isSameDay;
        }

        public static Occurence CreateDefault()
        {
            var o = new Occurence();
            o.Monday = true;
            o.Tuesday = true;
            o.Wednesday = true;
            o.Thursday = true;
            o.Friday = true;
            return o;
        }
    }

    class Program
    {

        public static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure(config =>
            {
                config.SetApplicationName("zoombie");
                config.ValidateExamples();
                config.AddExample(new[] { "run" });
                config.AddExample(new[] { "run", "--file", "meetings.json" });
                config.AddExample(new[] { "run", "--today" });
                config.AddExample(new []{ "run", "--days", "Mo+Tu+Th"});
                config.AddExample(new []{ "run", "--days", "Mo+Tu+Th", "--file", "meetings.json"});
                config.AddExample(new[] { "create" });
                config.AddExample(new[] { "create", "-f", "meetings.json" });
                config.AddExample(new[] { "create", "--file", "meetings.json" });

                // Run
                config.AddCommand<RunCommand>("run");

                // Create
                config.AddCommand<CreateCommand>("create");
            });
            return app.Run(args);
        }
    }
}
