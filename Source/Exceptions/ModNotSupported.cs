namespace MMM;

public class ModNotSupported : ApplicationException
{
    public ModNotSupported(string message, Exception? inner = null) : base(message, inner)
    {

    }
}
