
namespace MMM;

static partial class Log
{
    public static bool AskYesNo(string question, bool? defaultValue = null)
    {
        Console.WriteLine();

        do
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(":: ");
            Console.ForegroundColor = ConsoleColor.White;

            Console.Write(question);

            Console.Write(" [");
            Console.Write(defaultValue.HasValue ? defaultValue.Value ? "Y/n" : "y/N" : "y/n");
            Console.Write("]");

            Console.Write(" > ");
            string? input = Console.ReadLine()?.ToLowerInvariant();
            if (input == "y") return true;
            if (input == "n") return false;

            if (input is not null && input.Length == 0 && defaultValue.HasValue) return defaultValue.Value;
        }
        while (true);
    }
}
