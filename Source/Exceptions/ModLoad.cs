namespace MMM;

public class ModLoadException : ApplicationException
{
    public ModLoadException(string message, Exception? inner = null) : base(message, inner)
    {

    }
}
