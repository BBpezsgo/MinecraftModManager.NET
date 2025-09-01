namespace MMM;

public class ApplicationArgumentsException(string message, Exception? inner = null) : ApplicationException(message, inner);
