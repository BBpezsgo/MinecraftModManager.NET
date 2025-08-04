namespace MMM;

public class ModNotSupported(string message, Exception? inner = null) : ApplicationException(message, inner);
