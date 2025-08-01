
namespace MMM;

static partial class Log
{
    public static bool AskYesNo(string question)
    {
        do
        {
            Console.Write(question);
            Console.Write(" > ");
            string? input = Console.ReadLine()?.ToLowerInvariant();
            if (input == "y") return true;
            if (input == "n") return false;
        }
        while (true);
    }
}
