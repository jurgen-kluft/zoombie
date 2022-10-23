using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Zoombie
{
    [Description("Have Zoombie create 'zoom-meetings.json' with 3 default meetings")]
    public sealed class CreateCommand : Command<CreateCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-f|--file <FILENAME>")]
            [Description("Have zoombie create 3 default meetings and write it to '[grey]'FILENAME'[/]'.")]
            [DefaultValue("zoom-meetings.json")]
            public string Filename { get; set; }

        }

        public override int Execute(CommandContext context, Settings settings)
        {
            if (!File.Exists(settings.Filename))
            {
                FileStream fs = File.Create(settings.Filename);
                StreamWriter writer = new(fs);

                List<Meeting> mts = new();
                mts.Add(Meeting.CreateDefault("Math Test Review", new(9, 5, 0)));
                mts.Add(Meeting.CreateDefault("Physics Mock Test", new(13, 37, 0)));
                mts.Add(Meeting.CreateDefault("Dinner", new(18, 15, 0)));

                JsonSerializerOptions options = new() { WriteIndented = true };
                var jsonText = JsonSerializer.Serialize<List<Meeting>>(mts, options);
                writer.Write(jsonText);

                writer.Close();
                fs.Close();

                AnsiConsole.WriteLine($"Saved meetings to '{settings.Filename}'.");
                return 1;
            }

            return 0;
        }
    }
}
