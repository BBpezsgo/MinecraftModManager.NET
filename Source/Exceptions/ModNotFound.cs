namespace MMM;

public class ModNotFoundException : ApplicationException
{
    public ModNotFoundException(string message, Exception? inner = null) : base(message, inner)
    {

    }
}
