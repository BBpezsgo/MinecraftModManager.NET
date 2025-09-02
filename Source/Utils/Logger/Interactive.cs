
namespace MMM;

public static partial class Log
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
            string? input = Console.ReadLine()?.ToLowerInvariant().Trim();

            if (input is "y" or "ye" or "yes") return true;
            if (input is "n" or "no" or "nu" or "nuh") return false;

            if (input is not null && input.Length == 0 && defaultValue.HasValue) return defaultValue.Value;
        }
        while (true);
    }

    public static string AskInput(string question, string? defaultValue = null)
    {
        Console.WriteLine();

        do
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write(":: ");
            Console.ForegroundColor = ConsoleColor.White;

            Console.Write(question);

            if (defaultValue is not null)
            {
                Console.Write(" [");
                Console.Write(defaultValue);
                Console.Write("]");
            }

            Console.Write(" > ");
            string? input = Console.ReadLine()?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(input)) return input;
            if (defaultValue is not null) return defaultValue;
        }
        while (true);
    }

    public static string AskInput(string question, Func<string, bool> validator, string? defaultValue = null)
    {
        string result;
        do
        {
            result = AskInput(question, defaultValue);
        } while (!validator.Invoke(result));
        return result;
    }
}
