namespace MMM;

public class ModLoadException(string message, Exception? inner = null) : ApplicationException(message, inner);
